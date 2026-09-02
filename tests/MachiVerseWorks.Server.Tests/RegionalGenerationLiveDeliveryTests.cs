using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachiVerseWorks.Server.Tests;

public sealed class RegionalGenerationLiveDeliveryTests
{
    [Fact]
    public async Task Protocol218ClientReceivesAuthoritativeRegionalGenerationWithStableRelations()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 0, snapshotRate: 30);
        var runtime = host.App.Services.GetRequiredService<SimulationRuntime>();
        var authoritative = runtime.Mutate(static world => world.GenerateRegionalGeneration(
            new WorldVolume(-1000d, -1000d, 0d, 1000d, 1000d, 100d),
            new RegionalGenerationOptions(
                RegionalGenerationQualityPreset.Draft,
                settlementCount: 2,
                iterationBudget: 1)));

        using var socket = await host.ConnectWebSocketAsync();
        var version = new ProtocolVersion(2, 18);
        await ServerTestHost.SendAsync(socket, new HelloMessage(), version);
        var hello = await ReceiveFrameAsync(socket, TimeSpan.FromSeconds(3));
        Assert.Equal(MessageType.HelloAck, hello.Header.MessageType);

        var regionalFrame = await ReceiveUntilAsync(socket, MessageType.RegionalGenerationSnapshot, TimeSpan.FromSeconds(5));
        Assert.True(RegionalGenerationProtocolCodec.TryDeserialize(regionalFrame.Frame, out var envelope, out var error), error.ToString());
        var message = Assert.IsType<RegionalGenerationSnapshotMessage>(envelope!.Message);

        Assert.Equal(authoritative.WorldSeed, message.WorldSeed);
        Assert.Equal(authoritative.TickCount, message.TickCount);
        Assert.Equal(authoritative.Settlements.Count, message.Settlements.Count);
        Assert.Equal(authoritative.Parcels.Count, message.Parcels.Count);
        Assert.Equal(authoritative.Buildings.Count, message.Buildings.Count);

        var settlementIds = message.Settlements.Select(static item => item.Id).ToHashSet();
        var districtIds = message.Districts.Select(static item => item.Id).ToHashSet();
        var parcelIds = message.Parcels.Select(static item => item.Id).ToHashSet();
        Assert.All(message.Corridors, corridor =>
        {
            Assert.Contains(corridor.FromSettlementId, settlementIds);
            Assert.Contains(corridor.ToSettlementId, settlementIds);
        });
        Assert.All(message.Parcels, parcel =>
        {
            Assert.Contains(parcel.SettlementId, settlementIds);
            Assert.Contains(parcel.DistrictId, districtIds);
        });
        Assert.All(message.Buildings, building => Assert.Contains(building.ParcelId, parcelIds));
    }

    private static async Task<(ProtocolFrameHeader Header, byte[] Frame)> ReceiveUntilAsync(
        ClientWebSocket socket,
        MessageType target,
        TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!cancellation.IsCancellationRequested)
        {
            var received = await ReceiveFrameAsync(socket, timeout, cancellation.Token);
            if (received.Header.MessageType == target) return received;
        }
        throw new TimeoutException($"Did not receive {target}.");
    }

    private static async Task<(ProtocolFrameHeader Header, byte[] Frame)> ReceiveFrameAsync(
        ClientWebSocket socket,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCancellation.Token);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new InvalidOperationException("Server closed the WebSocket before the expected protocol message was received.");
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        var frame = stream.ToArray();
        Assert.True(ProtocolFrameHeader.TryRead(frame, out var header, out var error), error.ToString());
        return (header, frame);
    }
}
