namespace MachiVerseWorks.Simulation;

public sealed record SimulationCheckpoint(
    int TickRate,
    ulong Seed,
    double SpatialCellSize,
    ulong TickCount,
    long ElapsedTicks,
    ulong RandomState,
    ulong NextAgentId,
    IReadOnlyList<SimulationAgentCheckpoint> Agents,
    ulong NextBuildingId,
    IReadOnlyList<SimulationBuildingCheckpoint> Buildings,
    ulong NextPoiId,
    IReadOnlyList<SimulationPoiCheckpoint> Pois,
    ulong NextRoadNodeId,
    IReadOnlyList<SimulationRoadNodeCheckpoint> RoadNodes,
    ulong NextRoadSegmentId,
    IReadOnlyList<SimulationRoadSegmentCheckpoint> RoadSegments,
    ulong NextLaneId,
    IReadOnlyList<SimulationLaneCheckpoint> Lanes,
    ulong NextLaneConnectionId,
    IReadOnlyList<SimulationLaneConnectionCheckpoint> LaneConnections,
    ulong NextRoadAccessPointId,
    IReadOnlyList<SimulationRoadAccessPointCheckpoint> RoadAccessPoints,
    ulong NextPedestrianId = 1,
    IReadOnlyList<SimulationPedestrianCheckpoint>? Pedestrians = null);

public readonly record struct SimulationAgentCheckpoint(AgentId Id, WorldPoint Position, WorldVector Velocity, bool IsActive);
public readonly record struct SimulationBuildingCheckpoint(BuildingId Id, BuildingKind Kind, WorldVolume Bounds);
public readonly record struct SimulationPoiCheckpoint(PoiId Id, PoiKind Kind, WorldPoint Position, BuildingId? BuildingId);
public readonly record struct SimulationRoadNodeCheckpoint(RoadNodeId Id, RoadNodeKind Kind, WorldPoint Position);
public readonly record struct SimulationRoadSegmentCheckpoint(RoadSegmentId Id, RoadKind Kind, RoadNodeId StartNodeId, RoadNodeId EndNodeId);
public readonly record struct SimulationLaneCheckpoint(LaneId Id, RoadSegmentId SegmentId, LaneDirection Direction, ushort Order, double WidthMeters, double SpeedLimitMetersPerSecond);
public readonly record struct SimulationLaneConnectionCheckpoint(LaneConnectionId Id, LaneId FromLaneId, LaneId ToLaneId, RoadNodeId ViaNodeId, TurnMovement Movement);
public readonly record struct SimulationRoadAccessPointCheckpoint(RoadAccessPointId Id, RoadSegmentId SegmentId, double SegmentOffset, BuildingId? BuildingId, PoiId? PoiId, RoadAccessMode Mode);
public readonly record struct SimulationPedestrianCheckpoint(
    PedestrianId Id,
    TripRequestId TripRequestId,
    TripEndpoint Origin,
    TripEndpoint Destination,
    TravelMode Mode,
    double WalkingSpeedMetersPerSecond,
    int LegIndex,
    double ProgressMeters,
    PedestrianMovementState State);