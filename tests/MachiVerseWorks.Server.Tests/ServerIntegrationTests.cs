using System.Net;
using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ServerIntegrationTests
{
    private static readonly SubscribeVolumeMessage FullTestVolume = new(-100d, -100d, -100d, 100d, 100d, 100d);

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
    public async Task SubscribeVolumePublishesSpawnThenUpdateWithThreeDimensionalState()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 4, snapshotRate: 30);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);
        await ServerTestHost.SendAsync(socket, FullTestVolume, ProtocolVersion.Current);

        ulong? spawnedAgentId = null;
        AgentSpawnMessage? spawnMessage = null;
        var sawUpdate = false;
        for (var index = 0; index < 32 && !sawUpdate; index++)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            switch (message)
            {
                case AgentSpawnMessage spawn:
                    spawnedAgentId ??= spawn.AgentId;
                    spawnMessage ??= spawn;
                    break;
                case AgentUpdateMessage update when spawnedAgentId == update.AgentId:
                    sawUpdate = true;
                    break;
            }
        }
        Assert.IsNotNull(spawnedAgentId);
        Assert.IsNotNull(spawnMessage);
        Assert.IsTrue(double.IsFinite(spawnMessage.Z));
        Assert.IsTrue(double.IsFinite(spawnMessage.VelocityZ));
        Assert.IsTrue(sawUpdate);
    }

    [TestMethod]
    public async Task ChangingSubscriptionVolumeRemovesAgentsFromPreviousVolume()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 4, snapshotRate: 60);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);
        await ServerTestHost.SendAsync(socket, FullTestVolume, ProtocolVersion.Current);

        var knownAgentIds = new HashSet<ulong>();
        var sawUpdate = false;
        for (var index = 0; index < 64 && (knownAgentIds.Count < 4 || !sawUpdate); index++)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (message is AgentSpawnMessage spawn) knownAgentIds.Add(spawn.AgentId);
            if (message is AgentUpdateMessage update && knownAgentIds.Contains(update.AgentId)) sawUpdate = true;
        }
        Assert.AreEqual(4, knownAgentIds.Count);
        Assert.IsTrue(sawUpdate);

        await ServerTestHost.SendAsync(socket, new SubscribeVolumeMessage(1000d, 1000d, 1000d, 1100d, 1100d, 1100d), ProtocolVersion.Current);
        var removedAgentIds = new HashSet<ulong>();
        for (var index = 0; index < 64 && removedAgentIds.Count < knownAgentIds.Count; index++)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (message is AgentRemoveMessage remove && knownAgentIds.Contains(remove.AgentId)) removedAgentIds.Add(remove.AgentId);
        }
        CollectionAssert.AreEquivalent(knownAgentIds.ToArray(), removedAgentIds.ToArray());
    }

    [TestMethod]
    public async Task SubscriptionOutsideSpatialGridReturnsErrorAndConnectionRemainsUsable()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 1, snapshotRate: 30);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);
        await ServerTestHost.SendAsync(socket, new SubscribeVolumeMessage(-1e300, -1e300, -1e300, 1e300, 1e300, 1e300), ProtocolVersion.Current);

        var error = Assert.IsInstanceOfType<ProtocolErrorMessage>(await ReceiveUntilAsync(socket, static message => message is ProtocolErrorMessage));
        Assert.AreEqual(ProtocolErrorCode.InvalidRequest, error.Code);
        Assert.IsTrue(error.Parameters.Any(parameter => parameter.Key == ProtocolErrorParameterKeys.DetailCode && parameter.Value == SubscriptionVolumePolicy.OutsideSpatialGridDetailCode));

        await ServerTestHost.SendAsync(socket, FullTestVolume, ProtocolVersion.Current);
        Assert.IsInstanceOfType<AgentSpawnMessage>(await ReceiveUntilAsync(socket, static message => message is AgentSpawnMessage));
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

    [TestMethod]
    public async Task ReconnectCanReceiveFreshSubscriptionVolumeState()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 4, snapshotRate: 30);
        using (var firstSocket = await host.ConnectWebSocketAsync())
        {
            await ServerTestHost.HandshakeAsync(firstSocket);
            await ServerTestHost.SendAsync(firstSocket, FullTestVolume, ProtocolVersion.Current);
            await ReceiveUntilAsync(firstSocket, static message => message is AgentSpawnMessage);
            await firstSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", CancellationToken.None);
        }
        using var secondSocket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(secondSocket);
        await ServerTestHost.SendAsync(secondSocket, FullTestVolume, ProtocolVersion.Current);
        Assert.IsInstanceOfType<AgentSpawnMessage>(await ReceiveUntilAsync(secondSocket, static message => message is AgentSpawnMessage));
    }

    [TestMethod]
    public async Task E2eMetricsRecordSnapshotBytesEncodeAndSendTime()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 4, snapshotRate: 30);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);
        await ServerTestHost.SendAsync(socket, FullTestVolume, ProtocolVersion.Current);
        var metrics = host.App.Services.GetRequiredService<E2eMetrics>();
        await WaitUntilAsync(() => metrics.Capture().TotalSnapshotDeliveries > 0, TimeSpan.FromSeconds(3));
        var snapshot = metrics.Capture();
        Assert.IsTrue(snapshot.TotalMessages > 0);
        Assert.IsTrue(snapshot.TotalBytes > 0);
        Assert.IsTrue(snapshot.TotalEncodeTimeMs >= 0d);
        Assert.IsTrue(snapshot.TotalSendTimeMs >= 0d);
    }

    [TestMethod]
    [DataRow(10_000)]
    [DataRow(100_000)]
    public async Task LargeThreeDimensionalSimulationPublishesOnlySubscribedAgents(int initialAgentCount)
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: initialAgentCount, snapshotRate: 20, spawnHalfExtent: 500d);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);
        await ServerTestHost.SendAsync(socket, new SubscribeVolumeMessage(-50d, -50d, -50d, 50d, 50d, 50d), ProtocolVersion.Current);
        var metrics = host.App.Services.GetRequiredService<E2eMetrics>();
        await WaitUntilAsync(() => metrics.Capture().LastAgentCount > 0, TimeSpan.FromSeconds(10));
        var snapshot = metrics.Capture();
        Assert.IsTrue(snapshot.LastAgentCount > 0);
        Assert.IsTrue(snapshot.LastAgentCount < initialAgentCount);
        Assert.IsTrue(snapshot.LastMessageCount > 0);
        Assert.IsTrue(snapshot.LastBytes > 0);
    }

    private static async Task<IProtocolMessage> ReceiveUntilAsync(ClientWebSocket socket, Func<IProtocolMessage, bool> predicate)
    {
        for (var index = 0; index < 128; index++)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (predicate(message)) return message;
        }
        Assert.Fail("Expected protocol message was not received.");
        throw new InvalidOperationException();
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
