using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class ProtocolVersionTests
{
    [TestMethod]
    public void CurrentProtocolIs212AndAdvertisesPower()
    {
        Assert.AreEqual(new ProtocolVersion(2, 12), ProtocolVersion.Current);
        Assert.IsTrue(ProtocolVersion.Current.SupportsMultimodalTransit);
        Assert.IsTrue(ProtocolVersion.Current.SupportsPersonInspectionClear);
        Assert.IsTrue(ProtocolVersion.Current.SupportsEconomy);
        Assert.IsTrue(ProtocolVersion.Current.SupportsLogistics);
        Assert.IsTrue(ProtocolVersion.Current.SupportsPower);
        Assert.IsFalse(new ProtocolVersion(2, 7).SupportsMultimodalTransit);
        Assert.IsFalse(new ProtocolVersion(2, 8).SupportsPersonInspectionClear);
        Assert.IsFalse(new ProtocolVersion(2, 9).SupportsEconomy);
        Assert.IsFalse(new ProtocolVersion(2, 10).SupportsLogistics);
        Assert.IsFalse(new ProtocolVersion(2, 11).SupportsPower);
    }

    [TestMethod]
    public void NegotiationKeepsAcceptedRequestedMinorAsTheConnectionVersion()
    {
        var supported = new ProtocolVersion(2, 12);
        var requested = new ProtocolVersion(2, 11);

        var accepted = supported.TryNegotiate(requested, out var negotiated);

        Assert.IsTrue(accepted);
        Assert.AreEqual(requested, negotiated);
    }

    [TestMethod]
    public void NegotiationRejectsNewerMinorAndDifferentMajor()
    {
        var supported = new ProtocolVersion(2, 12);

        Assert.IsFalse(supported.TryNegotiate(new ProtocolVersion(2, 13), out _));
        Assert.IsFalse(supported.TryNegotiate(new ProtocolVersion(1, 12), out _));
        Assert.IsFalse(supported.TryNegotiate(new ProtocolVersion(3, 0), out _));
    }
}
