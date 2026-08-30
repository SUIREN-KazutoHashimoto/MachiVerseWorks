namespace MachiVerseWorks.Simulation.Internal;

internal sealed class MultimodalTransitStore
{
    private readonly Dictionary<TransitStopId, TransitStopSnapshot> stops = [];
    private readonly Dictionary<TransitLineId, TransitLineSnapshot> lines = [];
    private readonly Dictionary<TransitServicePatternId, TransitServicePatternSnapshot> patterns = [];
    private readonly Dictionary<TransitTripId, TransitTripSnapshot> trips = [];
    private readonly Dictionary<TransitVehicleId, TransitVehicleState> vehicles = [];
    private readonly Dictionary<TaxiRequestId, TaxiRequestStateData> taxiRequests = [];
    private readonly Dictionary<JourneyId, JourneySnapshot> journeys = [];
    private readonly Dictionary<PassengerId, PassengerStateData> passengers = [];
    private ulong nextStopId = 1;
    private ulong nextLineId = 1;
    private ulong nextPatternId = 1;
    private ulong nextTripId = 1;
    private ulong nextVehicleId = 1;
    private ulong nextTaxiRequestId = 1;
    private ulong nextJourneyId = 1;
    private ulong nextPassengerId = 1;

    public ulong NextStopId => nextStopId;
    public ulong NextLineId => nextLineId;
    public ulong NextPatternId => nextPatternId;
    public ulong NextTripId => nextTripId;
    public ulong NextVehicleId => nextVehicleId;
    public ulong NextTaxiRequestId => nextTaxiRequestId;
    public ulong NextJourneyId => nextJourneyId;
    public ulong NextPassengerId => nextPassengerId;
    public int StopCount => stops.Count;
    public int VehicleCount => vehicles.Count;
    public int TaxiRequestCount => taxiRequests.Count;
    public int JourneyCount => journeys.Count;
    public int PassengerCount => passengers.Count;

    public TransitStopId AddStop(TransitStopKind kind, WorldPoint position, LaneId? laneId, StationId? stationId, PlatformId? platformId)
    {
        EnsureCapacity(nextStopId, "Transit stop");
        var id = new TransitStopId(nextStopId++);
        stops.Add(id, new TransitStopSnapshot(id, kind, position, laneId, stationId, platformId));
        return id;
    }

    public TransitLineId AddLine(TransitMode mode)
    {
        if (mode is not (TransitMode.Bus or TransitMode.Railway)) throw new ArgumentOutOfRangeException(nameof(mode));
        EnsureCapacity(nextLineId, "Transit line");
        var id = new TransitLineId(nextLineId++);
        lines.Add(id, new TransitLineSnapshot(id, mode));
        return id;
    }

    public TransitServicePatternId AddPattern(TransitLineId lineId, IReadOnlyList<TransitPatternStopSnapshot> patternStops)
    {
        if (!lines.ContainsKey(lineId)) throw new ArgumentException($"Transit line {lineId.Value} does not exist.", nameof(lineId));
        ArgumentNullException.ThrowIfNull(patternStops);
        if (patternStops.Count < 2) throw new ArgumentException("A service pattern requires at least two stops.", nameof(patternStops));
        var copied = new TransitPatternStopSnapshot[patternStops.Count];
        var seen = new HashSet<TransitStopId>();
        for (var index = 0; index < patternStops.Count; index++)
        {
            var item = patternStops[index];
            if (!stops.ContainsKey(item.StopId)) throw new ArgumentException($"Transit stop {item.StopId.Value} does not exist.", nameof(patternStops));
            if (!seen.Add(item.StopId)) throw new ArgumentException("A service pattern cannot contain the same stop twice.", nameof(patternStops));
            if (index == 0 && item.TravelTicksFromPrevious != 0) throw new ArgumentException("The first pattern stop must have zero travel ticks.", nameof(patternStops));
            if (index > 0 && item.TravelTicksFromPrevious == 0) throw new ArgumentException("Travel ticks between stops must be greater than zero.", nameof(patternStops));
            copied[index] = item;
        }
        EnsureCapacity(nextPatternId, "Transit service pattern");
        var id = new TransitServicePatternId(nextPatternId++);
        patterns.Add(id, new TransitServicePatternSnapshot(id, lineId, Array.AsReadOnly(copied)));
        return id;
    }

