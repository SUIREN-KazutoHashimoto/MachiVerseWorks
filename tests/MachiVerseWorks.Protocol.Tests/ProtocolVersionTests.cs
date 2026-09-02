using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class ProtocolVersionTests
{
    [TestMethod]
    public void CurrentProtocolIs218AndAdvertisesRegionalGeneration()
    {
        Assert.AreEqual(new ProtocolVersion(2, 18), ProtocolVersion.Current);
        Assert.IsTrue(ProtocolVersion.Current.SupportsMultimodalTransit);
        Assert.IsTrue(ProtocolVersion.Current.SupportsPersonInspectionClear);
        Assert.IsTrue(ProtocolVersion.Current.SupportsEconomy);
        Assert.IsTrue(ProtocolVersion.Current.SupportsLogistics);
        Assert.IsTrue(ProtocolVersion.Current.SupportsPower);
        Assert.IsTrue(ProtocolVersion.Current.SupportsWaterSewer);
        Assert.IsTrue(ProtocolVersion.Current.SupportsGas);
        Assert.IsTrue(ProtocolVersion.Current.SupportsOptical);
        Assert.IsTrue(ProtocolVersion.Current.SupportsRadio);
        Assert.IsTrue(ProtocolVersion.Current.SupportsWorldEnvironment);
        Assert.IsTrue(ProtocolVersion.Current.SupportsRegionalGeneration);
        Assert.IsFalse(new ProtocolVersion(2, 7).SupportsMultimodalTransit);
        Assert.IsFalse(new ProtocolVersion(2, 8).SupportsPersonInspectionClear);
        Assert.IsFalse(new ProtocolVersion(2, 9).SupportsEconomy);
        Assert.IsFalse(new ProtocolVersion(2, 10).SupportsLogistics);
        Assert.IsFalse(new ProtocolVersion(2, 11).SupportsPower);
        Assert.IsFalse(new ProtocolVersion(2, 12).SupportsWaterSewer);
        Assert.IsFalse(new ProtocolVersion(2, 13).SupportsGas);
        Assert.IsFalse(new ProtocolVersion(2, 14).SupportsOptical);
        Assert.IsFalse(new ProtocolVersion(2, 15).SupportsRadio);
        Assert.IsFalse(new ProtocolVersion(2, 16).SupportsWorldEnvironment);
        Assert.IsFalse(new ProtocolVersion(2, 17).SupportsRegionalGeneration);
    }

    [TestMethod]
    public void NegotiationKeepsAcceptedRequestedMinorAsTheConnectionVersion()
    {
        var supported = ProtocolVersion.Current;
        var requested = new ProtocolVersion(2, 15);

        var accepted = supported.TryNegotiate(requested, out var negotiated);

        Assert.IsTrue(accepted);
        Assert.AreEqual(requested, negotiated);
    }

    [TestMethod]
    public void NegotiationRejectsNewerMinorAndDifferentMajor()
    {
        var supported = ProtocolVersion.Current;

        Assert.IsFalse(supported.TryNegotiate(new ProtocolVersion(2, 19), out _));
        Assert.IsFalse(supported.TryNegotiate(new ProtocolVersion(1, 18), out _));
        Assert.IsFalse(supported.TryNegotiate(new ProtocolVersion(3, 0), out _));
    }
}
