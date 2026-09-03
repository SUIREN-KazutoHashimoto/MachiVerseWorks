using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private const double DefaultWalkingSpeedMetersPerSecond = 1.4d;
    private const double DefaultTransferRadiusMeters = 300d;
    private readonly MultimodalTransitStore _multimodalTransit = new();

    public int TransitStopCount => _multimodalTransit.StopCount;
    public int TransitVehicleCount => _multimodalTransit.VehicleCount;
    public int TaxiRequestCount => _multimodalTransit.TaxiRequestCount;
    public int JourneyCount => _multimodalTransit.JourneyCount;
    public int PassengerCount => _multimodalTransit.PassengerCount;

    public TransitStopId CreateBusStop(LaneId laneId, WorldPoint position)
    {
        ValidatePoint(position);
        if (!_roads.TryGetLane(laneId, out _)) throw new ArgumentException($"Lane {laneId.Value} does not exist.", nameof(laneId));
        return _multimodalTransit.AddStop(TransitStopKind.Bus, position, laneId, null, null);
    }

    public TransitStopId CreateRailwayTransitStop(StationId stationId, WorldPoint position, PlatformId? platformId = null)
    {
        ValidatePoint(position);
        var railway = _railway.CreateSnapshot();
        if (!railway.Stations.Any(item => item.Id == stationId)) throw new ArgumentException($"Station {stationId.Value} does not exist.", nameof(stationId));
        if (platformId is { } platform && !railway.Platforms.Any(item => item.Id == platform && item.StationId == stationId))
            throw new ArgumentException($"Platform {platform.Value} does not belong to Station {stationId.Value}.", nameof(platformId));
        return _multimodalTransit.AddStop(TransitStopKind.Railway, position, null, stationId, platformId);
    }

    public TransitLineId CreateTransitLine(TransitMode mode) => _multimodalTransit.AddLine(mode);

    public TransitServicePatternId CreateTransitServicePattern(TransitLineId lineId, IReadOnlyList<TransitPatternStopSnapshot> stops) =>
        _multimodalTransit.AddPattern(lineId, stops);

    public TransitServicePatternId CreateRailwayServicePattern(TransitLineId lineId, RailwayServiceId serviceId)
    {
        if (!_multimodalTransit.TryGetLine(lineId, out var line) || line.Mode != TransitMode.Railway)
            throw new ArgumentException("The line must be an existing Railway transit line.", nameof(lineId));
        var operations = CreateRailwayOperationsSnapshot();
        var service = operations.Services.FirstOrDefault(item => item.Id == serviceId)
            ?? throw new ArgumentException($"Railway service {serviceId.Value} does not exist.", nameof(serviceId));
        var timetable = operations.Timetables.First(item => item.Id == service.TimetableId);
        var infrastructure = CreateRailwayInfrastructureSnapshot();
        var patternStops = new TransitPatternStopSnapshot[timetable.Stops.Count];
        ulong previousDeparture = 0;
        for (var index = 0; index < timetable.Stops.Count; index++)
        {
            var timetableStop = timetable.Stops[index];
            var stop = _multimodalTransit.GetStops().FirstOrDefault(item => item.Kind == TransitStopKind.Railway && item.StationId == timetableStop.StationId);
            if (stop.Id.Value == 0)
            {
                var station = infrastructure.Stations.First(item => item.Id == timetableStop.StationId);
                var bounds = station.Bounds;
                var position = new WorldPoint((bounds.MinX + bounds.MaxX) * 0.5d, (bounds.MinY + bounds.MaxY) * 0.5d, (bounds.MinZ + bounds.MaxZ) * 0.5d);
                var id = CreateRailwayTransitStop(timetableStop.StationId, position, timetableStop.PreferredPlatformId);
                _multimodalTransit.TryGetStop(id, out stop);
            }
            var travel = index == 0 ? 0UL : timetableStop.PlannedArrivalTick > previousDeparture ? timetableStop.PlannedArrivalTick - previousDeparture : 1UL;
            var dwell = Math.Max(timetableStop.MinimumDwellTicks, timetableStop.PlannedDepartureTick >= timetableStop.PlannedArrivalTick ? timetableStop.PlannedDepartureTick - timetableStop.PlannedArrivalTick : 0UL);
            patternStops[index] = new TransitPatternStopSnapshot(stop.Id, travel, dwell);
            previousDeparture = timetableStop.PlannedDepartureTick;
        }
        return _multimodalTransit.AddPattern(lineId, patternStops, serviceId);
    }

    public TransitTripId CreateTransitTrip(TransitServicePatternId patternId, ulong plannedStartTick) => _multimodalTransit.AddTrip(patternId, plannedStartTick);
    public TransitVehicleId CreateBusTransitVehicle(TransitTripId tripId) => _multimodalTransit.AddBusVehicle(tripId);

    public TransitVehicleId CreateTaxiVehicle(WorldPoint position)
    {
        ValidatePoint(position);
        return _multimodalTransit.AddTaxiVehicle(position);
    }

    public TaxiRequestId CreateTaxiRequest(TripRequestId tripRequestId, WorldPoint pickup, WorldPoint dropOff)
    {
        ValidatePoint(pickup);
        ValidatePoint(dropOff);
        return _multimodalTransit.AddTaxiRequest(tripRequestId, pickup, dropOff, Time.TickCount);
    }

    public void DispatchTaxiRequests()
    {
        var requests = _multimodalTransit.GetTaxiRequestStates();
        for (var index = 0; index < requests.Length; index++)
            if (requests[index].State == TaxiRequestState.Requested) _multimodalTransit.AssignNearestTaxi(requests[index]);
    }

    public JourneyId PlanMultimodalJourney(TripRequest request, ulong? departureTick = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTripEndpoint(request.Origin, nameof(request));
        ValidateTripEndpoint(request.Destination, nameof(request));
        var startsAt = departureTick ?? Time.TickCount;
        if (!TryResolveRoadAccessPosition(request.Origin, RoadAccessMode.Foot, out var origin))
            throw new InvalidOperationException("Journey origin has no walkable Road access point.");
        if (!TryResolveRoadAccessPosition(request.Destination, RoadAccessMode.Foot, out var destination))
            throw new InvalidOperationException("Journey destination has no walkable Road access point.");

        var stops = _multimodalTransit.GetStops();
        var patterns = _multimodalTransit.GetPatterns();
        var lines = _multimodalTransit.GetLines().ToDictionary(static item => item.Id);
        if (stops.Length == 0 || patterns.Length == 0)
        {
            var walkTicks = SecondsToTicks(Distance(origin, destination) / DefaultWalkingSpeedMetersPerSecond);
            return _multimodalTransit.AddJourney(request.Id, startsAt, [new JourneyLegSnapshot(TransitMode.Walk, request.Origin, request.Destination, null, null, null, null, walkTicks)]);
        }

        var stopIndex = new Dictionary<TransitStopId, int>(stops.Length);
        for (var index = 0; index < stops.Length; index++) stopIndex.Add(stops[index].Id, index);
        var best = new ulong[stops.Length];
        var previous = new JourneyEdge?[stops.Length];
        Array.Fill(best, ulong.MaxValue);
        var frontier = new PriorityQueue<int, (ulong Cost, ulong StopId)>();
        for (var index = 0; index < stops.Length; index++)
        {
            var accessTicks = SecondsToTicks(Distance(origin, stops[index].Position) / DefaultWalkingSpeedMetersPerSecond);
            best[index] = accessTicks;
            frontier.Enqueue(index, (accessTicks, stops[index].Id.Value));
        }

        while (frontier.TryDequeue(out var currentIndex, out var priority))
        {
            if (priority.Cost != best[currentIndex]) continue;
            var current = stops[currentIndex];
            for (var patternIndex = 0; patternIndex < patterns.Length; patternIndex++)
            {
                var pattern = patterns[patternIndex];
                for (var patternStopIndex = 0; patternStopIndex < pattern.Stops.Count - 1; patternStopIndex++)
                {
                    if (pattern.Stops[patternStopIndex].StopId != current.Id) continue;
                    var nextPatternStop = pattern.Stops[patternStopIndex + 1];
                    var nextIndex = stopIndex[nextPatternStop.StopId];
                    var edgeTicks = checked(nextPatternStop.TravelTicksFromPrevious + pattern.Stops[patternStopIndex].DwellTicks);
                    var candidate = checked(best[currentIndex] + edgeTicks);
                    if (candidate < best[nextIndex])
                    {
                        best[nextIndex] = candidate;
                        previous[nextIndex] = new JourneyEdge(current.Id, stops[nextIndex].Id, lines[pattern.LineId].Mode, pattern.LineId, pattern.RailwayServiceId, edgeTicks, 0);
                        frontier.Enqueue(nextIndex, (candidate, stops[nextIndex].Id.Value));
                    }
                }
            }
            for (var transferIndex = 0; transferIndex < stops.Length; transferIndex++)
            {
                if (transferIndex == currentIndex) continue;
                var meters = Distance(current.Position, stops[transferIndex].Position);
                if (meters > DefaultTransferRadiusMeters) continue;
                var transferTicks = SecondsToTicks(meters / DefaultWalkingSpeedMetersPerSecond);
                var candidate = checked(best[currentIndex] + transferTicks);
                if (candidate < best[transferIndex])
                {
                    best[transferIndex] = candidate;
                    previous[transferIndex] = new JourneyEdge(current.Id, stops[transferIndex].Id, TransitMode.Walk, null, null, transferTicks, 0);
                    frontier.Enqueue(transferIndex, (candidate, stops[transferIndex].Id.Value));
                }
            }
        }

        var destinationIndex = -1;
        var destinationCost = ulong.MaxValue;
        for (var index = 0; index < stops.Length; index++)
        {
            if (previous[index] is null) continue;
            var egress = SecondsToTicks(Distance(stops[index].Position, destination) / DefaultWalkingSpeedMetersPerSecond);
            var candidate = checked(best[index] + egress);
            if (candidate < destinationCost || (candidate == destinationCost && stops[index].Id.Value < stops[destinationIndex].Id.Value))
            {
                destinationIndex = index;
                destinationCost = candidate;
            }
        }
        if (destinationIndex < 0)
        {
            var walkTicks = SecondsToTicks(Distance(origin, destination) / DefaultWalkingSpeedMetersPerSecond);
            return _multimodalTransit.AddJourney(request.Id, startsAt, [new JourneyLegSnapshot(TransitMode.Walk, request.Origin, request.Destination, null, null, null, null, walkTicks)]);
        }

        var reversed = new List<JourneyEdge>();
        var cursor = destinationIndex;
        while (previous[cursor] is { } edge)
        {
            reversed.Add(edge);
            cursor = stopIndex[edge.FromStopId];
        }
        reversed.Reverse();
        var legs = new List<JourneyLegSnapshot>(reversed.Count + 2);
        var firstStop = stops[cursor];
        var accessDuration = SecondsToTicks(Distance(origin, firstStop.Position) / DefaultWalkingSpeedMetersPerSecond);
        legs.Add(new JourneyLegSnapshot(TransitMode.Walk, request.Origin, null, null, firstStop.Id, null, null, accessDuration));
        foreach (var edge in reversed)
            legs.Add(new JourneyLegSnapshot(edge.Mode, null, null, edge.FromStopId, edge.ToStopId, edge.LineId, edge.RailwayServiceId, edge.DurationTicks, edge.TransferTicks));
        var lastStop = stops[destinationIndex];
        var egressDuration = SecondsToTicks(Distance(lastStop.Position, destination) / DefaultWalkingSpeedMetersPerSecond);
        legs.Add(new JourneyLegSnapshot(TransitMode.Walk, null, request.Destination, lastStop.Id, null, null, null, egressDuration));
        return _multimodalTransit.AddJourney(request.Id, startsAt, legs);
    }

    public ModeChoiceDecision ChooseMode(TripRequest request, bool hasPrivateVehicle)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryResolveRoadAccessPosition(request.Origin, RoadAccessMode.Foot, out var origin) || !TryResolveRoadAccessPosition(request.Destination, RoadAccessMode.Foot, out var destination))
            throw new InvalidOperationException("Mode choice requires walkable origin and destination access.");
        var walkingTicks = SecondsToTicks(Distance(origin, destination) / DefaultWalkingSpeedMetersPerSecond);
        var best = new ModeChoiceDecision(TransitMode.Walk, walkingTicks);
        JourneyId? plannedTransitJourney = null;
        try
        {
            var journeyId = PlanMultimodalJourney(request);
            plannedTransitJourney = journeyId;
            _multimodalTransit.TryGetJourney(journeyId, out var journey);
            var duration = journey.EstimatedArrivalTick - journey.DepartureTick;
            var primary = journey.Legs.FirstOrDefault(static leg => leg.Mode is TransitMode.Bus or TransitMode.Railway).Mode;
            if (primary is TransitMode.Bus or TransitMode.Railway && duration < best.EstimatedDurationTicks) best = new ModeChoiceDecision(primary, duration, journeyId);
        }
        catch (InvalidOperationException) { }
        if (_multimodalTransit.GetVehicleStates().Any(static item => item.Kind == TransitVehicleKind.Taxi && item.State == TransitVehicleMovementState.Idle))
        {
            var taxiTicks = SecondsToTicks(Distance(origin, destination) / 8d);
            if (taxiTicks < best.EstimatedDurationTicks) best = new ModeChoiceDecision(TransitMode.Taxi, taxiTicks);
        }
        if (hasPrivateVehicle)
        {
            try
            {
                var route = FindRoadRoute(new RouteRequest(origin, destination, RoutingCostMetric.EstimatedTravelTime));
                var motorTicks = SecondsToTicks(route.EstimatedTravelTimeSeconds);
                if (motorTicks < best.EstimatedDurationTicks) best = new ModeChoiceDecision(TransitMode.Motor, motorTicks);
            }
            catch (InvalidOperationException) { }
        }
        if (plannedTransitJourney is { } unusedJourney && best.JourneyId != unusedJourney) _multimodalTransit.RemoveJourney(unusedJourney);
        return best;
    }

    public PassengerId CreatePassenger(TripRequestId tripRequestId, JourneyId journeyId) => _multimodalTransit.AddPassenger(tripRequestId, journeyId, Time.TickCount);

    public MultimodalTransitSnapshot CreateMultimodalTransitSnapshot() => _multimodalTransit.CreateSnapshot(Time.TickCount);

    private void StepMultimodalTransit(ulong tickCount)
    {
        DispatchTaxiRequests();
        StepBusVehicles(tickCount);
        StepTaxiVehicles(tickCount);
        _multimodalTransit.StepPassengers(tickCount);
    }

    private void StepBusVehicles(ulong tickCount)
    {
        var vehicles = _multimodalTransit.GetVehicleStates();
        for (var index = 0; index < vehicles.Length; index++)
        {
            var vehicle = vehicles[index];
            if (vehicle.Kind != TransitVehicleKind.Bus || vehicle.TripId is not { } tripId) continue;
            _multimodalTransit.TryGetTrip(tripId, out var trip);
            _multimodalTransit.TryGetPattern(trip.PatternId, out var pattern);
            if (vehicle.State == TransitVehicleMovementState.EnRouteToStop && vehicle.RoadVehicleId is { } roadVehicleId)
            {
                if (!TryGetVehicleSnapshot(roadVehicleId, out var roadVehicle)) continue;
                vehicle.Position = roadVehicle.Position;
                if (roadVehicle.State != VehicleMovementState.Arrived) continue;
                RemoveVehicleCore(roadVehicleId);
                vehicle.RoadVehicleId = null;
                vehicle.StopIndex++;
                var stop = GetTransitStop(pattern.Stops[vehicle.StopIndex].StopId);
                vehicle.Position = stop.Position;
                vehicle.DwellUntilTick = checked(tickCount + pattern.Stops[vehicle.StopIndex].DwellTicks);
                vehicle.State = vehicle.StopIndex == pattern.Stops.Count - 1 ? TransitVehicleMovementState.Completed : TransitVehicleMovementState.Dwelling;
                continue;
            }
            if (vehicle.State is TransitVehicleMovementState.AwaitingDeparture or TransitVehicleMovementState.Dwelling)
            {
                var departure = vehicle.State == TransitVehicleMovementState.AwaitingDeparture ? Math.Max(trip.PlannedStartTick, vehicle.DwellUntilTick) : vehicle.DwellUntilTick;
                if (tickCount < departure) continue;
                if (vehicle.StopIndex >= pattern.Stops.Count - 1) { vehicle.State = TransitVehicleMovementState.Completed; continue; }
                var from = GetTransitStop(pattern.Stops[vehicle.StopIndex].StopId);
                var to = GetTransitStop(pattern.Stops[vehicle.StopIndex + 1].StopId);
                try
                {
                    var route = FindRoadRoute(new RouteRequest(from.Position, to.Position, RoutingCostMetric.EstimatedTravelTime));
                    vehicle.RoadVehicleId = CreateVehicle(route, new VehicleDimensions(12d, 2.55d, 3.2d), new VehiclePerformance(22.2222222222d, 1.2d, 3d, 2.5d, 2d));
                    vehicle.State = TransitVehicleMovementState.EnRouteToStop;
                    vehicle.EstimatedArrivalTick = checked(tickCount + pattern.Stops[vehicle.StopIndex + 1].TravelTicksFromPrevious);
                }
                catch (InvalidOperationException) { }
            }
        }
    }

    private void StepTaxiVehicles(ulong tickCount)
    {
        var vehicles = _multimodalTransit.GetVehicleStates();
        for (var index = 0; index < vehicles.Length; index++)
        {
            var vehicle = vehicles[index];
            if (vehicle.Kind != TransitVehicleKind.Taxi || vehicle.ActiveTaxiRequestId is not { } requestId) continue;
            _multimodalTransit.TryGetTaxiRequest(requestId, out var request);
            if (vehicle.RoadVehicleId is { } roadVehicleId)
            {
                if (!TryGetVehicleSnapshot(roadVehicleId, out var roadVehicle)) continue;
                vehicle.Position = roadVehicle.Position;
                if (roadVehicle.State != VehicleMovementState.Arrived) continue;
                RemoveVehicleCore(roadVehicleId);
                vehicle.RoadVehicleId = null;
                if (vehicle.State == TransitVehicleMovementState.EnRouteToPickup)
                {
                    vehicle.Position = request.Pickup;
                    request.State = TaxiRequestState.PickingUp;
                    request.PickupTick = tickCount;
                    StartTaxiRoadLeg(vehicle, request, request.Pickup, request.DropOff, TransitVehicleMovementState.EnRouteToDropOff);
                    request.State = TaxiRequestState.Riding;
                }
                else if (vehicle.State == TransitVehicleMovementState.EnRouteToDropOff)
                {
                    vehicle.Position = request.DropOff;
                    vehicle.State = TransitVehicleMovementState.Idle;
                    vehicle.ActiveTaxiRequestId = null;
                    request.State = TaxiRequestState.Completed;
                    request.CompletedTick = tickCount;
                }
                continue;
            }
            if (request.State == TaxiRequestState.Assigned) StartTaxiRoadLeg(vehicle, request, vehicle.Position, request.Pickup, TransitVehicleMovementState.EnRouteToPickup);
        }
    }

    private void StartTaxiRoadLeg(TransitVehicleState vehicle, TaxiRequestStateData request, WorldPoint origin, WorldPoint destination, TransitVehicleMovementState state)
    {
        try
        {
            var route = FindRoadRoute(new RouteRequest(origin, destination, RoutingCostMetric.EstimatedTravelTime));
            vehicle.RoadVehicleId = CreateVehicle(route);
            vehicle.State = state;
            vehicle.EstimatedArrivalTick = checked(Time.TickCount + SecondsToTicks(route.EstimatedTravelTimeSeconds));
        }
        catch (InvalidOperationException)
        {
            request.State = TaxiRequestState.Failed;
            vehicle.State = TransitVehicleMovementState.Idle;
            vehicle.ActiveTaxiRequestId = null;
        }
    }

    private static void ValidateMultimodalTransitCheckpointReferences(SimulationCheckpoint checkpoint)
    {
        var transit = checkpoint.MultimodalTransit;
        if (transit is null)
        {
            if ((checkpoint.Persons ?? []).Any(static item => item.TravelState == PersonTravelState.Transit))
                throw new ArgumentException("Transit Person state requires Multimodal Transit checkpoint data.", nameof(checkpoint));
            return;
        }

        var laneIds = checkpoint.Lanes.Select(static item => item.Id).ToHashSet();
        var stationIds = (checkpoint.Stations ?? []).Select(static item => item.Id).ToHashSet();
        var platformById = (checkpoint.Platforms ?? []).ToDictionary(static item => item.Id);
        var railwayServiceIds = (checkpoint.RailwayServices ?? []).Select(static item => item.Id).ToHashSet();
        var stopIds = transit.Stops.Select(static item => item.Id).ToHashSet();
        var lineById = transit.Lines.ToDictionary(static item => item.Id);
        var patternById = transit.Patterns.ToDictionary(static item => item.Id);
        var roadVehicleIds = (checkpoint.Vehicles ?? []).Select(static item => item.Id).ToHashSet();
        var transitVehicleIds = transit.Vehicles.Select(static item => item.Id).ToHashSet();
        var tripIds = transit.Trips.Select(static item => item.Id).ToHashSet();
        var journeyById = transit.Journeys.ToDictionary(static item => item.Id);

        foreach (var stop in transit.Stops)
        {
            if (!Enum.IsDefined(stop.Kind)) throw new ArgumentException($"Transit Stop {stop.Id.Value} has an invalid kind.", nameof(checkpoint));
            if (stop.Kind == TransitStopKind.Bus && (stop.LaneId is not { } laneId || !laneIds.Contains(laneId)))
                throw new ArgumentException($"Bus Stop {stop.Id.Value} references a missing Lane.", nameof(checkpoint));
            if (stop.Kind == TransitStopKind.Railway)
            {
                if (stop.StationId is not { } stationId || !stationIds.Contains(stationId)) throw new ArgumentException($"Railway Stop {stop.Id.Value} references a missing Station.", nameof(checkpoint));
                if (stop.PlatformId is { } platformId && (!platformById.TryGetValue(platformId, out var platform) || platform.StationId != stationId))
                    throw new ArgumentException($"Railway Stop {stop.Id.Value} references a missing or mismatched Platform.", nameof(checkpoint));
            }
        }
        foreach (var line in transit.Lines)
            if (line.Mode is not (TransitMode.Bus or TransitMode.Railway)) throw new ArgumentException($"Transit Line {line.Id.Value} has an invalid mode.", nameof(checkpoint));
        foreach (var pattern in transit.Patterns)
        {
            if (!lineById.TryGetValue(pattern.LineId, out var line)) throw new ArgumentException($"Transit Pattern {pattern.Id.Value} references a missing Line.", nameof(checkpoint));
            if (pattern.Stops.Count < 2) throw new ArgumentException($"Transit Pattern {pattern.Id.Value} must contain at least two stops.", nameof(checkpoint));
            for (var index = 0; index < pattern.Stops.Count; index++)
            {
                var stop = pattern.Stops[index];
                if (!stopIds.Contains(stop.StopId)) throw new ArgumentException($"Transit Pattern {pattern.Id.Value} references a missing Stop.", nameof(checkpoint));
                if ((index == 0 && stop.TravelTicksFromPrevious != 0) || (index > 0 && stop.TravelTicksFromPrevious == 0))
                    throw new ArgumentException($"Transit Pattern {pattern.Id.Value} has invalid stop travel timing.", nameof(checkpoint));
            }
            if (line.Mode == TransitMode.Railway)
            {
                if (pattern.RailwayServiceId is not { } serviceId || !railwayServiceIds.Contains(serviceId)) throw new ArgumentException($"Railway Pattern {pattern.Id.Value} references a missing Railway Service.", nameof(checkpoint));
            }
            else if (pattern.RailwayServiceId is not null) throw new ArgumentException($"Bus Pattern {pattern.Id.Value} cannot reference a Railway Service.", nameof(checkpoint));
        }
        foreach (var trip in transit.Trips)
            if (!patternById.ContainsKey(trip.PatternId)) throw new ArgumentException($"Transit Trip {trip.Id.Value} references a missing Pattern.", nameof(checkpoint));
        foreach (var vehicle in transit.Vehicles)
        {
            if (!Enum.IsDefined(vehicle.Kind) || !Enum.IsDefined(vehicle.State)) throw new ArgumentException($"Transit Vehicle {vehicle.Id.Value} has invalid state.", nameof(checkpoint));
            if (vehicle.Kind == TransitVehicleKind.Bus && (vehicle.TripId is not { } busTripId || !tripIds.Contains(busTripId))) throw new ArgumentException($"Bus Vehicle {vehicle.Id.Value} references a missing Trip.", nameof(checkpoint));
            if (vehicle.Kind == TransitVehicleKind.Taxi && vehicle.TripId is not null) throw new ArgumentException($"Taxi Vehicle {vehicle.Id.Value} cannot reference a scheduled Transit Trip.", nameof(checkpoint));
            if (vehicle.RoadVehicleId is { } roadVehicleId && !roadVehicleIds.Contains(roadVehicleId)) throw new ArgumentException($"Transit Vehicle {vehicle.Id.Value} references a missing Road Vehicle.", nameof(checkpoint));
        }
        foreach (var request in transit.TaxiRequests)
            if (request.AssignedVehicleId is { } vehicleId && !transitVehicleIds.Contains(vehicleId)) throw new ArgumentException($"Taxi Request {request.Id.Value} references a missing Transit Vehicle.", nameof(checkpoint));
        foreach (var journey in transit.Journeys)
        {
            if (journey.Legs.Count == 0) throw new ArgumentException($"Journey {journey.Id.Value} has no legs.", nameof(checkpoint));
            foreach (var leg in journey.Legs)
            {
                if (!Enum.IsDefined(leg.Mode)) throw new ArgumentException($"Journey {journey.Id.Value} has an invalid mode.", nameof(checkpoint));
                if (leg.FromStopId is { } from && !stopIds.Contains(from)) throw new ArgumentException($"Journey {journey.Id.Value} references a missing origin Stop.", nameof(checkpoint));
                if (leg.ToStopId is { } to && !stopIds.Contains(to)) throw new ArgumentException($"Journey {journey.Id.Value} references a missing destination Stop.", nameof(checkpoint));
                if (leg.LineId is { } lineId && !lineById.ContainsKey(lineId)) throw new ArgumentException($"Journey {journey.Id.Value} references a missing Line.", nameof(checkpoint));
                if (leg.RailwayServiceId is { } serviceId && !railwayServiceIds.Contains(serviceId)) throw new ArgumentException($"Journey {journey.Id.Value} references a missing Railway Service.", nameof(checkpoint));
            }
        }
        foreach (var passenger in transit.Passengers)
        {
            if (!journeyById.TryGetValue(passenger.JourneyId, out var journey)) throw new ArgumentException($"Passenger {passenger.Id.Value} references a missing Journey.", nameof(checkpoint));
            if (passenger.LegIndex < 0 || passenger.LegIndex >= journey.Legs.Count) throw new ArgumentException($"Passenger {passenger.Id.Value} has an invalid Journey leg index.", nameof(checkpoint));
            if (!Enum.IsDefined(passenger.State)) throw new ArgumentException($"Passenger {passenger.Id.Value} has an invalid state.", nameof(checkpoint));
        }
        var activeTransitTripRequests = transit.Passengers.Select(static item => item.TripRequestId).Concat(transit.TaxiRequests.Select(static item => item.TripRequestId)).ToHashSet();
        foreach (var person in checkpoint.Persons ?? [])
            if (person.TravelState == PersonTravelState.Transit && (person.ActiveTripRequestId is not { } id || !activeTransitTripRequests.Contains(id)))
                throw new ArgumentException($"Transit Person {person.Id.Value} references a missing Passenger or Taxi Request.", nameof(checkpoint));
    }

    private TransitStopSnapshot GetTransitStop(TransitStopId id)
    {
        if (!_multimodalTransit.TryGetStop(id, out var stop)) throw new InvalidOperationException($"Transit stop {id.Value} disappeared.");
        return stop;
    }

    private ulong SecondsToTicks(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0d) return 1;
        var ticks = Math.Ceiling(seconds * Config.TickRate);
        return ticks >= ulong.MaxValue ? ulong.MaxValue : Math.Max(1UL, (ulong)ticks);
    }

    private static double Distance(WorldPoint left, WorldPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        var dz = left.Z - right.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private readonly record struct JourneyEdge(
        TransitStopId FromStopId,
        TransitStopId ToStopId,
        TransitMode Mode,
        TransitLineId? LineId,
        RailwayServiceId? RailwayServiceId,
        ulong DurationTicks,
        ulong TransferTicks);
}
