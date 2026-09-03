namespace MachiVerseWorks.Simulation.Internal;

internal sealed class MultimodalTransitStore
{
    private const double TransferSpatialCellSizeMeters = 300d;

    private readonly Dictionary<TransitStopId, TransitStopSnapshot> stops = [];
    private readonly Dictionary<TransitLineId, TransitLineSnapshot> lines = [];
    private readonly Dictionary<TransitServicePatternId, TransitServicePatternSnapshot> patterns = [];
    private readonly Dictionary<TransitTripId, TransitTripSnapshot> trips = [];
    private readonly Dictionary<TransitVehicleId, TransitVehicleState> vehicles = [];
    private readonly Dictionary<TaxiRequestId, TaxiRequestStateData> taxiRequests = [];
    private readonly Dictionary<JourneyId, JourneySnapshot> journeys = [];
    private readonly Dictionary<PassengerId, PassengerStateData> passengers = [];
    private readonly Dictionary<TripRequestId, TaxiRequestId> taxiRequestByTrip = [];
    private readonly Dictionary<TripRequestId, PassengerId> passengerByTrip = [];
    private readonly Dictionary<TransitStopId, List<TransitPatternEdge>> outgoingPatternEdges = [];
    private readonly Dictionary<SpatialCell, List<TransitStopId>> stopSpatialIndex = [];
    private ulong nextStopId = 1, nextLineId = 1, nextPatternId = 1, nextTripId = 1, nextVehicleId = 1, nextTaxiRequestId = 1, nextJourneyId = 1, nextPassengerId = 1;

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
        var stop = new TransitStopSnapshot(id, kind, position, laneId, stationId, platformId);
        stops.Add(id, stop);
        IndexStop(stop);
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

    public TransitServicePatternId AddPattern(TransitLineId lineId, IReadOnlyList<TransitPatternStopSnapshot> patternStops, RailwayServiceId? railwayServiceId = null)
    {
        if (!lines.TryGetValue(lineId, out var line)) throw new ArgumentException($"Transit line {lineId.Value} does not exist.", nameof(lineId));
        if (railwayServiceId is not null && line.Mode != TransitMode.Railway) throw new ArgumentException("Railway Service can only be linked to a Railway line.", nameof(railwayServiceId));
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
        var pattern = new TransitServicePatternSnapshot(id, lineId, Array.AsReadOnly(copied), railwayServiceId);
        patterns.Add(id, pattern);
        IndexPattern(pattern, line.Mode);
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
            DwellUntilTick = checked(trip.PlannedStartTick + pattern.Stops[0].DwellTicks),
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
        if (taxiRequestByTrip.ContainsKey(tripRequestId) || passengerByTrip.ContainsKey(tripRequestId))
            throw new InvalidOperationException($"Trip Request {tripRequestId.Value} already has active multimodal state.");
        EnsureCapacity(nextTaxiRequestId, "Taxi request");
        var id = new TaxiRequestId(nextTaxiRequestId++);
        taxiRequests.Add(id, new TaxiRequestStateData(id, tripRequestId, pickup, dropOff, requestedTick));
        taxiRequestByTrip.Add(tripRequestId, id);
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
        if (!journeys.TryGetValue(journeyId, out var journey) || journey.TripRequestId != tripRequestId) throw new ArgumentException("Passenger Journey does not match the Trip Request.", nameof(journeyId));
        if (passengerByTrip.ContainsKey(tripRequestId) || taxiRequestByTrip.ContainsKey(tripRequestId))
            throw new InvalidOperationException($"Trip Request {tripRequestId.Value} already has active multimodal state.");
        EnsureCapacity(nextPassengerId, "Passenger");
        var id = new PassengerId(nextPassengerId++);
        passengers.Add(id, new PassengerStateData(id, tripRequestId, journeyId, tickCount));
        passengerByTrip.Add(tripRequestId, id);
        return id;
    }

