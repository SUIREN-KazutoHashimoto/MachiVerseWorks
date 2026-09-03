using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class Worker1FinalBatchRegressionTests
{
    [TestMethod]
    public void ModeChoiceDoesNotTreatInfiniteRoadEtaAsOneTick()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        var startNode = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var endNode = world.CreateRoadNode(new WorldPoint(100d, 0d, 0d));
        var segment = world.CreateRoadSegment(startNode, endNode);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: double.Epsilon);
        var origin = world.CreateBuilding(new WorldVolume(-1d, 5d, 0d, 1d, 7d, 2d));
        var destination = world.CreateBuilding(new WorldVolume(99d, 5d, 0d, 101d, 7d, 2d));
        world.CreateRoadAccessPoint(segment, 0d, buildingId: origin, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        world.CreateRoadAccessPoint(segment, 1d, buildingId: destination, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        var request = new TripRequest(new TripRequestId(1), TripEndpoint.ForBuilding(origin), TripEndpoint.ForBuilding(destination));

        var decision = world.ChooseMode(request, hasPrivateVehicle: true);

        Assert.AreEqual(TransitMode.Walk, decision.Mode);
        Assert.IsTrue(decision.EstimatedDurationTicks > 1UL);
    }

    [TestMethod]
    public void BusArrivalDwellOverflowDoesNotCommitArrival()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        var startNode = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var endNode = world.CreateRoadNode(new WorldPoint(20d, 0d, 0d));
        var segment = world.CreateRoadSegment(startNode, endNode);
        var lane = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 20d);
        var first = world.CreateBusStop(lane, new WorldPoint(1d, 0d, 0d));
        var second = world.CreateBusStop(lane, new WorldPoint(19d, 0d, 0d));
        var line = world.CreateTransitLine(TransitMode.Bus);
        var pattern = world.CreateTransitServicePattern(line, [
            new TransitPatternStopSnapshot(first, 0UL, 0UL),
            new TransitPatternStopSnapshot(second, 1UL, ulong.MaxValue),
        ]);
        var trip = world.CreateTransitTrip(pattern, 0UL);
        var busId = world.CreateBusTransitVehicle(trip);

        var overflowed = false;
        for (var index = 0; index < 500; index++)
        {
            try
            {
                world.Step();
            }
            catch (OverflowException)
            {
                overflowed = true;
                break;
            }
        }

        Assert.IsTrue(overflowed, "The Bus never reached the dwell-overflow boundary.");
        var bus = world.CreateMultimodalTransitSnapshot().Vehicles.Single(item => item.Id == busId);
        Assert.AreEqual(TransitVehicleMovementState.EnRouteToStop, bus.State);
        Assert.AreEqual(0, bus.StopIndex);
        Assert.IsTrue(bus.RoadVehicleId.HasValue);
        Assert.IsTrue(world.TryGetVehicleSnapshot(bus.RoadVehicleId.Value, out var roadVehicle));
        Assert.AreEqual(VehicleMovementState.Arrived, roadVehicle.State);
    }

    [TestMethod]
    public void InitializeRegionalWorldFailureDoesNotCommitGenerationAndCanRetry()
    {
        var world = new SimulationWorld(new SimulationConfig(
            tickRate: 2,
            seed: 32_299,
            worldEnvironment: CreateEnvironmentConfig(32_299)));
        var existingNode = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            world.InitializeRegionalWorld(
                CreateRegionalVolume(),
                new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2, iterationBudget: 1),
                out _));

        Assert.IsFalse(world.HasRegionalGeneration);
        Assert.AreEqual(1, world.RoadNodeCount);
        Assert.IsTrue(world.RemoveRoadNode(existingNode));

        _ = world.InitializeRegionalWorld(
            CreateRegionalVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2, iterationBudget: 1),
            out _);
        Assert.IsTrue(world.HasRegionalGeneration);
    }

    [TestMethod]
    public void LegacyWideLanesRemainValidWhileAggregateOverflowIsRejected()
    {
        var world = new SimulationWorld();
        var startNode = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var endNode = world.CreateRoadNode(new WorldPoint(100d, 0d, 0d));
        var segment = world.CreateRoadSegment(startNode, endNode);

        world.CreateLane(segment, LaneDirection.Forward, 0, 30d, 10d);
        world.CreateLane(segment, LaneDirection.Forward, 1, 250d, 10d);
        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());
        Assert.AreEqual(2, restored.LaneCount);

        var overflowWorld = new SimulationWorld();
        var overflowStart = overflowWorld.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var overflowEnd = overflowWorld.CreateRoadNode(new WorldPoint(100d, 0d, 0d));
        var overflowSegment = overflowWorld.CreateRoadSegment(overflowStart, overflowEnd);
        overflowWorld.CreateLane(overflowSegment, LaneDirection.Forward, 0, double.MaxValue, 10d);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            overflowWorld.CreateLane(overflowSegment, LaneDirection.Forward, 1, 1d, 10d));
        Assert.AreEqual(1, overflowWorld.LaneCount);
    }

    [TestMethod]
    public void CheckpointRestoreRejectsAggregateLaneWidthOverflow()
    {
        var world = new SimulationWorld();
        var startNode = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var endNode = world.CreateRoadNode(new WorldPoint(100d, 0d, 0d));
        var segment = world.CreateRoadSegment(startNode, endNode);
        world.CreateLane(segment, LaneDirection.Forward, 0, 3.5d, 10d);
        world.CreateLane(segment, LaneDirection.Forward, 1, 3.5d, 10d);
        var checkpoint = world.CreateCheckpoint();
        var lanes = checkpoint.Lanes.ToArray();
        lanes[0] = lanes[0] with { WidthMeters = double.MaxValue };
        lanes[1] = lanes[1] with { WidthMeters = 1d };

        Assert.ThrowsExactly<ArgumentException>(() =>
            SimulationWorld.RestoreCheckpoint(checkpoint with { Lanes = lanes }));
    }

    [TestMethod]
    public void RailwayServicePatternUsesPlatformAwareStopIdentityForStationRevisit()
    {
        var world = new SimulationWorld();
        var n0 = world.CreateTrackNode(new WorldPoint(-10d, 0d, 0d));
        var n1 = world.CreateTrackNode(new WorldPoint(0d, 0d, 0d), TrackNodeKind.Junction);
        var n2 = world.CreateTrackNode(new WorldPoint(10d, 0d, 0d), TrackNodeKind.Junction);
        var n3 = world.CreateTrackNode(new WorldPoint(20d, 0d, 0d), TrackNodeKind.Junction);
        var n4 = world.CreateTrackNode(new WorldPoint(30d, 0d, 0d));
        var depotOut = world.CreateTrackSegment(n0, n1, TrackDirection.StartToEnd, usage: TrackUsage.Depot);
        var firstMain = world.CreateTrackSegment(n1, n2, TrackDirection.StartToEnd);
        var secondMain = world.CreateTrackSegment(n2, n3, TrackDirection.StartToEnd);
        var depotIn = world.CreateTrackSegment(n3, n4, TrackDirection.StartToEnd, usage: TrackUsage.Depot);
        world.CreateTrackConnection(depotOut, firstMain, n1);
        world.CreateTrackConnection(firstMain, secondMain, n2);
        world.CreateTrackConnection(secondMain, depotIn, n3);
        world.CreateBlockSection([depotOut]);
        world.CreateBlockSection([firstMain]);
        world.CreateBlockSection([secondMain]);
        world.CreateBlockSection([depotIn]);
        var station = world.CreateStation(new WorldVolume(-1d, -5d, -1d, 21d, 5d, 4d));
        var firstPlatform = world.CreatePlatform(station, firstMain, 0.2d, 0.8d, new WorldVolume(1d, -2d, -1d, 9d, 2d, 3d));
        var secondPlatform = world.CreatePlatform(station, secondMain, 0.2d, 0.8d, new WorldVolume(11d, -2d, -1d, 19d, 2d, 3d));
        var originDepot = world.CreateDepot(new WorldVolume(-11d, -4d, -1d, 1d, 4d, 4d), [depotOut]);
        var destinationDepot = world.CreateDepot(new WorldVolume(19d, -4d, -1d, 31d, 4d, 4d), [depotIn]);
        var formation = world.CreateTrainFormation(20d, 18d, 1.4d, 1.8d, 100);
        var route = world.CreateRailwayRoute([depotOut, firstMain, secondMain, depotIn]);
        var timetable = world.CreateTimetable([
            new TimetableStopSnapshot(station, 10UL, 11UL, 1UL, firstPlatform),
            new TimetableStopSnapshot(station, 20UL, 21UL, 1UL, secondPlatform),
        ]);
        var service = world.CreateRailwayService(formation, route, timetable, originDepot, destinationDepot, plannedStartTick: 1UL);
        var line = world.CreateTransitLine(TransitMode.Railway);

        var patternId = world.CreateRailwayServicePattern(line, service);
        var snapshot = world.CreateMultimodalTransitSnapshot();
        var pattern = snapshot.Patterns.Single(item => item.Id == patternId);

        Assert.AreEqual(2, pattern.Stops.Count);
        Assert.AreNotEqual(pattern.Stops[0].StopId, pattern.Stops[1].StopId);
        var firstStop = snapshot.Stops.Single(item => item.Id == pattern.Stops[0].StopId);
        var secondStop = snapshot.Stops.Single(item => item.Id == pattern.Stops[1].StopId);
        Assert.AreEqual(station, firstStop.StationId);
        Assert.AreEqual(station, secondStop.StationId);
        Assert.AreEqual(firstPlatform, firstStop.PlatformId);
        Assert.AreEqual(secondPlatform, secondStop.PlatformId);
    }

    [TestMethod]
    public void RailwayServicePatternFailureRollsBackAutoCreatedStopsAndIds()
    {
        var world = new SimulationWorld();
        var fixture = RailwayOperationsFixtures.SeedDeterministic(world);
        var line = world.CreateTransitLine(TransitMode.Railway);
        var checkpoint = world.CreateCheckpoint();
        var transit = checkpoint.MultimodalTransit!;
        var restored = SimulationWorld.RestoreCheckpoint(checkpoint with
        {
            MultimodalTransit = transit with { NextPatternId = ulong.MaxValue },
        });
        var before = restored.CreateCheckpoint().MultimodalTransit!;

        Assert.ThrowsExactly<OverflowException>(() => restored.CreateRailwayServicePattern(line, fixture.FirstServiceId));

        var afterSnapshot = restored.CreateMultimodalTransitSnapshot();
        var after = restored.CreateCheckpoint().MultimodalTransit!;
        Assert.AreEqual(0, afterSnapshot.Stops.Length);
        Assert.AreEqual(0, afterSnapshot.Patterns.Length);
        Assert.AreEqual(before.NextStopId, after.NextStopId);
        Assert.AreEqual(before.NextPatternId, after.NextPatternId);
    }

    private static WorldEnvironmentConfig CreateEnvironmentConfig(ulong worldSeed) => new(
        worldSeed,
        new WorldVector(0d, 1d, 0d),
        latitudeDegrees: 43d,
        continentality: 0.55d,
        maritimeInfluence: 0.45d,
        meanAnnualTemperatureCelsius: 10d,
        seasonalityCelsius: 20d,
        annualPrecipitationMillimeters: 950d);

    private static WorldVolume CreateRegionalVolume() =>
        new(-700_000d, -700_000d, -12_000d, 700_000d, 700_000d, 12_000d);
}