    public TransitTripId AddTrip(TransitServicePatternId patternId, ulong plannedStartTick)
    {
        if (!patterns.ContainsKey(patternId)) throw new ArgumentException($"Transit service pattern {patternId.Value} does not exist.", nameof(patternId));
        EnsureCapacity(nextTripId, "Transit trip");
        var id = new TransitTripId(nextTripId++);
        trips.Add(id, new TransitTripSnapshot(id, patternId, plannedStartTick, null));
        return id;
    }

    public TransitVehicleId AddBusVehicle(TransitTripId tripId)
    {
        if (!trips.TryGetValue(tripId, out var trip)) throw new ArgumentException($"Transit trip {tripId.Value} does not exist.", nameof(tripId));
        if (trip.VehicleId is not null) throw new InvalidOperationException($"Transit trip {tripId.Value} already has a Vehicle.");
        var pattern = patterns[trip.PatternId];
        if (lines[pattern.LineId].Mode != TransitMode.Bus) throw new InvalidOperationException("Only Bus trips can create a Road Traffic bus Vehicle.");
        EnsureCapacity(nextVehicleId, "Transit vehicle");
        var id = new TransitVehicleId(nextVehicleId++);
        var first = stops[pattern.Stops[0].StopId];
        vehicles.Add(id, new TransitVehicleState(id, TransitVehicleKind.Bus, tripId, first.Position)
        {
            State = TransitVehicleMovementState.AwaitingDeparture,
            EstimatedArrivalTick = trip.PlannedStartTick,
            DwellUntilTick = trip.PlannedStartTick + pattern.Stops[0].DwellTicks,
        });
        trips[tripId] = trip with { VehicleId = id };
        return id;
    }

    public TransitVehicleId AddTaxiVehicle(WorldPoint position)
    {
        EnsureCapacity(nextVehicleId, "Transit vehicle");
        var id = new TransitVehicleId(nextVehicleId++);
        vehicles.Add(id, new TransitVehicleState(id, TransitVehicleKind.Taxi, null, position));
        return id;
    }

    public TaxiRequestId AddTaxiRequest(TripRequestId tripRequestId, WorldPoint pickup, WorldPoint dropOff, ulong requestedTick)
    {
        if (tripRequestId.Value == 0) throw new ArgumentOutOfRangeException(nameof(tripRequestId));
        EnsureCapacity(nextTaxiRequestId, "Taxi request");
        var id = new TaxiRequestId(nextTaxiRequestId++);
        taxiRequests.Add(id, new TaxiRequestStateData(id, tripRequestId, pickup, dropOff, requestedTick));
        return id;
    }

    public JourneyId AddJourney(TripRequestId tripRequestId, ulong departureTick, IReadOnlyList<JourneyLegSnapshot> legs)
    {
        ArgumentNullException.ThrowIfNull(legs);
        if (tripRequestId.Value == 0 || legs.Count == 0) throw new ArgumentException("Journey requires a Trip Request and at least one leg.");
        ulong total = 0;
        var copied = new JourneyLegSnapshot[legs.Count];
        for (var index = 0; index < legs.Count; index++)
        {
            var leg = legs[index];
            checked { total += leg.EstimatedDurationTicks + leg.TransferTicks; }
            copied[index] = leg;
        }
        EnsureCapacity(nextJourneyId, "Journey");
        var id = new JourneyId(nextJourneyId++);
        journeys.Add(id, new JourneySnapshot(id, tripRequestId, departureTick, checked(departureTick + total), Array.AsReadOnly(copied)));
        return id;
    }

    public PassengerId AddPassenger(TripRequestId tripRequestId, JourneyId journeyId, ulong tickCount)
    {
        if (!journeys.TryGetValue(journeyId, out var journey) || journey.TripRequestId != tripRequestId)
            throw new ArgumentException("Passenger Journey does not match the Trip Request.", nameof(journeyId));
        EnsureCapacity(nextPassengerId, "Passenger");
        var id = new PassengerId(nextPassengerId++);
        passengers.Add(id, new PassengerStateData(id, tripRequestId, journeyId, tickCount));
        return id;
    }

