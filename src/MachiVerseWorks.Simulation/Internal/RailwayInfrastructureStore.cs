namespace MachiVerseWorks.Simulation.Internal;

internal sealed class RailwayInfrastructureStore
{
    private readonly Dictionary<TrackNodeId, TrackNodeSnapshot> _nodes = [];
    private readonly Dictionary<TrackSegmentId, TrackSegmentSnapshot> _segments = [];
    private readonly Dictionary<TrackConnectionId, TrackConnectionSnapshot> _connections = [];
    private readonly Dictionary<BlockSectionId, BlockSectionSnapshot> _blocks = [];
    private readonly Dictionary<StationId, StationSnapshot> _stations = [];
    private readonly Dictionary<PlatformId, PlatformSnapshot> _platforms = [];
    private readonly Dictionary<PlatformAccessPointId, PlatformAccessPointSnapshot> _platformAccessPoints = [];
    private readonly Dictionary<DepotId, DepotSnapshot> _depots = [];
    private readonly Dictionary<TrackNodeId, int> _degreeByNode = [];
    private readonly HashSet<TrackSegmentId> _blockedSegments = [];
    private ulong _nextNodeId = 1;
    private ulong _nextSegmentId = 1;
    private ulong _nextConnectionId = 1;
    private ulong _nextBlockId = 1;
    private ulong _nextStationId = 1;
    private ulong _nextPlatformId = 1;
    private ulong _nextPlatformAccessPointId = 1;
    private ulong _nextDepotId = 1;

    public int NodeCount => _nodes.Count;
    public int SegmentCount => _segments.Count;
    public int ConnectionCount => _connections.Count;
    public int BlockCount => _blocks.Count;
    public int StationCount => _stations.Count;
    public int PlatformCount => _platforms.Count;
    public int PlatformAccessPointCount => _platformAccessPoints.Count;
    public int DepotCount => _depots.Count;
    public ulong NextNodeId => _nextNodeId;
    public ulong NextSegmentId => _nextSegmentId;
    public ulong NextConnectionId => _nextConnectionId;
    public ulong NextBlockId => _nextBlockId;
    public ulong NextStationId => _nextStationId;
    public ulong NextPlatformId => _nextPlatformId;
    public ulong NextPlatformAccessPointId => _nextPlatformAccessPointId;
    public ulong NextDepotId => _nextDepotId;

    public TrackNodeId AddNode(WorldPoint position, TrackNodeKind kind)
    {
        EnsureCapacity(_nextNodeId, "Track node");
        var id = new TrackNodeId(_nextNodeId++);
        _nodes.Add(id, new TrackNodeSnapshot(id, kind, position));
        _degreeByNode.Add(id, 0);
        return id;
    }

    public TrackSegmentId AddSegment(
        TrackNodeId startNodeId,
        TrackNodeId endNodeId,
        TrackDirection direction,
        double gaugeMeters,
        double speedLimitMetersPerSecond,
        TrackElectrification electrification,
        TrackUsage usage)
    {
        EnsureCapacity(_nextSegmentId, "Track segment");
        if (startNodeId == endNodeId) throw new ArgumentException("A track segment must connect two distinct track nodes.");
        ValidateNodeAttachment(startNodeId);
        ValidateNodeAttachment(endNodeId);
        ValidateSegmentAttributes(direction, gaugeMeters, speedLimitMetersPerSecond, electrification, usage);
        var id = new TrackSegmentId(_nextSegmentId++);
        _segments.Add(id, new TrackSegmentSnapshot(id, startNodeId, endNodeId, direction, gaugeMeters, speedLimitMetersPerSecond, electrification, usage));
        _degreeByNode[startNodeId]++;
        _degreeByNode[endNodeId]++;
        return id;
    }

