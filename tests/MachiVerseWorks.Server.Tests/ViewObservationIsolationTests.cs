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
        new(-60d, -140d, -80d, 140d, 60d, 120d),
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
                await ReceiveUntilAgentSnapshotAsync(socket);

                foreach (var navigationVolume in NavigationVolumes)
                {
                    await ServerTestHost.SendAsync(socket, navigationVolume, ProtocolVersion.Current);
                    await ServerTestHost.SendAsync(socket, FullTestVolume, ProtocolVersion.Current);
                    await ReceiveUntilAgentSnapshotAsync(socket);
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

    private static async Task ReceiveUntilAgentSnapshotAsync(ClientWebSocket socket)
    {
        for (var index = 0; index < 64; index++)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (message is AgentSpawnMessage or AgentUpdateMessage) return;
        }

        Assert.Fail("Expected an agent observation snapshot was not received.");
    }
}
