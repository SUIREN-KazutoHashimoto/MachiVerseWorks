using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class EntityInspectionProtocolCodecTests
{
    [TestMethod]
    public void InspectEntityRoundTripsTypeAndId()
    {
        var source = new InspectEntityMessage(ProtocolEntityType.Building, 42);

        var frame = EntityInspectionProtocolCodec.Serialize(source, ProtocolVersion.Current);

        Assert.IsTrue(EntityInspectionProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        var decoded = (InspectEntityMessage)envelope.Message;
        Assert.AreEqual(source, decoded);
    }

    [TestMethod]
    public void EntitySnapshotRoundTripsBoundedReadModel()
    {
        var source = new EntityInspectionSnapshotMessage(
            ProtocolEntityType.Settlement,
            7,
            123,
            2050,
            Found: true,
            CurrentState: [new ProtocolInspectionField("population", "12000")],
            Relations: [new ProtocolInspectionRelation("Trade", ProtocolEntityType.Settlement, 8, 0.75)],
            RecentPast: [new ProtocolInspectionEvent(9, 2049, "Growth", "population +120")],
            PlannedFutureAvailable: false,
            PlannedFuture: []);

        var frame = EntityInspectionProtocolCodec.Serialize(source, ProtocolVersion.Current);

        Assert.IsTrue(EntityInspectionProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        var decoded = (EntityInspectionSnapshotMessage)envelope.Message;
        Assert.AreEqual(source.EntityType, decoded.EntityType);
        Assert.AreEqual(source.EntityId, decoded.EntityId);
        Assert.AreEqual(source.CurrentState[0], decoded.CurrentState[0]);
        Assert.AreEqual(source.Relations[0], decoded.Relations[0]);
        Assert.AreEqual(source.RecentPast[0], decoded.RecentPast[0]);
        Assert.IsFalse(decoded.PlannedFutureAvailable);
        Assert.AreEqual(0, decoded.PlannedFuture.Count);
    }

    [TestMethod]
    public void EntitySnapshotRejectsUnboundedRecentPast()
    {
        var recent = Enumerable.Range(1, EntityInspectionProtocolCodec.MaximumRecentEvents + 1)
            .Select(index => new ProtocolInspectionEvent((ulong)index, 2000 + index, "Growth", "bounded event"))
            .ToArray();
        var source = new EntityInspectionSnapshotMessage(
            ProtocolEntityType.Settlement,
            7,
            123,
            2050,
            Found: true,
            CurrentState: [],
            Relations: [],
            RecentPast: recent,
            PlannedFutureAvailable: false,
            PlannedFuture: []);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EntityInspectionProtocolCodec.Serialize(source, ProtocolVersion.Current));
    }

    [TestMethod]
    public void MissingEntityCannotCarryDerivedState()
    {
        var source = new EntityInspectionSnapshotMessage(
            ProtocolEntityType.Train,
            99,
            123,
            null,
            Found: false,
            CurrentState: [new ProtocolInspectionField("state", "Running")],
            Relations: [],
            RecentPast: [],
            PlannedFutureAvailable: false,
            PlannedFuture: []);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EntityInspectionProtocolCodec.Serialize(source, ProtocolVersion.Current));
    }
}
