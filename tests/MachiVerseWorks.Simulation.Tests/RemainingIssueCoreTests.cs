using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RemainingIssueCoreTests
{
    [TestMethod]
    public void RestoreRejectsPedestrianWithMotorTravelMode()
    {
        var fixture = CreateWalkingPopulationFixture();
        fixture.World.Step();
        var checkpoint = fixture.World.CreateCheckpoint();
        var pedestrians = checkpoint.Pedestrians!.ToArray();
        pedestrians[0] = pedestrians[0] with { Mode = TravelMode.Motor };

        Assert.ThrowsExactly<ArgumentException>(() =>
            SimulationWorld.RestoreCheckpoint(checkpoint with { Pedestrians = pedestrians }));
    }

    [TestMethod]
    public void RestoreRejectsPersonResidenceThatDiffersFromHousehold()
    {
        var fixture = CreateWalkingPopulationFixture();
        var checkpoint = fixture.World.CreateCheckpoint();
        var persons = checkpoint.Persons!.ToArray();
        persons[0] = persons[0] with { Residence = TripEndpoint.ForBuilding(fixture.Work) };

        Assert.ThrowsExactly<ArgumentException>(() =>
            SimulationWorld.RestoreCheckpoint(checkpoint with { Persons = persons }));
    }

    [TestMethod]
    public void RestoreRejectsInvalidPopulationScheduleNeedsAndActiveTripShape()
    {
        var fixture = CreateWalkingPopulationFixture();
        fixture.World.Step();
        var checkpoint = fixture.World.CreateCheckpoint();
        var original = checkpoint.Persons![0];

        var invalidSchedule = original with
        {
            Schedule = [new DailyActivityWindow(ActivityKind.Work, 0, 0, TripEndpoint.ForBuilding(fixture.Work))],
        };
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SimulationWorld.RestoreCheckpoint(checkpoint with { Persons = [invalidSchedule] }));

        var invalidNeeds = original with
        {
            Needs = [new PersonNeed(NeedKind.Work, 0.5), new PersonNeed(NeedKind.Work, 0.4)],
        };
        Assert.ThrowsExactly<ArgumentException>(() =>
            SimulationWorld.RestoreCheckpoint(checkpoint with { Persons = [invalidNeeds] }));

        var invalidActiveTrip = original with { ActiveTripRequestId = null };
        Assert.ThrowsExactly<ArgumentException>(() =>
            SimulationWorld.RestoreCheckpoint(checkpoint with { Persons = [invalidActiveTrip] }));
    }

    [TestMethod]
    public void FailedPopulationDispatchDoesNotConsumeTripRequestId()
    {
        var world = new SimulationWorld();
        var home = world.CreateBuilding(new WorldVolume(0, 0, 0, 2, 2, 2), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(20, 0, 0, 22, 2, 2), BuildingKind.Commercial);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        world.CreatePerson(
            household,
            new PersonDemographics(30, IsEmployed: true),
            [new DailyActivityWindow(ActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(work), ActivityPriority.High)]);

        world.Step();
        world.Step();

        Assert.AreEqual(1UL, world.CreateCheckpoint().NextTripRequestId);
    }

    [TestMethod]
    public void PublicRemovalCannotStrandActivePopulationTrip()
    {
        var fixture = CreateWalkingPopulationFixture();
        fixture.World.Step();
        Assert.IsTrue(fixture.World.TryGetPersonSnapshot(fixture.Person, out var person));
        Assert.IsNotNull(person.PedestrianId);

        Assert.ThrowsExactly<InvalidOperationException>(() => fixture.World.RemovePedestrian(person.PedestrianId.Value));
        Assert.AreEqual(1, fixture.World.PedestrianCount);
    }

    [TestMethod]
    public void RoadTopologyMutationIsRejectedWhileVehicleRouteIsStored()
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(100, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateLane(segment, LaneDirection.Forward, 0);
        var route = world.FindRoadRoute(new RouteRequest(new WorldPoint(1, 0, 0), new WorldPoint(99, 0, 0)));
        world.CreateVehicle(route);

        Assert.ThrowsExactly<InvalidOperationException>(() => world.CreateRoadNode(new WorldPoint(200, 0, 0)));
        Assert.ThrowsExactly<InvalidOperationException>(() => world.UpdateLane(route.OriginLaneId, segment, LaneDirection.Forward, 0, 3.5, 10));
    }

    [TestMethod]
    public void ArrivedVehiclesRemainObservableButDoNotReserveLaneOccupancy()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateLane(segment, LaneDirection.Forward, 0);
        var route = world.FindRoadRoute(new RouteRequest(new WorldPoint(1, 0, 0), new WorldPoint(19, 0, 0)));
        var vehicle = world.CreateVehicle(route);
        VehicleSnapshot snapshot = default;
        for (var tick = 0; tick < 2_000; tick++)
        {
            world.Step();
            Assert.IsTrue(world.TryGetVehicleSnapshot(vehicle, out snapshot));
            if (snapshot.State == VehicleMovementState.Arrived) break;
        }
        Assert.AreEqual(VehicleMovementState.Arrived, snapshot.State);

        var checkpoint = world.CreateCheckpoint();
        var first = checkpoint.Vehicles![0];
        var duplicate = first with { Id = new VehicleId(2) };
        var restored = SimulationWorld.RestoreCheckpoint(checkpoint with
        {
            NextVehicleId = 3,
            Vehicles = [first, duplicate],
        });

        Assert.AreEqual(2, restored.VehicleCount);
        Assert.AreEqual(0, restored.ActiveVehicleCount);
    }

    [TestMethod]
    public void MotorDispatchTriesAllEndpointAccessCandidatesDeterministically()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var home = world.CreateBuilding(new WorldVolume(0, -2, 0, 2, 2, 3), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(998, -2, 0, 1000, 2, 3), BuildingKind.Commercial);

        var isolatedStart = world.CreateRoadNode(new WorldPoint(-100, 0, 0));
        var isolatedEnd = world.CreateRoadNode(new WorldPoint(-50, 0, 0));
        var isolated = world.CreateRoadSegment(isolatedStart, isolatedEnd);
        world.CreateLane(isolated, LaneDirection.Forward, 0);
        world.CreateRoadAccessPoint(isolated, 0.5, home, mode: RoadAccessMode.Motor);

        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(1000, 0, 0));
        var connected = world.CreateRoadSegment(start, end);
        world.CreateLane(connected, LaneDirection.Forward, 0);
        world.CreateRoadAccessPoint(connected, 0.01, home, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(connected, 0.99, work, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);

        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(
            household,
            new PersonDemographics(40, IsEmployed: true, HasPrivateVehicle: true),
            [new DailyActivityWindow(ActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(work), ActivityPriority.High)]);

        world.Step();

        Assert.IsTrue(world.TryGetPersonSnapshot(person, out var snapshot));
        Assert.AreEqual(PersonTravelState.Driving, snapshot.TravelState);
        Assert.IsNotNull(snapshot.VehicleId);
    }

    private static PopulationFixture CreateWalkingPopulationFixture()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var home = world.CreateBuilding(new WorldVolume(0, -2, 0, 2, 2, 3), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(38, -2, 0, 40, 2, 3), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(40, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateRoadAccessPoint(segment, 0.05, home, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.95, work, mode: RoadAccessMode.Foot);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(
            household,
            new PersonDemographics(30, IsEmployed: true),
            [new DailyActivityWindow(ActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(work), ActivityPriority.High)]);
        return new PopulationFixture(world, home, work, person);
    }

    private sealed record PopulationFixture(SimulationWorld World, BuildingId Home, BuildingId Work, PersonId Person);
}