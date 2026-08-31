using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class IntersectionControlRegressionTests
{
    [TestMethod]
    public void MultipleIntersectionsKeepIndependentDeterministicControllers()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        CreateFourWayController(world, new WorldPoint(-100d, 0d, 0d));
        CreateFourWayController(world, new WorldPoint(100d, 0d, 0d));

        for (var tick = 0; tick < 777; tick++) world.Step();
        var before = world.CreateIntersectionControlSnapshot();
        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());
        var after = restored.CreateIntersectionControlSnapshot();

        Assert.AreEqual(2, before.Controllers.Count);
        Assert.AreEqual(2, after.Controllers.Count);
        for (var index = 0; index < before.Controllers.Count; index++)
        {
            Assert.AreEqual(IntersectionControlMode.FixedSignal, before.Controllers[index].Mode);
            Assert.AreEqual(before.Controllers[index].IntersectionNodeId, after.Controllers[index].IntersectionNodeId);
            Assert.AreEqual(before.Controllers[index].PhaseIndex, after.Controllers[index].PhaseIndex);
            Assert.AreEqual(before.Controllers[index].PhaseTick, after.Controllers[index].PhaseTick);
            CollectionAssert.AreEqual(
                before.Controllers[index].MovementStates.Select(static item => item.Indication).ToArray(),
                after.Controllers[index].MovementStates.Select(static item => item.Indication).ToArray());
        }
    }

    [TestMethod]
    public void LeftAndRightTurnMovementsRemainExplicitAndConflictSafe()
    {
        var world = new SimulationWorld();
        var center = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection);
        var west = CreateArm(world, center, new WorldPoint(-20d, 0d, 0d), 0);
        var east = CreateArm(world, center, new WorldPoint(20d, 0d, 0d), 0);
        var south = CreateArm(world, center, new WorldPoint(0d, -20d, 0d), 0);
        var north = CreateArm(world, center, new WorldPoint(0d, 20d, 0d), 0);

        var left = world.CreateLaneConnection(south.Inbound, west.Outbound, center, TurnMovement.Left);
        var right = world.CreateLaneConnection(north.Inbound, west.Outbound, center, TurnMovement.Right);
        world.CreateLaneConnection(west.Inbound, east.Outbound, center, TurnMovement.Straight);
        world.CreateLaneConnection(east.Inbound, north.Outbound, center, TurnMovement.Left);

        var controller = world.CreateIntersectionControlSnapshot().Controllers.Single();
        var leftMovement = controller.Movements.Single(item => item.ConnectionId == left);
        var rightMovement = controller.Movements.Single(item => item.ConnectionId == right);

        Assert.AreEqual(TurnMovement.Left, leftMovement.TurnMovement);
        Assert.AreEqual(TurnMovement.Right, rightMovement.TurnMovement);
        Assert.IsTrue(leftMovement.Conflicts.Contains(rightMovement.Id));
        Assert.IsTrue(rightMovement.Conflicts.Contains(leftMovement.Id));
    }

    [TestMethod]
    public void HighLoadBlockedIntersectionBuildsDeterministicQueues()
    {
        const int lanesPerArm = 8;
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var center = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection);
        var west = CreateMultiLaneArm(world, center, new WorldPoint(-30d, 0d, 0d), lanesPerArm);
        var east = CreateMultiLaneArm(world, center, new WorldPoint(30d, 0d, 0d), lanesPerArm);
        var south = CreateMultiLaneArm(world, center, new WorldPoint(0d, -30d, 0d), lanesPerArm);
        var north = CreateMultiLaneArm(world, center, new WorldPoint(0d, 30d, 0d), lanesPerArm);

        var westEast = CreateFlowConnections(world, center, west, east);
        var eastWest = CreateFlowConnections(world, center, east, west);
        var southNorth = CreateFlowConnections(world, center, south, north);
        var northSouth = CreateFlowConnections(world, center, north, south);
        AddBlockedFlows(world, west, east, westEast);
        AddBlockedFlows(world, east, west, eastWest);
        AddBlockedFlows(world, south, north, southNorth);
        AddBlockedFlows(world, north, south, northSouth);

        for (var tick = 0; tick < world.Config.TickRate * 5; tick++) world.Step();

        var metrics = world.CreateTrafficMetrics();
        var controller = world.CreateIntersectionControlSnapshot().Controllers.Single();
        var queuedAtStopLines = controller.MovementStates.Sum(static item => item.QueueLength);
        Assert.AreEqual(lanesPerArm * 8, metrics.VehicleCount);
        Assert.IsTrue(metrics.QueueLength >= lanesPerArm * 4);
        Assert.AreEqual(lanesPerArm * 4, queuedAtStopLines);

        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());
        var restoredController = restored.CreateIntersectionControlSnapshot().Controllers.Single();
        Assert.AreEqual(controller.PhaseIndex, restoredController.PhaseIndex);
        CollectionAssert.AreEqual(
            controller.MovementStates.Select(static item => item.Indication).ToArray(),
            restoredController.MovementStates.Select(static item => item.Indication).ToArray());
    }

    private static void CreateFourWayController(SimulationWorld world, WorldPoint origin)
    {
        var center = world.CreateRoadNode(origin, RoadNodeKind.Intersection);
        var west = CreateArm(world, center, new WorldPoint(origin.X - 20d, origin.Y, origin.Z), 0);
        var east = CreateArm(world, center, new WorldPoint(origin.X + 20d, origin.Y, origin.Z), 0);
        var south = CreateArm(world, center, new WorldPoint(origin.X, origin.Y - 20d, origin.Z), 0);
        var north = CreateArm(world, center, new WorldPoint(origin.X, origin.Y + 20d, origin.Z), 0);
        world.CreateLaneConnection(west.Inbound, east.Outbound, center, TurnMovement.Straight);
        world.CreateLaneConnection(east.Inbound, west.Outbound, center, TurnMovement.Straight);
        world.CreateLaneConnection(south.Inbound, north.Outbound, center, TurnMovement.Straight);
        world.CreateLaneConnection(north.Inbound, south.Outbound, center, TurnMovement.Straight);
    }

    private static Arm CreateArm(SimulationWorld world, RoadNodeId center, WorldPoint endpoint, ushort order)
    {
        var endpointId = world.CreateRoadNode(endpoint);
        var segment = world.CreateRoadSegment(center, endpointId, RoadKind.Local);
        return new Arm(
            segment,
            world.CreateLane(segment, LaneDirection.Reverse, order, speedLimitMetersPerSecond: 10d),
            world.CreateLane(segment, LaneDirection.Forward, order, speedLimitMetersPerSecond: 10d));
    }

    private static MultiLaneArm CreateMultiLaneArm(SimulationWorld world, RoadNodeId center, WorldPoint endpoint, int laneCount)
    {
        var endpointId = world.CreateRoadNode(endpoint);
        var segment = world.CreateRoadSegment(center, endpointId, RoadKind.Local);
        var inbound = new LaneId[laneCount];
        var outbound = new LaneId[laneCount];
        for (ushort order = 0; order < laneCount; order++)
        {
            inbound[order] = world.CreateLane(segment, LaneDirection.Reverse, order, speedLimitMetersPerSecond: 10d);
            outbound[order] = world.CreateLane(segment, LaneDirection.Forward, order, speedLimitMetersPerSecond: 10d);
        }
        return new MultiLaneArm(segment, inbound, outbound);
    }

    private static LaneConnectionId[] CreateFlowConnections(SimulationWorld world, RoadNodeId center, MultiLaneArm from, MultiLaneArm to)
    {
        var connections = new LaneConnectionId[from.Inbound.Length];
        for (var index = 0; index < connections.Length; index++)
            connections[index] = world.CreateLaneConnection(from.Inbound[index], to.Outbound[index], center, TurnMovement.Straight);
        return connections;
    }

    private static void AddBlockedFlows(SimulationWorld world, MultiLaneArm from, MultiLaneArm to, LaneConnectionId[] connections)
    {
        for (var index = 0; index < from.Inbound.Length; index++)
        {
            world.CreateVehicle([
                new RouteLaneStep(to.Outbound[index], to.Segment, 0d, 0d, 0d, 0d, null),
            ]);
            world.CreateVehicle([
                new RouteLaneStep(from.Inbound[index], from.Segment, 1d, 0d, 30d, 3d, connections[index]),
                new RouteLaneStep(to.Outbound[index], to.Segment, 0d, 1d, 30d, 3d, null),
            ], initialSpeedMetersPerSecond: 8d);
        }
    }

    private readonly record struct Arm(RoadSegmentId Segment, LaneId Inbound, LaneId Outbound);
    private readonly record struct MultiLaneArm(RoadSegmentId Segment, LaneId[] Inbound, LaneId[] Outbound);
}
