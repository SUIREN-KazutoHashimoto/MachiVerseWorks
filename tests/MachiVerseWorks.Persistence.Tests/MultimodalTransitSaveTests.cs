using System.Text;
using System.Text.Json;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class MultimodalTransitSaveTests
{
    [TestMethod]
    public void SaveV10DuringTransferRestoresAndContinuesDeterministically()
    {
        var original = CreateTransferWorld(out var passengerId);
        for (var tick = 0; tick < 1000; tick++)
        {
            original.Step();
            if (GetPassenger(original, passengerId).State == PassengerState.Transfer) break;
        }
        Assert.AreEqual(PassengerState.Transfer, GetPassenger(original, passengerId).State);

        var bytes = WorldSaveSerializer.Serialize(original);
        StringAssert.Contains(Encoding.UTF8.GetString(bytes), "\"formatVersion\": 12");
        StringAssert.Contains(Encoding.UTF8.GetString(bytes), "\"multimodalTransit\"");
        var restored = WorldSaveSerializer.Deserialize(bytes);
        Assert.AreEqual(GetPassenger(original, passengerId), GetPassenger(restored, passengerId));

        for (var tick = 0; tick < 500; tick++) { original.Step(); restored.Step(); }
        Assert.AreEqual(GetPassenger(original, passengerId), GetPassenger(restored, passengerId));
        Assert.AreEqual(PassengerState.Arrived, GetPassenger(restored, passengerId).State);
    }

    [TestMethod]
    public void RailwayOperationsV9MigratesWithEmptyMultimodalTransit()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        var currentJson = WorldSaveSerializer.Serialize(world);
        using var document = JsonDocument.Parse(currentJson);
        var simulation = document.RootElement.GetProperty("simulation");
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 9);
            writer.WritePropertyName("simulation");
            writer.WriteStartObject();
            foreach (var property in simulation.EnumerateObject())
            {
                if (property.NameEquals("multimodalTransit")) continue;
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var restored = WorldSaveSerializer.Deserialize(output.ToArray());
        var transit = restored.CreateMultimodalTransitSnapshot();
        Assert.AreEqual(0, transit.Stops.Length);
        Assert.AreEqual(0, transit.Lines.Length);
        Assert.AreEqual(0, transit.Journeys.Length);
        Assert.AreEqual(2, restored.CreateRailwayOperationsSnapshot().Services.Length);
    }

    private static SimulationWorld CreateTransferWorld(out PassengerId passengerId)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(100, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        var lane = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        var origin = world.CreateBuilding(new WorldVolume(0, 5, 0, 5, 10, 5));
        var destination = world.CreateBuilding(new WorldVolume(95, 5, 0, 100, 10, 5));
        world.CreateRoadAccessPoint(segment, 0.05d, buildingId: origin, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        world.CreateRoadAccessPoint(segment, 0.95d, buildingId: destination, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        var a = world.CreateBusStop(lane, new WorldPoint(15, 0, 0));
        var b = world.CreateBusStop(lane, new WorldPoint(40, 0, 0));
        var c = world.CreateBusStop(lane, new WorldPoint(60, 0, 0));
        var d = world.CreateBusStop(lane, new WorldPoint(85, 0, 0));
        var firstLine = world.CreateTransitLine(TransitMode.Bus);
        var secondLine = world.CreateTransitLine(TransitMode.Bus);
        world.CreateTransitServicePattern(firstLine, [new(a, 0, 1), new(b, 5, 1)]);
        world.CreateTransitServicePattern(secondLine, [new(c, 0, 1), new(d, 5, 1)]);
        var tripRequest = new TripRequest(new TripRequestId(1), TripEndpoint.ForBuilding(origin), TripEndpoint.ForBuilding(destination));
        var journeyId = world.PlanMultimodalJourney(tripRequest);
        passengerId = world.CreatePassenger(tripRequest.Id, journeyId);
        return world;
    }

    private static PassengerSnapshot GetPassenger(SimulationWorld world, PassengerId id) =>
        world.CreateMultimodalTransitSnapshot().Passengers.Single(item => item.Id == id);
}
