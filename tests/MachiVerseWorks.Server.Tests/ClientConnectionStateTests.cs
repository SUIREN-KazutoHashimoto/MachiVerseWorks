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
