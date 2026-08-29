using System.Net.WebSockets;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ClientConnectionStateTests
{
    [TestMethod]
    public void StaleSubscriptionCommitPreservesDeliveredAgentsForNextArea()
    {
        using var socket = new StubWebSocket();
        using var connection = new ClientConnection(Guid.NewGuid(), socket);
        var firstArea = new WorldRect(-100d, -100d, 100d, 100d);
        var nextArea = new WorldRect(1_000d, 1_000d, 1_100d, 1_100d);

        connection.SetSubscription(firstArea);
        Assert.IsTrue(connection.TryCaptureSubscription(out var firstSubscription));

        connection.SetSubscription(nextArea);
        var deliveredAgentIds = new HashSet<ulong> { 1, 2, 3 };

        Assert.IsFalse(connection.TryReplaceKnownAgentIds(
            firstSubscription.Revision,
            deliveredAgentIds));
        Assert.IsTrue(connection.TryCaptureSubscription(out var nextSubscription));
        Assert.AreEqual(nextArea, nextSubscription.Area);
        CollectionAssert.AreEquivalent(
            deliveredAgentIds.ToArray(),
            nextSubscription.KnownAgentIds.ToArray());
    }

    private sealed class StubWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        public override WebSocketState State => WebSocketState.Open;

        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
