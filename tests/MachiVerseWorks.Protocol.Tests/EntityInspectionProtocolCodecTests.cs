using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Protocol.Tests;

public sealed class EntityInspectionProtocolCodecTests
{
    [Fact]
    public void InspectEntityRoundTripsTypeAndId()
    {
        var source = new InspectEntityMessage(ProtocolEntityType.Building, 42);

        var frame = EntityInspectionProtocolCodec.Serialize(source, ProtocolVersion.Current);

        Assert.True(EntityInspectionProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.Equal(ProtocolDecodeError.None, error);
        var decoded = Assert.IsType<InspectEntityMessage>(envelope!.Message);
        Assert.Equal(source, decoded);
    }

    [Fact]
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

        Assert.True(EntityInspectionProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.Equal(ProtocolDecodeError.None, error);
        var decoded = Assert.IsType<EntityInspectionSnapshotMessage>(envelope!.Message);
        Assert.Equal(source.EntityType, decoded.EntityType);
        Assert.Equal(source.EntityId, decoded.EntityId);
        Assert.Equal(source.CurrentState, decoded.CurrentState);
        Assert.Equal(source.Relations, decoded.Relations);
        Assert.Equal(source.RecentPast, decoded.RecentPast);
        Assert.False(decoded.PlannedFutureAvailable);
        Assert.Empty(decoded.PlannedFuture);
    }

    [Fact]
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

        Assert.Throws<ArgumentOutOfRangeException>(() => EntityInspectionProtocolCodec.Serialize(source, ProtocolVersion.Current));
    }

    [Fact]
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

        Assert.Throws<ArgumentOutOfRangeException>(() => EntityInspectionProtocolCodec.Serialize(source, ProtocolVersion.Current));
    }
}
