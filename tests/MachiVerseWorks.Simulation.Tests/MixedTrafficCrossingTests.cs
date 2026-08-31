using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class MixedTrafficCrossingTests
{
    [TestMethod]
    public void FixedSignalKeepsPedestrianClosedUntilAllRedThenResumes()
    {
        var fixture = CreateFourWayFixture();
        var walkingRoute = fixture.World.FindWalkingRoute(
            TripEndpoint.ForBuilding(fixture.SouthBuilding),
            TripEndpoint.ForBuilding(fixture.NorthBuilding));
        Assert.AreEqual(2, walkingRoute.Legs.Count);
        var crossingId = FindRouteCrossing(fixture.World, walkingRoute);

        var initialControl = fixture.World.CreateIntersectionControlSnapshot().Controllers.Single();
        Assert.AreEqual(IntersectionControlMode.FixedSignal, initialControl.Mode);
        Assert.IsTrue(initialControl.MovementStates.Any(static state => state.Indication == SignalIndication.Green));
        Assert.IsFalse(GetCrossing(fixture.World, crossingId).IsOpen);

        fixture.World.CreateVehicle(fixture.WestEastRoute, initialSpeedMetersPerSecond: 8d);
        var pedestrian = fixture.World.CreatePedestrian(
            new TripRequest(
                new TripRequestId(1),
                TripEndpoint.ForBuilding(fixture.SouthBuilding),
                TripEndpoint.ForBuilding(fixture.NorthBuilding),
                TravelMode.Foot),
            walkingSpeedMetersPerSecond: 5d);

        fixture.World.Step();
        fixture.World.Step();
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var waiting));
        Assert.AreEqual(PedestrianMovementState.WaitingForCrossing, waiting.State);
        Assert.AreEqual(0d, waiting.Position.X, 1e-9);
        Assert.AreEqual(0d, waiting.Position.Y, 1e-9);
        Assert.AreEqual(1, fixture.World.VehicleCount);

        var guard = 40;
        while (guard-- > 0 && !GetCrossing(fixture.World, crossingId).IsOpen) fixture.World.Step();
        Assert.IsTrue(guard > 0, "Signalized crossing did not reach the deterministic all-red window.");

        var allRed = fixture.World.CreateIntersectionControlSnapshot().Controllers.Single();
        Assert.IsTrue(allRed.MovementStates.All(static state => state.Indication == SignalIndication.Red));
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var resumed));
        Assert.AreEqual(PedestrianMovementState.Walking, resumed.State);
        Assert.IsTrue(resumed.Position.Y > 0d, "Pedestrian did not resume across the intersection during all-red.");

        Assert.IsTrue(fixture.World.SetPedestrianCrossingOpen(crossingId, false));
        Assert.IsFalse(GetCrossing(fixture.World, crossingId).IsOpen, "Manual close must dominate the automatic all-red opening.");
        Assert.IsTrue(fixture.World.SetPedestrianCrossingOpen(crossingId, true));
        Assert.IsTrue(GetCrossing(fixture.World, crossingId).IsOpen, "Manual open only releases the manual gate during an automatically safe phase.");

        fixture.World.Step();
        Assert.IsFalse(GetCrossing(fixture.World, crossingId).IsOpen, "Manual open must not override a vehicle signal phase.");
    }

    [TestMethod]
    public void UnsignalizedVehicleEntryGrantHasPriorityOverPedestrianCrossingForThatTick()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var center = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection);
        var west = CreateArm(world, center, new WorldPoint(-10d, 0d, 0d));
        var east = CreateArm(world, center, new WorldPoint(10d, 0d, 0d));
        var south = CreateArm(world, center, new WorldPoint(0d, -10d, 0d));
        var westEast = world.CreateLaneConnection(west.InboundLaneId, east.OutboundLaneId, center, TurnMovement.Straight);
        world.CreateLaneConnection(south.InboundLaneId, east.OutboundLaneId, center, TurnMovement.Left);

        var southBuilding = world.CreateBuilding(new WorldVolume(-1d, -11d, 0d, 1d, -9d, 3d), BuildingKind.Residential);
        var westBuilding = world.CreateBuilding(new WorldVolume(-11d, -1d, 0d, -9d, 1d, 3d), BuildingKind.Commercial);
        world.CreateRoadAccessPoint(south.SegmentId, 1d, southBuilding, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(west.SegmentId, 1d, westBuilding, mode: RoadAccessMode.Foot);

        var walkingRoute = world.FindWalkingRoute(
            TripEndpoint.ForBuilding(southBuilding),
            TripEndpoint.ForBuilding(westBuilding));
        var crossingId = FindRouteCrossing(world, walkingRoute);
        var initialControl = world.CreateIntersectionControlSnapshot().Controllers.Single();
        Assert.AreEqual(IntersectionControlMode.Unsignalized, initialControl.Mode);
        Assert.IsTrue(GetCrossing(world, crossingId).IsOpen);

        var blocker = world.CreateVehicle(
        [
            new RouteLaneStep(east.OutboundLaneId, east.SegmentId, 0d, 1d, 10d, 10_000d, null),
        ], performance: new VehiclePerformance(0.001d, 0.001d, 1d, 2d, 1.5d));
        var vehicle = world.CreateVehicle(
        [
            new RouteLaneStep(west.InboundLaneId, west.SegmentId, 1d, 0d, 10d, 1d, westEast),
            new RouteLaneStep(east.OutboundLaneId, east.SegmentId, 0d, 1d, 10d, 1d, null),
        ], initialSpeedMetersPerSecond: 8d);
        var pedestrian = world.CreatePedestrian(
            new TripRequest(
                new TripRequestId(1),
                TripEndpoint.ForBuilding(southBuilding),
                TripEndpoint.ForBuilding(westBuilding),
                TravelMode.Foot),
            walkingSpeedMetersPerSecond: 10d);
        Assert.IsTrue(world.SetPedestrianCrossingOpen(crossingId, false));

        for (var tick = 0; tick < world.Config.TickRate * 3; tick++) world.Step();

        Assert.IsTrue(world.TryGetVehicleSnapshot(vehicle, out var stoppedVehicle));
        Assert.AreEqual(0, stoppedVehicle.RouteStepIndex);
        Assert.AreEqual(VehicleMovementState.WaitingForTraffic, stoppedVehicle.State);
        Assert.AreEqual(10d, stoppedVehicle.RouteProgressMeters, 1e-8);
        Assert.IsTrue(world.TryGetPedestrianSnapshot(pedestrian, out var manuallyWaiting));
        Assert.AreEqual(PedestrianMovementState.WaitingForCrossing, manuallyWaiting.State);
        Assert.AreEqual(0d, manuallyWaiting.Position.X, 1e-9);
        Assert.AreEqual(0d, manuallyWaiting.Position.Y, 1e-9);

        Assert.IsTrue(world.RemoveVehicle(blocker));
        Assert.IsTrue(world.SetPedestrianCrossingOpen(crossingId, true));
        world.Step();

        var granted = world.CreateIntersectionControlSnapshot().Controllers.Single();
        Assert.IsTrue(granted.MovementStates.Any(static state => state.EntryGrantedThisTick));
        Assert.IsFalse(GetCrossing(world, crossingId).IsOpen);
        Assert.IsTrue(world.TryGetPedestrianSnapshot(pedestrian, out var automaticallyWaiting));
        Assert.AreEqual(PedestrianMovementState.WaitingForCrossing, automaticallyWaiting.State);

        world.Step();

        var released = world.CreateIntersectionControlSnapshot().Controllers.Single();
        Assert.IsFalse(released.MovementStates.Any(static state => state.EntryGrantedThisTick));
        Assert.IsTrue(GetCrossing(world, crossingId).IsOpen);
        Assert.IsTrue(world.TryGetPedestrianSnapshot(pedestrian, out var resumed));
        Assert.AreNotEqual(PedestrianMovementState.WaitingForCrossing, resumed.State);
        Assert.IsTrue(resumed.Position.X < 0d, "Pedestrian did not resume after the unsignalized Vehicle grant cleared.");
    }

    private static FourWayFixture CreateFourWayFixture()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1));
        var center = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection);
        var west = CreateArm(world, center, new WorldPoint(-10d, 0d, 0d));
        var east = CreateArm(world, center, new WorldPoint(10d, 0d, 0d));
        var south = CreateArm(world, center, new WorldPoint(0d, -10d, 0d));
        var north = CreateArm(world, center, new WorldPoint(0d, 10d, 0d));

        var westEast = world.CreateLaneConnection(west.InboundLaneId, east.OutboundLaneId, center, TurnMovement.Straight);
        world.CreateLaneConnection(south.InboundLaneId, north.OutboundLaneId, center, TurnMovement.Straight);
        world.CreateLaneConnection(east.InboundLaneId, west.OutboundLaneId, center, TurnMovement.Straight);
        world.CreateLaneConnection(north.InboundLaneId, south.OutboundLaneId, center, TurnMovement.Straight);

        var southBuilding = world.CreateBuilding(new WorldVolume(-1d, -11d, 0d, 1d, -9d, 3d), BuildingKind.Residential);
        var northBuilding = world.CreateBuilding(new WorldVolume(-1d, 9d, 0d, 1d, 11d, 3d), BuildingKind.Commercial);
        world.CreateRoadAccessPoint(south.SegmentId, 1d, southBuilding, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(north.SegmentId, 1d, northBuilding, mode: RoadAccessMode.Foot);

        IReadOnlyList<RouteLaneStep> westEastRoute =
        [
            new RouteLaneStep(west.InboundLaneId, west.SegmentId, 1d, 0d, 10d, 1d, westEast),
            new RouteLaneStep(east.OutboundLaneId, east.SegmentId, 0d, 1d, 10d, 1d, null),
        ];
        return new FourWayFixture(world, southBuilding, northBuilding, westEastRoute);
    }

    private static Arm CreateArm(SimulationWorld world, RoadNodeId center, WorldPoint endpoint)
    {
        var endpointId = world.CreateRoadNode(endpoint);
        var segment = world.CreateRoadSegment(center, endpointId, RoadKind.Local);
        var outbound = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        var inbound = world.CreateLane(segment, LaneDirection.Reverse, 0, speedLimitMetersPerSecond: 10d);
        return new Arm(segment, inbound, outbound);
    }

    private static PedestrianCrossingId FindRouteCrossing(SimulationWorld world, PedestrianRoute route)
    {
        Assert.IsTrue(route.Legs.Count >= 2, "Walking route must cross an intersection.");
        var first = route.Legs[0].EdgeId;
        var second = route.Legs[1].EdgeId;
        return world.CreatePedestrianNetworkSnapshot().Crossings.Single(crossing =>
            crossing.FirstEdgeId == first && crossing.SecondEdgeId == second
            || crossing.FirstEdgeId == second && crossing.SecondEdgeId == first).Id;
    }

    private static PedestrianCrossingSnapshot GetCrossing(SimulationWorld world, PedestrianCrossingId id) =>
        world.CreatePedestrianNetworkSnapshot().Crossings.Single(crossing => crossing.Id == id);

    private readonly record struct Arm(RoadSegmentId SegmentId, LaneId InboundLaneId, LaneId OutboundLaneId);
    private sealed record FourWayFixture(
        SimulationWorld World,
        BuildingId SouthBuilding,
        BuildingId NorthBuilding,
        IReadOnlyList<RouteLaneStep> WestEastRoute);
}
