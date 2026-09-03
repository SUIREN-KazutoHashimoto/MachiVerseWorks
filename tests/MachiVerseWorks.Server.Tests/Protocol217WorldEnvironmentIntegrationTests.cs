using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class Protocol217WorldEnvironmentIntegrationTests
{
    [TestMethod]
    public async Task Protocol217HandshakeAndSubscriptionPublishWorldEnvironmentSnapshot()
    {
        var protocol217 = new ProtocolVersion(2, 17);
        var additionalConfiguration = new Dictionary<string, string?>
        {
            ["Simulation:Seed"] = "29027",
            ["Simulation:SpatialCellSize"] = "4096",
            ["Server:MaximumSubscriptionCellCount"] = "524288",
        };

        await using var host = await ServerTestHost.StartAsync(
            initialAgentCount: 0,
            snapshotRate: 2,
            additionalConfiguration: additionalConfiguration);
        using var socket = await host.ConnectWebSocketAsync();

        var helloAck = await ServerTestHost.HandshakeAsync(socket, protocol217);
        Assert.AreEqual(protocol217, helloAck.ProtocolVersion);

        var volume = new WorldVolume(-500_000d, -500_000d, -12_000d, 500_000d, 500_000d, 12_000d);
        await ServerTestHost.SendAsync(
            socket,
            new SubscribeVolumeMessage(volume.MinX, volume.MinY, volume.MinZ, volume.MaxX, volume.MaxY, volume.MaxZ),
            protocol217);

        for (var index = 0; index < 32; index++)
        {
            var envelope = await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3));
            if (envelope.Message is ProtocolErrorMessage error)
            {
                Assert.Fail($"World environment subscription was rejected: {error.Code}: {string.Join(", ", error.Parameters.Select(static item => $"{item.Key}={item.Value}"))}");
            }

            if (envelope.Message is not WorldEnvironmentSnapshotMessage environment) continue;
            Assert.AreEqual(protocol217, envelope.Version);
            Assert.AreEqual(64, environment.Samples.Count);
            Assert.AreEqual(environment.Samples.Count, environment.TerrainSamples.Count);
            Assert.IsTrue(environment.Features.Count > 0);
            return;
        }

        Assert.Fail("WorldEnvironmentSnapshot was not published after a valid Protocol 2.17 subscription.");
    }
}
