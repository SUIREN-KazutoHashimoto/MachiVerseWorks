using System.Text.Json;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class PedestrianSaveTests
{
    [TestMethod]
    public void CurrentFormatRoundTripPreservesPedestrianRouteProgressAndNextId()
    {
        var world = CreateWorld(out var origin, out var destination);
        var first = world.CreatePedestrian(new TripRequest(new TripRequestId(10), TripEndpoint.ForBuilding(origin), TripEndpoint.ForBuilding(destination), TravelMode.Foot), 2.25d);
        for (var tick = 0; tick < 25; tick++) world.Step();

        var bytes = WorldSaveSerializer.Serialize(world);
        using var document = JsonDocument.Parse(bytes);
        var simulation = document.RootElement.GetProperty("simulation");
        Assert.AreEqual(SaveFormatVersion.Current, document.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.AreEqual(1, simulation.GetProperty("pedestrians").GetArrayLength());
        Assert.AreEqual(2UL, simulation.GetProperty("nextPedestrianId").GetUInt64());

        var restored = WorldSaveSerializer.Deserialize(bytes);
        Assert.IsTrue(world.TryGetPedestrianSnapshot(first, out var expected));
        Assert.IsTrue(restored.TryGetPedestrianSnapshot(first, out var actual));
        Assert.AreEqual(expected, actual);

        for (var tick = 0; tick < 20; tick++) { world.Step(); restored.Step(); }
        Assert.IsTrue(world.TryGetPedestrianSnapshot(first, out expected));
        Assert.IsTrue(restored.TryGetPedestrianSnapshot(first, out actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CurrentFormatRoundTripPreservesCrossingPermission()
    {
        var world = CreateCrossingWorld(out var origin, out var destination);
        var crossing = world.CreatePedestrianNetworkSnapshot().Crossings.Single();
        Assert.IsTrue(world.SetPedestrianCrossingOpen(crossing.Id, false));
        var pedestrian = world.CreatePedestrian(new TripRequest(new TripRequestId(20), TripEndpoint.ForBuilding(origin), TripEndpoint.ForBuilding(destination), TravelMode.Foot), 6d);

        for (var tick = 0; tick < 500; tick++)
        {
            world.Step();
            Assert.IsTrue(world.TryGetPedestrianSnapshot(pedestrian, out var snapshot));
            if (snapshot.State == PedestrianMovementState.WaitingForCrossing) break;
        }
        Assert.IsTrue(world.TryGetPedestrianSnapshot(pedestrian, out var waiting));
        Assert.AreEqual(PedestrianMovementState.WaitingForCrossing, waiting.State);

        var bytes = WorldSaveSerializer.Serialize(world);
        using var document = JsonDocument.Parse(bytes);
        var savedCrossings = document.RootElement.GetProperty("simulation").GetProperty("pedestrianCrossings");
        Assert.AreEqual(1, savedCrossings.GetArrayLength());
        Assert.IsFalse(savedCrossings[0].GetProperty("isOpen").GetBoolean());

        var restored = WorldSaveSerializer.Deserialize(bytes);
        var restoredCrossing = restored.CreatePedestrianNetworkSnapshot().Crossings.Single(item => item.Id == crossing.Id);
        Assert.IsFalse(restoredCrossing.IsOpen);
        restored.Step();
        Assert.IsTrue(restored.TryGetPedestrianSnapshot(pedestrian, out var restoredWaiting));
        Assert.AreEqual(PedestrianMovementState.WaitingForCrossing, restoredWaiting.State);
        Assert.AreEqual(waiting.Position, restoredWaiting.Position);
    }

    [TestMethod]
    public void PedestrianCountLimitIsAppliedBeforeMaterialization()
    {
        var json = """{"formatVersion":5,"simulation":{"tickRate":30,"seed":1,"spatialCellSize":64,"tickCount":0,"elapsedTicks":0,"randomState":1,"nextAgentId":1,"agents":[],"nextBuildingId":1,"buildings":[],"nextPoiId":1,"pois":[],"nextRoadNodeId":1,"roadNodes":[],"nextRoadSegmentId":1,"roadSegments":[],"nextLaneId":1,"lanes":[],"nextLaneConnectionId":1,"laneConnections":[],"nextRoadAccessPointId":1,"roadAccessPoints":[],"nextPedestrianId":3,"pedestrians":[{},{}]}}"""u8.ToArray();
        var limits = new WorldSaveLimits(maximumBytes: json.Length, maximumPedestrianCount: 1);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json, limits));
        StringAssert.Contains(exception.Message, "Pedestrian count");
        StringAssert.Contains(exception.Message, "before deserialization");
    }

    [TestMethod]
    public void PedestrianCrossingCountLimitIsAppliedBeforeMaterialization()
    {
        var json = """{"formatVersion":5,"simulation":{"tickRate":30,"seed":1,"spatialCellSize":64,"tickCount":0,"elapsedTicks":0,"randomState":1,"nextAgentId":1,"agents":[],"nextBuildingId":1,"buildings":[],"nextPoiId":1,"pois":[],"nextRoadNodeId":1,"roadNodes":[],"nextRoadSegmentId":1,"roadSegments":[],"nextLaneId":1,"lanes":[],"nextLaneConnectionId":1,"laneConnections":[],"nextRoadAccessPointId":1,"roadAccessPoints":[],"nextPedestrianId":1,"pedestrians":[],"pedestrianCrossings":[{},{}]}}"""u8.ToArray();
        var limits = new WorldSaveLimits(maximumBytes: json.Length, maximumPedestrianCrossingCount: 1);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json, limits));
        StringAssert.Contains(exception.Message, "PedestrianCrossing count");
        StringAssert.Contains(exception.Message, "before deserialization");
    }

    private static SimulationWorld CreateWorld(out BuildingId origin, out BuildingId destination)
    {
        var world = new SimulationWorld();
        origin = world.CreateBuilding(new WorldVolume(-11, -1, 0, -9, 1, 2));
        destination = world.CreateBuilding(new WorldVolume(9, -1, 0, 11, 1, 2));
        var start = world.CreateRoadNode(new WorldPoint(-20, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateRoadAccessPoint(segment, 0.25, origin, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.75, destination, mode: RoadAccessMode.Foot);
        return world;
    }

    private static SimulationWorld CreateCrossingWorld(out BuildingId origin, out BuildingId destination)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        origin = world.CreateBuilding(new WorldVolume(-16, -2, 0, -14, 2, 3));
        destination = world.CreateBuilding(new WorldVolume(14, -2, 0, 16, 2, 3));
        var start = world.CreateRoadNode(new WorldPoint(-20, 0, 0));
        var intersection = world.CreateRoadNode(new WorldPoint(0, 0, 0), RoadNodeKind.Intersection);
        var end = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var firstSegment = world.CreateRoadSegment(start, intersection);
        var secondSegment = world.CreateRoadSegment(intersection, end);
        world.CreateRoadAccessPoint(firstSegment, 0.25, origin, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(secondSegment, 0.75, destination, mode: RoadAccessMode.Foot);
        return world;
    }
}