    public TrackConnectionId AddConnection(TrackSegmentId fromSegmentId, TrackSegmentId toSegmentId, TrackNodeId viaNodeId)
    {
        EnsureCapacity(_nextConnectionId, "Track connection");
        if (fromSegmentId == toSegmentId) throw new ArgumentException("A track connection must connect two distinct track segments.");
        if (!_segments.TryGetValue(fromSegmentId, out var from)) throw new ArgumentException($"Track segment {fromSegmentId.Value} does not exist.", nameof(fromSegmentId));
        if (!_segments.TryGetValue(toSegmentId, out var to)) throw new ArgumentException($"Track segment {toSegmentId.Value} does not exist.", nameof(toSegmentId));
        if (!_nodes.TryGetValue(viaNodeId, out var via)) throw new ArgumentException($"Track node {viaNodeId.Value} does not exist.", nameof(viaNodeId));
        if (via.Kind == TrackNodeKind.Endpoint) throw new InvalidOperationException("Track connections require a Junction or Switch node.");
        if (!IsIncident(from, viaNodeId) || !IsIncident(to, viaNodeId)) throw new InvalidOperationException("Both track segments must meet at the declared via node.");
        if (!CanArrive(from, viaNodeId) || !CanDepart(to, viaNodeId)) throw new InvalidOperationException("Track segment directions do not allow the declared traversal.");
        if (_connections.Values.Any(item => item.FromSegmentId == fromSegmentId && item.ToSegmentId == toSegmentId && item.ViaNodeId == viaNodeId))
            throw new ArgumentException("An equivalent track connection already exists.");
        var id = new TrackConnectionId(_nextConnectionId++);
        _connections.Add(id, new TrackConnectionSnapshot(id, fromSegmentId, toSegmentId, viaNodeId));
        return id;
    }

    public BlockSectionId AddBlock(IReadOnlyList<TrackSegmentId> trackSegmentIds)
    {
        ArgumentNullException.ThrowIfNull(trackSegmentIds);
        EnsureCapacity(_nextBlockId, "Block section");
        if (trackSegmentIds.Count == 0) throw new ArgumentException("A block section must contain at least one track segment.", nameof(trackSegmentIds));
        var unique = new HashSet<TrackSegmentId>();
        foreach (var segmentId in trackSegmentIds)
        {
            if (!_segments.ContainsKey(segmentId)) throw new ArgumentException($"Track segment {segmentId.Value} does not exist.", nameof(trackSegmentIds));
            if (!unique.Add(segmentId)) throw new ArgumentException($"Track segment {segmentId.Value} is duplicated in the block section.", nameof(trackSegmentIds));
            if (_blockedSegments.Contains(segmentId)) throw new InvalidOperationException($"Track segment {segmentId.Value} already belongs to another block section.");
        }
        var id = new BlockSectionId(_nextBlockId++);
        var ordered = unique.OrderBy(static item => item.Value).ToArray();
        _blocks.Add(id, new BlockSectionSnapshot(id, ordered));
        foreach (var segmentId in ordered) _blockedSegments.Add(segmentId);
        return id;
    }

    public StationId AddStation(WorldVolume bounds)
    {
        EnsureCapacity(_nextStationId, "Station");
        var id = new StationId(_nextStationId++);
        _stations.Add(id, new StationSnapshot(id, bounds));
        return id;
    }

    public PlatformId AddPlatform(
        StationId stationId,
        TrackSegmentId trackSegmentId,
        double startSegmentOffset,
        double endSegmentOffset,
        WorldVolume bounds)
    {
        EnsureCapacity(_nextPlatformId, "Platform");
        if (!_stations.ContainsKey(stationId)) throw new ArgumentException($"Station {stationId.Value} does not exist.", nameof(stationId));
        if (!_segments.ContainsKey(trackSegmentId)) throw new ArgumentException($"Track segment {trackSegmentId.Value} does not exist.", nameof(trackSegmentId));
        ValidatePlatformOffsets(startSegmentOffset, endSegmentOffset);
        var id = new PlatformId(_nextPlatformId++);
        _platforms.Add(id, new PlatformSnapshot(id, stationId, trackSegmentId, startSegmentOffset, endSegmentOffset, bounds));
        return id;
    }

