using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class MultimodalTransitTests
{
    [TestMethod]
    public void BusPatternUsesRoadTrafficAndCompletesWithDwell()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        var a = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var b = world.CreateRoadNode(new WorldPoint(50, 0, 0));
        var segment = world.CreateRoadSegment(a, b);
        var lane = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        var first = world.CreateBusStop(lane, new WorldPoint(1, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(49, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        var pattern = world.CreateTransitServicePattern(line, [new(first, 0, 1), new(second, 50, 2)]);
        var trip = world.CreateTransitTrip(pattern, 0);
        var bus = world.CreateBusTransitVehicle(trip);

        for (var index = 0; index < 200; index++) world.Step();

        var snapshot = world.CreateMultimodalTransitSnapshot();
        var vehicle = snapshot.Vehicles.Single(item => item.Id == bus);
        Assert.AreEqual(TransitVehicleMovementState.Completed, vehicle.State);
        Assert.IsTrue(snapshot.Vehicles.Any(item => item.RoadVehicleId is null));
    }

    [TestMethod]
    public void TaxiDispatchUsesNearestIdleVehicleAndCompletesRequest()
    {
        var world = CreateRoadWorld();
        var near = world.CreateTaxiVehicle(new WorldPoint(1, 0, 0));
        _ = world.CreateTaxiVehicle(new WorldPoint(90, 0, 0));
        var request = world.CreateTaxiRequest(new TripRequestId(1), new WorldPoint(10, 0, 0), new WorldPoint(80, 0, 0));
        world.DispatchTaxiRequests();
        var assigned = world.CreateMultimodalTransitSnapshot().TaxiRequests.Single(item => item.Id == request);
        Assert.AreEqual(near, assigned.AssignedVehicleId);

        for (var index = 0; index < 500 && world.CreateMultimodalTransitSnapshot().TaxiRequests.Single(item => item.Id == request).State != TaxiRequestState.Completed; index++) world.Step();
        Assert.AreEqual(TaxiRequestState.Completed, world.CreateMultimodalTransitSnapshot().TaxiRequests.Single(item => item.Id == request).State);
    }

    [TestMethod]
    public void JourneyPlannerCombinesWalkBusAndWalkWithPassengerStateMachine()
    {
        var world = CreateRoadWorld(withEndpoints: true);
        var road = world.CreateRoadNetworkSnapshot();
        var lane = road.Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        world.CreateTransitServicePattern(line, [new(first, 0, 1), new(second, 20, 1)]);
        var request = new TripRequest(new TripRequestId(1), TripEndpoint.ForBuilding(new BuildingId(1)), TripEndpoint.ForBuilding(new BuildingId(2)));
        var journeyId = world.PlanMultimodalJourney(request);
        var journey = world.CreateMultimodalTransitSnapshot().Journeys.Single(item => item.Id == journeyId);
        CollectionAssert.AreEqual(new[] { TransitMode.Walk, TransitMode.Bus, TransitMode.Walk }, journey.Legs.Select(static item => item.Mode).ToArray());
        var passenger = world.CreatePassenger(request.Id, journeyId);
        for (var index = 0; index < 200; index++) world.Step();
        Assert.AreEqual(PassengerState.Arrived, world.CreateMultimodalTransitSnapshot().Passengers.Single(item => item.Id == passenger).State);
    }

    private static SimulationWorld CreateRoadWorld(bool withEndpoints = false)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        var a = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var b = world.CreateRoadNode(new WorldPoint(100, 0, 0));
        var segment = world.CreateRoadSegment(a, b);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        if (withEndpoints)
        {
            var origin = world.CreateBuilding(new WorldVolume(0, 5, 0, 5, 10, 5));
            var destination = world.CreateBuilding(new WorldVolume(95, 5, 0, 100, 10, 5));
            world.CreateRoadAccessPoint(segment, 0.05d, buildingId: origin, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
            world.CreateRoadAccessPoint(segment, 0.95d, buildingId: destination, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        }
        return world;
    }
}
