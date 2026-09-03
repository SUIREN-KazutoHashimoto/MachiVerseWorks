using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RailwayOperationsTests
{
    [TestMethod]
    public void RouteValidationRejectsDisconnectedTrackSequence()
    {
        var world = new SimulationWorld();
        var a = world.CreateTrackNode(new WorldPoint(0d, 0d, 0d));
        var b = world.CreateTrackNode(new WorldPoint(10d, 0d, 0d), TrackNodeKind.Junction);
        var c = world.CreateTrackNode(new WorldPoint(20d, 0d, 0d));
        var first = world.CreateTrackSegment(a, b);
        var second = world.CreateTrackSegment(b, c);

        Assert.ThrowsExactly<ArgumentException>(() => world.CreateRailwayRoute([first, second]));
    }

    [TestMethod]
    public void FailedRouteCreationDoesNotConsumeStableId()
    {
        var world = new SimulationWorld();
        var a = world.CreateTrackNode(new WorldPoint(0d, 0d, 0d));
        var b = world.CreateTrackNode(new WorldPoint(10d, 0d, 0d), TrackNodeKind.Junction);
        var c = world.CreateTrackNode(new WorldPoint(20d, 0d, 0d));
        var d = world.CreateTrackNode(new WorldPoint(0d, 20d, 0d));
        var e = world.CreateTrackNode(new WorldPoint(10d, 20d, 0d));
        var first = world.CreateTrackSegment(a, b);
        var second = world.CreateTrackSegment(b, c);
        var disconnected = world.CreateTrackSegment(d, e);
        world.CreateTrackConnection(first, second, b);
        var expectedNextId = world.CreateCheckpoint().NextRailwayRouteId;

        Assert.ThrowsExactly<ArgumentException>(() => world.CreateRailwayRoute([new TrackSegmentId(ulong.MaxValue)]));
        Assert.ThrowsExactly<ArgumentException>(() => world.CreateRailwayRoute([first, disconnected]));
        Assert.ThrowsExactly<ArgumentException>(() => world.CreateRailwayRoute([first, first]));

        var failed = world.CreateCheckpoint();
        Assert.AreEqual(expectedNextId, failed.NextRailwayRouteId);
        Assert.AreEqual(0, failed.RailwayRoutes!.Count);

        var created = world.CreateRailwayRoute([first, second]);
        Assert.AreEqual(expectedNextId, created.Value);
        Assert.AreEqual(expectedNextId + 1, world.CreateCheckpoint().NextRailwayRouteId);
    }

    [TestMethod]
    public void PreferredPlatformOutsideRouteFallsBackToEligiblePlatform()
    {
        var world = new SimulationWorld();
        var n0 = world.CreateTrackNode(new WorldPoint(-60d, 0d, 0d));
        var n1 = world.CreateTrackNode(new WorldPoint(-40d, 0d, 0d), TrackNodeKind.Junction);
        var n2 = world.CreateTrackNode(new WorldPoint(40d, 0d, 0d), TrackNodeKind.Junction);
        var n3 = world.CreateTrackNode(new WorldPoint(60d, 0d, 0d));
        var depotOut = world.CreateTrackSegment(n0, n1, TrackDirection.StartToEnd, usage: TrackUsage.Depot);
        var main = world.CreateTrackSegment(n1, n2, TrackDirection.StartToEnd);
        var depotIn = world.CreateTrackSegment(n2, n3, TrackDirection.StartToEnd, usage: TrackUsage.Depot);
        world.CreateTrackConnection(depotOut, main, n1);
        world.CreateTrackConnection(main, depotIn, n2);
        world.CreateBlockSection([depotOut]);
        world.CreateBlockSection([main]);
        world.CreateBlockSection([depotIn]);

        var offRouteStart = world.CreateTrackNode(new WorldPoint(-20d, 10d, 0d));
        var offRouteEnd = world.CreateTrackNode(new WorldPoint(20d, 10d, 0d));
        var offRoute = world.CreateTrackSegment(offRouteStart, offRouteEnd);
        var station = world.CreateStation(new WorldVolume(-24d, -4d, -1d, 24d, 14d, 4d));
        var preferred = world.CreatePlatform(station, offRoute, 0.25d, 0.75d, new WorldVolume(-10d, 8d, -1d, 10d, 12d, 3d));
        var alternate = world.CreatePlatform(station, main, 0.4d, 0.6d, new WorldVolume(-10d, -2d, -1d, 10d, 2d, 3d));
        var originDepot = world.CreateDepot(new WorldVolume(-64d, -4d, -1d, -36d, 4d, 4d), [depotOut]);
        var destinationDepot = world.CreateDepot(new WorldVolume(36d, -4d, -1d, 64d, 4d, 4d), [depotIn]);

        var formation = world.CreateTrainFormation(20d, 18d, 1.4d, 1.8d, 100);
        var route = world.CreateRailwayRoute([depotOut, main, depotIn]);
        var timetable = world.CreateTimetable([
            new TimetableStopSnapshot(station, 120, 130, 5, preferred),
        ]);
        var service = world.CreateRailwayService(formation, route, timetable, originDepot, destinationDepot, plannedStartTick: 1);
        var train = world.CreateTrain(service);
        var observedFallback = false;
        var observedTrain = false;

        for (var tick = 0; tick < 2400; tick++)
        {
            world.Step();
            var snapshot = world.CreateRailwayOperationsSnapshot();
            var trainState = snapshot.Trains.SingleOrDefault(item => item.Id == train);
            if (trainState is not null)
            {
                observedTrain = true;
                if (trainState.AssignedPlatformId is { } assigned)
                {
                    Assert.AreEqual(alternate, assigned, "A route-external preferred Platform must not block fallback to an eligible Platform.");
                    observedFallback = true;
                }
            }

            if (!snapshot.Services.Any(item => item.Id == service) && !snapshot.Trains.Any(item => item.Id == train)) break;
        }

        var completed = world.CreateRailwayOperationsSnapshot();
        Assert.IsTrue(observedTrain, "The train was never observed in active operations.");
        Assert.IsTrue(observedFallback, "The alternate Platform was never assigned.");
        Assert.IsFalse(completed.Services.Any(item => item.Id == service), Describe(completed));
        Assert.IsFalse(completed.Trains.Any(item => item.Id == train), Describe(completed));
    }

    [TestMethod]
    public void MultipleTrainsNeverOwnTheSameBlockOrPlatform()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        var observedDelay = false;
        var observedTrainIds = new HashSet<TrainId>();

        for (var tick = 0; tick < 2400; tick++)
        {
            world.Step();
            var snapshot = world.CreateRailwayOperationsSnapshot();
            foreach (var train in snapshot.Trains) observedTrainIds.Add(train.Id);

            foreach (var blockOwners in snapshot.Trains.Where(static train => train.CurrentBlockId is not null).GroupBy(static train => train.CurrentBlockId!.Value))
                Assert.AreEqual(1, blockOwners.Count(), $"Block conflict at tick {world.Time.TickCount}.");
            foreach (var platformOwners in snapshot.Trains.Where(static train => train.AssignedPlatformId is not null).GroupBy(static train => train.AssignedPlatformId!.Value))
                Assert.AreEqual(1, platformOwners.Count(), $"Platform conflict at tick {world.Time.TickCount}.");
            if (snapshot.Services.Any(static service => service.DelayTicks > 0)) observedDelay = true;
        }

        var completed = world.CreateRailwayOperationsSnapshot();
        Assert.AreEqual(2, observedTrainIds.Count, "Both seeded trains should have entered active operations.");
        Assert.IsTrue(observedDelay);
        Assert.AreEqual(0, completed.Services.Length, Describe(completed));
        Assert.AreEqual(0, completed.Trains.Length, Describe(completed));
    }

    [TestMethod]
    public void CheckpointRejectsServiceWhoseOriginDepotDoesNotOwnRouteStart()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        var checkpoint = world.CreateCheckpoint();
        var services = checkpoint.RailwayServices!.ToArray();
        var first = services[0];
        Assert.AreNotEqual(first.OriginDepotId, first.DestinationDepotId);
        services[0] = first with { OriginDepotId = first.DestinationDepotId };

        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { RailwayServices = services }));
    }

    [TestMethod]
    public void CheckpointRejectsForgedRouteLengthAndServiceTrainCompletionMismatch()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        var checkpoint = world.CreateCheckpoint();
        var routes = checkpoint.RailwayRoutes!.ToArray();
        routes[0] = routes[0] with { LengthMeters = routes[0].LengthMeters * 10d };
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { RailwayRoutes = routes }));

        for (var tick = 0; tick < 3000 && world.CreateRailwayOperationsSnapshot().Services.Any(static service => service.State != RailwayServiceState.Completed); tick++) world.Step();
        checkpoint = world.CreateCheckpoint();
        var services = checkpoint.RailwayServices!.ToArray();
        services[0] = services[0] with { State = RailwayServiceState.Active };
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { RailwayServices = services }));
    }

    [TestMethod]
    public void CheckpointRestoreContinuesWithIdenticalOperationState()
    {
        var original = new SimulationWorld(new SimulationConfig(seed: 0x18UL));
        RailwayOperationsFixtures.SeedDeterministic(original);
        for (var tick = 0; tick < 180; tick++) original.Step();

        var restored = SimulationWorld.RestoreCheckpoint(original.CreateCheckpoint());
        for (var tick = 0; tick < 240; tick++)
        {
            original.Step();
            restored.Step();
        }

        var expected = original.CreateRailwayOperationsSnapshot();
        var actual = restored.CreateRailwayOperationsSnapshot();
        Assert.AreEqual(expected.Services.Length, actual.Services.Length);
        Assert.AreEqual(expected.Trains.Length, actual.Trains.Length);
        for (var index = 0; index < expected.Services.Length; index++) Assert.AreEqual(expected.Services[index], actual.Services[index]);
        for (var index = 0; index < expected.Trains.Length; index++) Assert.AreEqual(expected.Trains[index], actual.Trains[index]);
    }

    [TestMethod]
    public void StableIdsAndDefinitionsSurviveCheckpointRoundTrip()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        for (var tick = 0; tick < 20; tick++) world.Step();
        var expected = world.CreateCheckpoint();

        var restored = SimulationWorld.RestoreCheckpoint(expected);
        var actual = restored.CreateCheckpoint();

        Assert.AreEqual(expected.NextTrainFormationId, actual.NextTrainFormationId);
        Assert.AreEqual(expected.NextRailwayRouteId, actual.NextRailwayRouteId);
        Assert.AreEqual(expected.NextTimetableId, actual.NextTimetableId);
        Assert.AreEqual(expected.NextRailwayServiceId, actual.NextRailwayServiceId);
        Assert.AreEqual(expected.NextTrainId, actual.NextTrainId);
        CollectionAssert.AreEqual(expected.TrainFormations!.Select(static item => item.Id.Value).ToArray(), actual.TrainFormations!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.RailwayRoutes!.Select(static item => item.Id.Value).ToArray(), actual.RailwayRoutes!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.Timetables!.Select(static item => item.Id.Value).ToArray(), actual.Timetables!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.RailwayServices!.Select(static item => item.Id.Value).ToArray(), actual.RailwayServices!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.Trains!.Select(static item => item.Id.Value).ToArray(), actual.Trains!.Select(static item => item.Id.Value).ToArray());
    }

    private static string Describe(RailwayOperationsSnapshot snapshot) => string.Join(" | ", snapshot.Services.Select(static service => $"S{service.Id.Value}:{service.State}:delay={service.DelayTicks}:next={service.NextStopIndex}").Concat(snapshot.Trains.Select(static train => $"T{train.Id.Value}:{train.State}:distance={train.RouteDistanceMeters:F3}:speed={train.SpeedMetersPerSecond:F3}:block={train.CurrentBlockId?.Value}:platform={train.CurrentPlatformId?.Value}:assigned={train.AssignedPlatformId?.Value}")));
}
