using System.Text.Json;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class PopulationSaveTests
{
    [TestMethod]
    public void CurrentFormatRoundTripPreservesPopulationScheduleNeedsAndActiveTrip()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var home = world.CreateBuilding(new WorldVolume(0, -2, 0, 2, 2, 3), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(998, -2, 0, 1000, 2, 3), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(1000, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateRoadAccessPoint(segment, 0.01, home, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.99, work, mode: RoadAccessMode.Foot);

        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var schedule = new[]
        {
            new DailyActivityWindow(ActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(work), ActivityPriority.Critical),
        };
        var needs = new[]
        {
            new PersonNeed(NeedKind.Work, 0.25d, 0.04d),
            new PersonNeed(NeedKind.Rest, 0.75d, 0.02d),
        };
        var person = world.CreatePerson(household, new PersonDemographics(28, IsEmployed: true), schedule, needs);
        world.Step();

        Assert.IsTrue(world.TryGetPersonSnapshot(person, out var travelling));
        Assert.AreEqual(PersonTravelState.Walking, travelling.TravelState);
        Assert.IsNotNull(travelling.ActiveTripRequestId);
        Assert.IsNotNull(travelling.PedestrianId);

        var bytes = WorldSaveSerializer.Serialize(world);
        using var document = JsonDocument.Parse(bytes);
        Assert.AreEqual(SaveFormatVersion.Population, document.RootElement.GetProperty("formatVersion").GetInt32());
        var simulation = document.RootElement.GetProperty("simulation");
        Assert.AreEqual(1, simulation.GetProperty("households").GetArrayLength());
        Assert.AreEqual(1, simulation.GetProperty("persons").GetArrayLength());

        var restored = WorldSaveSerializer.Deserialize(bytes);
        Assert.AreEqual(1, restored.HouseholdCount);
        Assert.AreEqual(1, restored.PersonCount);
        Assert.IsTrue(restored.TryGetPersonDebugSnapshot(person, out var debug));
        Assert.IsNotNull(debug);
        Assert.AreEqual(schedule[0], debug.Schedule[0]);
        CollectionAssert.AreEqual(needs, debug.Needs.ToArray());
        Assert.AreEqual(PersonTravelState.Walking, debug.Person.TravelState);
        Assert.AreEqual(travelling.ActiveTripRequestId, debug.Person.ActiveTripRequestId);
        Assert.AreEqual(travelling.PedestrianId, debug.Person.PedestrianId);

        world.Step();
        restored.Step();
        Assert.IsTrue(world.TryGetPersonSnapshot(person, out var expected));
        Assert.IsTrue(restored.TryGetPersonSnapshot(person, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void VehicleFormatMigratesWithEmptyPopulation()
    {
        var world = new SimulationWorld();
        var bytes = WorldSaveSerializer.Serialize(world);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var simulationJson = root.GetProperty("simulation").GetRawText();
        var legacy = System.Text.Encoding.UTF8.GetBytes($"{{\"formatVersion\":6,\"simulation\":{simulationJson}}}");

        var restored = WorldSaveSerializer.Deserialize(legacy);

        Assert.AreEqual(0, restored.HouseholdCount);
        Assert.AreEqual(0, restored.PersonCount);
    }
}