    public PlatformAccessPointId AddPlatformAccessPoint(PlatformId platformId, RoadAccessPointId roadAccessPointId)
    {
        EnsureCapacity(_nextPlatformAccessPointId, "Platform access point");
        if (!_platforms.ContainsKey(platformId)) throw new ArgumentException($"Platform {platformId.Value} does not exist.", nameof(platformId));
        if (_platformAccessPoints.Values.Any(item => item.PlatformId == platformId && item.RoadAccessPointId == roadAccessPointId))
            throw new ArgumentException("An equivalent platform access point already exists.");
        var id = new PlatformAccessPointId(_nextPlatformAccessPointId++);
        _platformAccessPoints.Add(id, new PlatformAccessPointSnapshot(id, platformId, roadAccessPointId));
        return id;
    }

    public DepotId AddDepot(WorldVolume bounds, IReadOnlyList<TrackSegmentId> trackSegmentIds)
    {
        ArgumentNullException.ThrowIfNull(trackSegmentIds);
        EnsureCapacity(_nextDepotId, "Depot");
        if (trackSegmentIds.Count == 0) throw new ArgumentException("A depot must contain at least one siding or depot track segment.", nameof(trackSegmentIds));
        var unique = new HashSet<TrackSegmentId>();
        foreach (var segmentId in trackSegmentIds)
        {
            if (!_segments.TryGetValue(segmentId, out var segment)) throw new ArgumentException($"Track segment {segmentId.Value} does not exist.", nameof(trackSegmentIds));
            if (segment.Usage == TrackUsage.Mainline) throw new InvalidOperationException($"Track segment {segmentId.Value} is a mainline segment and cannot be a depot track.");
            if (!unique.Add(segmentId)) throw new ArgumentException($"Track segment {segmentId.Value} is duplicated in the depot.", nameof(trackSegmentIds));
        }
        var id = new DepotId(_nextDepotId++);
        _depots.Add(id, new DepotSnapshot(id, bounds, unique.OrderBy(static item => item.Value).ToArray()));
        return id;
    }

    public bool ContainsRoadAccessPointReference(RoadAccessPointId id) => _platformAccessPoints.Values.Any(item => item.RoadAccessPointId == id);

    public PlatformAccessPointSnapshot[] GetPlatformAccessPoints(PlatformId platformId) => _platformAccessPoints.Values
        .Where(item => item.PlatformId == platformId)
        .OrderBy(static item => item.Id.Value)
        .ToArray();

    public bool TryGetPlatform(PlatformId id, out PlatformSnapshot snapshot) => _platforms.TryGetValue(id, out snapshot);

    public RailwayInfrastructureSnapshot CreateSnapshot() => CreateSnapshotCore(
        _nodes.Keys.ToHashSet(),
        _segments.Keys.ToHashSet(),
        _stations.Keys.ToHashSet(),
        _platforms.Keys.ToHashSet(),
        _depots.Keys.ToHashSet());

    public RailwayInfrastructureSnapshot CreateSnapshot(WorldVolume volume)
    {
        var selectedNodes = _nodes.Values.Where(item => volume.Contains(item.Position)).Select(static item => item.Id).ToHashSet();
        var selectedSegments = new HashSet<TrackSegmentId>();
        foreach (var segment in _segments.Values)
        {
            var start = _nodes[segment.StartNodeId].Position;
            var end = _nodes[segment.EndNodeId].Position;
            if (!SegmentBoundsIntersectVolume(start, end, volume)) continue;
            selectedSegments.Add(segment.Id);
            selectedNodes.Add(segment.StartNodeId);
            selectedNodes.Add(segment.EndNodeId);
        }

        var selectedPlatforms = _platforms.Values
            .Where(item => VolumesIntersect(item.Bounds, volume) || selectedSegments.Contains(item.TrackSegmentId))
            .Select(static item => item.Id)
            .ToHashSet();
        foreach (var platformId in selectedPlatforms)
        {
            var platform = _platforms[platformId];
            selectedSegments.Add(platform.TrackSegmentId);
            var segment = _segments[platform.TrackSegmentId];
            selectedNodes.Add(segment.StartNodeId);
            selectedNodes.Add(segment.EndNodeId);
        }

        var selectedStations = _stations.Values.Where(item => VolumesIntersect(item.Bounds, volume)).Select(static item => item.Id).ToHashSet();
        foreach (var platformId in selectedPlatforms) selectedStations.Add(_platforms[platformId].StationId);

        var selectedDepots = _depots.Values
            .Where(item => VolumesIntersect(item.Bounds, volume) || item.TrackSegmentIds.Any(selectedSegments.Contains))
            .Select(static item => item.Id)
            .ToHashSet();

        return CreateSnapshotCore(selectedNodes, selectedSegments, selectedStations, selectedPlatforms, selectedDepots);
    }

