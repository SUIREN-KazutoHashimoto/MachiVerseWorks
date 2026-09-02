using MachiVerseWorks.Protocol;
using MachiVerseWorks.Server;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class GatewayPhase4EntityInspectionTests
{
    [TestMethod]
    public void SettlementInspectionIncludesCurrentRelationsAndBoundedRecentPast()
    {
        var regional = CreateRegionalSnapshot();
        var message = EntityInspectionMessageMapper.Create(
            new EntityInspectionTarget(ProtocolEntityType.Settlement, 1),
            CreatePopulationSnapshot(),
            new Dictionary<ulong, TrainSnapshot>(),
            regional);

        Assert.IsTrue(message.Found);
        Assert.AreEqual(ProtocolEntityType.Settlement, message.EntityType);
        Assert.IsTrue(message.CurrentState.Any(field => field.Name == "population" && field.Value == "12000"));
        Assert.IsTrue(message.Relations.Any(relation => relation.TargetType == ProtocolEntityType.Settlement && relation.TargetId == 2));
        Assert.AreEqual(EntityInspectionProtocolCodec.MaximumRecentEvents, message.RecentPast.Count);
        Assert.IsFalse(message.PlannedFutureAvailable);
        Assert.AreEqual(0, message.PlannedFuture.Count);
    }

    [TestMethod]
    public void BuildingInspectionIncludesStructuralRelationsAndSemanticEvents()
    {
        var regional = CreateRegionalSnapshot();
        var message = EntityInspectionMessageMapper.Create(
            new EntityInspectionTarget(ProtocolEntityType.Building, 20),
            CreatePopulationSnapshot(),
            new Dictionary<ulong, TrainSnapshot>(),
            regional);

        Assert.IsTrue(message.Found);
        Assert.IsTrue(message.CurrentState.Any(field => field.Name == "status"));
        Assert.IsTrue(message.Relations.Any(relation => relation.TargetType == ProtocolEntityType.Parcel && relation.TargetId == 10));
        Assert.IsTrue(message.Relations.Any(relation => relation.TargetType == ProtocolEntityType.Settlement && relation.TargetId == 1));
        Assert.IsTrue(message.RecentPast.Count > 0);
        Assert.IsFalse(message.PlannedFutureAvailable);
    }

    [TestMethod]
    public void SelectionRevisionChangesAcrossRetargetAndClear()
    {
        var registry = new EntityInspectionRegistry();
        var connectionId = Guid.NewGuid();

        registry.Set(connectionId, new EntityInspectionTarget(ProtocolEntityType.Person, 10));
        var first = registry.Capture(connectionId);
        registry.Set(connectionId, new EntityInspectionTarget(ProtocolEntityType.Train, 20));
        var second = registry.Capture(connectionId);
        registry.Clear(connectionId);
        var cleared = registry.Capture(connectionId);

        Assert.IsFalse(registry.IsCurrent(connectionId, first));
        Assert.IsTrue(second.Revision > first.Revision);
        Assert.IsNull(cleared.Target);
        Assert.IsTrue(cleared.Revision > second.Revision);
    }

    private static PopulationPublishSnapshot CreatePopulationSnapshot() => new(
        1,
        1,
        900,
        new PopulationStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 900),
        new Dictionary<ulong, PersonSnapshot>());

    private static PersistentRegionalEvolutionSnapshotMessage CreateRegionalSnapshot()
    {
        var events = Enumerable.Range(1, 40)
            .Select(index => new ProtocolRegionalEvolutionEvent(
                (ulong)index,
                2000 + index,
                (byte)RegionalEvolutionEventKind.Growth,
                1,
                index % 3 == 0 ? 20UL : 0UL,
                $"population +{index}"))
            .ToArray();

        return new PersistentRegionalEvolutionSnapshotMessage(
            2040,
            900,
            [
                new ProtocolSettlementEvolution(1, 10, 20, 0, 12_000, 4_000, 0.7, 0.6, 0.8, 12_000, (byte)SettlementScale.Town, (byte)SettlementTrend.Growing, true, 1980, null),
                new ProtocolSettlementEvolution(2, 20_000, 20, 0, 5_000, 1_500, 0.5, 0.4, 0.6, 7_000, (byte)SettlementScale.Town, (byte)SettlementTrend.Stable, true, 1990, null),
            ],
            [new ProtocolParcelEvolution(10, 1, 0.8, 0.7, (byte)ParcelDevelopmentState.Occupied, 20)],
            [new ProtocolBuildingLifecycle(20, 10, (byte)GeneratedBuildingUse.Residential, 2000, 2038, 0.8, 0.9, 120, (byte)BuildingLifecycleStatus.Active)],
            [new ProtocolServiceCatchment(1, (byte)RegionalServiceKind.Commerce, 8_000, 0.7)],
            [new ProtocolInfrastructureDemand(1, (byte)InfrastructureDemandKind.Road, 0.55, "population/jobs/accessibility")],
            [new ProtocolRegionalRelation(1, 1, 2, (byte)RegionalRelationKind.Trade, 0.65, true, 2020)],
            events,
            [],
            []);
    }
}
