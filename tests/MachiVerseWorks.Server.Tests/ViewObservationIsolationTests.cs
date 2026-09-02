using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ViewObservationIsolationTests
{
    private static readonly SubscribeVolumeMessage FullTestVolume = new(-100d, -100d, -100d, 100d, 100d, 100d);
    private static readonly SubscribeVolumeMessage[] NavigationVolumes =
    [
        new(9_900d, -100d, -100d, 10_100d, 100d, 100d),
        new(999_900d, -2_000_100d, -100d, 1_000_100d, -1_999_900d, 100d),
        new(-100d, -100d, 900d, 100d, 100d, 1_100d),
    ];

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    public async Task ViewConnectionsDoNotChangeAuthoritativeSimulationDigest(int viewConnectionCount)
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 4, snapshotRate: 30);
        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        simulation.Pause();
        var beforeDigest = ComputeDigest(simulation.CaptureCheckpoint());
        var sockets = new List<ClientWebSocket>(viewConnectionCount);

        try
        {
            for (var index = 0; index < viewConnectionCount; index++)
            {
                var socket = await host.ConnectWebSocketAsync();
                sockets.Add(socket);
                await ServerTestHost.HandshakeAsync(socket);
                await ServerTestHost.SendAsync(socket, FullTestVolume, ProtocolVersion.Current);
                var knownAgentId = await ReceiveUntilVisibleAgentAsync(socket);

                foreach (var navigationVolume in NavigationVolumes)
                {
                    await ServerTestHost.SendAsync(socket, navigationVolume, ProtocolVersion.Current);
                    await ReceiveUntilAgentRemoveAsync(socket, knownAgentId);

                    await ServerTestHost.SendAsync(socket, FullTestVolume, ProtocolVersion.Current);
                    await ReceiveUntilAgentSpawnAsync(socket, knownAgentId);
                }
            }

            var afterDigest = ComputeDigest(simulation.CaptureCheckpoint());
            Assert.AreEqual(beforeDigest, afterDigest, $"{viewConnectionCount} read-only View connection(s) changed authoritative simulation state while navigating subscriptions.");
        }
        finally
        {
            foreach (var socket in sockets) socket.Dispose();
        }
    }

    private static string ComputeDigest(SimulationCheckpoint checkpoint)
    {
        var canonicalBytes = JsonSerializer.SerializeToUtf8Bytes(checkpoint);
        return Convert.ToHexString(SHA256.HashData(canonicalBytes));
    }

    private static async Task<ulong> ReceiveUntilVisibleAgentAsync(ClientWebSocket socket)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
                if (message is AgentSpawnMessage spawn) return spawn.AgentId;
                if (message is AgentUpdateMessage update) return update.AgentId;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }

        Assert.Fail("Expected an initial visible agent observation was not received.");
        return 0;
    }

    private static async Task ReceiveUntilAgentRemoveAsync(ClientWebSocket socket, ulong agentId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (message is AgentRemoveMessage remove && remove.AgentId == agentId) return;
        }

        Assert.Fail($"Expected AgentRemove for agent {agentId} after the navigation subscription was not received.");
    }

    private static async Task ReceiveUntilAgentSpawnAsync(ClientWebSocket socket, ulong agentId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (message is AgentSpawnMessage spawn && spawn.AgentId == agentId) return;
        }

        Assert.Fail($"Expected AgentSpawn for agent {agentId} after restoring the full subscription was not received.");
    }
}
