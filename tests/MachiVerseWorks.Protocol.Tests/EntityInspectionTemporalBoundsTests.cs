using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class EntityInspectionTemporalBoundsTests
{
    [TestMethod]
    public void RecentPastOutsideConfiguredYearWindowIsRejected()
    {
        var message = new EntityInspectionSnapshotMessage(
            ProtocolEntityType.Settlement,
            1,
            100,
            2050,
            Found: true,
            CurrentState: [],
            Relations: [],
            RecentPast: [new ProtocolInspectionEvent(1, 2018, "Growth", "too old for the bounded recent window")],
            PlannedFutureAvailable: false,
            PlannedFuture: []);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            EntityInspectionProtocolCodec.Serialize(message, ProtocolVersion.Current));
    }

    [TestMethod]
    public void PlannedFutureOutsideConfiguredYearWindowIsRejected()
    {
        var message = new EntityInspectionSnapshotMessage(
            ProtocolEntityType.Settlement,
            1,
            100,
            2050,
            Found: true,
            CurrentState: [],
            Relations: [],
            RecentPast: [],
            PlannedFutureAvailable: true,
            PlannedFuture: [new ProtocolInspectionEvent(0, 2067, "Scheduled", "outside planned window")]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            EntityInspectionProtocolCodec.Serialize(message, ProtocolVersion.Current));
    }
}