    public bool TryGetStop(TransitStopId id, out TransitStopSnapshot snapshot) => stops.TryGetValue(id, out snapshot);
    public bool TryGetLine(TransitLineId id, out TransitLineSnapshot snapshot) => lines.TryGetValue(id, out snapshot);
    public bool TryGetPattern(TransitServicePatternId id, out TransitServicePatternSnapshot snapshot) => patterns.TryGetValue(id, out snapshot!);
    public bool TryGetTrip(TransitTripId id, out TransitTripSnapshot snapshot) => trips.TryGetValue(id, out snapshot);
    public bool TryGetJourney(JourneyId id, out JourneySnapshot snapshot) => journeys.TryGetValue(id, out snapshot!);
    public bool RemoveJourney(JourneyId id) => journeys.Remove(id);
    public bool TryGetVehicle(TransitVehicleId id, out TransitVehicleState state) => vehicles.TryGetValue(id, out state!);
    public bool TryGetTaxiRequest(TaxiRequestId id, out TaxiRequestStateData state) => taxiRequests.TryGetValue(id, out state!);

    public bool TryGetPassengerForTrip(TripRequestId id, out PassengerStateData state)
    {
        if (!passengerByTrip.TryGetValue(id, out var passengerId) || !passengers.TryGetValue(passengerId, out state!))
        {
            state = null!;
            return false;
        }
        if (state.State == PassengerState.Arrived)
        {
            passengerByTrip.Remove(id);
            passengers.Remove(passengerId);
            journeys.Remove(state.JourneyId);
        }
        return true;
    }

    public bool TryGetTaxiRequestForTrip(TripRequestId id, out TaxiRequestStateData state)
    {
        if (!taxiRequestByTrip.TryGetValue(id, out var requestId) || !taxiRequests.TryGetValue(requestId, out state!))
        {
            state = null!;
            return false;
        }
        if (state.State == TaxiRequestState.Completed)
        {
            taxiRequestByTrip.Remove(id);
            taxiRequests.Remove(requestId);
        }
        return true;
    }

    public bool RetireCompletedTrip(TripRequestId id)
    {
        if (passengerByTrip.TryGetValue(id, out var passengerId) && passengers.TryGetValue(passengerId, out var passenger))
        {
            if (passenger.State != PassengerState.Arrived) return false;
            passengerByTrip.Remove(id);
            passengers.Remove(passengerId);
            journeys.Remove(passenger.JourneyId);
            return true;
        }

        if (taxiRequestByTrip.TryGetValue(id, out var taxiRequestId) && taxiRequests.TryGetValue(taxiRequestId, out var request))
        {
            if (request.State != TaxiRequestState.Completed) return false;
            if (request.AssignedVehicleId is { } vehicleId
                && vehicles.TryGetValue(vehicleId, out var vehicle)
                && vehicle.ActiveTaxiRequestId == request.Id)
            {
                vehicle.ActiveTaxiRequestId = null;
            }
            taxiRequestByTrip.Remove(id);
            taxiRequests.Remove(taxiRequestId);
            return true;
        }

        return false;
    }

    public TransitPatternEdge[] GetOutgoingPatternEdges(TransitStopId stopId) => outgoingPatternEdges.TryGetValue(stopId, out var edges)
        ? edges.OrderBy(static item => item.PatternId.Value).ThenBy(static item => item.PatternStopIndex).ToArray()
        : [];

