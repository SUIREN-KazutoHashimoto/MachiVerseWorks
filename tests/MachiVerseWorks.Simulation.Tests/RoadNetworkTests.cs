using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RoadNetworkTests
{
    private static readonly ulong[] ExpectedNodeIds = [1UL, 2UL, 3UL];
    private static readonly ulong[] ExpectedSegmentIds = [1UL, 2UL];

    [TestMethod]
    public void StableIdsTopologyAndLaneOrderAreDeterministic()
    {
        var world = new SimulationWorld(new SimulationConfig(spatialCellSize: 32d)); var a = world.CreateRoadNode(new WorldPoint(0, 0, 0)); var junction = world.CreateRoadNode(new WorldPoint(100, 0, 0), RoadNodeKind.Intersection); var b = world.CreateRoadNode(new WorldPoint(100, 100, 0)); var incoming = world.CreateRoadSegment(a, junction, RoadKind.Collector); var outgoing = world.CreateRoadSegment(junction, b, RoadKind.Local); var inLane = world.CreateLane(incoming, LaneDirection.Forward, 0, 3.5, 16.7); var outLane = world.CreateLane(outgoing, LaneDirection.Forward, 0, 3.25, 13.9); var turn = world.CreateLaneConnection(inLane, outLane, junction, TurnMovement.Left);
        Assert.AreEqual(1UL, a.Value); Assert.AreEqual(2UL, junction.Value); Assert.AreEqual(1UL, incoming.Value); Assert.AreEqual(1UL, inLane.Value); Assert.AreEqual(1UL, turn.Value);
        var snapshot = world.CreateRoadNetworkSnapshot(); CollectionAssert.AreEqual(ExpectedNodeIds, snapshot.Nodes.Select(static x => x.Id.Value).ToArray()); CollectionAssert.AreEqual(ExpectedSegmentIds, snapshot.Segments.Select(static x => x.Id.Value).ToArray());
    }
    [TestMethod]
    public void CrossingGeometryNeverCreatesImplicitTopology()
    {
        var world = new SimulationWorld(new SimulationConfig(spatialCellSize: 16d)); var west = world.CreateRoadNode(new WorldPoint(-100, 0, 0)); var east = world.CreateRoadNode(new WorldPoint(100, 0, 0)); var south = world.CreateRoadNode(new WorldPoint(0, -100, 20)); var north = world.CreateRoadNode(new WorldPoint(0, 100, 20)); world.CreateRoadSegment(west, east); world.CreateRoadSegment(south, north); var snapshot = world.CreateRoadNetworkSnapshot(new WorldVolume(-5, -5, -5, 5, 5, 25)); Assert.AreEqual(2, snapshot.Segments.Count); Assert.AreEqual(0, snapshot.Connections.Count); Assert.AreEqual(4, snapshot.Nodes.Count);
    }
    [TestMethod]
    public void EndpointCannotBecomeImplicitIntersectionAndFailuresAreAtomic()
    {
        var world = new SimulationWorld(); var center = world.CreateRoadNode(new WorldPoint(0, 0, 0)); var a = world.CreateRoadNode(new WorldPoint(-10, 0, 0)); var b = world.CreateRoadNode(new WorldPoint(10, 0, 0)); world.CreateRoadSegment(a, center); Assert.ThrowsExactly<InvalidOperationException>(() => world.CreateRoadSegment(center, b)); Assert.AreEqual(1, world.RoadSegmentCount); Assert.IsTrue(world.UpdateRoadNode(center, new WorldPoint(0, 0, 0), RoadNodeKind.Intersection)); world.CreateRoadSegment(center, b); Assert.AreEqual(2, world.RoadSegmentCount);
    }
    [TestMethod]
    public void LaneConnectionMustFollowDirectedIntersectionTopology()
    {
        var world = new SimulationWorld(); var a = world.CreateRoadNode(new WorldPoint(-10, 0, 0)); var j = world.CreateRoadNode(new WorldPoint(0, 0, 0), RoadNodeKind.Intersection); var b = world.CreateRoadNode(new WorldPoint(10, 0, 0)); var s1 = world.CreateRoadSegment(a, j); var s2 = world.CreateRoadSegment(j, b); var forward1 = world.CreateLane(s1, LaneDirection.Forward, 0); var forward2 = world.CreateLane(s2, LaneDirection.Forward, 0); var reverse2 = world.CreateLane(s2, LaneDirection.Reverse, 0); world.CreateLaneConnection(forward1, forward2, j, TurnMovement.Straight); Assert.ThrowsExactly<InvalidOperationException>(() => world.CreateLaneConnection(forward1, reverse2, j)); Assert.AreEqual(1, world.LaneConnectionCount);
    }
    [TestMethod]
    public void IntersectionReferencedByLaneConnectionCannotBeDemotedToEndpoint()
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var junction = world.CreateRoadNode(new WorldPoint(10, 0, 0), RoadNodeKind.Intersection);
        var segment = world.CreateRoadSegment(start, junction);
        var incoming = world.CreateLane(segment, LaneDirection.Forward, 0);
        var outgoing = world.CreateLane(segment, LaneDirection.Reverse, 0);
        world.CreateLaneConnection(incoming, outgoing, junction);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            world.UpdateRoadNode(junction, new WorldPoint(10, 0, 0), RoadNodeKind.Endpoint));

        Assert.IsTrue(world.TryGetRoadNodeSnapshot(junction, out var snapshot));
        Assert.AreEqual(RoadNodeKind.Intersection, snapshot.Kind);
        Assert.AreEqual(1, world.LaneConnectionCount);
    }
    [TestMethod]
    public void RoadAccessPointRequiresExistingUrbanReference()
    {
        var world = new SimulationWorld(); var a = world.CreateRoadNode(new WorldPoint(0, 0, 0)); var b = world.CreateRoadNode(new WorldPoint(20, 0, 0)); var segment = world.CreateRoadSegment(a, b); var building = world.CreateBuilding(new WorldVolume(0, 5, 0, 10, 15, 10)); var poi = world.CreatePoi(new WorldPoint(5, 10, 0), PoiKind.Service, building); var access = world.CreateRoadAccessPoint(segment, 0.5, building, poi, RoadAccessMode.Motor | RoadAccessMode.Foot); Assert.AreEqual(1UL, access.Value); Assert.ThrowsExactly<ArgumentException>(() => world.CreateRoadAccessPoint(segment, 0.5, new BuildingId(999))); Assert.AreEqual(1, world.RoadAccessPointCount);
    }
    [TestMethod]
    public void RoadAccessPointReferencesBlockUrbanEntityRemoval()
    {
        var world = new SimulationWorld();
        var a = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var b = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var segment = world.CreateRoadSegment(a, b);
        var building = world.CreateBuilding(new WorldVolume(0, 5, 0, 10, 15, 10));
        var buildingAccess = world.CreateRoadAccessPoint(segment, 0.25, buildingId: building);

        Assert.ThrowsExactly<InvalidOperationException>(() => world.RemoveBuilding(building));
        Assert.IsTrue(world.RemoveRoadAccessPoint(buildingAccess));
        Assert.IsTrue(world.RemoveBuilding(building));

        var poi = world.CreatePoi(new WorldPoint(15, 5, 0), PoiKind.Service);
        var poiAccess = world.CreateRoadAccessPoint(segment, 0.75, poiId: poi, mode: RoadAccessMode.Foot);

        Assert.ThrowsExactly<InvalidOperationException>(() => world.RemovePoi(poi));
        Assert.IsTrue(world.RemoveRoadAccessPoint(poiAccess));
        Assert.IsTrue(world.RemovePoi(poi));
    }
    [TestMethod]
    public void CheckpointRoundTripPreservesTopologyAndNextIds()
    {
        var world = CreateFixture(); var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint()); var expected = world.CreateCheckpoint(); var actual = restored.CreateCheckpoint();
        Assert.AreEqual(expected.NextRoadNodeId, actual.NextRoadNodeId); Assert.AreEqual(expected.NextRoadSegmentId, actual.NextRoadSegmentId); Assert.AreEqual(expected.NextLaneId, actual.NextLaneId); Assert.AreEqual(expected.NextLaneConnectionId, actual.NextLaneConnectionId); Assert.AreEqual(expected.NextRoadAccessPointId, actual.NextRoadAccessPointId);
        CollectionAssert.AreEqual(expected.RoadNodes.ToArray(), actual.RoadNodes.ToArray()); CollectionAssert.AreEqual(expected.RoadSegments.ToArray(), actual.RoadSegments.ToArray()); CollectionAssert.AreEqual(expected.Lanes.ToArray(), actual.Lanes.ToArray()); CollectionAssert.AreEqual(expected.LaneConnections.ToArray(), actual.LaneConnections.ToArray()); CollectionAssert.AreEqual(expected.RoadAccessPoints.ToArray(), actual.RoadAccessPoints.ToArray());
        Assert.AreEqual(world.CreateRoadNode(new WorldPoint(500, 500, 0)), restored.CreateRoadNode(new WorldPoint(500, 500, 0)));
    }
    [TestMethod]
    public void SpatialQueryReturnsOnlyThreeDimensionalRoadVolume()
    {
        var world = new SimulationWorld(new SimulationConfig(spatialCellSize: 16)); var a0 = world.CreateRoadNode(new WorldPoint(-50, 0, 0)); var a1 = world.CreateRoadNode(new WorldPoint(50, 0, 0)); var b0 = world.CreateRoadNode(new WorldPoint(-50, 0, 100)); var b1 = world.CreateRoadNode(new WorldPoint(50, 0, 100)); var ground = world.CreateRoadSegment(a0, a1); var elevated = world.CreateRoadSegment(b0, b1); var low = world.CreateRoadNetworkSnapshot(new WorldVolume(-100, -10, -10, 100, 10, 10)); CollectionAssert.AreEqual(new[] { ground }, low.Segments.Select(static x => x.Id).ToArray()); Assert.IsFalse(low.Segments.Any(x => x.Id == elevated));
    }
    [TestMethod]
    public void SegmentUpdateRollsBackWhenExistingTurnWouldDangle()
    {
        var world = CreateFixture(); var before = world.CreateRoadNetworkSnapshot(); var third = world.CreateRoadNode(new WorldPoint(100, 100, -15), RoadNodeKind.Intersection); var second = before.Segments[1]; Assert.ThrowsExactly<InvalidOperationException>(() => world.UpdateRoadSegment(second.Id, third, second.EndNodeId, second.Kind)); var after = world.CreateRoadNetworkSnapshot(); Assert.AreEqual(second, after.Segments.Single(x => x.Id == second.Id));
    }
    private static SimulationWorld CreateFixture() { var world = new SimulationWorld(); var a = world.CreateRoadNode(new WorldPoint(0, 0, -15)); var j = world.CreateRoadNode(new WorldPoint(100, 0, -15), RoadNodeKind.Intersection); var b = world.CreateRoadNode(new WorldPoint(200, 0, -15)); var s1 = world.CreateRoadSegment(a, j, RoadKind.Arterial); var s2 = world.CreateRoadSegment(j, b, RoadKind.Arterial); var l1 = world.CreateLane(s1, LaneDirection.Forward, 0); var l2 = world.CreateLane(s2, LaneDirection.Forward, 0); world.CreateLaneConnection(l1, l2, j, TurnMovement.Straight); return world; }
}
