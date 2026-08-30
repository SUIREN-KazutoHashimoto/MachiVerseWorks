using System.Net.WebSockets;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ClientConnectionStateTests
{
    [TestMethod]
    public void StaleSubscriptionCommitPreservesDeliveredAgentsForNextVolume()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        var firstVolume = new WorldVolume(-100d, -100d, -20d, 100d, 100d, 40d);
        var nextVolume = new WorldVolume(1_000d, 1_000d, 80d, 1_100d, 1_100d, 120d);

        connection.SetSubscription(firstVolume);
        Assert.IsTrue(connection.TryCaptureSubscription(out var firstSubscription));
        connection.SetSubscription(nextVolume);
        var deliveredAgentIds = new HashSet<ulong> { 1, 2, 3 };

        Assert.IsFalse(connection.TryReplaceKnownAgentIds(firstSubscription.Revision, deliveredAgentIds));
        Assert.IsTrue(connection.TryCaptureSubscription(out var nextSubscription));
        Assert.AreEqual(nextVolume, nextSubscription.Volume);
        CollectionAssert.AreEquivalent(deliveredAgentIds.ToArray(), nextSubscription.KnownAgentIds.ToArray());
    }

    [TestMethod]
    public void StaleSubscriptionCommitAlsoAppliesDeliveredRemoves()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        connection.SetSubscription(new WorldVolume(-100d, -100d, -20d, 100d, 100d, 40d));
        Assert.IsTrue(connection.TryCaptureSubscription(out var initialSubscription));
        Assert.IsTrue(connection.TryReplaceKnownAgentIds(initialSubscription.Revision, new HashSet<ulong> { 10 }));
        Assert.IsTrue(connection.TryCaptureSubscription(out var staleSubscription));

        connection.SetSubscription(new WorldVolume(1_000d, 1_000d, 80d, 1_100d, 1_100d, 120d));
        Assert.IsFalse(connection.TryReplaceKnownAgentIds(staleSubscription.Revision, []));
        Assert.IsTrue(connection.TryCaptureSubscription(out var nextSubscription));
        Assert.AreEqual(0, nextSubscription.KnownAgentIds.Count);
    }

    [TestMethod]
    public void StaleSubscriptionCommitPreservesDeliveredVehiclesForNextVolume()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        connection.SetSubscription(new WorldVolume(-100d, -100d, -20d, 100d, 100d, 40d));
        Assert.IsTrue(connection.TryCaptureSubscription(out var staleSubscription));
        connection.SetSubscription(new WorldVolume(1_000d, 1_000d, 80d, 1_100d, 1_100d, 120d));
        var deliveredVehicleIds = new HashSet<ulong> { 100, 101 };

        Assert.IsFalse(connection.TryReplaceKnownEntityIds(
            staleSubscription.Revision,
            [],
            [],
            deliveredVehicleIds));
        Assert.IsTrue(connection.TryCaptureSubscription(out var nextSubscription));
        CollectionAssert.AreEquivalent(deliveredVehicleIds.ToArray(), nextSubscription.KnownVehicleIds.ToArray());
    }

    [TestMethod]
    public void RoadSnapshotIsNeededOncePerSubscriptionAndRoadRevision()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        connection.SetSubscription(new WorldVolume(-100, -100, -100, 100, 100, 100));
        Assert.IsTrue(connection.TryCaptureSubscription(out var subscription));

        Assert.IsTrue(connection.NeedsRoadSnapshot(subscription.Revision, 1));
        Assert.IsTrue(connection.TryMarkRoadSnapshotDelivered(subscription.Revision, 1));
        Assert.IsFalse(connection.NeedsRoadSnapshot(subscription.Revision, 1));
        Assert.IsTrue(connection.NeedsRoadSnapshot(subscription.Revision, 2));

        connection.SetSubscription(new WorldVolume(-200, -200, -200, 200, 200, 200));
        Assert.IsTrue(connection.TryCaptureSubscription(out var next));
        Assert.IsTrue(connection.NeedsRoadSnapshot(next.Revision, 1));
    }

    [TestMethod]
    public void StaleRoadDeliveryDoesNotSuppressCurrentSubscriptionRoadSnapshot()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        connection.SetSubscription(new WorldVolume(-100, -100, -100, 100, 100, 100));
        Assert.IsTrue(connection.TryCaptureSubscription(out var stale));
        connection.SetSubscription(new WorldVolume(0, 0, 0, 10, 10, 10));
        Assert.IsTrue(connection.TryCaptureSubscription(out var current));

        Assert.IsFalse(connection.TryMarkRoadSnapshotDelivered(stale.Revision, 1));
        Assert.IsTrue(connection.NeedsRoadSnapshot(current.Revision, 1));
    }

    private sealed class StubWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
