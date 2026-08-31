using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class EconomyProtocolCodecTests
{
    [TestMethod]
    public void EconomySnapshotRoundTripsAtProtocol210()
    {
        var message = new EconomySnapshotMessage(
            new ProtocolEconomyStatistics(2, 3, 4, 5, 6, 7_000, 800, 90, 10_000, 900, 400, 12.5, 8, 4_800),
            [
                new ProtocolCompanyEconomy(1, ProtocolIndustrySector.Retail, 4_000, 300, 200, 10, 8.5, 2, 3),
                new ProtocolCompanyEconomy(2, ProtocolIndustrySector.Services, 6_000, 600, 200, 7, 4, 1, 2),
            ],
            [new ProtocolHouseholdEconomy(1, 7_000, 800, 90)]);

        var frame = EconomyProtocolCodec.Serialize(message, ProtocolVersion.Current);
        var decoded = EconomyProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsTrue(decoded);
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        var actual = envelope.Message as EconomySnapshotMessage;
        Assert.IsNotNull(actual);
        Assert.AreEqual(message.Statistics, actual.Statistics);
        CollectionAssert.AreEqual(message.Companies.ToArray(), actual.Companies.ToArray());
        CollectionAssert.AreEqual(message.Households.ToArray(), actual.Households.ToArray());
    }

    [TestMethod]
    public void EconomySnapshotRequiresProtocol210()
    {
        var message = new EconomySnapshotMessage(
            new ProtocolEconomyStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            [],
            []);

        Assert.Throws<ArgumentOutOfRangeException>(() => EconomyProtocolCodec.Serialize(message, new ProtocolVersion(2, 9)));
    }

    [TestMethod]
    public void EconomySnapshotRejectsNegativeMoney()
    {
        var message = new EconomySnapshotMessage(
            new ProtocolEconomyStatistics(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            [new ProtocolCompanyEconomy(1, ProtocolIndustrySector.Retail, -1, 0, 0, 0, 0, 0, 0)],
            []);

        Assert.Throws<ArgumentOutOfRangeException>(() => EconomyProtocolCodec.Serialize(message, ProtocolVersion.Current));
    }
}