    public bool TryGetStop(TransitStopId id, out TransitStopSnapshot snapshot) => stops.TryGetValue(id, out snapshot);
    public bool TryGetLine(TransitLineId id, out TransitLineSnapshot snapshot) => lines.TryGetValue(id, out snapshot);
    public bool TryGetPattern(TransitServicePatternId id, out TransitServicePatternSnapshot snapshot) => patterns.TryGetValue(id, out snapshot!);
    public bool TryGetTrip(TransitTripId id, out TransitTripSnapshot snapshot) => trips.TryGetValue(id, out snapshot);
    public bool TryGetJourney(JourneyId id, out JourneySnapshot snapshot) => journeys.TryGetValue(id, out snapshot!);
    public bool TryGetVehicle(TransitVehicleId id, out TransitVehicleState state) => vehicles.TryGetValue(id, out state!);
    public bool TryGetTaxiRequest(TaxiRequestId id, out TaxiRequestStateData state) => taxiRequests.TryGetValue(id, out state!);

    public TransitStopSnapshot[] GetStops() => stops.Values.OrderBy(static item => item.Id.Value).ToArray();
    public TransitLineSnapshot[] GetLines() => lines.Values.OrderBy(static item => item.Id.Value).ToArray();
    public TransitServicePatternSnapshot[] GetPatterns() => patterns.Values.OrderBy(static item => item.Id.Value).ToArray();
    public TransitTripSnapshot[] GetTrips() => trips.Values.OrderBy(static item => item.Id.Value).ToArray();
    public TransitVehicleState[] GetVehicleStates() => vehicles.Values.OrderBy(static item => item.Id.Value).ToArray();
    public TaxiRequestStateData[] GetTaxiRequestStates() => taxiRequests.Values.OrderBy(static item => item.Id.Value).ToArray();
    public JourneySnapshot[] GetJourneys() => journeys.Values.OrderBy(static item => item.Id.Value).ToArray();
    public PassengerStateData[] GetPassengerStates() => passengers.Values.OrderBy(static item => item.Id.Value).ToArray();

    public TransitVehicleId? AssignNearestTaxi(TaxiRequestStateData request)
    {
        TransitVehicleState? best = null;
        var bestDistanceSquared = double.PositiveInfinity;
        foreach (var vehicle in vehicles.Values)
        {
            if (vehicle.Kind != TransitVehicleKind.Taxi || vehicle.State != TransitVehicleMovementState.Idle || vehicle.ActiveTaxiRequestId is not null) continue;
            var dx = vehicle.Position.X - request.Pickup.X;
            var dy = vehicle.Position.Y - request.Pickup.Y;
            var dz = vehicle.Position.Z - request.Pickup.Z;
            var distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);
            if (distanceSquared < bestDistanceSquared || (distanceSquared == bestDistanceSquared && (best is null || vehicle.Id.Value < best.Id.Value)))
            {
                best = vehicle;
                bestDistanceSquared = distanceSquared;
            }
        }
        if (best is null) return null;
        best.ActiveTaxiRequestId = request.Id;
        request.AssignedVehicleId = best.Id;
        request.State = TaxiRequestState.Assigned;
        return best.Id;
    }

    public void StepPassengers(ulong tickCount)
    {
        foreach (var passenger in passengers.Values)
        {
            if (passenger.State == PassengerState.Arrived) continue;
            var journey = journeys[passenger.JourneyId];
            var leg = journey.Legs[passenger.LegIndex];
            var elapsed = tickCount >= passenger.StateEnteredTick ? tickCount - passenger.StateEnteredTick : 0;
            switch (passenger.State)
            {
                case PassengerState.Waiting:
                    passenger.State = leg.Mode == TransitMode.Walk ? PassengerState.Riding : PassengerState.Boarding;
                    passenger.StateEnteredTick = tickCount;
                    break;
                case PassengerState.Boarding:
                    if (elapsed >= 1) { passenger.State = PassengerState.Riding; passenger.StateEnteredTick = tickCount; }
                    break;
                case PassengerState.Riding:
                    if (elapsed >= Math.Max(1UL, leg.EstimatedDurationTicks)) { passenger.State = PassengerState.Alighting; passenger.StateEnteredTick = tickCount; }
                    break;
                case PassengerState.Alighting:
                    if (elapsed < 1) break;
                    if (passenger.LegIndex >= journey.Legs.Count - 1)
                    {
                        passenger.State = PassengerState.Arrived;
                        passenger.StateEnteredTick = tickCount;
                    }
                    else
                    {
                        passenger.LegIndex++;
                        passenger.State = leg.TransferTicks > 0 ? PassengerState.Transfer : PassengerState.Waiting;
                        passenger.StateEnteredTick = tickCount;
                    }
                    break;
                case PassengerState.Transfer:
                    if (elapsed >= Math.Max(1UL, journey.Legs[passenger.LegIndex - 1].TransferTicks)) { passenger.State = PassengerState.Waiting; passenger.StateEnteredTick = tickCount; }
                    break;
            }
        }
    }

    public MultimodalTransitSnapshot CreateSnapshot(ulong tickCount)
    {
        var vehicleSnapshots = GetVehicleStates().Select(item => item.ToSnapshot(tickCount)).ToArray();
        var taxiSnapshots = GetTaxiRequestStates().Select(static item => item.ToSnapshot()).ToArray();
        var passengerSnapshots = GetPassengerStates().Select(item => item.ToSnapshot(tickCount)).ToArray();
        return new MultimodalTransitSnapshot(GetStops(), GetLines(), GetPatterns(), GetTrips(), vehicleSnapshots, taxiSnapshots, GetJourneys(), passengerSnapshots);
    }

    private static void EnsureCapacity(ulong nextId, string name)
    {
        if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted.");
    }
}

