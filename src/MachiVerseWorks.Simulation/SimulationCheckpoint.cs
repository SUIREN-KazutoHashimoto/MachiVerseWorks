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
    IReadOnlyList<SimulationPedestrianCheckpoint>? Pedestrians = null,
    IReadOnlyList<SimulationPedestrianCrossingCheckpoint>? PedestrianCrossings = null,
    ulong NextVehicleId = 1,
    IReadOnlyList<SimulationVehicleCheckpoint>? Vehicles = null,
    ulong NextHouseholdId = 1,
    IReadOnlyList<SimulationHouseholdCheckpoint>? Households = null,
    ulong NextPersonId = 1,
    IReadOnlyList<SimulationPersonCheckpoint>? Persons = null,
    ulong NextTripRequestId = 1,
    ulong NextTrackNodeId = 1,
    IReadOnlyList<SimulationTrackNodeCheckpoint>? TrackNodes = null,
    ulong NextTrackSegmentId = 1,
    IReadOnlyList<SimulationTrackSegmentCheckpoint>? TrackSegments = null,
    ulong NextTrackConnectionId = 1,
    IReadOnlyList<SimulationTrackConnectionCheckpoint>? TrackConnections = null,
    ulong NextBlockSectionId = 1,
    IReadOnlyList<SimulationBlockSectionCheckpoint>? BlockSections = null,
    ulong NextStationId = 1,
    IReadOnlyList<SimulationStationCheckpoint>? Stations = null,
    ulong NextPlatformId = 1,
    IReadOnlyList<SimulationPlatformCheckpoint>? Platforms = null,
    ulong NextPlatformAccessPointId = 1,
    IReadOnlyList<SimulationPlatformAccessPointCheckpoint>? PlatformAccessPoints = null,
    ulong NextDepotId = 1,
    IReadOnlyList<SimulationDepotCheckpoint>? Depots = null,
    ulong NextTrainFormationId = 1,
    IReadOnlyList<TrainFormationSnapshot>? TrainFormations = null,
    ulong NextRailwayRouteId = 1,
    IReadOnlyList<RailwayRouteSnapshot>? RailwayRoutes = null,
    ulong NextTimetableId = 1,
    IReadOnlyList<TimetableSnapshot>? Timetables = null,
    ulong NextRailwayServiceId = 1,
    IReadOnlyList<RailwayServiceSnapshot>? RailwayServices = null,
    ulong NextTrainId = 1,
    IReadOnlyList<TrainSnapshot>? Trains = null,
    MultimodalTransitCheckpoint? MultimodalTransit = null);

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
public readonly record struct SimulationPedestrianCrossingCheckpoint(PedestrianCrossingId Id, bool IsOpen);
public readonly record struct SimulationVehicleCheckpoint(
    VehicleId Id,
    VehicleDimensions Dimensions,
    VehiclePerformance Performance,
    IReadOnlyList<RouteLaneStep> RouteSteps,
    int RouteStepIndex,
    double RouteProgressMeters,
    double SpeedMetersPerSecond,
    VehicleMovementState State);
public readonly record struct SimulationHouseholdCheckpoint(HouseholdId Id, TripEndpoint Residence);
public readonly record struct SimulationPersonCheckpoint(
    PersonId Id,
    HouseholdId HouseholdId,
    PersonDemographics Demographics,
    TripEndpoint Residence,
    TripEndpoint CurrentLocation,
    ActivityKind CurrentActivity,
    PersonTravelState TravelState,
    TripEndpoint? Destination,
    ActivityKind? DestinationActivity,
    TripRequestId? ActiveTripRequestId,
    TravelMode? ActiveTravelMode,
    PedestrianId? PedestrianId,
    VehicleId? VehicleId,
    IReadOnlyList<DailyActivityWindow> Schedule,
    IReadOnlyList<PersonNeed> Needs);

public readonly record struct SimulationTrackNodeCheckpoint(TrackNodeId Id, TrackNodeKind Kind, WorldPoint Position);
public readonly record struct SimulationTrackSegmentCheckpoint(
    TrackSegmentId Id,
    TrackNodeId StartNodeId,
    TrackNodeId EndNodeId,
    TrackDirection Direction,
    double GaugeMeters,
    double SpeedLimitMetersPerSecond,
    TrackElectrification Electrification,
    TrackUsage Usage);
public readonly record struct SimulationTrackConnectionCheckpoint(TrackConnectionId Id, TrackSegmentId FromSegmentId, TrackSegmentId ToSegmentId, TrackNodeId ViaNodeId);
public sealed record SimulationBlockSectionCheckpoint(BlockSectionId Id, IReadOnlyList<TrackSegmentId> SegmentIds);
public readonly record struct SimulationStationCheckpoint(StationId Id, WorldVolume Bounds);
public readonly record struct SimulationPlatformCheckpoint(
    PlatformId Id,
    StationId StationId,
    TrackSegmentId TrackSegmentId,
    double StartSegmentOffset,
    double EndSegmentOffset,
    WorldVolume Bounds);
public readonly record struct SimulationPlatformAccessPointCheckpoint(PlatformAccessPointId Id, PlatformId PlatformId, RoadAccessPointId RoadAccessPointId);
public sealed record SimulationDepotCheckpoint(DepotId Id, WorldVolume Bounds, IReadOnlyList<TrackSegmentId> TrackSegmentIds);
