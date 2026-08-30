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
    public SavePedestrianCrossingData?[]? PedestrianCrossings { get; init; }
    public ulong? NextVehicleId { get; init; }
    public SaveVehicleData?[]? Vehicles { get; init; }
    public ulong? NextHouseholdId { get; init; }
    public SaveHouseholdData?[]? Households { get; init; }
    public ulong? NextPersonId { get; init; }
    public SavePersonData?[]? Persons { get; init; }
    public ulong? NextTripRequestId { get; init; }
    public ulong? NextTrackNodeId { get; init; }
    public SaveTrackNodeData?[]? TrackNodes { get; init; }
    public ulong? NextTrackSegmentId { get; init; }
    public SaveTrackSegmentData?[]? TrackSegments { get; init; }
    public ulong? NextTrackConnectionId { get; init; }
    public SaveTrackConnectionData?[]? TrackConnections { get; init; }
    public ulong? NextBlockSectionId { get; init; }
    public SaveBlockSectionData?[]? BlockSections { get; init; }
    public ulong? NextStationId { get; init; }
    public SaveStationData?[]? Stations { get; init; }
    public ulong? NextPlatformId { get; init; }
    public SavePlatformData?[]? Platforms { get; init; }
    public ulong? NextPlatformAccessPointId { get; init; }
    public SavePlatformAccessPointData?[]? PlatformAccessPoints { get; init; }
    public ulong? NextDepotId { get; init; }
    public SaveDepotData?[]? Depots { get; init; }
    public SaveRailwayOperationsData? RailwayOperations { get; init; }
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

internal sealed class SavePedestrianCrossingData
{
    public ulong? Id { get; init; }
    public bool? IsOpen { get; init; }
}

internal sealed class SaveVehicleData
{
    public ulong? Id { get; init; }
    public double? LengthMeters { get; init; }
    public double? WidthMeters { get; init; }
    public double? HeightMeters { get; init; }
    public double? MaximumSpeedMetersPerSecond { get; init; }
    public double? MaximumAccelerationMetersPerSecondSquared { get; init; }
    public double? ComfortableDecelerationMetersPerSecondSquared { get; init; }
    public double? MinimumGapMeters { get; init; }
    public double? TimeHeadwaySeconds { get; init; }
    public SaveVehicleRouteStepData?[]? RouteSteps { get; init; }
    public int? RouteStepIndex { get; init; }
    public double? RouteProgressMeters { get; init; }
    public double? SpeedMetersPerSecond { get; init; }
    public byte? State { get; init; }
}

internal sealed class SaveVehicleRouteStepData
{
    public ulong? LaneId { get; init; }
    public ulong? SegmentId { get; init; }
    public double? StartSegmentOffset { get; init; }
    public double? EndSegmentOffset { get; init; }
    public double? DistanceMeters { get; init; }
    public double? EstimatedTravelTimeSeconds { get; init; }
    public required ulong? ExitConnectionId { get; init; }
}

internal sealed class SaveHouseholdData
{
    public ulong? Id { get; init; }
    public required ulong? ResidenceBuildingId { get; init; }
    public required ulong? ResidencePoiId { get; init; }
}

internal sealed class SavePersonData
{
    public ulong? Id { get; init; }
    public ulong? HouseholdId { get; init; }
    public int? AgeYears { get; init; }
    public bool? IsEmployed { get; init; }
    public bool? IsStudent { get; init; }
    public bool? HasPrivateVehicle { get; init; }
    public required ulong? ResidenceBuildingId { get; init; }
    public required ulong? ResidencePoiId { get; init; }
    public required ulong? CurrentBuildingId { get; init; }
    public required ulong? CurrentPoiId { get; init; }
    public byte? CurrentActivity { get; init; }
    public byte? TravelState { get; init; }
    public required ulong? DestinationBuildingId { get; init; }
    public required ulong? DestinationPoiId { get; init; }
    public byte? DestinationActivity { get; init; }
    public ulong? ActiveTripRequestId { get; init; }
    public byte? ActiveTravelMode { get; init; }
    public ulong? PedestrianId { get; init; }
    public ulong? VehicleId { get; init; }
    public SaveDailyActivityWindowData?[]? Schedule { get; init; }
    public SavePersonNeedData?[]? Needs { get; init; }
}

internal sealed class SaveDailyActivityWindowData
{
    public byte? Activity { get; init; }
    public ushort? StartMinuteOfDay { get; init; }
    public ushort? EndMinuteOfDay { get; init; }
    public required ulong? DestinationBuildingId { get; init; }
    public required ulong? DestinationPoiId { get; init; }
    public byte? Priority { get; init; }
}

internal sealed class SavePersonNeedData
{
    public byte? Kind { get; init; }
    public double? Satisfaction { get; init; }
    public double? DecayPerHour { get; init; }
}

internal sealed class SaveTrackNodeData
{
    public ulong? Id { get; init; }
    public byte? Kind { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
}

internal sealed class SaveTrackSegmentData
{
    public ulong? Id { get; init; }
    public ulong? StartNodeId { get; init; }
    public ulong? EndNodeId { get; init; }
    public byte? Direction { get; init; }
    public double? GaugeMeters { get; init; }
    public double? SpeedLimitMetersPerSecond { get; init; }
    public byte? Electrification { get; init; }
    public byte? Usage { get; init; }
}

internal sealed class SaveTrackConnectionData
{
    public ulong? Id { get; init; }
    public ulong? FromSegmentId { get; init; }
    public ulong? ToSegmentId { get; init; }
    public ulong? ViaNodeId { get; init; }
}

internal sealed class SaveBlockSectionData
{
    public ulong? Id { get; init; }
    public ulong?[]? SegmentIds { get; init; }
}

internal sealed class SaveStationData
{
    public ulong? Id { get; init; }
    public double? MinX { get; init; }
    public double? MinY { get; init; }
    public double? MinZ { get; init; }
    public double? MaxX { get; init; }
    public double? MaxY { get; init; }
    public double? MaxZ { get; init; }
}

internal sealed class SavePlatformData
{
    public ulong? Id { get; init; }
    public ulong? StationId { get; init; }
    public ulong? TrackSegmentId { get; init; }
    public double? StartSegmentOffset { get; init; }
    public double? EndSegmentOffset { get; init; }
    public double? MinX { get; init; }
    public double? MinY { get; init; }
    public double? MinZ { get; init; }
    public double? MaxX { get; init; }
    public double? MaxY { get; init; }
    public double? MaxZ { get; init; }
}

internal sealed class SavePlatformAccessPointData
{
    public ulong? Id { get; init; }
    public ulong? PlatformId { get; init; }
    public ulong? RoadAccessPointId { get; init; }
}

internal sealed class SaveDepotData
{
    public ulong? Id { get; init; }
    public double? MinX { get; init; }
    public double? MinY { get; init; }
    public double? MinZ { get; init; }
    public double? MaxX { get; init; }
    public double? MaxY { get; init; }
    public double? MaxZ { get; init; }
    public ulong?[]? TrackSegmentIds { get; init; }
}
