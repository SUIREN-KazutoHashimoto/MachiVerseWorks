using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class GatewayPhase3IntegrationTests
{
    [TestMethod]
    public async Task RapidSubscriptionChangesEventuallyCommitOnlyFinalVolumeState()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 4, snapshotRate: 60);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);
        var registry = host.App.Services.GetRequiredService<ClientConnectionRegistry>();
        await WaitUntilAsync(() => registry.Count == 1, TimeSpan.FromSeconds(2));
        var connection = registry.CreateSnapshot().Single();

        var initial = new SubscribeVolumeMessage(-100d, -100d, -100d, 100d, 100d, 100d);
        await ServerTestHost.SendAsync(socket, initial, ProtocolVersion.Current);
        await WaitUntilAsync(() =>
            connection.TryCaptureSubscription(out var state)
            && state.KnownAgentIds.Count == 4
            && state.CommittedDelivery?.SubscriptionRevision == state.Revision,
            TimeSpan.FromSeconds(5));

        var farA = new SubscribeVolumeMessage(1000d, 1000d, 1000d, 1100d, 1100d, 1100d);
        var nearAgain = new SubscribeVolumeMessage(-100d, -100d, -100d, 100d, 100d, 100d);
        var final = new SubscribeVolumeMessage(2000d, 2000d, 2000d, 2100d, 2100d, 2100d);
        await ServerTestHost.SendAsync(socket, farA, ProtocolVersion.Current);
        await ServerTestHost.SendAsync(socket, nearAgain, ProtocolVersion.Current);
        await ServerTestHost.SendAsync(socket, final, ProtocolVersion.Current);

        await WaitUntilAsync(() =>
        {
            if (!connection.TryCaptureSubscription(out var state)) return false;
            var expected = new WorldVolume(final.MinX, final.MinY, final.MinZ, final.MaxX, final.MaxY, final.MaxZ);
            return state.Volume == expected
                && state.KnownAgentIds.Count == 0
                && state.CommittedDelivery?.SubscriptionRevision == state.Revision
                && state.RoadDelivery?.SubscriptionRevision == state.Revision
                && state.RailwayDelivery?.SubscriptionRevision == state.Revision;
        }, TimeSpan.FromSeconds(8));
    }

    [TestMethod]
    public async Task WorldReplacementRemovesOldGenerationBeforeRespawningReusedAgentId()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 2, snapshotRate: 60);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);
        await ServerTestHost.SendAsync(socket, new SubscribeVolumeMessage(-100d, -100d, -100d, 100d, 100d, 100d), ProtocolVersion.Current);

        var initialIds = new HashSet<ulong>();
        while (initialIds.Count < 2)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (message is AgentSpawnMessage spawn) initialIds.Add(spawn.AgentId);
        }
        Assert.IsTrue(initialIds.Contains(1));

        var replacement = new SimulationWorld(new SimulationConfig(30, 999UL, 64d));
        replacement.CreateAgents(1, new WorldVolume(-1d, -1d, -1d, 1d, 1d, 1d));
        var runtime = host.App.Services.GetRequiredService<SimulationRuntime>();
        runtime.ReplaceWorld(replacement);

        var removedReusedId = false;
        var respawnedReusedId = false;
        for (var index = 0; index < 128 && !respawnedReusedId; index++)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (message is AgentRemoveMessage remove && remove.AgentId == 1) removedReusedId = true;
            if (message is AgentSpawnMessage spawn && spawn.AgentId == 1)
            {
                Assert.IsTrue(removedReusedId, "A reused entity ID must be removed from the old generation before it is spawned in the new generation.");
                respawnedReusedId = true;
            }
        }
        Assert.IsTrue(respawnedReusedId);
    }

    [TestMethod]
    public async Task Protocol20ClientReceivesOnlyProtocol20ObservationMessages()
    {
        var version = new ProtocolVersion(2, 0);
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 2, snapshotRate: 30);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.SendAsync(socket, new HelloMessage(), version);
        var hello = Assert.IsInstanceOfType<HelloAckMessage>((await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message);
        Assert.AreEqual(version, hello.ProtocolVersion);

        await ServerTestHost.SendAsync(socket, new SubscribeVolumeMessage(-100d, -100d, -100d, 100d, 100d, 100d), version);
        var received = 0;
        while (received < 8)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            Assert.IsTrue(
                message is AgentSpawnMessage or AgentUpdateMessage or AgentRemoveMessage,
                $"Protocol 2.0 client received unsupported observation message {message.Type}.");
            received++;
        }
    }

    [TestMethod]
    public async Task RapidInspectThenClearLeavesNoSelectedPersonState()
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Simulation:PopulationFixture"] = "true",
        };
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 0, snapshotRate: 60, additionalConfiguration: configuration);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);
        var registry = host.App.Services.GetRequiredService<ClientConnectionRegistry>();
        await WaitUntilAsync(() => registry.Count == 1, TimeSpan.FromSeconds(2));
        var connection = registry.CreateSnapshot().Single();

        await ServerTestHost.SendAsync(socket, new InspectPersonMessage(1), ProtocolVersion.Current);
        await ServerTestHost.SendAsync(socket, new ClearPersonInspectionMessage(), ProtocolVersion.Current);

        await WaitUntilAsync(() =>
        {
            var inspection = connection.CaptureInspectionState();
            return inspection.Revision >= 2 && inspection.PersonId is null;
        }, TimeSpan.FromSeconds(3));
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Condition was not satisfied before timeout.");
            await Task.Delay(20);
        }
    }
}
