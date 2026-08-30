namespace MachiVerseWorks.Protocol;

public enum ProtocolTransitMode : byte { Walk = 0, Bus = 1, Railway = 2, Taxi = 3, Motor = 4 }
public enum ProtocolTransitStopKind : byte { Bus = 0, Railway = 1 }
public enum ProtocolTransitVehicleKind : byte { Bus = 0, Taxi = 1 }
public enum ProtocolTransitVehicleState : byte { Idle = 0, AwaitingDeparture = 1, EnRouteToStop = 2, Dwelling = 3, EnRouteToPickup = 4, EnRouteToDropOff = 5, Completed = 6 }

public readonly record struct ProtocolTransitLine(ulong Id, ProtocolTransitMode Mode);
public readonly record struct ProtocolTransitStop(ulong Id, ProtocolTransitStopKind Kind, double X, double Y, double Z, ulong LaneId, ulong StationId, ulong PlatformId);
public readonly record struct ProtocolTransitPatternStop(ulong StopId, ulong TravelTicksFromPrevious, ulong DwellTicks);
public sealed record ProtocolTransitPattern(ulong Id, ulong LineId, ulong RailwayServiceId, IReadOnlyList<ProtocolTransitPatternStop> Stops);
public readonly record struct ProtocolTransitVehicle(
    ulong Id, ProtocolTransitVehicleKind Kind, ulong TripId, ulong RoadVehicleId, int StopIndex,
    double X, double Y, double Z, ProtocolTransitVehicleState State, ulong EstimatedArrivalTick, ulong DwellUntilTick);
public readonly record struct ProtocolTransitArrivalEstimate(ulong StopId, ulong LineId, ulong VehicleId, ulong EstimatedArrivalTick);

public sealed record MultimodalTransitSnapshotMessage(
    ulong TickCount,
    IReadOnlyList<ProtocolTransitLine> Lines,
    IReadOnlyList<ProtocolTransitStop> Stops,
    IReadOnlyList<ProtocolTransitPattern> Patterns,
    IReadOnlyList<ProtocolTransitVehicle> Vehicles,
    IReadOnlyList<ProtocolTransitArrivalEstimate> ArrivalEstimates) : IProtocolMessage
{
    public MessageType Type => MessageType.MultimodalTransitSnapshot;
}
