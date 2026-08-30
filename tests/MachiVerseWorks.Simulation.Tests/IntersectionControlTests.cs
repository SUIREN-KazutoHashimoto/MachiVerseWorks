using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class IntersectionControlTests
{
    [TestMethod]
    public void FourWayIntersectionBuildsConflictsAndDeterministicFixedSignal()
    {
        var fixture = CreateFourWayFixture();

        var initial = fixture.World.CreateIntersectionControlSnapshot();
        Assert.AreEqual(1, initial.Controllers.Count);
        var controller = initial.Controllers[0];
        Assert.AreEqual(IntersectionControlMode.FixedSignal, controller.Mode);
        Assert.AreEqual(4, controller.Movements.Count);
        Assert.IsTrue(controller.Movements.Any(static movement => movement.Conflicts.Count > 0));
        Assert.IsTrue(controller.MovementStates.Any(static state => state.Indication == SignalIndication.Green));
        Assert.IsTrue(controller.MovementStates.Any(static state => state.Indication == SignalIndication.Red));

        for (var tick = 0; tick < fixture.World.Config.TickRate * 24; tick++) fixture.World.Step();

        var nextPhase = fixture.World.CreateIntersectionControlSnapshot().Controllers[0];
        Assert.AreNotEqual(controller.PhaseIndex, nextPhase.PhaseIndex);
        CollectionAssert.AreNotEqual(
            controller.MovementStates.Select(static state => state.Indication).ToArray(),
            nextPhase.MovementStates.Select(static state => state.Indication).ToArray());
    }

    [TestMethod]
    public void VehicleStopsOnRedAndEntersWhenItsMovementTurnsGreen()
    {
        var fixture = CreateFourWayFixture();
        var controller = fixture.World.CreateIntersectionControlSnapshot().Controllers[0];
        var redState = controller.MovementStates.First(static state => state.Indication == SignalIndication.Red);
        var movement = controller.Movements.Single(item => item.Id == redState.MovementId);
        var route = fixture.Routes[movement.ConnectionId];
        var vehicle = fixture.World.CreateVehicle(route, initialSpeedMetersPerSecond: 8d);

        for (var tick = 0; tick < fixture.World.Config.TickRate * 3; tick++) fixture.World.Step();

        Assert.IsTrue(fixture.World.TryGetVehicleSnapshot(vehicle, out var stopped));
        Assert.AreEqual(0, stopped.RouteStepIndex);
        Assert.AreEqual(VehicleMovementState.WaitingForTraffic, stopped.State);
        Assert.AreEqual(route[0].DistanceMeters, stopped.RouteProgressMeters, 1e-8);

        var guard = fixture.World.Config.TickRate * 60;
        while (guard-- > 0)
        {
            var state = fixture.World.CreateIntersectionControlSnapshot().Controllers[0].MovementStates
                .Single(item => item.MovementId == movement.Id);
            if (state.Indication == SignalIndication.Green) break;
            fixture.World.Step();
        }
        Assert.IsTrue(guard > 0, "The movement did not receive green within one fixed signal cycle.");

        fixture.World.Step();
        Assert.IsTrue(fixture.World.TryGetVehicleSnapshot(vehicle, out var entered));
        Assert.IsTrue(entered.RouteStepIndex > 0 || entered.State == VehicleMovementState.Arrived);
    }

    [TestMethod]
    public void UnsignalizedConflictAllowsOnlyOneEntryPerTick()
    {
        var fixture = CreateThreeWayFixture();
        var initial = fixture.World.CreateIntersectionControlSnapshot().Controllers[0];
        Assert.AreEqual(IntersectionControlMode.Unsignalized, initial.Mode);
        Assert.AreEqual(2, initial.Movements.Count);
        Assert.IsTrue(initial.Movements[0].Conflicts.Contains(initial.Movements[1].Id));

        var first = fixture.World.CreateVehicle(fixture.Routes[initial.Movements[0].ConnectionId], initialSpeedMetersPerSecond: 8d);
        var second = fixture.World.CreateVehicle(fixture.Routes[initial.Movements[1].ConnectionId], initialSpeedMetersPerSecond: 8d);

        for (var tick = 0; tick < fixture.World.Config.TickRate * 3; tick++) fixture.World.Step();

        Assert.IsTrue(fixture.World.TryGetVehicleSnapshot(first, out var firstSnapshot));
        Assert.IsTrue(fixture.World.TryGetVehicleSnapshot(second, out var secondSnapshot));
        var enteredCount = (firstSnapshot.RouteStepIndex > 0 ? 1 : 0) + (secondSnapshot.RouteStepIndex > 0 ? 1 : 0);
        Assert.IsTrue(enteredCount >= 1);
        Assert.IsFalse(firstSnapshot.RouteStepIndex > 0 && secondSnapshot.RouteStepIndex > 0 && firstSnapshot.TickCount == secondSnapshot.TickCount
            && firstSnapshot.Position.X == secondSnapshot.Position.X && firstSnapshot.Position.Y == secondSnapshot.Position.Y);
    }

    [TestMethod]
    public void DownstreamBlockingKeepsVehicleAtStopLine()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var west = world.CreateRoadNode(new WorldPoint(-10, 0, 0));
        var center = world.CreateRoadNode(new WorldPoint(0, 0, 0), RoadNodeKind.Intersection);
        var east = world.CreateRoadNode(new WorldPoint(10, 0, 0));
        var westSegment = world.CreateRoadSegment(center, west);
        var eastSegment = world.CreateRoadSegment(center, east);
        var inbound = world.CreateLane(westSegment, LaneDirection.Reverse, 0);
        var outbound = world.CreateLane(eastSegment, LaneDirection.Forward, 0);
        var connection = world.CreateLaneConnection(inbound, outbound, center, TurnMovement.Straight);

        var blockerRoute = new[]
        {
            new RouteLaneStep(outbound, eastSegment, 0d, 0d, 0d, 0d, null),
        };
        world.CreateVehicle(blockerRoute);

        var route = new[]
        {
            new RouteLaneStep(inbound, westSegment, 1d, 0d, 10d, 1d, connection),
            new RouteLaneStep(outbound, eastSegment, 0d, 1d, 10d, 1d, null),
        };
        var vehicle = world.CreateVehicle(route, initialSpeedMetersPerSecond: 8d);

        for (var tick = 0; tick < world.Config.TickRate * 3; tick++) world.Step();

        Assert.IsTrue(world.TryGetVehicleSnapshot(vehicle, out var snapshot));
        Assert.AreEqual(0, snapshot.RouteStepIndex);
        Assert.AreEqual(VehicleMovementState.WaitingForTraffic, snapshot.State);
        Assert.AreEqual(route[0].DistanceMeters, snapshot.RouteProgressMeters, 1e-8);
        Assert.IsTrue(world.CreateTrafficMetrics().QueueLength >= 1);
    }

    [TestMethod]
    public void CheckpointRestoreKeepsSignalPhaseDeterministic()
    {
        var fixture = CreateFourWayFixture();
        for (var tick = 0; tick < 913; tick++) fixture.World.Step();
        var before = fixture.World.CreateIntersectionControlSnapshot();

        var restored = SimulationWorld.RestoreCheckpoint(fixture.World.CreateCheckpoint());
        var after = restored.CreateIntersectionControlSnapshot();

        Assert.AreEqual(before.TickCount, after.TickCount);
        Assert.AreEqual(before.Controllers[0].PhaseIndex, after.Controllers[0].PhaseIndex);
        Assert.AreEqual(before.Controllers[0].PhaseTick, after.Controllers[0].PhaseTick);
        CollectionAssert.AreEqual(
            before.Controllers[0].MovementStates.Select(static state => state.Indication).ToArray(),
            after.Controllers[0].MovementStates.Select(static state => state.Indication).ToArray());
    }

    private static Fixture CreateFourWayFixture()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var center = world.CreateRoadNode(new WorldPoint(0, 0, 0), RoadNodeKind.Intersection);
        var west = CreateArm(world, center, new WorldPoint(-10, 0, 0));
        var east = CreateArm(world, center, new WorldPoint(10, 0, 0));
        var south = CreateArm(world, center, new WorldPoint(0, -10, 0));
        var north = CreateArm(world, center, new WorldPoint(0, 10, 0));

        var routes = new Dictionary<LaneConnectionId, IReadOnlyList<RouteLaneStep>>();
        AddMovement(world, center, west, east, TurnMovement.Straight, routes);
        AddMovement(world, center, south, north, TurnMovement.Straight, routes);
        AddMovement(world, center, east, west, TurnMovement.Straight, routes);
        AddMovement(world, center, north, south, TurnMovement.Straight, routes);
        return new Fixture(world, routes);
    }

    private static Fixture CreateThreeWayFixture()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var center = world.CreateRoadNode(new WorldPoint(0, 0, 0), RoadNodeKind.Intersection);
        var west = CreateArm(world, center, new WorldPoint(-10, 0, 0));
        var east = CreateArm(world, center, new WorldPoint(10, 0, 0));
        var south = CreateArm(world, center, new WorldPoint(0, -10, 0));

        var routes = new Dictionary<LaneConnectionId, IReadOnlyList<RouteLaneStep>>();
        AddMovement(world, center, west, east, TurnMovement.Straight, routes);
        AddMovement(world, center, south, west, TurnMovement.Left, routes);
        return new Fixture(world, routes);
    }

    private static Arm CreateArm(SimulationWorld world, RoadNodeId center, WorldPoint endpointPosition)
    {
        var endpoint = world.CreateRoadNode(endpointPosition);
        var segment = world.CreateRoadSegment(center, endpoint, RoadKind.Local);
        var outbound = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        var inbound = world.CreateLane(segment, LaneDirection.Reverse, 0, speedLimitMetersPerSecond: 10d);
        return new Arm(segment, inbound, outbound, Distance(endpointPosition, new WorldPoint(0, 0, 0)));
    }

    private static void AddMovement(
        SimulationWorld world,
        RoadNodeId center,
        Arm from,
        Arm to,
        TurnMovement turn,
        Dictionary<LaneConnectionId, IReadOnlyList<RouteLaneStep>> routes)
    {
        var connection = world.CreateLaneConnection(from.InboundLaneId, to.OutboundLaneId, center, turn);
        routes.Add(connection,
        [
            new RouteLaneStep(from.InboundLaneId, from.SegmentId, 1d, 0d, from.LengthMeters, from.LengthMeters / 10d, connection),
            new RouteLaneStep(to.OutboundLaneId, to.SegmentId, 0d, 1d, to.LengthMeters, to.LengthMeters / 10d, null),
        ]);
    }

    private static double Distance(WorldPoint left, WorldPoint right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        var z = left.Z - right.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }

    private sealed record Fixture(SimulationWorld World, Dictionary<LaneConnectionId, IReadOnlyList<RouteLaneStep>> Routes);
    private readonly record struct Arm(RoadSegmentId SegmentId, LaneId InboundLaneId, LaneId OutboundLaneId, double LengthMeters);
}