    public RailwayInfrastructureValidationResult ValidateConnectivity()
    {
        if (_segments.Count == 0) return new RailwayInfrastructureValidationResult(0, _connections.Count);
        var adjacency = _segments.Keys.ToDictionary(static id => id, static _ => new List<TrackSegmentId>());
        foreach (var connection in _connections.Values)
        {
            adjacency[connection.FromSegmentId].Add(connection.ToSegmentId);
            adjacency[connection.ToSegmentId].Add(connection.FromSegmentId);
        }
        var visited = new HashSet<TrackSegmentId>();
        var components = 0;
        foreach (var segmentId in _segments.Keys.OrderBy(static id => id.Value))
        {
            if (!visited.Add(segmentId)) continue;
            components++;
            var pending = new Stack<TrackSegmentId>();
            pending.Push(segmentId);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var next in adjacency[current]) if (visited.Add(next)) pending.Push(next);
            }
        }
        return new RailwayInfrastructureValidationResult(components, _connections.Count);
    }

    public IReadOnlyList<SimulationTrackNodeCheckpoint> CreateNodeCheckpoint() => _nodes.Values.OrderBy(static item => item.Id.Value)
        .Select(static item => new SimulationTrackNodeCheckpoint(item.Id, item.Kind, item.Position)).ToArray();
    public IReadOnlyList<SimulationTrackSegmentCheckpoint> CreateSegmentCheckpoint() => _segments.Values.OrderBy(static item => item.Id.Value)
        .Select(static item => new SimulationTrackSegmentCheckpoint(item.Id, item.StartNodeId, item.EndNodeId, item.Direction, item.GaugeMeters, item.SpeedLimitMetersPerSecond, item.Electrification, item.Usage)).ToArray();
    public IReadOnlyList<SimulationTrackConnectionCheckpoint> CreateConnectionCheckpoint() => _connections.Values.OrderBy(static item => item.Id.Value)
        .Select(static item => new SimulationTrackConnectionCheckpoint(item.Id, item.FromSegmentId, item.ToSegmentId, item.ViaNodeId)).ToArray();
    public IReadOnlyList<SimulationBlockSectionCheckpoint> CreateBlockCheckpoint() => _blocks.Values.OrderBy(static item => item.Id.Value)
        .Select(static item => new SimulationBlockSectionCheckpoint(item.Id, item.SegmentIds.ToArray())).ToArray();
    public IReadOnlyList<SimulationStationCheckpoint> CreateStationCheckpoint() => _stations.Values.OrderBy(static item => item.Id.Value)
        .Select(static item => new SimulationStationCheckpoint(item.Id, item.Bounds)).ToArray();
    public IReadOnlyList<SimulationPlatformCheckpoint> CreatePlatformCheckpoint() => _platforms.Values.OrderBy(static item => item.Id.Value)
        .Select(static item => new SimulationPlatformCheckpoint(item.Id, item.StationId, item.TrackSegmentId, item.StartSegmentOffset, item.EndSegmentOffset, item.Bounds)).ToArray();
    public IReadOnlyList<SimulationPlatformAccessPointCheckpoint> CreatePlatformAccessPointCheckpoint() => _platformAccessPoints.Values.OrderBy(static item => item.Id.Value)
        .Select(static item => new SimulationPlatformAccessPointCheckpoint(item.Id, item.PlatformId, item.RoadAccessPointId)).ToArray();
    public IReadOnlyList<SimulationDepotCheckpoint> CreateDepotCheckpoint() => _depots.Values.OrderBy(static item => item.Id.Value)
        .Select(static item => new SimulationDepotCheckpoint(item.Id, item.Bounds, item.TrackSegmentIds.ToArray())).ToArray();

    public void Restore(SimulationCheckpoint checkpoint)
    {
        _nodes.Clear(); _segments.Clear(); _connections.Clear(); _blocks.Clear(); _stations.Clear(); _platforms.Clear(); _platformAccessPoints.Clear(); _depots.Clear(); _degreeByNode.Clear(); _blockedSegments.Clear();
        foreach (var item in checkpoint.TrackNodes ?? []) { _nodes.Add(item.Id, new TrackNodeSnapshot(item.Id, item.Kind, item.Position)); _degreeByNode.Add(item.Id, 0); }
        foreach (var item in checkpoint.TrackSegments ?? []) { _segments.Add(item.Id, new TrackSegmentSnapshot(item.Id, item.StartNodeId, item.EndNodeId, item.Direction, item.GaugeMeters, item.SpeedLimitMetersPerSecond, item.Electrification, item.Usage)); _degreeByNode[item.StartNodeId]++; _degreeByNode[item.EndNodeId]++; }
        foreach (var item in checkpoint.TrackConnections ?? []) _connections.Add(item.Id, new TrackConnectionSnapshot(item.Id, item.FromSegmentId, item.ToSegmentId, item.ViaNodeId));
        foreach (var item in checkpoint.BlockSections ?? []) { var segments = item.SegmentIds.ToArray(); _blocks.Add(item.Id, new BlockSectionSnapshot(item.Id, segments)); foreach (var segmentId in segments) _blockedSegments.Add(segmentId); }
        foreach (var item in checkpoint.Stations ?? []) _stations.Add(item.Id, new StationSnapshot(item.Id, item.Bounds));
        foreach (var item in checkpoint.Platforms ?? []) _platforms.Add(item.Id, new PlatformSnapshot(item.Id, item.StationId, item.TrackSegmentId, item.StartSegmentOffset, item.EndSegmentOffset, item.Bounds));
        foreach (var item in checkpoint.PlatformAccessPoints ?? []) _platformAccessPoints.Add(item.Id, new PlatformAccessPointSnapshot(item.Id, item.PlatformId, item.RoadAccessPointId));
        foreach (var item in checkpoint.Depots ?? []) _depots.Add(item.Id, new DepotSnapshot(item.Id, item.Bounds, item.TrackSegmentIds.ToArray()));
        _nextNodeId = checkpoint.NextTrackNodeId; _nextSegmentId = checkpoint.NextTrackSegmentId; _nextConnectionId = checkpoint.NextTrackConnectionId; _nextBlockId = checkpoint.NextBlockSectionId; _nextStationId = checkpoint.NextStationId; _nextPlatformId = checkpoint.NextPlatformId; _nextPlatformAccessPointId = checkpoint.NextPlatformAccessPointId; _nextDepotId = checkpoint.NextDepotId;
    }

    private RailwayInfrastructureSnapshot CreateSnapshotCore(
        HashSet<TrackNodeId> selectedNodes,
        HashSet<TrackSegmentId> selectedSegments,
        HashSet<StationId> selectedStations,
        HashSet<PlatformId> selectedPlatforms,
        HashSet<DepotId> selectedDepots)
    {
        var nodes = _nodes.Values.Where(item => selectedNodes.Contains(item.Id)).OrderBy(static item => item.Id.Value).ToArray();
        var segments = _segments.Values.Where(item => selectedSegments.Contains(item.Id)).OrderBy(static item => item.Id.Value).ToArray();
        var connections = _connections.Values.Where(item => selectedSegments.Contains(item.FromSegmentId) && selectedSegments.Contains(item.ToSegmentId) && selectedNodes.Contains(item.ViaNodeId)).OrderBy(static item => item.Id.Value).ToArray();
        var blocks = _blocks.Values.Where(item => item.SegmentIds.Any(selectedSegments.Contains)).OrderBy(static item => item.Id.Value).Select(static item => new BlockSectionSnapshot(item.Id, item.SegmentIds.ToArray())).ToArray();
        var stations = _stations.Values.Where(item => selectedStations.Contains(item.Id)).OrderBy(static item => item.Id.Value).ToArray();
        var platforms = _platforms.Values.Where(item => selectedPlatforms.Contains(item.Id)).OrderBy(static item => item.Id.Value).ToArray();
        var accessPoints = _platformAccessPoints.Values.Where(item => selectedPlatforms.Contains(item.PlatformId)).OrderBy(static item => item.Id.Value).ToArray();
        var depots = _depots.Values.Where(item => selectedDepots.Contains(item.Id)).OrderBy(static item => item.Id.Value).Select(static item => new DepotSnapshot(item.Id, item.Bounds, item.TrackSegmentIds.ToArray())).ToArray();
        return new RailwayInfrastructureSnapshot(nodes, segments, connections, blocks, stations, platforms, accessPoints, depots);
    }

    private void ValidateNodeAttachment(TrackNodeId id)
    {
        if (!_nodes.TryGetValue(id, out var node)) throw new ArgumentException($"Track node {id.Value} does not exist.");
        if (node.Kind == TrackNodeKind.Endpoint && _degreeByNode[id] > 0) throw new InvalidOperationException($"Endpoint track node {id.Value} cannot connect more than one track segment; use Junction or Switch.");
    }

    private static void ValidateSegmentAttributes(TrackDirection direction, double gaugeMeters, double speedLimitMetersPerSecond, TrackElectrification electrification, TrackUsage usage)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!double.IsFinite(gaugeMeters) || gaugeMeters <= 0d) throw new ArgumentOutOfRangeException(nameof(gaugeMeters));
        if (!double.IsFinite(speedLimitMetersPerSecond) || speedLimitMetersPerSecond <= 0d) throw new ArgumentOutOfRangeException(nameof(speedLimitMetersPerSecond));
        if (!Enum.IsDefined(electrification)) throw new ArgumentOutOfRangeException(nameof(electrification));
        if (!Enum.IsDefined(usage)) throw new ArgumentOutOfRangeException(nameof(usage));
    }

    private static void ValidatePlatformOffsets(double startSegmentOffset, double endSegmentOffset)
    {
        if (!double.IsFinite(startSegmentOffset) || !double.IsFinite(endSegmentOffset) || startSegmentOffset < 0d || endSegmentOffset > 1d || endSegmentOffset <= startSegmentOffset)
            throw new ArgumentOutOfRangeException(nameof(startSegmentOffset), "Platform offsets must be finite and satisfy 0 <= start < end <= 1.");
    }

    private static bool IsIncident(TrackSegmentSnapshot segment, TrackNodeId nodeId) => segment.StartNodeId == nodeId || segment.EndNodeId == nodeId;
    private static bool CanArrive(TrackSegmentSnapshot segment, TrackNodeId nodeId) => segment.Direction switch
    {
        TrackDirection.Bidirectional => IsIncident(segment, nodeId),
        TrackDirection.StartToEnd => segment.EndNodeId == nodeId,
        TrackDirection.EndToStart => segment.StartNodeId == nodeId,
        _ => false,
    };
    private static bool CanDepart(TrackSegmentSnapshot segment, TrackNodeId nodeId) => segment.Direction switch
    {
        TrackDirection.Bidirectional => IsIncident(segment, nodeId),
        TrackDirection.StartToEnd => segment.StartNodeId == nodeId,
        TrackDirection.EndToStart => segment.EndNodeId == nodeId,
        _ => false,
    };

    private static bool SegmentBoundsIntersectVolume(WorldPoint first, WorldPoint second, WorldVolume volume) =>
        Math.Max(first.X, second.X) >= volume.MinX && Math.Min(first.X, second.X) <= volume.MaxX
        && Math.Max(first.Y, second.Y) >= volume.MinY && Math.Min(first.Y, second.Y) <= volume.MaxY
        && Math.Max(first.Z, second.Z) >= volume.MinZ && Math.Min(first.Z, second.Z) <= volume.MaxZ;

    private static bool VolumesIntersect(WorldVolume first, WorldVolume second) =>
        first.MaxX >= second.MinX && first.MinX <= second.MaxX
        && first.MaxY >= second.MinY && first.MinY <= second.MaxY
        && first.MaxZ >= second.MinZ && first.MinZ <= second.MaxZ;

    private static void EnsureCapacity(ulong nextId, string entityName)
    {
        if (nextId == ulong.MaxValue) throw new OverflowException($"{entityName} ID capacity has been exhausted.");
    }
}
