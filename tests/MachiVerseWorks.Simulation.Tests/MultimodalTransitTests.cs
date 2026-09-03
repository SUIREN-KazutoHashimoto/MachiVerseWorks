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
        for (var index = 0; index < 1000 && world.CreateMultimodalTransitSnapshot().Passengers.Single(item => item.Id == passenger).State != PassengerState.Arrived; index++) world.Step();
        Assert.AreEqual(PassengerState.Arrived, world.CreateMultimodalTransitSnapshot().Passengers.Single(item => item.Id == passenger).State);
    }


    [TestMethod]
    public void RailwayServicePatternParticipatesInWalkRailwayWalkJourney()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        var fixture = RailwayOperationsFixtures.SeedDeterministic(world);
        var origin = world.CreateBuilding(new WorldVolume(-42, 10, 0, -38, 14, 4));
        var destination = world.CreateBuilding(new WorldVolume(52, 10, 0, 56, 14, 4));
        var roadStart = world.CreateRoadNode(new WorldPoint(-40, 12, 0));
        var roadEnd = world.CreateRoadNode(new WorldPoint(54, 12, 0));
        var roadSegment = world.CreateRoadSegment(roadStart, roadEnd);
        world.CreateLane(roadSegment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        world.CreateRoadAccessPoint(roadSegment, 0d, buildingId: origin, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        world.CreateRoadAccessPoint(roadSegment, 1d, buildingId: destination, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        var line = world.CreateTransitLine(TransitMode.Railway);
        world.CreateRailwayServicePattern(line, fixture.FirstServiceId);

        var request = new TripRequest(new TripRequestId(77), TripEndpoint.ForBuilding(origin), TripEndpoint.ForBuilding(destination));
        var journeyId = world.PlanMultimodalJourney(request);
        var journey = world.CreateMultimodalTransitSnapshot().Journeys.Single(item => item.Id == journeyId);

        Assert.IsTrue(journey.Legs.Any(static item => item.Mode == TransitMode.Railway));
        Assert.IsTrue(journey.Legs.Any(item => item.RailwayServiceId == fixture.FirstServiceId));
        Assert.AreEqual(TransitMode.Walk, journey.Legs[0].Mode);
        Assert.AreEqual(TransitMode.Walk, journey.Legs[^1].Mode);
    }

    [TestMethod]
    public void PassengerUsesTransferStateForStopToStopWalkAndCheckpointContinuesDeterministically()
    {
        var world = CreateRoadWorld(withEndpoints: true);
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var busA = world.CreateBusStop(lane, new WorldPoint(15, 0, 0));
        var busB = world.CreateBusStop(lane, new WorldPoint(40, 0, 0));
        var busC = world.CreateBusStop(lane, new WorldPoint(60, 0, 0));
        var busD = world.CreateBusStop(lane, new WorldPoint(85, 0, 0));
        var firstLine = world.CreateTransitLine(TransitMode.Bus);
        var secondLine = world.CreateTransitLine(TransitMode.Bus);
        world.CreateTransitServicePattern(firstLine, [new(busA, 0, 1), new(busB, 5, 1)]);
        world.CreateTransitServicePattern(secondLine, [new(busC, 0, 1), new(busD, 5, 1)]);
        var request = new TripRequest(new TripRequestId(88), TripEndpoint.ForBuilding(new BuildingId(1)), TripEndpoint.ForBuilding(new BuildingId(2)));
        var journeyId = world.PlanMultimodalJourney(request);
        var journey = world.CreateMultimodalTransitSnapshot().Journeys.Single(item => item.Id == journeyId);
        Assert.IsTrue(journey.Legs.Any(static item => item.Mode == TransitMode.Walk && item.OriginEndpoint is null && item.DestinationEndpoint is null));
        var passenger = world.CreatePassenger(request.Id, journeyId);

        var observedTransfer = false;
        for (var index = 0; index < 1000; index++)
        {
            world.Step();
            var state = world.CreateMultimodalTransitSnapshot().Passengers.Single(item => item.Id == passenger).State;
            if (state == PassengerState.Transfer) { observedTransfer = true; break; }
        }
        Assert.IsTrue(observedTransfer);

        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());
        for (var index = 0; index < 500; index++) { world.Step(); restored.Step(); }
        Assert.AreEqual(
            world.CreateMultimodalTransitSnapshot().Passengers.Single(item => item.Id == passenger),
            restored.CreateMultimodalTransitSnapshot().Passengers.Single(item => item.Id == passenger));
    }


    [TestMethod]
    public void CheckpointRejectsBrokenTaxiAssignmentInvariants()
    {
        var world = CreateRoadWorld();
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        var pattern = world.CreateTransitServicePattern(line, [new(first, 0, 1), new(second, 10, 1)]);
        var trip = world.CreateTransitTrip(pattern, 0);
        var bus = world.CreateBusTransitVehicle(trip);
        var taxi = world.CreateTaxiVehicle(new WorldPoint(5, 0, 0));
        var firstRequest = world.CreateTaxiRequest(new TripRequestId(100), new WorldPoint(10, 0, 0), new WorldPoint(90, 0, 0));
        var secondRequest = world.CreateTaxiRequest(new TripRequestId(101), new WorldPoint(20, 0, 0), new WorldPoint(70, 0, 0));
        world.DispatchTaxiRequests();
        var checkpoint = world.CreateCheckpoint();
        var transit = checkpoint.MultimodalTransit!;

        var duplicateActive = transit.TaxiRequests.Select(item => item.Id == secondRequest ? item with { State = TaxiRequestState.Assigned, AssignedVehicleId = taxi } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { TaxiRequests = duplicateActive } }));

        var busAssigned = transit.TaxiRequests.Select(item => item.Id == firstRequest ? item with { AssignedVehicleId = bus } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { TaxiRequests = busAssigned } }));

        var missingAssignment = transit.TaxiRequests.Select(item => item.Id == firstRequest ? item with { AssignedVehicleId = null } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { TaxiRequests = missingAssignment } }));
    }

    [TestMethod]
    public void CheckpointRejectsBrokenTripBusBijectionAndBusState()
    {
        var world = CreateRoadWorld();
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        var pattern = world.CreateTransitServicePattern(line, [new(first, 0, 1), new(second, 10, 1)]);
        var tripId = world.CreateTransitTrip(pattern, 0);
        var busId = world.CreateBusTransitVehicle(tripId);
        var checkpoint = world.CreateCheckpoint();
        var transit = checkpoint.MultimodalTransit!;

        var brokenTrips = transit.Trips.Select(item => item.Id == tripId ? item with { VehicleId = null } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { Trips = brokenTrips } }));

        var negativeStop = transit.Vehicles.Select(item => item.Id == busId ? item with { StopIndex = -1 } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { Vehicles = negativeStop } }));

        var stuckEnRoute = transit.Vehicles.Select(item => item.Id == busId ? item with { State = TransitVehicleMovementState.EnRouteToStop, RoadVehicleId = null } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { Vehicles = stuckEnRoute } }));
    }

    [TestMethod]
    public void GenericPatternCreationRejectsRailwayLineWithoutRailwayService()
    {
        var world = CreateRoadWorld();
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var railwayLine = world.CreateTransitLine(TransitMode.Railway);

        Assert.ThrowsExactly<ArgumentException>(() => world.CreateTransitServicePattern(railwayLine, [new(first, 0, 1), new(second, 10, 1)]));
    }

    [TestMethod]
    public void CheckpointRejectsPassengerWhoseTripRequestDiffersFromJourney()
    {
        var world = CreateRoadWorld(withEndpoints: true);
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        world.CreateTransitServicePattern(line, [new(first, 0, 1), new(second, 10, 1)]);
        var request = new TripRequest(new TripRequestId(100), TripEndpoint.ForBuilding(new BuildingId(1)), TripEndpoint.ForBuilding(new BuildingId(2)));
        var journey = world.PlanMultimodalJourney(request);
        world.CreatePassenger(request.Id, journey);
        var checkpoint = world.CreateCheckpoint();
        var transit = checkpoint.MultimodalTransit!;
        var passengers = transit.Passengers.Select(item => item with { TripRequestId = new TripRequestId(101) }).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { Passengers = passengers } }));
    }

    [TestMethod]
    public void DeterministicFixtureContainsWalkRailwayWalkBusAndTaxi()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 19_014, spatialCellSize: 64d));
        var fixture = MultimodalTransitFixtures.SeedDeterministic(world);
        var snapshot = world.CreateMultimodalTransitSnapshot();
        var journey = snapshot.Journeys.Single(item => item.Id == fixture.RailwayJourneyId);

        Assert.AreEqual(TransitMode.Walk, journey.Legs[0].Mode);
        Assert.IsTrue(journey.Legs.Any(static item => item.Mode == TransitMode.Railway));
        Assert.AreEqual(TransitMode.Walk, journey.Legs[^1].Mode);
        Assert.IsTrue(snapshot.Lines.Any(item => item.Id == fixture.BusLineId && item.Mode == TransitMode.Bus));
        Assert.IsTrue(snapshot.Vehicles.Any(item => item.Id == fixture.BusVehicleId && item.Kind == TransitVehicleKind.Bus));
        Assert.IsTrue(snapshot.Vehicles.Any(item => item.Id == fixture.TaxiVehicleId && item.Kind == TransitVehicleKind.Taxi));
        Assert.AreEqual(fixture.TaxiVehicleId, snapshot.TaxiRequests.Single(item => item.Id == fixture.TaxiRequestId).AssignedVehicleId);
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
