using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class GatewayPhase3ReviewTests
{
    [TestMethod]
    public async Task RemovingConnectionDiscardsSchedulerStateEvenWithWaitingLane()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var registry = new ClientConnectionRegistry(scheduler);
        using var socket = new BlockingWebSocket();
        using var connection = registry.Register(socket);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(scheduler.TryReserve(connection.Id, ObservationDeliveryLane.Snapshot));
        scheduler.StartReserved(connection.Id, () => release.Task);
        Assert.IsFalse(scheduler.TryReserve(connection.Id, ObservationDeliveryLane.Population));
        Assert.AreEqual(1, scheduler.TrackedConnectionCount);

        Assert.IsTrue(registry.Remove(connection.Id));
        Assert.AreEqual(0, scheduler.TrackedConnectionCount);

        release.SetResult();
        await Task.Yield();
        Assert.AreEqual(0, scheduler.TrackedConnectionCount);
    }

    [TestMethod]
    public void RemovingConnectionBetweenReserveAndStartCancelsReservedDelivery()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var registry = new ClientConnectionRegistry(scheduler);
        using var socket = new BlockingWebSocket();
        using var connection = registry.Register(socket);
        var deliveryFactoryInvoked = false;

        Assert.IsTrue(scheduler.TryReserve(connection.Id, ObservationDeliveryLane.Snapshot));
        Assert.IsTrue(registry.Remove(connection.Id));
        Assert.AreEqual(1, scheduler.TrackedConnectionCount);

        var started = scheduler.StartReserved(
            connection.Id,
            () =>
            {
                deliveryFactoryInvoked = true;
                return Task.CompletedTask;
            });

        Assert.IsFalse(started);
        Assert.IsFalse(deliveryFactoryInvoked);
        Assert.AreEqual(0, scheduler.TrackedConnectionCount);
    }

    [TestMethod]
    public async Task DomainDeliveryLanesYieldToEachOtherUnderContention()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var connectionId = Guid.NewGuid();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Economy));
        scheduler.StartReserved(connectionId, () => release.Task);
        Assert.IsFalse(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Logistics));
        Assert.IsFalse(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Power));

        release.SetResult();
        await WaitUntilAsync(() => scheduler.InFlightCount == 0, TimeSpan.FromSeconds(1));

        Assert.IsFalse(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Economy));
        Assert.IsTrue(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Logistics));
        scheduler.ReleaseReservation(connectionId);

        Assert.IsFalse(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Economy));
        Assert.IsTrue(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Power));
        scheduler.ReleaseReservation(connectionId);

        Assert.IsTrue(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Economy));
        scheduler.ReleaseReservation(connectionId);
    }

    [TestMethod]
    public async Task InspectionIsRevalidatedAfterWaitingForSendGate()
    {
        using var socket = new BlockingWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        var version = new ProtocolVersion(2, 0);
        var message = new ProtocolErrorMessage(ProtocolErrorCode.InvalidRequest, []);
        var cache = new ObservationCache();

        connection.SetInspectedPerson(10);
        var inspection = connection.CaptureInspectionState();

        var blockingSend = connection.SendAsync(message, version, CancellationToken.None);
        await socket.FirstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var guardedSend = connection.SendCachedIfInspectionCurrentAsync(
            message,
            version,
            new EncodedObservationCacheKey(
                "inspection-race-test",
                version,
                new ObservationRevision(1, 1),
                "person:10"),
            cache,
            inspection,
            CancellationToken.None);

        connection.ClearPersonInspection();
        socket.ReleaseFirstSend.SetResult();

        _ = await blockingSend;
        var guardedResult = await guardedSend;
        Assert.IsNull(guardedResult);
        Assert.AreEqual(1, socket.SendCount);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Condition was not satisfied before timeout.");
            await Task.Delay(10);
        }
    }

    private sealed class BlockingWebSocket : WebSocket
    {
        private int _sendCount;

        public TaskCompletionSource FirstSendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SendCount => Volatile.Read(ref _sendCount);
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public override async Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _sendCount) == 1)
            {
                FirstSendStarted.TrySetResult();
                await ReleaseFirstSend.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
