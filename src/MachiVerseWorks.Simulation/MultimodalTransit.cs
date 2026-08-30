using System.Globalization;

namespace MachiVerseWorks.Simulation;

public readonly record struct TransitStopId(ulong Value) { public override string ToString() => Value.ToString(CultureInfo.InvariantCulture); }
public readonly record struct TransitLineId(ulong Value) { public override string ToString() => Value.ToString(CultureInfo.InvariantCulture); }
public readonly record struct TransitServicePatternId(ulong Value) { public override string ToString() => Value.ToString(CultureInfo.InvariantCulture); }
public readonly record struct TransitTripId(ulong Value) { public override string ToString() => Value.ToString(CultureInfo.InvariantCulture); }
public readonly record struct TransitVehicleId(ulong Value) { public override string ToString() => Value.ToString(CultureInfo.InvariantCulture); }
public readonly record struct TaxiRequestId(ulong Value) { public override string ToString() => Value.ToString(CultureInfo.InvariantCulture); }
public readonly record struct JourneyId(ulong Value) { public override string ToString() => Value.ToString(CultureInfo.InvariantCulture); }
public readonly record struct PassengerId(ulong Value) { public override string ToString() => Value.ToString(CultureInfo.InvariantCulture); }

public enum TransitMode : byte { Walk = 0, Bus = 1, Railway = 2, Taxi = 3 }
public enum TransitStopKind : byte { Bus = 0, Railway = 1 }
public enum TransitVehicleKind : byte { Bus = 0, Taxi = 1 }
public enum TransitVehicleMovementState : byte
{
    Idle = 0,
    AwaitingDeparture = 1,
    EnRouteToStop = 2,
    Dwelling = 3,
    EnRouteToPickup = 4,
    EnRouteToDropOff = 5,
    Completed = 6,
}
public enum TaxiRequestState : byte { Requested = 0, Assigned = 1, PickingUp = 2, Riding = 3, Completed = 4, Failed = 5 }
public enum PassengerState : byte { Waiting = 0, Boarding = 1, Riding = 2, Transfer = 3, Alighting = 4, Arrived = 5 }

public readonly record struct TransitStopSnapshot(
    TransitStopId Id,
    TransitStopKind Kind,
    WorldPoint Position,
    LaneId? LaneId = null,
    StationId? StationId = null,
    PlatformId? PlatformId = null);

public readonly record struct TransitLineSnapshot(TransitLineId Id, TransitMode Mode);

public readonly record struct TransitPatternStopSnapshot(
    TransitStopId StopId,
    ulong TravelTicksFromPrevious,
    ulong DwellTicks);

public sealed record TransitServicePatternSnapshot(
    TransitServicePatternId Id,
    TransitLineId LineId,
    IReadOnlyList<TransitPatternStopSnapshot> Stops);

public readonly record struct TransitTripSnapshot(
    TransitTripId Id,
    TransitServicePatternId PatternId,
    ulong PlannedStartTick,
    TransitVehicleId? VehicleId);

public readonly record struct TransitVehicleSnapshot(
    TransitVehicleId Id,
    TransitVehicleKind Kind,
    TransitTripId? TripId,
    VehicleId? RoadVehicleId,
    int StopIndex,
    WorldPoint Position,
    TransitVehicleMovementState State,
    ulong EstimatedArrivalTick,
    ulong DwellUntilTick,
    ulong TickCount);

public readonly record struct TaxiRequestSnapshot(
    TaxiRequestId Id,
    TripRequestId TripRequestId,
    WorldPoint Pickup,
    WorldPoint DropOff,
    TaxiRequestState State,
    TransitVehicleId? AssignedVehicleId,
    ulong RequestedTick,
    ulong PickupTick,
    ulong CompletedTick);

public readonly record struct JourneyLegSnapshot(
    TransitMode Mode,
    TripEndpoint? OriginEndpoint,
    TripEndpoint? DestinationEndpoint,
    TransitStopId? FromStopId,
    TransitStopId? ToStopId,
    TransitLineId? LineId,
    RailwayServiceId? RailwayServiceId,
    ulong EstimatedDurationTicks,
    ulong TransferTicks = 0);

public sealed record JourneySnapshot(
    JourneyId Id,
    TripRequestId TripRequestId,
    ulong DepartureTick,
    ulong EstimatedArrivalTick,
    IReadOnlyList<JourneyLegSnapshot> Legs);

public readonly record struct PassengerSnapshot(
    PassengerId Id,
    TripRequestId TripRequestId,
    JourneyId JourneyId,
    int LegIndex,
    PassengerState State,
    ulong StateEnteredTick,
    ulong TickCount);

public readonly record struct ModeChoiceDecision(
    TransitMode Mode,
    ulong EstimatedDurationTicks,
    JourneyId? JourneyId = null,
    TaxiRequestId? TaxiRequestId = null);

public sealed record MultimodalTransitSnapshot(
    TransitStopSnapshot[] Stops,
    TransitLineSnapshot[] Lines,
    TransitServicePatternSnapshot[] Patterns,
    TransitTripSnapshot[] Trips,
    TransitVehicleSnapshot[] Vehicles,
    TaxiRequestSnapshot[] TaxiRequests,
    JourneySnapshot[] Journeys,
    PassengerSnapshot[] Passengers);
