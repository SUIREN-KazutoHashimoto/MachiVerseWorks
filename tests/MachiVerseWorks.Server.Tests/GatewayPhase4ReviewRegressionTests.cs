using MachiVerseWorks.Protocol;
using MachiVerseWorks.Server;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class GatewayPhase4ReviewRegressionTests
{
    [TestMethod]
    public async Task DistinctPublisherLanesPreserveQueuedDomainDelivery()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var connectionId = Guid.NewGuid();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Economy));
        Assert.IsTrue(scheduler.StartReserved(connectionId, () => release.Task));
        Assert.IsFalse(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Gas));

        release.SetResult();
        await WaitUntilAsync(() => scheduler.InFlightCount == 0, TimeSpan.FromSeconds(1));

        Assert.IsTrue(scheduler.TryReserve(connectionId, ObservationDeliveryLane.Gas));
        scheduler.ReleaseReservation(connectionId);
    }

    [TestMethod]
    public void SparseOldRegionalEventIsExcludedBeforeInspectionSerialization()
    {
        var regional = CreateRegionalSnapshot(
            [new ProtocolRegionalEvolutionEvent(
                1,
                2008,
                (byte)RegionalEvolutionEventKind.Growth,
                1,
                0,
                "outside recent window")]);

        var message = EntityInspectionMessageMapper.Create(
            new EntityInspectionTarget(ProtocolEntityType.Settlement, 1),
            CreatePopulationSnapshot(),
            new Dictionary<ulong, TrainSnapshot>(),
            regional);

        Assert.AreEqual(0, message.RecentPast.Count);
        var frame = EntityInspectionProtocolCodec.Serialize(message, ProtocolVersion.Current);
        Assert.IsTrue(frame.Length > ProtocolFrameHeader.Size);
    }

    [TestMethod]
    public void PersonBuildingRelationsUseGeneratedInspectionIds()
    {
        var materializedBuildingId = new BuildingId(500);
        var endpoint = TripEndpoint.ForBuilding(materializedBuildingId);
        var person = new PersonSnapshot(
            new PersonId(7),
            new HouseholdId(3),
            new PersonDemographics(30),
            endpoint,
            endpoint,
            ActivityKind.Home,
            PersonTravelState.AtActivity,
            endpoint,
            ActivityKind.Work,
            null,
            null,
            null,
            null,
            900);
        var population = new PopulationPublishSnapshot(
            1,
            1,
            900,
            new PopulationStatistics(1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 900),
            new Dictionary<ulong, PersonSnapshot> { [7] = person });
        var generatedIds = new Dictionary<ulong, ulong> { [500] = 20 };

        var message = EntityInspectionMessageMapper.Create(
            new EntityInspectionTarget(ProtocolEntityType.Person, 7),
            population,
            new Dictionary<ulong, VehicleSnapshot>(),
            new Dictionary<ulong, TrainSnapshot>(),
            generatedIds,
            null);

        Assert.IsTrue(message.Found);
        Assert.IsTrue(message.Relations.Any(relation =>
            relation.TargetType == ProtocolEntityType.Building && relation.TargetId == 20));
        Assert.IsFalse(message.Relations.Any(relation =>
            relation.TargetType == ProtocolEntityType.Building && relation.TargetId == 500));
    }

    private static PopulationPublishSnapshot CreatePopulationSnapshot() => new(
        1,
        1,
        900,
        new PopulationStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 900),
        new Dictionary<ulong, PersonSnapshot>());

    private static PersistentRegionalEvolutionSnapshotMessage CreateRegionalSnapshot(
        IReadOnlyList<ProtocolRegionalEvolutionEvent> events) =>
        new(
            2040,
            900,
            [new ProtocolSettlementEvolution(
                1,
                10,
                20,
                0,
                12_000,
                4_000,
                0.7,
                0.6,
                0.8,
                12_000,
                (byte)SettlementScale.Town,
                (byte)SettlementTrend.Growing,
                true,
                1980,
                null)],
            [],
            [],
            [],
            [],
            [],
            events,
            [],
            []);

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Condition was not satisfied before timeout.");
            await Task.Delay(10);
        }
    }
}
