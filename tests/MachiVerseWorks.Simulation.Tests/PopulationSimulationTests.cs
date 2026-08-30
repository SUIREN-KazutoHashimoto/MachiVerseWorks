using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class PopulationSimulationTests
{
    [TestMethod]
    public void PopulationTripRequestStartsWalkingPedestrianAndCompletesActivity()
    {
        var fixture = CreateWalkingFixture();
        var household = fixture.World.CreateHousehold(TripEndpoint.ForBuilding(fixture.Home));
        var person = fixture.World.CreatePerson(
            household,
            new PersonDemographics(35, IsEmployed: true),
            [new DailyActivityWindow(ActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(fixture.Work), ActivityPriority.High)]);

        fixture.World.Step();

        Assert.IsTrue(fixture.World.TryGetPersonSnapshot(person, out var travelling));
        Assert.AreEqual(PersonTravelState.Walking, travelling.TravelState);
        Assert.AreEqual(TravelMode.Foot, travelling.ActiveTravelMode);
        Assert.IsNotNull(travelling.ActiveTripRequestId);
        Assert.IsNotNull(travelling.PedestrianId);
        Assert.AreEqual(1, fixture.World.ActivePedestrianCount);

        for (var tick = 0; tick < 2_000 && travelling.TravelState != PersonTravelState.AtActivity; tick++)
        {
            fixture.World.Step();
            Assert.IsTrue(fixture.World.TryGetPersonSnapshot(person, out travelling));
        }

        Assert.AreEqual(PersonTravelState.AtActivity, travelling.TravelState);
        Assert.AreEqual(ActivityKind.Work, travelling.CurrentActivity);
        Assert.AreEqual(TripEndpoint.ForBuilding(fixture.Work), travelling.CurrentLocation);
        Assert.AreEqual(0, fixture.World.ActivePedestrianCount);
    }

    [TestMethod]
    public void PrivateVehicleEligiblePersonDispatchesTripToRoadTraffic()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var home = world.CreateBuilding(new WorldVolume(0, -3, 0, 4, 3, 4), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(996, -3, 0, 1000, 3, 4), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(1000, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateLane(segment, LaneDirection.Forward, 0);
        world.CreateRoadAccessPoint(segment, 0.01, home, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.99, work, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(
            household,
            new PersonDemographics(40, IsEmployed: true, HasPrivateVehicle: true),
            [new DailyActivityWindow(ActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(work), ActivityPriority.High)]);

        world.Step();

        Assert.IsTrue(world.TryGetPersonSnapshot(person, out var snapshot));
        Assert.AreEqual(PersonTravelState.Driving, snapshot.TravelState);
        Assert.AreEqual(TravelMode.Motor, snapshot.ActiveTravelMode);
        Assert.IsNotNull(snapshot.VehicleId);
        Assert.IsNull(snapshot.PedestrianId);
        Assert.AreEqual(1, world.ActiveVehicleCount);
    }

    [TestMethod]
    public void CheckpointPreservesHouseholdsScheduleNeedsAndActivePopulationTrip()
    {
        var fixture = CreateWalkingFixture(distanceMeters: 1000d);
        var household = fixture.World.CreateHousehold(TripEndpoint.ForBuilding(fixture.Home));
        var schedule = new[]
        {
            new DailyActivityWindow(ActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(fixture.Work), ActivityPriority.Critical),
        };
        var needs = new[] { new PersonNeed(NeedKind.Work, 0.25d, 0.04d) };
        var person = fixture.World.CreatePerson(household, new PersonDemographics(28, IsEmployed: true), schedule, needs);
        fixture.World.Step();

        var restored = SimulationWorld.RestoreCheckpoint(fixture.World.CreateCheckpoint());

        Assert.AreEqual(1, restored.HouseholdCount);
        Assert.AreEqual(1, restored.PersonCount);
        Assert.IsTrue(restored.TryGetPersonDebugSnapshot(person, out var debug));
        Assert.IsNotNull(debug);
        Assert.AreEqual(PersonTravelState.Walking, debug.Person.TravelState);
        Assert.AreEqual(schedule[0], debug.Schedule[0]);
        Assert.AreEqual(NeedKind.Work, debug.Needs[0].Kind);
        Assert.IsNotNull(debug.Person.ActiveTripRequestId);

        fixture.World.Step();
        restored.Step();
        Assert.IsTrue(fixture.World.TryGetPersonSnapshot(person, out var expected));
        Assert.IsTrue(restored.TryGetPersonSnapshot(person, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DailyScheduleGeneratesAndCompletesMultipleTripsDeterministically()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1));
        var home = world.CreateBuilding(new WorldVolume(0, -2, 0, 2, 2, 3), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(18, -2, 0, 22, 2, 3), BuildingKind.Commercial);
        var recreation = world.CreateBuilding(new WorldVolume(38, -2, 0, 42, 2, 3), BuildingKind.Civic);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(50, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateRoadAccessPoint(segment, 0.02, home, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.40, work, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.80, recreation, mode: RoadAccessMode.Foot);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(
            household,
            new PersonDemographics(32, IsEmployed: true),
            [
                new DailyActivityWindow(ActivityKind.Home, 0, 60),
                new DailyActivityWindow(ActivityKind.Work, 60, 120, TripEndpoint.ForBuilding(work), ActivityPriority.High),
                new DailyActivityWindow(ActivityKind.Recreation, 120, 180, TripEndpoint.ForBuilding(recreation)),
                new DailyActivityWindow(ActivityKind.Home, 180, 1440),
            ]);

        var completedLocationChanges = 0;
        var previousLocation = TripEndpoint.ForBuilding(home);
        for (var second = 0; second < 86_400; second++)
        {
            world.Step();
            Assert.IsTrue(world.TryGetPersonSnapshot(person, out var snapshot));
            if (snapshot.TravelState == PersonTravelState.AtActivity && snapshot.CurrentLocation != previousLocation)
            {
                completedLocationChanges++;
                previousLocation = snapshot.CurrentLocation;
            }
        }

        Assert.IsTrue(completedLocationChanges >= 3, $"Expected at least three completed daily trips, observed {completedLocationChanges}.");
        Assert.IsTrue(world.TryGetPersonSnapshot(person, out var final));
        Assert.AreEqual(ActivityKind.Home, final.CurrentActivity);
        Assert.AreEqual(TripEndpoint.ForBuilding(home), final.CurrentLocation);
    }

    private static WalkingFixture CreateWalkingFixture(double distanceMeters = 40d)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var home = world.CreateBuilding(new WorldVolume(0, -2, 0, 2, 2, 3), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(distanceMeters - 2, -2, 0, distanceMeters, 2, 3), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(distanceMeters, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateRoadAccessPoint(segment, 0.05, home, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.95, work, mode: RoadAccessMode.Foot);
        return new WalkingFixture(world, home, work);
    }

    private sealed record WalkingFixture(SimulationWorld World, BuildingId Home, BuildingId Work);
}