internal sealed class TransitVehicleState(TransitVehicleId id, TransitVehicleKind kind, TransitTripId? tripId, WorldPoint position)
{
    public TransitVehicleId Id { get; } = id;
    public TransitVehicleKind Kind { get; } = kind;
    public TransitTripId? TripId { get; } = tripId;
    public VehicleId? RoadVehicleId { get; set; }
    public TaxiRequestId? ActiveTaxiRequestId { get; set; }
    public int StopIndex { get; set; }
    public WorldPoint Position { get; set; } = position;
    public TransitVehicleMovementState State { get; set; } = TransitVehicleMovementState.Idle;
    public ulong EstimatedArrivalTick { get; set; }
    public ulong DwellUntilTick { get; set; }
    public TransitVehicleSnapshot ToSnapshot(ulong tickCount) => new(Id, Kind, TripId, RoadVehicleId, StopIndex, Position, State, EstimatedArrivalTick, DwellUntilTick, tickCount);
}

internal sealed class TaxiRequestStateData(TaxiRequestId id, TripRequestId tripRequestId, WorldPoint pickup, WorldPoint dropOff, ulong requestedTick)
{
    public TaxiRequestId Id { get; } = id;
    public TripRequestId TripRequestId { get; } = tripRequestId;
    public WorldPoint Pickup { get; } = pickup;
    public WorldPoint DropOff { get; } = dropOff;
    public TaxiRequestState State { get; set; } = TaxiRequestState.Requested;
    public TransitVehicleId? AssignedVehicleId { get; set; }
    public ulong RequestedTick { get; } = requestedTick;
    public ulong PickupTick { get; set; }
    public ulong CompletedTick { get; set; }
    public TaxiRequestSnapshot ToSnapshot() => new(Id, TripRequestId, Pickup, DropOff, State, AssignedVehicleId, RequestedTick, PickupTick, CompletedTick);
}

internal sealed class PassengerStateData(PassengerId id, TripRequestId tripRequestId, JourneyId journeyId, ulong tickCount)
{
    public PassengerId Id { get; } = id;
    public TripRequestId TripRequestId { get; } = tripRequestId;
    public JourneyId JourneyId { get; } = journeyId;
    public int LegIndex { get; set; }
    public PassengerState State { get; set; } = PassengerState.Waiting;
    public ulong StateEnteredTick { get; set; } = tickCount;
    public PassengerSnapshot ToSnapshot(ulong tickCount) => new(Id, TripRequestId, JourneyId, LegIndex, State, StateEnteredTick, tickCount);
}
