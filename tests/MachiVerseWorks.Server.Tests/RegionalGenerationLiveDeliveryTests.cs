using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RegionalGenerationLiveDeliveryTests
{
    [TestMethod]
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
        Assert.AreEqual(MessageType.HelloAck, hello.Header.MessageType);

        var regionalFrame = await ReceiveUntilAsync(socket, MessageType.RegionalGenerationSnapshot, TimeSpan.FromSeconds(5));
        Assert.IsTrue(RegionalGenerationProtocolCodec.TryDeserialize(regionalFrame.Frame, out var envelope, out var error), error.ToString());
        Assert.IsNotNull(envelope);
        var message = envelope.Message as RegionalGenerationSnapshotMessage;
        Assert.IsNotNull(message);

        Assert.AreEqual(authoritative.WorldSeed, message.WorldSeed);
        Assert.AreEqual(authoritative.TickCount, message.TickCount);
        Assert.AreEqual(authoritative.Settlements.Count, message.Settlements.Count);
        Assert.AreEqual(authoritative.Parcels.Count, message.Parcels.Count);
        Assert.AreEqual(authoritative.Buildings.Count, message.Buildings.Count);

        var settlementIds = message.Settlements.Select(static item => item.Id).ToHashSet();
        var districtIds = message.Districts.Select(static item => item.Id).ToHashSet();
        var parcelIds = message.Parcels.Select(static item => item.Id).ToHashSet();
        foreach (var corridor in message.Corridors)
        {
            CollectionAssert.Contains(settlementIds.ToList(), corridor.FromSettlementId);
            CollectionAssert.Contains(settlementIds.ToList(), corridor.ToSettlementId);
        }
        foreach (var parcel in message.Parcels)
        {
            CollectionAssert.Contains(settlementIds.ToList(), parcel.SettlementId);
            CollectionAssert.Contains(districtIds.ToList(), parcel.DistrictId);
        }
        foreach (var building in message.Buildings)
            CollectionAssert.Contains(parcelIds.ToList(), building.ParcelId);
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
        Assert.IsTrue(ProtocolFrameHeader.TryRead(frame, out var header, out var error), error.ToString());
        return (header, frame);
    }
}
