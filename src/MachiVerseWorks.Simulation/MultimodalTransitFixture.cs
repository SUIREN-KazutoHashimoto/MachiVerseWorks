namespace MachiVerseWorks.Simulation;

public sealed record MultimodalTransitFixture(
    BuildingId OriginBuildingId,
    BuildingId DestinationBuildingId,
    LaneId RoadLaneId,
    TransitLineId RailwayLineId,
    TransitServicePatternId RailwayPatternId,
    JourneyId RailwayJourneyId,
    PassengerId RailwayPassengerId,
    TransitLineId BusLineId,
    TransitServicePatternId BusPatternId,
    TransitTripId BusTripId,
    TransitVehicleId BusVehicleId,
    TransitVehicleId TaxiVehicleId,
    TaxiRequestId TaxiRequestId,
    RailwayOperationsFixture RailwayOperations);

public static class MultimodalTransitFixtures
{
    public static MultimodalTransitFixture SeedDeterministic(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var railway = RailwayOperationsFixtures.SeedDeterministic(world);

        const double roadY = 0d;
        var west = world.CreateRoadNode(new WorldPoint(-80d, roadY, 0d));
        var east = world.CreateRoadNode(new WorldPoint(80d, roadY, 0d));
        var road = world.CreateRoadSegment(west, east, RoadKind.Local);
        var lane = world.CreateLane(road, LaneDirection.Forward, order: 0, speedLimitMetersPerSecond: 12d);

        var origin = world.CreateBuilding(new WorldVolume(-72d, -2d, 0d, -68d, 2d, 4d), BuildingKind.Residential);
        var destination = world.CreateBuilding(new WorldVolume(68d, -2d, 0d, 72d, 2d, 4d), BuildingKind.Commercial);
        world.CreateRoadAccessPoint(road, 0.0625d, origin, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        world.CreateRoadAccessPoint(road, 0.9375d, destination, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);

        var railwayLine = world.CreateTransitLine(TransitMode.Railway);
        var railwayPattern = world.CreateRailwayServicePattern(railwayLine, railway.FirstServiceId);
        var railwayTripRequest = new TripRequest(
            new TripRequestId(19_001),
            TripEndpoint.ForBuilding(origin),
            TripEndpoint.ForBuilding(destination),
            TravelMode.Any);
        var railwayJourney = world.PlanMultimodalJourney(railwayTripRequest);
        var journey = world.CreateMultimodalTransitSnapshot().Journeys.Single(item => item.Id == railwayJourney);
        if (journey.Legs.Count < 3
            || journey.Legs[0].Mode != TransitMode.Walk
            || !journey.Legs.Any(static leg => leg.Mode == TransitMode.Railway)
            || journey.Legs[^1].Mode != TransitMode.Walk)
            throw new InvalidOperationException("Deterministic multimodal fixture did not produce walk -> railway -> walk.");
        var passenger = world.CreatePassenger(railwayTripRequest.Id, railwayJourney);

        var busWest = world.CreateBusStop(lane, new WorldPoint(-55d, roadY, 0d));
        var busEast = world.CreateBusStop(lane, new WorldPoint(55d, roadY, 0d));
        var busLine = world.CreateTransitLine(TransitMode.Bus);
        var busPattern = world.CreateTransitServicePattern(busLine, [
            new TransitPatternStopSnapshot(busWest, 0, 20),
            new TransitPatternStopSnapshot(busEast, 300, 20),
        ]);
        var busTrip = world.CreateTransitTrip(busPattern, world.Time.TickCount + 1);
        var busVehicle = world.CreateBusTransitVehicle(busTrip);

        var taxiVehicle = world.CreateTaxiVehicle(new WorldPoint(-65d, roadY, 0d));
        var taxiRequest = world.CreateTaxiRequest(new TripRequestId(19_002), new WorldPoint(-60d, roadY, 0d), new WorldPoint(60d, roadY, 0d));
        world.DispatchTaxiRequests();

        return new MultimodalTransitFixture(
            origin,
            destination,
            lane,
            railwayLine,
            railwayPattern,
            railwayJourney,
            passenger,
            busLine,
            busPattern,
            busTrip,
            busVehicle,
            taxiVehicle,
            taxiRequest,
            railway);
    }
}
