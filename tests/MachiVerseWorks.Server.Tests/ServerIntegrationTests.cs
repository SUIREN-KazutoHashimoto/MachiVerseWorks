using System.Net;
using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ServerIntegrationTests
{
    [TestMethod]
    public async Task ServerStartsHealthEndpointAndStopsTickLoopGracefully()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 0, tickRate: 60);
        using var client = host.CreateHttpClient();

        using var response = await client.GetAsync("/health");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        await WaitUntilAsync(() => simulation.TickCount > 0, TimeSpan.FromSeconds(2));

        await host.StopAsync();
        var stoppedTick = simulation.TickCount;
        await Task.Delay(100);
        Assert.AreEqual(stoppedTick, simulation.TickCount);
    }

    [TestMethod]
    public async Task WebSocketHandshakeReturnsHelloAck()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 0, tickRate: 30);
        using var socket = await host.ConnectWebSocketAsync();

        await ServerTestHost.SendAsync(socket, new HelloMessage(), ProtocolVersion.Current);
        var envelope = await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3));

        var helloAck = Assert.IsInstanceOfType<HelloAckMessage>(envelope.Message);
        Assert.AreEqual(ProtocolVersion.Current, helloAck.ProtocolVersion);
        Assert.AreEqual((ushort)30, helloAck.TickRate);
    }

    [TestMethod]
    public async Task SubscribeAreaPublishesSpawnThenUpdate()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 4, snapshotRate: 30);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);

        await ServerTestHost.SendAsync(
            socket,
            new SubscribeAreaMessage(-100d, -100d, 100d, 100d),
            ProtocolVersion.Current);

        ulong? spawnedAgentId = null;
        var sawUpdate = false;
        for (var index = 0; index < 32 && !sawUpdate; index++)
        {
            var envelope = await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3));
            switch (envelope.Message)
            {
                case AgentSpawnMessage spawn:
                    spawnedAgentId ??= spawn.AgentId;
                    break;
                case AgentUpdateMessage update when spawnedAgentId == update.AgentId:
                    sawUpdate = true;
                    break;
            }
        }

        Assert.IsNotNull(spawnedAgentId);
        Assert.IsTrue(sawUpdate);
    }

    [TestMethod]
    public async Task ChangingSubscriptionRemovesAgentsFromPreviousArea()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 4, snapshotRate: 60);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);

        await ServerTestHost.SendAsync(
            socket,
            new SubscribeAreaMessage(-100d, -100d, 100d, 100d),
            ProtocolVersion.Current);

        var knownAgentIds = new HashSet<ulong>();
        var sawUpdate = false;
        for (var index = 0; index < 64 && (knownAgentIds.Count < 4 || !sawUpdate); index++)
        {
            var envelope = await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3));
            switch (envelope.Message)
            {
                case AgentSpawnMessage spawn:
                    knownAgentIds.Add(spawn.AgentId);
                    break;
                case AgentUpdateMessage update when knownAgentIds.Contains(update.AgentId):
                    sawUpdate = true;
                    break;
            }
        }

        Assert.AreEqual(4, knownAgentIds.Count);
        Assert.IsTrue(sawUpdate);

        await ServerTestHost.SendAsync(
            socket,
            new SubscribeAreaMessage(1000d, 1000d, 1100d, 1100d),
            ProtocolVersion.Current);

        var removedAgentIds = new HashSet<ulong>();
        for (var index = 0; index < 64 && removedAgentIds.Count < knownAgentIds.Count; index++)
        {
            var envelope = await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3));
            if (envelope.Message is AgentRemoveMessage remove && knownAgentIds.Contains(remove.AgentId))
            {
                removedAgentIds.Add(remove.AgentId);
            }
        }

        CollectionAssert.AreEquivalent(knownAgentIds.ToArray(), removedAgentIds.ToArray());
    }

    [TestMethod]
    public async Task SubscriptionOutsideSpatialGridReturnsErrorAndConnectionRemainsUsable()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 1, snapshotRate: 30);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);

        await ServerTestHost.SendAsync(
            socket,
            new SubscribeAreaMessage(-1e300, -1e300, 1e300, 1e300),
            ProtocolVersion.Current);

        var errorEnvelope = await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3));
        var error = Assert.IsInstanceOfType<ProtocolErrorMessage>(errorEnvelope.Message);
        Assert.AreEqual(ProtocolErrorCode.InvalidRequest, error.Code);
        Assert.IsTrue(error.Parameters.Any(parameter =>
            parameter.Key == ProtocolErrorParameterKeys.DetailCode &&
            parameter.Value == SubscriptionAreaPolicy.OutsideSpatialGridDetailCode));

        await ServerTestHost.SendAsync(
            socket,
            new SubscribeAreaMessage(-100d, -100d, 100d, 100d),
            ProtocolVersion.Current);

        var sawSpawn = false;
        for (var index = 0; index < 16 && !sawSpawn; index++)
        {
            var envelope = await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3));
            sawSpawn = envelope.Message is AgentSpawnMessage;
        }

        Assert.IsTrue(sawSpawn);
    }

    [TestMethod]
    public async Task DisconnectRemovesConnectionState()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 0);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);

        var connections = host.App.Services.GetRequiredService<ClientConnectionRegistry>();
        await WaitUntilAsync(() => connections.Count == 1, TimeSpan.FromSeconds(2));

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test", CancellationToken.None);
        await WaitUntilAsync(() => connections.Count == 0, TimeSpan.FromSeconds(2));

        Assert.AreEqual(0, connections.Count);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail("Condition was not satisfied before timeout.");
            }

            await Task.Delay(20);
        }
    }
}
