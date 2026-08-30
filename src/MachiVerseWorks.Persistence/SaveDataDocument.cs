namespace MachiVerseWorks.Persistence;

internal sealed class SaveDataDocument
{
    public int? FormatVersion { get; init; }
    public SaveSimulationData? Simulation { get; init; }
}

internal sealed class SaveSimulationData
{
    public int? TickRate { get; init; }
    public ulong? Seed { get; init; }
    public double? SpatialCellSize { get; init; }
    public ulong? TickCount { get; init; }
    public long? ElapsedTicks { get; init; }
    public ulong? RandomState { get; init; }
    public ulong? NextAgentId { get; init; }
    public SaveAgentData?[]? Agents { get; init; }
    public ulong? NextBuildingId { get; init; }
    public SaveBuildingData?[]? Buildings { get; init; }
    public ulong? NextPoiId { get; init; }
    public SavePoiData?[]? Pois { get; init; }
    public ulong? NextRoadNodeId { get; init; }
    public SaveRoadNodeData?[]? RoadNodes { get; init; }
    public ulong? NextRoadSegmentId { get; init; }
    public SaveRoadSegmentData?[]? RoadSegments { get; init; }
    public ulong? NextLaneId { get; init; }
    public SaveLaneData?[]? Lanes { get; init; }
    public ulong? NextLaneConnectionId { get; init; }
    public SaveLaneConnectionData?[]? LaneConnections { get; init; }
    public ulong? NextRoadAccessPointId { get; init; }
    public SaveRoadAccessPointData?[]? RoadAccessPoints { get; init; }
    public ulong? NextPedestrianId { get; init; }
    public SavePedestrianData?[]? Pedestrians { get; init; }
}

internal sealed class SaveAgentData
{
    public ulong? Id { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public double? VelocityX { get; init; }
    public double? VelocityY { get; init; }
    public double? VelocityZ { get; init; }
    public bool? IsActive { get; init; }
}

internal sealed class SaveBuildingData
{
    public ulong? Id { get; init; }
    public byte? Kind { get; init; }
    public double? MinX { get; init; }
    public double? MinY { get; init; }
    public double? MinZ { get; init; }
    public double? MaxX { get; init; }
    public double? MaxY { get; init; }
    public double? MaxZ { get; init; }
}

internal sealed class SavePoiData
{
    public ulong? Id { get; init; }
    public byte? Kind { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public required ulong? BuildingId { get; init; }
}

internal sealed class SaveRoadNodeData
{
    public ulong? Id { get; init; }
    public byte? Kind { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
}

internal sealed class SaveRoadSegmentData
{
    public ulong? Id { get; init; }
    public byte? Kind { get; init; }
    public ulong? StartNodeId { get; init; }
    public ulong? EndNodeId { get; init; }
}

internal sealed class SaveLaneData
{
    public ulong? Id { get; init; }
    public ulong? SegmentId { get; init; }
    public byte? Direction { get; init; }
    public ushort? Order { get; init; }
    public double? WidthMeters { get; init; }
    public double? SpeedLimitMetersPerSecond { get; init; }
}

internal sealed class SaveLaneConnectionData
{
    public ulong? Id { get; init; }
    public ulong? FromLaneId { get; init; }
    public ulong? ToLaneId { get; init; }
    public ulong? ViaNodeId { get; init; }
    public byte? Movement { get; init; }
}

internal sealed class SaveRoadAccessPointData
{
    public ulong? Id { get; init; }
    public ulong? SegmentId { get; init; }
    public double? SegmentOffset { get; init; }
    public required ulong? BuildingId { get; init; }
    public required ulong? PoiId { get; init; }
    public byte? Mode { get; init; }
}

internal sealed class SavePedestrianData
{
    public ulong? Id { get; init; }
    public ulong? TripRequestId { get; init; }
    public required ulong? OriginBuildingId { get; init; }
    public required ulong? OriginPoiId { get; init; }
    public required ulong? DestinationBuildingId { get; init; }
    public required ulong? DestinationPoiId { get; init; }
    public byte? Mode { get; init; }
    public double? WalkingSpeedMetersPerSecond { get; init; }
    public int? LegIndex { get; init; }
    public double? ProgressMeters { get; init; }
    public byte? State { get; init; }
}
