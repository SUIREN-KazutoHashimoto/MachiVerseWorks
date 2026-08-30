using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class IntersectionControlSaveTests
{
    [TestMethod]
    public void SaveRoundTripPreservesDerivedSignalControllerState()
    {
        var world = CreateFourWayIntersection();
        for (var tick = 0; tick < 913; tick++) world.Step();

        var before = world.CreateIntersectionControlSnapshot();
        var restored = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));
        var after = restored.CreateIntersectionControlSnapshot();

        Assert.AreEqual(before.TickCount, after.TickCount);
        Assert.AreEqual(before.Controllers.Count, after.Controllers.Count);
        for (var index = 0; index < before.Controllers.Count; index++)
        {
            var expected = before.Controllers[index];
            var actual = after.Controllers[index];
            Assert.AreEqual(expected.IntersectionNodeId, actual.IntersectionNodeId);
            Assert.AreEqual(expected.Mode, actual.Mode);
            Assert.AreEqual(expected.PhaseIndex, actual.PhaseIndex);
            Assert.AreEqual(expected.PhaseTick, actual.PhaseTick);
            CollectionAssert.AreEqual(
                expected.MovementStates.Select(static item => item.Indication).ToArray(),
                actual.MovementStates.Select(static item => item.Indication).ToArray());
        }
    }

    private static SimulationWorld CreateFourWayIntersection()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var center = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection);
        var west = CreateArm(world, center, new WorldPoint(-20d, 0d, 0d));
        var east = CreateArm(world, center, new WorldPoint(20d, 0d, 0d));
        var south = CreateArm(world, center, new WorldPoint(0d, -20d, 0d));
        var north = CreateArm(world, center, new WorldPoint(0d, 20d, 0d));
        world.CreateLaneConnection(west.Inbound, east.Outbound, center, TurnMovement.Straight);
        world.CreateLaneConnection(east.Inbound, west.Outbound, center, TurnMovement.Straight);
        world.CreateLaneConnection(south.Inbound, north.Outbound, center, TurnMovement.Straight);
        world.CreateLaneConnection(north.Inbound, south.Outbound, center, TurnMovement.Straight);
        return world;
    }

    private static Arm CreateArm(SimulationWorld world, RoadNodeId center, WorldPoint endpoint)
    {
        var endpointId = world.CreateRoadNode(endpoint);
        var segment = world.CreateRoadSegment(center, endpointId, RoadKind.Local);
        return new Arm(
            world.CreateLane(segment, LaneDirection.Reverse, 0),
            world.CreateLane(segment, LaneDirection.Forward, 0));
    }

    private readonly record struct Arm(LaneId Inbound, LaneId Outbound);
}
