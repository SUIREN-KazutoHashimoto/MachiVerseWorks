namespace MachiVerseWorks.Protocol;

public readonly record struct ProtocolTrainState(
    ulong Id,
    ulong FormationId,
    ulong ServiceId,
    ulong RouteId,
    double X,
    double Y,
    double Z,
    double ForwardX,
    double ForwardY,
    double ForwardZ,
    double SpeedMetersPerSecond,
    byte State,
    ulong CurrentBlockId,
    ulong CurrentPlatformId,
    ulong AssignedPlatformId,
    ulong CurrentDepotId,
    ulong DwellDepartureTick);

public readonly record struct ProtocolRailwayServiceState(
    ulong Id,
    ulong FormationId,
    ulong RouteId,
    ulong TimetableId,
    ulong OriginDepotId,
    ulong DestinationDepotId,
    ulong PlannedStartTick,
    byte State,
    ulong DelayTicks,
    int NextStopIndex,
    ulong TrainId);

public readonly record struct ProtocolTimetableStop(
    ulong StationId,
    ulong PlannedArrivalTick,
    ulong PlannedDepartureTick,
    ulong MinimumDwellTicks,
    ulong PreferredPlatformId);

public sealed record ProtocolTimetable(ulong Id, IReadOnlyList<ProtocolTimetableStop> Stops);

public sealed record RailwayOperationsSnapshotMessage(
    ulong TickCount,
    IReadOnlyList<ProtocolTrainState> Trains,
    IReadOnlyList<ProtocolRailwayServiceState> Services,
    IReadOnlyList<ProtocolTimetable> Timetables) : IProtocolMessage
{
    public MessageType Type => MessageType.RailwayOperationsSnapshot;
}
