using MachiVerseWorks.Simulation.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RailwayBlockSeparationContractTests
{
    [TestMethod]
    public void BlockOwnershipFollowsRoutePointAndDoesNotWaitForFormationRearClearance()
    {
        var fixture = CreateFixture(middleSegmentBlocked: true);
        var first = fixture.Store.CreateTrain(fixture.Store.CreateService(
            fixture.FormationId,
            fixture.RouteId,
            fixture.TimetableId,
            fixture.OriginDepotId,
            fixture.DestinationDepotId,
            plannedStartTick: 0));
        var second = fixture.Store.CreateTrain(fixture.Store.CreateService(
            fixture.FormationId,
            fixture.RouteId,
            fixture.TimetableId,
            fixture.OriginDepotId,
            fixture.DestinationDepotId,
            plannedStartTick: 0));

        fixture.Store.Step(1d, 1);
        fixture.Store.Step(1d, 2);

        var snapshot = fixture.Store.CreateSnapshot();
        var firstTrain = snapshot.Trains.Single(train => train.Id == first);
        var secondTrain = snapshot.Trains.Single(train => train.Id == second);
        var formation = snapshot.Formations.Single(item => item.Id == fixture.FormationId);

        Assert.AreEqual(250d, formation.LengthMeters, 1e-9);
        Assert.IsTrue(firstTrain.RouteDistanceMeters < formation.LengthMeters,
            "The test requires the first formation rear to extend behind the route-point representative position.");
        Assert.AreEqual(fixture.MiddleBlockId, firstTrain.CurrentBlockId);
        Assert.AreEqual(fixture.OriginBlockId, secondTrain.CurrentBlockId,
            "The previous block is released as soon as the first Train route point enters the next block, without waiting for rear clearance.");
        Assert.AreEqual(TrainMovementState.Running, firstTrain.State);
        Assert.AreEqual(TrainMovementState.Running, secondTrain.State);
    }

    [TestMethod]
    public void TrackOutsideBlockSectionsProvidesNoBlockBasedTrainSeparation()
    {
        var fixture = CreateFixture(middleSegmentBlocked: false);
        var first = fixture.Store.CreateTrain(fixture.Store.CreateService(
            fixture.FormationId,
            fixture.RouteId,
            fixture.TimetableId,
            fixture.OriginDepotId,
            fixture.DestinationDepotId,
            plannedStartTick: 0));
        var second = fixture.Store.CreateTrain(fixture.Store.CreateService(
            fixture.FormationId,
            fixture.RouteId,
            fixture.TimetableId,
            fixture.OriginDepotId,
            fixture.DestinationDepotId,
            plannedStartTick: 0));

        fixture.Store.Step(1d, 1);
        fixture.Store.Step(1d, 2);
        fixture.Store.Step(1d, 3);

        var snapshot = fixture.Store.CreateSnapshot();
        var firstTrain = snapshot.Trains.Single(train => train.Id == first);
        var secondTrain = snapshot.Trains.Single(train => train.Id == second);

        Assert.IsTrue(firstTrain.RouteDistanceMeters > 100d && firstTrain.RouteDistanceMeters < 400d);
        Assert.IsTrue(secondTrain.RouteDistanceMeters > 100d && secondTrain.RouteDistanceMeters < 400d);
        Assert.IsNull(firstTrain.CurrentBlockId);
        Assert.IsNull(secondTrain.CurrentBlockId);
        Assert.AreEqual(TrainMovementState.Running, firstTrain.State);
        Assert.AreEqual(TrainMovementState.Running, secondTrain.State);
    }

    private static Fixture CreateFixture(bool middleSegmentBlocked)
    {
        var n1 = new TrackNodeId(1);
        var n2 = new TrackNodeId(2);
        var n3 = new TrackNodeId(3);
        var n4 = new TrackNodeId(4);
        var s1 = new TrackSegmentId(1);
        var s2 = new TrackSegmentId(2);
        var s3 = new TrackSegmentId(3);
        var block1 = new BlockSectionId(1);
        var block2 = new BlockSectionId(2);
        var block3 = new BlockSectionId(3);
        var station = new StationId(1);
        var platform = new PlatformId(1);
        var originDepot = new DepotId(1);
        var destinationDepot = new DepotId(2);

        var blocks = new List<BlockSectionSnapshot>
        {
            new(block1, [s1]),
        };
        if (middleSegmentBlocked) blocks.Add(new BlockSectionSnapshot(block2, [s2]));
        blocks.Add(new BlockSectionSnapshot(block3, [s3]));

        var infrastructure = new RailwayInfrastructureSnapshot(
            [
                new TrackNodeSnapshot(n1, TrackNodeKind.Endpoint, new WorldPoint(0d, 0d, 0d)),
                new TrackNodeSnapshot(n2, TrackNodeKind.Junction, new WorldPoint(100d, 0d, 0d)),
                new TrackNodeSnapshot(n3, TrackNodeKind.Junction, new WorldPoint(400d, 0d, 0d)),
                new TrackNodeSnapshot(n4, TrackNodeKind.Endpoint, new WorldPoint(500d, 0d, 0d)),
            ],
            [
                new TrackSegmentSnapshot(s1, n1, n2, TrackDirection.StartToEnd, 1.435d, 100d, TrackElectrification.None, TrackUsage.Depot),
                new TrackSegmentSnapshot(s2, n2, n3, TrackDirection.StartToEnd, 1.435d, 100d, TrackElectrification.None, TrackUsage.Mainline),
                new TrackSegmentSnapshot(s3, n3, n4, TrackDirection.StartToEnd, 1.435d, 100d, TrackElectrification.None, TrackUsage.Depot),
            ],
            [
                new TrackConnectionSnapshot(new TrackConnectionId(1), s1, s2, n2),
                new TrackConnectionSnapshot(new TrackConnectionId(2), s2, s3, n3),
            ],
            blocks,
            [new StationSnapshot(station, new WorldVolume(400d, -10d, -5d, 500d, 10d, 5d))],
            [new PlatformSnapshot(platform, station, s3, 0.2d, 0.8d, new WorldVolume(420d, -5d, -2d, 480d, 5d, 2d))],
            [],
            [
                new DepotSnapshot(originDepot, new WorldVolume(0d, -10d, -5d, 100d, 10d, 5d), [s1]),
                new DepotSnapshot(destinationDepot, new WorldVolume(400d, -10d, -5d, 500d, 10d, 5d), [s3]),
            ]);

        var store = new RailwayOperationsStore(infrastructure);
        var formation = store.CreateFormation(
            lengthMeters: 250d,
            maximumSpeedMetersPerSecond: 100d,
            maximumAccelerationMetersPerSecondSquared: 100d,
            serviceDecelerationMetersPerSecondSquared: 100d,
            capacity: 500);
        var route = store.CreateRoute([s1, s2, s3]);
        var timetable = store.CreateTimetable([
            new TimetableStopSnapshot(station, PlannedArrivalTick: 1000, PlannedDepartureTick: 1001, MinimumDwellTicks: 1, PreferredPlatformId: platform),
        ]);
        return new Fixture(store, formation, route, timetable, originDepot, destinationDepot, block1, middleSegmentBlocked ? block2 : null);
    }

    private sealed record Fixture(
        RailwayOperationsStore Store,
        TrainFormationId FormationId,
        RailwayRouteId RouteId,
        TimetableId TimetableId,
        DepotId OriginDepotId,
        DepotId DestinationDepotId,
        BlockSectionId OriginBlockId,
        BlockSectionId? MiddleBlockId);
}
