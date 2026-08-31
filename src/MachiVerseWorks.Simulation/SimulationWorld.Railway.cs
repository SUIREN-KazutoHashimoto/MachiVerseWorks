using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly RailwayInfrastructureStore _railway;

    public int TrackNodeCount => _railway.NodeCount;
    public int TrackSegmentCount => _railway.SegmentCount;
    public int TrackConnectionCount => _railway.ConnectionCount;
    public int BlockSectionCount => _railway.BlockCount;
    public int StationCount => _railway.StationCount;
    public int PlatformCount => _railway.PlatformCount;
    public int PlatformAccessPointCount => _railway.PlatformAccessPointCount;
    public int DepotCount => _railway.DepotCount;

    public TrackNodeId CreateTrackNode(WorldPoint position, TrackNodeKind kind = TrackNodeKind.Endpoint)
    {
        ValidatePoint(position);
        ValidateEnum(kind, nameof(kind));
        return _railway.AddNode(position, kind);
    }

    public TrackSegmentId CreateTrackSegment(
        TrackNodeId startNodeId,
        TrackNodeId endNodeId,
        TrackDirection direction = TrackDirection.Bidirectional,
        double gaugeMeters = 1.435d,
        double speedLimitMetersPerSecond = 22.2222222222d,
        TrackElectrification electrification = TrackElectrification.None,
        TrackUsage usage = TrackUsage.Mainline)
    {
        return _railway.AddSegment(startNodeId, endNodeId, direction, gaugeMeters, speedLimitMetersPerSecond, electrification, usage);
    }

    public TrackConnectionId CreateTrackConnection(TrackSegmentId fromSegmentId, TrackSegmentId toSegmentId, TrackNodeId viaNodeId) =>
        _railway.AddConnection(fromSegmentId, toSegmentId, viaNodeId);

    public BlockSectionId CreateBlockSection(IReadOnlyList<TrackSegmentId> trackSegmentIds)
    {
        ArgumentNullException.ThrowIfNull(trackSegmentIds);
        if (trackSegmentIds.Count > RailwayInfrastructureLimits.MaximumBlockSectionSegmentCount)
            throw new ArgumentOutOfRangeException(nameof(trackSegmentIds), trackSegmentIds.Count, $"A block section may contain at most {RailwayInfrastructureLimits.MaximumBlockSectionSegmentCount} track segments.");
        return _railway.AddBlock(trackSegmentIds);
    }

    public StationId CreateStation(WorldVolume bounds) => _railway.AddStation(bounds);

    public PlatformId CreatePlatform(
        StationId stationId,
        TrackSegmentId trackSegmentId,
        double startSegmentOffset,
        double endSegmentOffset,
        WorldVolume bounds) => _railway.AddPlatform(stationId, trackSegmentId, startSegmentOffset, endSegmentOffset, bounds);

    public PlatformAccessPointId CreatePlatformAccessPoint(PlatformId platformId, RoadAccessPointId roadAccessPointId)
    {
        if (!_roads.TryGetAccessPoint(roadAccessPointId, out var roadAccess))
            throw new ArgumentException($"Road access point {roadAccessPointId.Value} does not exist.", nameof(roadAccessPointId));
        if ((roadAccess.Mode & RoadAccessMode.Foot) == 0)
            throw new InvalidOperationException($"Road access point {roadAccessPointId.Value} does not permit pedestrian access.");
        return _railway.AddPlatformAccessPoint(platformId, roadAccessPointId);
    }

    public DepotId CreateDepot(WorldVolume bounds, IReadOnlyList<TrackSegmentId> trackSegmentIds)
    {
        ArgumentNullException.ThrowIfNull(trackSegmentIds);
        if (trackSegmentIds.Count > RailwayInfrastructureLimits.MaximumDepotTrackSegmentCount)
            throw new ArgumentOutOfRangeException(nameof(trackSegmentIds), trackSegmentIds.Count, $"A depot may contain at most {RailwayInfrastructureLimits.MaximumDepotTrackSegmentCount} track segments.");
        return _railway.AddDepot(bounds, trackSegmentIds);
    }

    public RailwayInfrastructureSnapshot CreateRailwayInfrastructureSnapshot() => _railway.CreateSnapshot();

    public RailwayInfrastructureSnapshot CreateRailwayInfrastructureSnapshot(WorldVolume volume)
    {
        _ = SpatialGrid.ToCell(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ), Config.SpatialCellSize);
        _ = SpatialGrid.ToCell(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ), Config.SpatialCellSize);
        return _railway.CreateSnapshot(volume);
    }

    public RailwayInfrastructureValidationResult ValidateRailwayInfrastructure() => _railway.ValidateConnectivity();

    public PedestrianRoute FindWalkingRouteToPlatform(TripEndpoint origin, PlatformId platformId)
    {
        if (!_railway.TryGetPlatform(platformId, out _)) throw new ArgumentException($"Platform {platformId.Value} does not exist.", nameof(platformId));
        EnsurePedestrianNetwork();
        PedestrianRoute? best = null;
        ulong bestAccessId = ulong.MaxValue;
        foreach (var platformAccess in _railway.GetPlatformAccessPoints(platformId))
        {
            if (!_roads.TryGetAccessPoint(platformAccess.RoadAccessPointId, out var roadAccess) || (roadAccess.Mode & RoadAccessMode.Foot) == 0) continue;
            var candidates = new TripEndpoint[2];
            var candidateCount = 0;
            if (roadAccess.PoiId is { } poiId) candidates[candidateCount++] = TripEndpoint.ForPoi(poiId);
            if (roadAccess.BuildingId is { } buildingId) candidates[candidateCount++] = TripEndpoint.ForBuilding(buildingId);
            for (var index = 0; index < candidateCount; index++)
            {
                try
                {
                    var route = _pedestrianNetwork.FindRoute(origin, candidates[index]);
                    if (best is null
                        || route.TotalLengthMeters < best.TotalLengthMeters
                        || (route.TotalLengthMeters == best.TotalLengthMeters && platformAccess.Id.Value < bestAccessId))
                    {
                        best = route;
                        bestAccessId = platformAccess.Id.Value;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Another platform access may still be reachable.
                }
            }
        }
        return best ?? throw new InvalidOperationException("No walkable pedestrian route reaches the requested platform access point.");
    }

    private static void ValidateRailwayCheckpoint(SimulationCheckpoint checkpoint, double cellSize)
    {
        var nodeData = checkpoint.TrackNodes ?? [];
        var segmentData = checkpoint.TrackSegments ?? [];
        var connectionData = checkpoint.TrackConnections ?? [];
        var blockData = checkpoint.BlockSections ?? [];
        var stationData = checkpoint.Stations ?? [];
        var platformData = checkpoint.Platforms ?? [];
        var accessData = checkpoint.PlatformAccessPoints ?? [];
        var depotData = checkpoint.Depots ?? [];

        ValidateNextId(checkpoint.NextTrackNodeId, nodeData.Select(static item => item.Id.Value), "Track node");
        ValidateNextId(checkpoint.NextTrackSegmentId, segmentData.Select(static item => item.Id.Value), "Track segment");
        ValidateNextId(checkpoint.NextTrackConnectionId, connectionData.Select(static item => item.Id.Value), "Track connection");
        ValidateNextId(checkpoint.NextBlockSectionId, blockData.Select(static item => item.Id.Value), "Block section");
        ValidateNextId(checkpoint.NextStationId, stationData.Select(static item => item.Id.Value), "Station");
        ValidateNextId(checkpoint.NextPlatformId, platformData.Select(static item => item.Id.Value), "Platform");
        ValidateNextId(checkpoint.NextPlatformAccessPointId, accessData.Select(static item => item.Id.Value), "Platform access point");
        ValidateNextId(checkpoint.NextDepotId, depotData.Select(static item => item.Id.Value), "Depot");

        var nodes = new Dictionary<TrackNodeId, SimulationTrackNodeCheckpoint>();
        foreach (var node in nodeData)
        {
            if (node.Id.Value == 0 || !nodes.TryAdd(node.Id, node)) throw new ArgumentException($"Track node ID {node.Id.Value} is zero or duplicated.", nameof(checkpoint));
            ValidateEnum(node.Kind, nameof(checkpoint));
            _ = SpatialGrid.ToCell(node.Position, cellSize);
        }

        var segments = new Dictionary<TrackSegmentId, SimulationTrackSegmentCheckpoint>();
        var degree = nodes.Keys.ToDictionary(static id => id, static _ => 0);
        foreach (var segment in segmentData)
        {
            if (segment.Id.Value == 0 || !segments.TryAdd(segment.Id, segment)) throw new ArgumentException($"Track segment ID {segment.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (segment.StartNodeId == segment.EndNodeId || !nodes.ContainsKey(segment.StartNodeId) || !nodes.ContainsKey(segment.EndNodeId)) throw new ArgumentException($"Track segment {segment.Id.Value} has invalid node references.", nameof(checkpoint));
            ValidateEnum(segment.Direction, nameof(checkpoint));
            ValidateEnum(segment.Electrification, nameof(checkpoint));
            ValidateEnum(segment.Usage, nameof(checkpoint));
            if (!double.IsFinite(segment.GaugeMeters) || segment.GaugeMeters <= 0d || !double.IsFinite(segment.SpeedLimitMetersPerSecond) || segment.SpeedLimitMetersPerSecond <= 0d)
                throw new ArgumentException($"Track segment {segment.Id.Value} has invalid gauge or speed limit.", nameof(checkpoint));
            degree[segment.StartNodeId]++;
            degree[segment.EndNodeId]++;
        }
        foreach (var entry in degree)
        {
            if (nodes[entry.Key].Kind == TrackNodeKind.Endpoint && entry.Value > 1) throw new ArgumentException($"Endpoint track node {entry.Key.Value} has degree {entry.Value}.", nameof(checkpoint));
        }

        var connectionIds = new HashSet<TrackConnectionId>();
        var connectionKeys = new HashSet<(TrackSegmentId From, TrackSegmentId To, TrackNodeId Via)>();
        foreach (var connection in connectionData)
        {
            if (connection.Id.Value == 0 || !connectionIds.Add(connection.Id)) throw new ArgumentException($"Track connection ID {connection.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (!segments.TryGetValue(connection.FromSegmentId, out var from) || !segments.TryGetValue(connection.ToSegmentId, out var to) || connection.FromSegmentId == connection.ToSegmentId || !nodes.TryGetValue(connection.ViaNodeId, out var via) || via.Kind == TrackNodeKind.Endpoint)
                throw new ArgumentException($"Track connection {connection.Id.Value} has invalid references.", nameof(checkpoint));
            if (!RailwayIsIncident(from, connection.ViaNodeId) || !RailwayIsIncident(to, connection.ViaNodeId) || !RailwayCanArrive(from, connection.ViaNodeId) || !RailwayCanDepart(to, connection.ViaNodeId))
                throw new ArgumentException($"Track connection {connection.Id.Value} is not traversable through its via node.", nameof(checkpoint));
            if (!connectionKeys.Add((connection.FromSegmentId, connection.ToSegmentId, connection.ViaNodeId))) throw new ArgumentException("Equivalent track connection is duplicated.", nameof(checkpoint));
        }

        var blockIds = new HashSet<BlockSectionId>();
        var blockedSegments = new HashSet<TrackSegmentId>();
        foreach (var block in blockData)
        {
            if (block is null || block.Id.Value == 0 || !blockIds.Add(block.Id) || block.SegmentIds is null || block.SegmentIds.Count == 0) throw new ArgumentException("Block section is null, empty, zero, or duplicated.", nameof(checkpoint));
            if (block.SegmentIds.Count > RailwayInfrastructureLimits.MaximumBlockSectionSegmentCount)
                throw new ArgumentException($"Block section {block.Id.Value} exceeds the {RailwayInfrastructureLimits.MaximumBlockSectionSegmentCount}-segment membership limit.", nameof(checkpoint));
            var local = new HashSet<TrackSegmentId>();
            foreach (var segmentId in block.SegmentIds)
            {
                if (!segments.ContainsKey(segmentId) || !local.Add(segmentId) || !blockedSegments.Add(segmentId)) throw new ArgumentException($"Block section {block.Id.Value} has invalid or duplicate track segment {segmentId.Value}.", nameof(checkpoint));
            }
        }

        var stations = new HashSet<StationId>();
        foreach (var station in stationData) if (station.Id.Value == 0 || !stations.Add(station.Id)) throw new ArgumentException($"Station ID {station.Id.Value} is zero or duplicated.", nameof(checkpoint));

        var platforms = new HashSet<PlatformId>();
        foreach (var platform in platformData)
        {
            if (platform.Id.Value == 0 || !platforms.Add(platform.Id) || !stations.Contains(platform.StationId) || !segments.ContainsKey(platform.TrackSegmentId)) throw new ArgumentException($"Platform {platform.Id.Value} has invalid references.", nameof(checkpoint));
            if (!double.IsFinite(platform.StartSegmentOffset) || !double.IsFinite(platform.EndSegmentOffset) || platform.StartSegmentOffset < 0d || platform.EndSegmentOffset > 1d || platform.EndSegmentOffset <= platform.StartSegmentOffset)
                throw new ArgumentException($"Platform {platform.Id.Value} has invalid segment offsets.", nameof(checkpoint));
        }

        var roadAccessPoints = checkpoint.RoadAccessPoints.ToDictionary(static item => item.Id);
        var platformAccessIds = new HashSet<PlatformAccessPointId>();
        var platformAccessKeys = new HashSet<(PlatformId, RoadAccessPointId)>();
        foreach (var access in accessData)
        {
            if (access.Id.Value == 0 || !platformAccessIds.Add(access.Id) || !platforms.Contains(access.PlatformId) || !roadAccessPoints.TryGetValue(access.RoadAccessPointId, out var roadAccess) || (roadAccess.Mode & RoadAccessMode.Foot) == 0)
                throw new ArgumentException($"Platform access point {access.Id.Value} has invalid references.", nameof(checkpoint));
            if (!platformAccessKeys.Add((access.PlatformId, access.RoadAccessPointId))) throw new ArgumentException("Equivalent platform access point is duplicated.", nameof(checkpoint));
        }

        var depotIds = new HashSet<DepotId>();
        foreach (var depot in depotData)
        {
            if (depot is null || depot.Id.Value == 0 || !depotIds.Add(depot.Id) || depot.TrackSegmentIds is null || depot.TrackSegmentIds.Count == 0) throw new ArgumentException("Depot is null, empty, zero, or duplicated.", nameof(checkpoint));
            if (depot.TrackSegmentIds.Count > RailwayInfrastructureLimits.MaximumDepotTrackSegmentCount)
                throw new ArgumentException($"Depot {depot.Id.Value} exceeds the {RailwayInfrastructureLimits.MaximumDepotTrackSegmentCount}-segment membership limit.", nameof(checkpoint));
            var local = new HashSet<TrackSegmentId>();
            foreach (var segmentId in depot.TrackSegmentIds)
            {
                if (!segments.TryGetValue(segmentId, out var segment) || segment.Usage == TrackUsage.Mainline || !local.Add(segmentId)) throw new ArgumentException($"Depot {depot.Id.Value} has invalid track segment {segmentId.Value}.", nameof(checkpoint));
            }
        }
    }

    private static bool RailwayIsIncident(SimulationTrackSegmentCheckpoint segment, TrackNodeId nodeId) => segment.StartNodeId == nodeId || segment.EndNodeId == nodeId;
    private static bool RailwayCanArrive(SimulationTrackSegmentCheckpoint segment, TrackNodeId nodeId) => segment.Direction switch
    {
        TrackDirection.Bidirectional => RailwayIsIncident(segment, nodeId),
        TrackDirection.StartToEnd => segment.EndNodeId == nodeId,
        TrackDirection.EndToStart => segment.StartNodeId == nodeId,
        _ => false,
    };
    private static bool RailwayCanDepart(SimulationTrackSegmentCheckpoint segment, TrackNodeId nodeId) => segment.Direction switch
    {
        TrackDirection.Bidirectional => RailwayIsIncident(segment, nodeId),
        TrackDirection.StartToEnd => segment.StartNodeId == nodeId,
        TrackDirection.EndToStart => segment.EndNodeId == nodeId,
        _ => false,
    };
}