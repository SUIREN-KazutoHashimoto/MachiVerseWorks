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
