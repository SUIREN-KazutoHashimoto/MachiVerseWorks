using System.Text;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class NestedSaveLimitTests
{
    [TestMethod]
    public void VehicleRouteStepsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        var limits = new WorldSaveLimits(maximumBytes: 100_000, maximumVehicleRouteStepCount: 1);
        AssertNestedBoundary(
            CreateSimulationJson("\"vehicles\":[{\"routeSteps\":[{}]}]"),
            CreateSimulationJson("\"vehicles\":[{\"routeSteps\":[{},{}]}]"),
            limits,
            "simulation.vehicles[].routeSteps");
    }

    [TestMethod]
    public void PersonScheduleAndNeedsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        var scheduleLimits = new WorldSaveLimits(maximumBytes: 100_000, maximumPersonScheduleEntryCount: 1);
        AssertNestedBoundary(
            CreateSimulationJson("\"persons\":[{\"schedule\":[{}]}]"),
            CreateSimulationJson("\"persons\":[{\"schedule\":[{},{}]}]"),
            scheduleLimits,
            "simulation.persons[].schedule");

        var needsAtLimit = string.Join(',', Enumerable.Repeat("{}", WorldSaveLimits.Default.MaximumPersonNeedCount));
        var needsAboveLimit = string.Join(',', Enumerable.Repeat("{}", WorldSaveLimits.Default.MaximumPersonNeedCount + 1));
        AssertNestedBoundary(
            CreateSimulationJson($"\"persons\":[{{\"needs\":[{needsAtLimit}]}}]"),
            CreateSimulationJson($"\"persons\":[{{\"needs\":[{needsAboveLimit}]}}]"),
            new WorldSaveLimits(maximumBytes: 100_000),
            "simulation.persons[].needs");
    }

    [TestMethod]
    public void RailwayInfrastructureMembershipIsRejectedBeforeDtoMaterializationAboveLimit()
    {
        var blockLimits = new WorldSaveLimits(maximumBytes: 100_000, maximumBlockSectionSegmentCount: 1);
        AssertNestedBoundary(
            CreateSimulationJson("\"blockSections\":[{\"segmentIds\":[1]}]"),
            CreateSimulationJson("\"blockSections\":[{\"segmentIds\":[1,2]}]"),
            blockLimits,
            "simulation.blockSections[].segmentIds");

        var depotLimits = new WorldSaveLimits(maximumBytes: 100_000, maximumDepotTrackSegmentCount: 1);
        AssertNestedBoundary(
            CreateSimulationJson("\"depots\":[{\"trackSegmentIds\":[1]}]"),
            CreateSimulationJson("\"depots\":[{\"trackSegmentIds\":[1,2]}]"),
            depotLimits,
            "simulation.depots[].trackSegmentIds");
    }

    [TestMethod]
    public void RailwayOperationsNestedCollectionsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        var routeLimits = new WorldSaveLimits(maximumBytes: 100_000, maximumRailwayRouteSegmentCount: 1);
        AssertNestedBoundary(
            CreateSimulationJson("\"railwayOperations\":{\"routes\":[{\"trackSegmentIds\":[1]}]}"),
            CreateSimulationJson("\"railwayOperations\":{\"routes\":[{\"trackSegmentIds\":[1,2]}]}"),
            routeLimits,
            "simulation.railwayOperations.routes[].trackSegmentIds");

        var stopLimits = new WorldSaveLimits(maximumBytes: 100_000, maximumTimetableStopCount: 1);
        AssertNestedBoundary(
            CreateSimulationJson("\"railwayOperations\":{\"timetables\":[{\"stops\":[{}]}]}"),
            CreateSimulationJson("\"railwayOperations\":{\"timetables\":[{\"stops\":[{},{}]}]}"),
            stopLimits,
            "simulation.railwayOperations.timetables[].stops");
    }

    [TestMethod]
    public void TimetableStopTotalIsRejectedBeforeDtoMaterialization()
    {
        var limits = new WorldSaveLimits(
            maximumBytes: 100_000,
            maximumTimetableStopCount: 1,
            maximumTimetableStopTotalCount: 1);
        var json = CreateSimulationJson("\"railwayOperations\":{\"timetables\":[{\"stops\":[{}]},{\"stops\":[{}]}]}");

        var exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(json), limits));

        StringAssert.Contains(exception.Message, "total RailwayOperations Timetable stop count");
        StringAssert.Contains(exception.Message, "before deserialization");
    }

    [TestMethod]
    public void OpticalCollectionsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        AssertNestedBoundary(
            CreateSimulationJson("\"economy\":{\"optical\":{\"nodes\":[{}]}}"),
            CreateSimulationJson("\"economy\":{\"optical\":{\"nodes\":[{},{}]}}"),
            new WorldSaveLimits(maximumBytes: 100_000, maximumRoadNodeCount: 1),
            "simulation.economy.optical.nodes");
        AssertNestedBoundary(
            CreateSimulationJson("\"economy\":{\"optical\":{\"fiberCables\":[{}]}}"),
            CreateSimulationJson("\"economy\":{\"optical\":{\"fiberCables\":[{},{}]}}"),
            new WorldSaveLimits(maximumBytes: 100_000, maximumRoadSegmentCount: 1),
            "simulation.economy.optical.fiberCables");
        foreach (var property in new[] { "equipment", "backhauls", "demands" })
        {
            AssertNestedBoundary(
                CreateSimulationJson($"\"economy\":{{\"optical\":{{\"{property}\":[{{}}]}}}}"),
                CreateSimulationJson($"\"economy\":{{\"optical\":{{\"{property}\":[{{}},{{}}]}}}}"),
                new WorldSaveLimits(maximumBytes: 100_000, maximumBuildingCount: 1),
                $"simulation.economy.optical.{property}");
        }
    }

    [TestMethod]
    public void EconomyCoreCollectionsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        foreach (var property in new[] { "companies", "establishments" })
        {
            AssertNestedBoundary(
                CreateSimulationJson($"\"economy\":{{\"{property}\":[{{}}]}}"),
                CreateSimulationJson($"\"economy\":{{\"{property}\":[{{}},{{}}]}}"),
                new WorldSaveLimits(maximumBytes: 100_000, maximumBuildingCount: 1),
                $"simulation.economy.{property}");
        }
        foreach (var property in new[] { "jobs", "employments" })
        {
            AssertNestedBoundary(
                CreateSimulationJson($"\"economy\":{{\"{property}\":[{{}}]}}"),
                CreateSimulationJson($"\"economy\":{{\"{property}\":[{{}},{{}}]}}"),
                new WorldSaveLimits(maximumBytes: 100_000, maximumPersonCount: 1),
                $"simulation.economy.{property}");
        }
    }

    [TestMethod]
    public void SerializeAppliesVehicleAndPersonNestedLimitsBeforeDtoProjection()
    {
        var vehicleWorld = CreateTwoStepVehicleWorld();
        var vehicleException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(vehicleWorld, new WorldSaveLimits(maximumVehicleRouteStepCount: 1)));
        StringAssert.Contains(vehicleException.Message, "VehicleRouteSteps");

        var populationWorld = new SimulationWorld();
        var home = populationWorld.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Residential);
        var household = populationWorld.CreateHousehold(TripEndpoint.ForBuilding(home));
        populationWorld.CreatePerson(
            household,
            new PersonDemographics(30),
            [
                new DailyActivityWindow(ActivityKind.Home, 0, 720),
                new DailyActivityWindow(ActivityKind.Home, 720, 1440),
            ]);
        var scheduleException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(populationWorld, new WorldSaveLimits(maximumPersonScheduleEntryCount: 1)));
        StringAssert.Contains(scheduleException.Message, "PersonScheduleEntries");
    }

    [TestMethod]
    public void SerializeAppliesRailwayInfrastructureMembershipLimitsBeforeDtoProjection()
    {
        var world = CreateTwoSegmentRailwayWorld();

        var blockException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(
                world,
                new WorldSaveLimits(maximumBlockSectionSegmentCount: 1, maximumDepotTrackSegmentCount: 2)));
        StringAssert.Contains(blockException.Message, "BlockSectionSegmentIds");

        var depotException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(
                world,
                new WorldSaveLimits(maximumBlockSectionSegmentCount: 2, maximumDepotTrackSegmentCount: 1)));
        StringAssert.Contains(depotException.Message, "DepotTrackSegmentIds");
    }

    [TestMethod]
    public void SerializeAppliesRailwayOperationsRouteAndTimetableLimits()
    {
        var world = new SimulationWorld(new SimulationConfig(seed: 0x1809UL));
        RailwayOperationsFixtures.SeedDeterministic(world);
        var checkpoint = world.CreateCheckpoint();
        var routes = checkpoint.RailwayRoutes ?? throw new AssertFailedException("Railway Operations fixture did not create routes.");
        var timetables = checkpoint.Timetables ?? throw new AssertFailedException("Railway Operations fixture did not create timetables.");
        var maximumRouteSegments = routes.Max(static route => route.TrackSegmentIds.Count);
        var maximumStops = timetables.Max(static timetable => timetable.Stops.Count);
        var totalStops = timetables.Sum(static timetable => timetable.Stops.Count);
        Assert.IsTrue(maximumRouteSegments > 1);
        Assert.IsTrue(maximumStops > 1);
        Assert.IsTrue(totalStops > 1);

        var routeException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(
                world,
                new WorldSaveLimits(maximumRailwayRouteSegmentCount: maximumRouteSegments - 1)));
        StringAssert.Contains(routeException.Message, "RailwayRouteTrackSegmentIds");

        var stopException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(
                world,
                new WorldSaveLimits(maximumTimetableStopCount: maximumStops - 1)));
        StringAssert.Contains(stopException.Message, "TimetableStopsPerTimetable");

        var totalException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(
                world,
                new WorldSaveLimits(maximumTimetableStopTotalCount: totalStops - 1)));
        StringAssert.Contains(totalException.Message, "TimetableStops");
    }

    private static void AssertNestedBoundary(string atLimitJson, string aboveLimitJson, WorldSaveLimits limits, string path)
    {
        var atLimitException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(atLimitJson), limits));
        Assert.IsFalse(
            atLimitException.Message.Contains(path, StringComparison.Ordinal),
            $"The configured boundary itself must not be rejected by the nested scanner: {atLimitException.Message}");

        var aboveLimitException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(aboveLimitJson), limits));
        StringAssert.Contains(aboveLimitException.Message, path);
        StringAssert.Contains(aboveLimitException.Message, "before deserialization");
    }

    private static string CreateSimulationJson(string simulationProperties) => $$"""
        {
          "formatVersion": 10,
          "simulation": {
            {{simulationProperties}}
          }
        }
        """;

    private static SimulationWorld CreateTwoStepVehicleWorld()
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var junction = world.CreateRoadNode(new WorldPoint(10, 0, 0), RoadNodeKind.Intersection);
        var end = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var firstSegment = world.CreateRoadSegment(start, junction);
        var secondSegment = world.CreateRoadSegment(junction, end);
        var firstLane = world.CreateLane(firstSegment, LaneDirection.Forward, 0);
        var secondLane = world.CreateLane(secondSegment, LaneDirection.Forward, 0);
        world.CreateLaneConnection(firstLane, secondLane, junction);
        var route = world.FindRoadRoute(new RouteRequest(new WorldPoint(1, 0, 0), new WorldPoint(19, 0, 0)));
        Assert.AreEqual(2, route.Steps.Count);
        world.CreateVehicle(route);
        return world;
    }

    private static SimulationWorld CreateTwoSegmentRailwayWorld()
    {
        var world = new SimulationWorld();
        var start = world.CreateTrackNode(new WorldPoint(0, 0, 0));
        var junction = world.CreateTrackNode(new WorldPoint(100, 0, 0), TrackNodeKind.Junction);
        var end = world.CreateTrackNode(new WorldPoint(200, 0, 0));
        var first = world.CreateTrackSegment(start, junction, usage: TrackUsage.Depot);
        var second = world.CreateTrackSegment(junction, end, usage: TrackUsage.Depot);
        world.CreateTrackConnection(first, second, junction);
        world.CreateBlockSection([first, second]);
        world.CreateDepot(new WorldVolume(0, -10, -5, 200, 10, 5), [first, second]);
        return world;
    }
}
