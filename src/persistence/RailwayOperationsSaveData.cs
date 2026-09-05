namespace MachiVerseWorks.Persistence;

internal sealed class SaveRailwayOperationsData
{
    public ulong? NextFormationId { get; init; }
    public SaveTrainFormationData?[]? Formations { get; init; }
    public ulong? NextRouteId { get; init; }
    public SaveRailwayRouteData?[]? Routes { get; init; }
    public ulong? NextTimetableId { get; init; }
    public SaveTimetableData?[]? Timetables { get; init; }
    public ulong? NextServiceId { get; init; }
    public SaveRailwayServiceData?[]? Services { get; init; }
    public ulong? NextTrainId { get; init; }
    public SaveTrainData?[]? Trains { get; init; }
}

internal sealed class SaveTrainFormationData
{
    public ulong? Id { get; init; }
    public double? LengthMeters { get; init; }
    public double? MaximumSpeedMetersPerSecond { get; init; }
    public double? MaximumAccelerationMetersPerSecondSquared { get; init; }
    public double? ServiceDecelerationMetersPerSecondSquared { get; init; }
    public int? Capacity { get; init; }
}

internal sealed class SaveRailwayRouteData
{
    public ulong? Id { get; init; }
    public ulong?[]? TrackSegmentIds { get; init; }
    public double? LengthMeters { get; init; }
}

internal sealed class SaveTimetableData
{
    public ulong? Id { get; init; }
    public SaveTimetableStopData?[]? Stops { get; init; }
}

internal sealed class SaveTimetableStopData
{
    public ulong? StationId { get; init; }
    public ulong? PlannedArrivalTick { get; init; }
    public ulong? PlannedDepartureTick { get; init; }
    public ulong? MinimumDwellTicks { get; init; }
    public required ulong? PreferredPlatformId { get; init; }
}

internal sealed class SaveRailwayServiceData
{
    public ulong? Id { get; init; }
    public ulong? FormationId { get; init; }
    public ulong? RouteId { get; init; }
    public ulong? TimetableId { get; init; }
    public ulong? OriginDepotId { get; init; }
    public ulong? DestinationDepotId { get; init; }
    public ulong? PlannedStartTick { get; init; }
    public byte? State { get; init; }
    public ulong? DelayTicks { get; init; }
    public int? NextStopIndex { get; init; }
    public required ulong? TrainId { get; init; }
}

internal sealed class SaveTrainData
{
    public ulong? Id { get; init; }
    public ulong? FormationId { get; init; }
    public ulong? ServiceId { get; init; }
    public ulong? RouteId { get; init; }
    public double? RouteDistanceMeters { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public double? ForwardX { get; init; }
    public double? ForwardY { get; init; }
    public double? ForwardZ { get; init; }
    public double? SpeedMetersPerSecond { get; init; }
    public byte? State { get; init; }
    public required ulong? CurrentBlockId { get; init; }
    public required ulong? CurrentPlatformId { get; init; }
    public required ulong? AssignedPlatformId { get; init; }
    public required ulong? CurrentDepotId { get; init; }
    public ulong? DwellDepartureTick { get; init; }
    public ulong? TickCount { get; init; }
}
