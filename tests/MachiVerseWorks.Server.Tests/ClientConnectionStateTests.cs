using System.Net.WebSockets;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ClientConnectionStateTests
{
    [TestMethod]
    public void StaleSubscriptionCommitUpdatesCommittedStateWithoutChangingDesiredRevision()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        var firstVolume = new WorldVolume(-100d, -100d, -20d, 100d, 100d, 40d);
        var nextVolume = new WorldVolume(1_000d, 1_000d, 80d, 1_100d, 1_100d, 120d);

        connection.SetSubscription(firstVolume);
        Assert.IsTrue(connection.TryCaptureSubscription(out var firstSubscription));
        connection.SetSubscription(nextVolume);
        var deliveredAgentIds = new HashSet<ulong> { 1, 2, 3 };

        Assert.IsFalse(connection.TryReplaceKnownEntityIds(
            firstSubscription.Revision,
            observationGeneration: 7,
            observationRevision: 11,
            deliveredAgentIds,
            [],
            []));

        Assert.IsTrue(connection.TryCaptureSubscription(out var nextSubscription));
        Assert.AreEqual(nextVolume, nextSubscription.Volume);
        Assert.AreEqual(firstSubscription.Revision + 1, nextSubscription.Revision);
        Assert.IsTrue(nextSubscription.CommittedDelivery.HasValue);
        Assert.AreEqual(firstSubscription.Revision, nextSubscription.CommittedDelivery.Value.SubscriptionRevision);
        Assert.AreEqual(7UL, nextSubscription.CommittedDelivery.Value.ObservationGeneration);
        Assert.AreEqual(11UL, nextSubscription.CommittedDelivery.Value.ObservationRevision);
        CollectionAssert.AreEquivalent(deliveredAgentIds.ToArray(), nextSubscription.KnownAgentIds.ToArray());
    }

    [TestMethod]
    public void StaleSubscriptionCommitAlsoAppliesDeliveredRemovesForNextConvergence()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        connection.SetSubscription(new WorldVolume(-100d, -100d, -20d, 100d, 100d, 40d));
        Assert.IsTrue(connection.TryCaptureSubscription(out var initialSubscription));
        Assert.IsTrue(connection.TryReplaceKnownEntityIds(initialSubscription.Revision, 1, 1, new HashSet<ulong> { 10 }, [], []));
        Assert.IsTrue(connection.TryCaptureSubscription(out var staleSubscription));

        connection.SetSubscription(new WorldVolume(1_000d, 1_000d, 80d, 1_100d, 1_100d, 120d));
        Assert.IsFalse(connection.TryReplaceKnownEntityIds(staleSubscription.Revision, 1, 2, [], [], []));
        Assert.IsTrue(connection.TryCaptureSubscription(out var nextSubscription));
        Assert.AreEqual(0, nextSubscription.KnownAgentIds.Count);
        Assert.AreEqual(staleSubscription.Revision, nextSubscription.CommittedDelivery?.SubscriptionRevision);
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
            2,
            3,
            [],
            [],
            deliveredVehicleIds));
        Assert.IsTrue(connection.TryCaptureSubscription(out var nextSubscription));
        CollectionAssert.AreEquivalent(deliveredVehicleIds.ToArray(), nextSubscription.KnownVehicleIds.ToArray());
    }

    [TestMethod]
    public void RoadAndRailwayMarkersIncludeObservationGeneration()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        connection.SetSubscription(new WorldVolume(-100, -100, -100, 100, 100, 100));
        Assert.IsTrue(connection.TryCaptureSubscription(out var subscription));

        Assert.IsTrue(connection.NeedsRoadSnapshot(subscription.Revision, 5, 1));
        Assert.IsTrue(connection.TryMarkRoadSnapshotDelivered(subscription.Revision, 5, 1));
        Assert.IsFalse(connection.NeedsRoadSnapshot(subscription.Revision, 5, 1));
        Assert.IsTrue(connection.NeedsRoadSnapshot(subscription.Revision, 6, 1));
        Assert.IsTrue(connection.NeedsRoadSnapshot(subscription.Revision, 5, 2));

        Assert.IsTrue(connection.NeedsRailwaySnapshot(subscription.Revision, 5, 3));
        Assert.IsTrue(connection.TryMarkRailwaySnapshotDelivered(subscription.Revision, 5, 3));
        Assert.IsFalse(connection.NeedsRailwaySnapshot(subscription.Revision, 5, 3));
        Assert.IsTrue(connection.NeedsRailwaySnapshot(subscription.Revision, 6, 3));
    }

    [TestMethod]
    public void StaleStaticDeliveryDoesNotSuppressCurrentSubscription()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        connection.SetSubscription(new WorldVolume(-100, -100, -100, 100, 100, 100));
        Assert.IsTrue(connection.TryCaptureSubscription(out var stale));
        connection.SetSubscription(new WorldVolume(0, 0, 0, 10, 10, 10));
        Assert.IsTrue(connection.TryCaptureSubscription(out var current));

        Assert.IsFalse(connection.TryMarkRoadSnapshotDelivered(stale.Revision, 7, 1));
        Assert.IsFalse(connection.TryMarkRailwaySnapshotDelivered(stale.Revision, 7, 1));
        Assert.IsTrue(connection.NeedsRoadSnapshot(current.Revision, 7, 1));
        Assert.IsTrue(connection.NeedsRailwaySnapshot(current.Revision, 7, 1));
    }

    [TestMethod]
    public void InspectionRevisionChangesOnSelectAndClear()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        var initial = connection.CaptureInspectionState();

        connection.SetInspectedPerson(10);
        var selected = connection.CaptureInspectionState();
        Assert.AreEqual(10UL, selected.PersonId);
        Assert.AreEqual(initial.Revision + 1, selected.Revision);
        Assert.IsTrue(connection.IsInspectionCurrent(selected));

        connection.ClearPersonInspection();
        var cleared = connection.CaptureInspectionState();
        Assert.IsNull(cleared.PersonId);
        Assert.AreEqual(selected.Revision + 1, cleared.Revision);
        Assert.IsFalse(connection.IsInspectionCurrent(selected));
        Assert.IsTrue(connection.IsInspectionCurrent(cleared));
    }

    [TestMethod]
    public void ReconnectedConnectionStartsWithoutConnectionLocalDeliveryState()
    {
        var registry = new ClientConnectionRegistry();
        using var firstSocket = new StubWebSocket();
        using var secondSocket = new StubWebSocket();
        using var first = registry.Register(firstSocket);
        first.SetSubscription(new WorldVolume(-10, -10, -10, 10, 10, 10));
        Assert.IsTrue(first.TryCaptureSubscription(out var firstSubscription));
        Assert.IsTrue(first.TryReplaceKnownEntityIds(firstSubscription.Revision, 4, 9, new HashSet<ulong> { 1 }, [], []));
        first.SetInspectedPerson(20);
        Assert.IsTrue(registry.Remove(first.Id));

        using var second = registry.Register(secondSocket);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.IsFalse(second.TryCaptureSubscription(out _));
        Assert.IsNull(second.CaptureInspectionState().PersonId);
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
