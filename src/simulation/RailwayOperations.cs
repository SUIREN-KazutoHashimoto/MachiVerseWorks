using System.Globalization;

namespace MachiVerseWorks.Simulation;

public readonly record struct TrainFormationId(ulong Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct RailwayRouteId(ulong Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct TimetableId(ulong Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct RailwayServiceId(ulong Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct TrainId(ulong Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public enum RailwayServiceState : byte
{
    Planned = 0,
    Active = 1,
    Completed = 2,
}

public enum TrainMovementState : byte
{
    InDepot = 0,
    WaitingForBlock = 1,
    Running = 2,
    ApproachingStation = 3,
    Dwelling = 4,
    Completed = 5,
}

public sealed record TrainFormationSnapshot(
    TrainFormationId Id,
    double LengthMeters,
    double MaximumSpeedMetersPerSecond,
    double MaximumAccelerationMetersPerSecondSquared,
    double ServiceDecelerationMetersPerSecondSquared,
    int Capacity);

public sealed record RailwayRouteSnapshot(
    RailwayRouteId Id,
    IReadOnlyList<TrackSegmentId> TrackSegmentIds,
    double LengthMeters);

public readonly record struct TimetableStopSnapshot(
    StationId StationId,
    ulong PlannedArrivalTick,
    ulong PlannedDepartureTick,
    ulong MinimumDwellTicks,
    PlatformId? PreferredPlatformId = null);

public sealed record TimetableSnapshot(
    TimetableId Id,
    IReadOnlyList<TimetableStopSnapshot> Stops);

public sealed record RailwayServiceSnapshot(
    RailwayServiceId Id,
    TrainFormationId FormationId,
    RailwayRouteId RouteId,
    TimetableId TimetableId,
    DepotId OriginDepotId,
    DepotId DestinationDepotId,
    ulong PlannedStartTick,
    RailwayServiceState State,
    ulong DelayTicks,
    int NextStopIndex,
    TrainId? TrainId);

public sealed record TrainSnapshot(
    TrainId Id,
    TrainFormationId FormationId,
    RailwayServiceId ServiceId,
    RailwayRouteId RouteId,
    double RouteDistanceMeters,
    WorldPoint Position,
    WorldVector Forward,
    double SpeedMetersPerSecond,
    TrainMovementState State,
    BlockSectionId? CurrentBlockId,
    PlatformId? CurrentPlatformId,
    PlatformId? AssignedPlatformId,
    DepotId? CurrentDepotId,
    ulong DwellDepartureTick,
    ulong TickCount);

public sealed record RailwayOperationsSnapshot(
    TrainFormationSnapshot[] Formations,
    RailwayRouteSnapshot[] Routes,
    TimetableSnapshot[] Timetables,
    RailwayServiceSnapshot[] Services,
    TrainSnapshot[] Trains);

public sealed record RailwayOperationsValidationResult(bool IsValid, IReadOnlyList<string> Errors);
