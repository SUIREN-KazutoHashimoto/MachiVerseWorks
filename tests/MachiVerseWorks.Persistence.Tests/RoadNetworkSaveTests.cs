using System.Text.Json;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class RoadNetworkSaveTests
{
    [TestMethod]
    public void FormatFourRoundTripPreservesRoadTopologyAndAccessReferences()
    {
        var world = new SimulationWorld();
        var building = world.CreateBuilding(new WorldVolume(20, 10, 0, 40, 30, 20));
        var poi = world.CreatePoi(new WorldPoint(30, 20, 0), PoiKind.Service, building);
        var a = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var j = world.CreateRoadNode(new WorldPoint(50, 0, 0), RoadNodeKind.Intersection);
        var b = world.CreateRoadNode(new WorldPoint(100, 0, 15));
        var first = world.CreateRoadSegment(a, j, RoadKind.Collector);
        var second = world.CreateRoadSegment(j, b, RoadKind.Arterial);
        var lane1 = world.CreateLane(first, LaneDirection.Forward, 0, 3.5, 15);
        var lane2 = world.CreateLane(second, LaneDirection.Forward, 0, 3.5, 20);
        world.CreateLaneConnection(lane1, lane2, j, TurnMovement.Straight);
        world.CreateRoadAccessPoint(first, 0.6, building, poi, RoadAccessMode.Motor | RoadAccessMode.Foot);

        var bytes = WorldSaveSerializer.Serialize(world);
        using var json = JsonDocument.Parse(bytes);
        Assert.AreEqual(SaveFormatVersion.RoadNetwork, json.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.AreEqual(3, json.RootElement.GetProperty("simulation").GetProperty("roadNodes").GetArrayLength());
        var restored = WorldSaveSerializer.Deserialize(bytes);
        var expected = world.CreateRoadNetworkSnapshot(); var actual = restored.CreateRoadNetworkSnapshot();
        CollectionAssert.AreEqual(expected.Nodes.ToArray(), actual.Nodes.ToArray());
        CollectionAssert.AreEqual(expected.Segments.ToArray(), actual.Segments.ToArray());
        CollectionAssert.AreEqual(expected.Lanes.ToArray(), actual.Lanes.ToArray());
        CollectionAssert.AreEqual(expected.Connections.ToArray(), actual.Connections.ToArray());
        CollectionAssert.AreEqual(expected.AccessPoints.ToArray(), actual.AccessPoints.ToArray());
    }

    [TestMethod]
    public void FormatThreeMigratesToEmptyRoadNetwork()
    {
        var legacy = """{"formatVersion":3,"simulation":{"tickRate":30,"seed":1,"spatialCellSize":64,"tickCount":0,"elapsedTicks":0,"randomState":1,"nextAgentId":1,"agents":[],"nextBuildingId":1,"buildings":[],"nextPoiId":1,"pois":[]}}"""u8.ToArray();
        var restored = WorldSaveSerializer.Deserialize(legacy);
        Assert.AreEqual(0, restored.RoadNodeCount); Assert.AreEqual(0, restored.RoadSegmentCount);
        Assert.AreEqual(1UL, restored.CreateRoadNode(new WorldPoint(0, 0, 0)).Value);
    }

    [TestMethod]
    public void DanglingRoadReferenceInFormatFourIsRejected()
    {
        var invalid = """{"formatVersion":4,"simulation":{"tickRate":30,"seed":1,"spatialCellSize":64,"tickCount":0,"elapsedTicks":0,"randomState":1,"nextAgentId":1,"agents":[],"nextBuildingId":1,"buildings":[],"nextPoiId":1,"pois":[],"nextRoadNodeId":1,"roadNodes":[],"nextRoadSegmentId":2,"roadSegments":[{"id":1,"kind":0,"startNodeId":10,"endNodeId":11}],"nextLaneId":1,"lanes":[],"nextLaneConnectionId":1,"laneConnections":[],"nextRoadAccessPointId":1,"roadAccessPoints":[]}}"""u8.ToArray();
        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(invalid));
    }
}