    public TransitStopSnapshot[] GetTransferCandidates(WorldPoint position, double radiusMeters)
    {
        if (!double.IsFinite(radiusMeters) || radiusMeters < 0d) throw new ArgumentOutOfRangeException(nameof(radiusMeters));
        if (radiusMeters == 0d) return [];
        var center = SpatialGrid.ToCell(position, TransferSpatialCellSizeMeters);
        var cellRadius = checked((int)Math.Ceiling(radiusMeters / TransferSpatialCellSizeMeters));
        var ids = new HashSet<TransitStopId>();
        for (var x = (long)center.X - cellRadius; x <= (long)center.X + cellRadius; x++)
        {
            if (x < int.MinValue || x > int.MaxValue) continue;
            for (var y = (long)center.Y - cellRadius; y <= (long)center.Y + cellRadius; y++)
            {
                if (y < int.MinValue || y > int.MaxValue) continue;
                for (var z = (long)center.Z - cellRadius; z <= (long)center.Z + cellRadius; z++)
                {
                    if (z < int.MinValue || z > int.MaxValue) continue;
                    if (!stopSpatialIndex.TryGetValue(new SpatialCell((int)x, (int)y, (int)z), out var cellStops)) continue;
                    foreach (var id in cellStops) ids.Add(id);
                }
            }
        }

        var radiusSquared = radiusMeters * radiusMeters;
        return ids.Select(id => stops[id])
            .Where(stop => DistanceSquared(position, stop.Position) <= radiusSquared)
            .OrderBy(static stop => stop.Id.Value)
            .ToArray();
    }

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
            var dx = vehicle.Position.X - request.Pickup.X; var dy = vehicle.Position.Y - request.Pickup.Y; var dz = vehicle.Position.Z - request.Pickup.Z;
            var distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);
            if (distanceSquared < bestDistanceSquared || (distanceSquared == bestDistanceSquared && (best is null || vehicle.Id.Value < best.Id.Value))) { best = vehicle; bestDistanceSquared = distanceSquared; }
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
                    passenger.State = IsTransferWalk(leg) ? PassengerState.Transfer : leg.Mode == TransitMode.Walk ? PassengerState.Riding : PassengerState.Boarding;
                    passenger.StateEnteredTick = tickCount;
                    break;
                case PassengerState.Boarding:
                    if (elapsed >= 1) { passenger.State = PassengerState.Riding; passenger.StateEnteredTick = tickCount; } break;
                case PassengerState.Riding:
                    if (elapsed >= Math.Max(1UL, leg.EstimatedDurationTicks)) { passenger.State = PassengerState.Alighting; passenger.StateEnteredTick = tickCount; } break;
                case PassengerState.Alighting:
                    if (elapsed < 1) break;
                    if (passenger.LegIndex >= journey.Legs.Count - 1) { passenger.State = PassengerState.Arrived; passenger.StateEnteredTick = tickCount; }
                    else { passenger.LegIndex++; passenger.State = leg.TransferTicks > 0 ? PassengerState.Transfer : PassengerState.Waiting; passenger.StateEnteredTick = tickCount; }
                    break;
                case PassengerState.Transfer:
                    if (elapsed < Math.Max(1UL, leg.EstimatedDurationTicks)) break;
                    if (passenger.LegIndex >= journey.Legs.Count - 1)
                    {
                        passenger.State = PassengerState.Arrived;
                    }
                    else
                    {
                        passenger.LegIndex++;
                        passenger.State = PassengerState.Waiting;
                    }
                    passenger.StateEnteredTick = tickCount;
                    break;
            }
        }
    }

    private static bool IsTransferWalk(JourneyLegSnapshot leg) =>
        leg.Mode == TransitMode.Walk && leg.OriginEndpoint is null && leg.DestinationEndpoint is null && leg.FromStopId is not null && leg.ToStopId is not null;

    public MultimodalTransitSnapshot CreateSnapshot(ulong tickCount) => new(
        GetStops(), GetLines(), GetPatterns(), GetTrips(),
        GetVehicleStates().Select(item => item.ToSnapshot(tickCount)).ToArray(),
        GetTaxiRequestStates().Select(static item => item.ToSnapshot()).ToArray(),
        GetJourneys(), GetPassengerStates().Select(item => item.ToSnapshot(tickCount)).ToArray());

    public MultimodalTransitCheckpoint CreateCheckpoint(ulong tickCount)
    {
        var snapshot = CreateSnapshot(tickCount);
        return new MultimodalTransitCheckpoint(nextStopId, snapshot.Stops, nextLineId, snapshot.Lines, nextPatternId, snapshot.Patterns, nextTripId, snapshot.Trips, nextVehicleId, snapshot.Vehicles, nextTaxiRequestId, snapshot.TaxiRequests, nextJourneyId, snapshot.Journeys, nextPassengerId, snapshot.Passengers);
    }

    public void Restore(MultimodalTransitCheckpoint? checkpoint)
    {
        stops.Clear(); lines.Clear(); patterns.Clear(); trips.Clear(); vehicles.Clear(); taxiRequests.Clear(); journeys.Clear(); passengers.Clear();
        taxiRequestByTrip.Clear(); passengerByTrip.Clear(); outgoingPatternEdges.Clear(); stopSpatialIndex.Clear();
        if (checkpoint is null) { nextStopId = nextLineId = nextPatternId = nextTripId = nextVehicleId = nextTaxiRequestId = nextJourneyId = nextPassengerId = 1; return; }
        ValidateNextId(checkpoint.NextStopId, checkpoint.Stops.Select(static x => x.Id.Value), "Transit stop");
        ValidateNextId(checkpoint.NextLineId, checkpoint.Lines.Select(static x => x.Id.Value), "Transit line");
        ValidateNextId(checkpoint.NextPatternId, checkpoint.Patterns.Select(static x => x.Id.Value), "Transit pattern");
        ValidateNextId(checkpoint.NextTripId, checkpoint.Trips.Select(static x => x.Id.Value), "Transit trip");
        ValidateNextId(checkpoint.NextVehicleId, checkpoint.Vehicles.Select(static x => x.Id.Value), "Transit vehicle");
        ValidateNextId(checkpoint.NextTaxiRequestId, checkpoint.TaxiRequests.Select(static x => x.Id.Value), "Taxi request");
        ValidateNextId(checkpoint.NextJourneyId, checkpoint.Journeys.Select(static x => x.Id.Value), "Journey");
        ValidateNextId(checkpoint.NextPassengerId, checkpoint.Passengers.Select(static x => x.Id.Value), "Passenger");
        foreach (var item in checkpoint.Stops) { stops.Add(item.Id, item); IndexStop(item); }
        foreach (var item in checkpoint.Lines) lines.Add(item.Id, item);
        foreach (var item in checkpoint.Patterns)
        {
            var pattern = new TransitServicePatternSnapshot(item.Id, item.LineId, Array.AsReadOnly(item.Stops.ToArray()), item.RailwayServiceId);
            patterns.Add(item.Id, pattern);
            IndexPattern(pattern, lines[item.LineId].Mode);
        }
        foreach (var item in checkpoint.Trips) trips.Add(item.Id, item);
        foreach (var item in checkpoint.Vehicles)
        {
            var state = new TransitVehicleState(item.Id, item.Kind, item.TripId, item.Position) { RoadVehicleId = item.RoadVehicleId, StopIndex = item.StopIndex, State = item.State, EstimatedArrivalTick = item.EstimatedArrivalTick, DwellUntilTick = item.DwellUntilTick };
            vehicles.Add(item.Id, state);
        }
        foreach (var item in checkpoint.TaxiRequests)
        {
            if (item.State == TaxiRequestState.Completed) continue;
            var state = new TaxiRequestStateData(item.Id, item.TripRequestId, item.Pickup, item.DropOff, item.RequestedTick) { State = item.State, AssignedVehicleId = item.AssignedVehicleId, PickupTick = item.PickupTick, CompletedTick = item.CompletedTick };
            taxiRequests.Add(item.Id, state);
            if (!taxiRequestByTrip.TryAdd(item.TripRequestId, item.Id)) throw new ArgumentException($"Trip Request {item.TripRequestId.Value} has duplicate Taxi requests.", nameof(checkpoint));
            if (item.AssignedVehicleId is { } vehicleId && item.State is (TaxiRequestState.Assigned or TaxiRequestState.PickingUp or TaxiRequestState.Riding) && vehicles.TryGetValue(vehicleId, out var vehicle)) vehicle.ActiveTaxiRequestId = item.Id;
        }
        foreach (var item in checkpoint.Journeys) journeys.Add(item.Id, new JourneySnapshot(item.Id, item.TripRequestId, item.DepartureTick, item.EstimatedArrivalTick, Array.AsReadOnly(item.Legs.ToArray())));
        var orphanJourneys = new HashSet<JourneyId>();
        foreach (var item in checkpoint.Passengers)
        {
            if (item.State == PassengerState.Arrived)
            {
                orphanJourneys.Add(item.JourneyId);
                continue;
            }
            passengers.Add(item.Id, new PassengerStateData(item.Id, item.TripRequestId, item.JourneyId, item.StateEnteredTick) { LegIndex = item.LegIndex, State = item.State });
            if (!passengerByTrip.TryAdd(item.TripRequestId, item.Id) || taxiRequestByTrip.ContainsKey(item.TripRequestId))
                throw new ArgumentException($"Trip Request {item.TripRequestId.Value} has duplicate active multimodal records.", nameof(checkpoint));
        }
        foreach (var journeyId in orphanJourneys) journeys.Remove(journeyId);
        nextStopId = checkpoint.NextStopId; nextLineId = checkpoint.NextLineId; nextPatternId = checkpoint.NextPatternId; nextTripId = checkpoint.NextTripId; nextVehicleId = checkpoint.NextVehicleId; nextTaxiRequestId = checkpoint.NextTaxiRequestId; nextJourneyId = checkpoint.NextJourneyId; nextPassengerId = checkpoint.NextPassengerId;
    }

    private void IndexStop(TransitStopSnapshot stop)
    {
        var cell = SpatialGrid.ToCell(stop.Position, TransferSpatialCellSizeMeters);
        if (!stopSpatialIndex.TryGetValue(cell, out var cellStops))
        {
            cellStops = [];
            stopSpatialIndex.Add(cell, cellStops);
        }
        cellStops.Add(stop.Id);
    }

    private void IndexPattern(TransitServicePatternSnapshot pattern, TransitMode mode)
    {
        for (var index = 0; index < pattern.Stops.Count - 1; index++)
        {
            var current = pattern.Stops[index];
            var next = pattern.Stops[index + 1];
            if (!outgoingPatternEdges.TryGetValue(current.StopId, out var edges))
            {
                edges = [];
                outgoingPatternEdges.Add(current.StopId, edges);
            }
            edges.Add(new TransitPatternEdge(
                pattern.Id,
                index,
                next.StopId,
                mode,
                pattern.LineId,
                pattern.RailwayServiceId,
                checked(next.TravelTicksFromPrevious + current.DwellTicks)));
        }
    }

    private static double DistanceSquared(WorldPoint left, WorldPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        var dz = left.Z - right.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static void ValidateNextId(ulong nextId, IEnumerable<ulong> ids, string name)
    {
        if (nextId == 0) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next {name} ID must be greater than zero.");
        var maximum = ids.Any() ? ids.Max() : 0UL;
        if (nextId <= maximum) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next {name} ID must be greater than every stored ID.");
    }
    private static void EnsureCapacity(ulong nextId, string name) { if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted."); }
}

internal readonly record struct TransitPatternEdge(
    TransitServicePatternId PatternId,
    int PatternStopIndex,
    TransitStopId ToStopId,
    TransitMode Mode,
    TransitLineId LineId,
    RailwayServiceId? RailwayServiceId,
    ulong DurationTicks);

internal sealed class TransitVehicleState(TransitVehicleId id, TransitVehicleKind kind, TransitTripId? tripId, WorldPoint position)
{
    public TransitVehicleId Id { get; } = id; public TransitVehicleKind Kind { get; } = kind; public TransitTripId? TripId { get; } = tripId; public VehicleId? RoadVehicleId { get; set; } public TaxiRequestId? ActiveTaxiRequestId { get; set; } public int StopIndex { get; set; } public WorldPoint Position { get; set; } = position; public TransitVehicleMovementState State { get; set; } = TransitVehicleMovementState.Idle; public ulong EstimatedArrivalTick { get; set; } public ulong DwellUntilTick { get; set; }
    public TransitVehicleSnapshot ToSnapshot(ulong tickCount) => new(Id, Kind, TripId, RoadVehicleId, StopIndex, Position, State, EstimatedArrivalTick, DwellUntilTick, tickCount);
}
internal sealed class TaxiRequestStateData(TaxiRequestId id, TripRequestId tripRequestId, WorldPoint pickup, WorldPoint dropOff, ulong requestedTick)
{
    public TaxiRequestId Id { get; } = id; public TripRequestId TripRequestId { get; } = tripRequestId; public WorldPoint Pickup { get; } = pickup; public WorldPoint DropOff { get; } = dropOff; public TaxiRequestState State { get; set; } = TaxiRequestState.Requested; public TransitVehicleId? AssignedVehicleId { get; set; } public ulong RequestedTick { get; } = requestedTick; public ulong PickupTick { get; set; } public ulong CompletedTick { get; set; }
    public TaxiRequestSnapshot ToSnapshot() => new(Id, TripRequestId, Pickup, DropOff, State, AssignedVehicleId, RequestedTick, PickupTick, CompletedTick);
}
internal sealed class PassengerStateData(PassengerId id, TripRequestId tripRequestId, JourneyId journeyId, ulong tickCount)
{
    public PassengerId Id { get; } = id; public TripRequestId TripRequestId { get; } = tripRequestId; public JourneyId JourneyId { get; } = journeyId; public int LegIndex { get; set; } public PassengerState State { get; set; } = PassengerState.Waiting; public ulong StateEnteredTick { get; set; } = tickCount;
    public PassengerSnapshot ToSnapshot(ulong tickCount) => new(Id, TripRequestId, JourneyId, LegIndex, State, StateEnteredTick, tickCount);
}
