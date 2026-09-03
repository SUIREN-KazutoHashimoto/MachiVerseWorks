using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class MultimodalTransitOversizeDeliveryGateTests
{
    [TestMethod]
    public void OversizeNotificationIsSentOncePerSubscriptionRevision()
    {
        var gate = new MultimodalTransitOversizeDeliveryGate();
        var connectionId = Guid.NewGuid();

        Assert.IsTrue(gate.ShouldSend(connectionId, subscriptionRevision: 1, isOversize: true));
        Assert.IsFalse(gate.ShouldSend(connectionId, subscriptionRevision: 1, isOversize: true));
        Assert.IsTrue(gate.ShouldSend(connectionId, subscriptionRevision: 2, isOversize: true));
        Assert.IsFalse(gate.ShouldSend(connectionId, subscriptionRevision: 2, isOversize: true));
    }

    [TestMethod]
    public void SuccessfulSnapshotClearsOversizeSuppression()
    {
        var gate = new MultimodalTransitOversizeDeliveryGate();
        var connectionId = Guid.NewGuid();

        Assert.IsTrue(gate.ShouldSend(connectionId, subscriptionRevision: 1, isOversize: true));
        Assert.IsFalse(gate.ShouldSend(connectionId, subscriptionRevision: 1, isOversize: true));
        Assert.IsTrue(gate.ShouldSend(connectionId, subscriptionRevision: 1, isOversize: false));
        Assert.IsTrue(gate.ShouldSend(connectionId, subscriptionRevision: 1, isOversize: true));
    }

    [TestMethod]
    public void RemovingConnectionClearsSuppressionState()
    {
        var gate = new MultimodalTransitOversizeDeliveryGate();
        var connectionId = Guid.NewGuid();

        Assert.IsTrue(gate.ShouldSend(connectionId, subscriptionRevision: 1, isOversize: true));
        gate.Remove(connectionId);
        Assert.IsTrue(gate.ShouldSend(connectionId, subscriptionRevision: 1, isOversize: true));
    }
}
