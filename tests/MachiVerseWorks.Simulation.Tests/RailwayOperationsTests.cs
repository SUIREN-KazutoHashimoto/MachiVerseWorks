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
        var first = world.CreateTrackSegment(a, b);
        var second = world.CreateTrackSegment(b, c);
        var expectedNextId = world.CreateCheckpoint().NextRailwayRouteId;

        Assert.ThrowsExactly<ArgumentException>(() => world.CreateRailwayRoute([new TrackSegmentId(ulong.MaxValue)]));
        Assert.ThrowsExactly<ArgumentException>(() => world.CreateRailwayRoute([first, second]));
        Assert.ThrowsExactly<ArgumentException>(() => world.CreateRailwayRoute([first, first]));

        var failed = world.CreateCheckpoint();
        Assert.AreEqual(expectedNextId, failed.NextRailwayRouteId);
        Assert.AreEqual(0, failed.RailwayRoutes!.Count);

        world.CreateTrackConnection(first, second, b);
        var created = world.CreateRailwayRoute([first, second]);
        Assert.AreEqual(expectedNextId, created.Value);
        Assert.AreEqual(expectedNextId + 1, world.CreateCheckpoint().NextRailwayRouteId);
    }

    [TestMethod]
    public void MultipleTrainsNeverOwnTheSameBlockOrPlatform()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        var observedDelay = false;

        for (var tick = 0; tick < 2400; tick++)
        {
            world.Step();
            var snapshot = world.CreateRailwayOperationsSnapshot();
            Assert.AreEqual(2, snapshot.Trains.Length);
            var first = snapshot.Trains[0];
            var second = snapshot.Trains[1];
            if (first.CurrentBlockId is { } firstBlock && second.CurrentBlockId is { } secondBlock)
                Assert.AreNotEqual(firstBlock, secondBlock, $"Block conflict at tick {world.Time.TickCount}.");
            if (first.AssignedPlatformId is { } firstPlatform && second.AssignedPlatformId is { } secondPlatform)
                Assert.AreNotEqual(firstPlatform, secondPlatform, $"Platform conflict at tick {world.Time.TickCount}.");
            if (snapshot.Services.Any(static service => service.DelayTicks > 0)) observedDelay = true;
        }

        var completed = world.CreateRailwayOperationsSnapshot();
        Assert.IsTrue(observedDelay);
        Assert.IsTrue(completed.Services.All(static service => service.State == RailwayServiceState.Completed), Describe(completed));
        Assert.IsTrue(completed.Trains.All(static train => train.State == TrainMovementState.Completed));
        Assert.IsTrue(completed.Trains.All(static train => train.CurrentDepotId is not null));
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
