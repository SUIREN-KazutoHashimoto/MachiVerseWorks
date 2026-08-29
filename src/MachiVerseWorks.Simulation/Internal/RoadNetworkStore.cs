namespace MachiVerseWorks.Simulation.Internal;

internal sealed class RoadNetworkStore
{
    private readonly Dictionary<RoadNodeId, RoadNodeSnapshot> nodes = [];
    private readonly Dictionary<RoadSegmentId, RoadSegmentSnapshot> segments = [];
    private readonly Dictionary<LaneId, LaneSnapshot> lanes = [];
    private readonly Dictionary<LaneConnectionId, LaneConnectionSnapshot> connections = [];
    private readonly Dictionary<RoadAccessPointId, RoadAccessPointSnapshot> accessPoints = [];
    private readonly Dictionary<RoadNodeId, int> degreeByNode = [];
    private readonly RoadSpatialIndex spatialIndex;
    private ulong nextNodeId = 1;
    private ulong nextSegmentId = 1;
    private ulong nextLaneId = 1;
    private ulong nextConnectionId = 1;
    private ulong nextAccessPointId = 1;

    public RoadNetworkStore(double cellSize) => spatialIndex = new RoadSpatialIndex(cellSize);

    public int NodeCount => nodes.Count;
    public int SegmentCount => segments.Count;
    public int LaneCount => lanes.Count;
    public int ConnectionCount => connections.Count;
    public int AccessPointCount => accessPoints.Count;
    public ulong NextNodeId => nextNodeId;
    public ulong NextSegmentId => nextSegmentId;
    public ulong NextLaneId => nextLaneId;
    public ulong NextConnectionId => nextConnectionId;
    public ulong NextAccessPointId => nextAccessPointId;

    public RoadNodeId AddNode(WorldPoint position, RoadNodeKind kind)
    {
        EnsureCapacity(nextNodeId, "Road node");
        spatialIndex.ValidatePosition(position);
        var id = new RoadNodeId(nextNodeId++);
        var snapshot = new RoadNodeSnapshot(id, kind, position);
        nodes.Add(id, snapshot);
        degreeByNode.Add(id, 0);
        spatialIndex.RegisterNode(id, position);
        return id;
    }

    public bool UpdateNode(RoadNodeId id, WorldPoint position, RoadNodeKind kind)
    {
        if (!nodes.TryGetValue(id, out var previous)) return false;
        spatialIndex.ValidatePosition(position);
        if (kind == RoadNodeKind.Endpoint && degreeByNode[id] > 1)
            throw new InvalidOperationException($"Road node {id.Value} has multiple incident segments and must remain an intersection.");

        var incident = segments.Values.Where(segment => segment.StartNodeId == id || segment.EndNodeId == id).ToArray();
        nodes[id] = new RoadNodeSnapshot(id, kind, position);
        spatialIndex.UpdateNode(id, position);
        foreach (var segment in incident)
        {
            var start = nodes[segment.StartNodeId].Position;
            var end = nodes[segment.EndNodeId].Position;
            spatialIndex.UpdateSegment(segment.Id, start, end);
        }
        return true;
    }

    public bool RemoveNode(RoadNodeId id)
    {
        if (!nodes.ContainsKey(id)) return false;
        if (degreeByNode[id] != 0) throw new InvalidOperationException($"Road node {id.Value} cannot be removed while segments reference it.");
        if (connections.Values.Any(connection => connection.ViaNodeId == id)) throw new InvalidOperationException($"Road node {id.Value} cannot be removed while lane connections reference it.");
        spatialIndex.RemoveNode(id);
        degreeByNode.Remove(id);
        return nodes.Remove(id);
    }

    public RoadSegmentId AddSegment(RoadNodeId startNodeId, RoadNodeId endNodeId, RoadKind kind)
    {
        EnsureCapacity(nextSegmentId, "Road segment");
        ValidateSegmentNodes(startNodeId, endNodeId, null);
        var id = new RoadSegmentId(nextSegmentId++);
        var snapshot = new RoadSegmentSnapshot(id, kind, startNodeId, endNodeId);
        segments.Add(id, snapshot);
        degreeByNode[startNodeId]++;
        degreeByNode[endNodeId]++;
        spatialIndex.RegisterSegment(id, nodes[startNodeId].Position, nodes[endNodeId].Position);
        return id;
    }

    public bool UpdateSegment(RoadSegmentId id, RoadNodeId startNodeId, RoadNodeId endNodeId, RoadKind kind)
    {
        if (!segments.TryGetValue(id, out var previous)) return false;
        ValidateSegmentNodes(startNodeId, endNodeId, id);
        degreeByNode[previous.StartNodeId]--;
        degreeByNode[previous.EndNodeId]--;
        degreeByNode[startNodeId]++;
        degreeByNode[endNodeId]++;
        segments[id] = new RoadSegmentSnapshot(id, kind, startNodeId, endNodeId);
        spatialIndex.UpdateSegment(id, nodes[startNodeId].Position, nodes[endNodeId].Position);
        ValidateConnectionsForSegment(id);
        return true;
    }

    public bool RemoveSegment(RoadSegmentId id)
    {
        if (!segments.TryGetValue(id, out var segment)) return false;
        if (lanes.Values.Any(lane => lane.SegmentId == id)) throw new InvalidOperationException($"Road segment {id.Value} cannot be removed while lanes reference it.");
        if (accessPoints.Values.Any(access => access.SegmentId == id)) throw new InvalidOperationException($"Road segment {id.Value} cannot be removed while access points reference it.");
        spatialIndex.RemoveSegment(id);
        degreeByNode[segment.StartNodeId]--;
        degreeByNode[segment.EndNodeId]--;
        return segments.Remove(id);
    }

    public LaneId AddLane(RoadSegmentId segmentId, LaneDirection direction, ushort order, double widthMeters, double speedLimitMetersPerSecond)
    {
        EnsureCapacity(nextLaneId, "Lane");
        ValidateLane(segmentId, direction, order, widthMeters, speedLimitMetersPerSecond, null);
        var id = new LaneId(nextLaneId++);
        lanes.Add(id, new LaneSnapshot(id, segmentId, direction, order, widthMeters, speedLimitMetersPerSecond));
        return id;
    }

    public bool UpdateLane(LaneId id, RoadSegmentId segmentId, LaneDirection direction, ushort order, double widthMeters, double speedLimitMetersPerSecond)
    {
        if (!lanes.ContainsKey(id)) return false;
        ValidateLane(segmentId, direction, order, widthMeters, speedLimitMetersPerSecond, id);
        var previous = lanes[id];
        lanes[id] = new LaneSnapshot(id, segmentId, direction, order, widthMeters, speedLimitMetersPerSecond);
        try
        {
            ValidateConnectionsForLane(id);
        }
        catch
        {
            lanes[id] = previous;
            throw;
        }
        return true;
    }

    public bool RemoveLane(LaneId id)
    {
        if (!lanes.ContainsKey(id)) return false;
        if (connections.Values.Any(connection => connection.FromLaneId == id || connection.ToLaneId == id))
            throw new InvalidOperationException($"Lane {id.Value} cannot be removed while lane connections reference it.");
        return lanes.Remove(id);
    }

    public LaneConnectionId AddConnection(LaneId fromLaneId, LaneId toLaneId, RoadNodeId viaNodeId, TurnMovement movement)
    {
        EnsureCapacity(nextConnectionId, "Lane connection");
        ValidateConnection(fromLaneId, toLaneId, viaNodeId, movement, null);
        var id = new LaneConnectionId(nextConnectionId++);
        connections.Add(id, new LaneConnectionSnapshot(id, fromLaneId, toLaneId, viaNodeId, movement));
        return id;
    }

    public bool UpdateConnection(LaneConnectionId id, LaneId fromLaneId, LaneId toLaneId, RoadNodeId viaNodeId, TurnMovement movement)
    {
        if (!connections.ContainsKey(id)) return false;
        ValidateConnection(fromLaneId, toLaneId, viaNodeId, movement, id);
        connections[id] = new LaneConnectionSnapshot(id, fromLaneId, toLaneId, viaNodeId, movement);
        return true;
    }

    public bool RemoveConnection(LaneConnectionId id) => connections.Remove(id);

    public RoadAccessPointId AddAccessPoint(RoadSegmentId segmentId, double segmentOffset, BuildingId? buildingId, PoiId? poiId, RoadAccessMode mode)
    {
        EnsureCapacity(nextAccessPointId, "Road access point");
        ValidateAccessPoint(segmentId, segmentOffset, buildingId, poiId, mode);
        var id = new RoadAccessPointId(nextAccessPointId++);
        accessPoints.Add(id, new RoadAccessPointSnapshot(id, segmentId, segmentOffset, buildingId, poiId, mode));
        return id;
    }

    public bool UpdateAccessPoint(RoadAccessPointId id, RoadSegmentId segmentId, double segmentOffset, BuildingId? buildingId, PoiId? poiId, RoadAccessMode mode)
    {
        if (!accessPoints.ContainsKey(id)) return false;
        ValidateAccessPoint(segmentId, segmentOffset, buildingId, poiId, mode);
        accessPoints[id] = new RoadAccessPointSnapshot(id, segmentId, segmentOffset, buildingId, poiId, mode);
        return true;
    }

    public bool RemoveAccessPoint(RoadAccessPointId id) => accessPoints.Remove(id);

    public bool TryGetNode(RoadNodeId id, out RoadNodeSnapshot snapshot) => nodes.TryGetValue(id, out snapshot);
    public bool TryGetSegment(RoadSegmentId id, out RoadSegmentSnapshot snapshot) => segments.TryGetValue(id, out snapshot);
    public bool TryGetLane(LaneId id, out LaneSnapshot snapshot) => lanes.TryGetValue(id, out snapshot);
    public bool TryGetConnection(LaneConnectionId id, out LaneConnectionSnapshot snapshot) => connections.TryGetValue(id, out snapshot);
    public bool TryGetAccessPoint(RoadAccessPointId id, out RoadAccessPointSnapshot snapshot) => accessPoints.TryGetValue(id, out snapshot);

    public RoadNetworkSnapshot CreateSnapshot() => CreateSnapshotCore(nodes.Keys.ToHashSet(), segments.Keys.ToHashSet());

    public RoadNetworkSnapshot CreateSnapshot(WorldVolume volume)
    {
        var selectedNodes = spatialIndex.QueryNodes(volume);
        var selectedSegments = spatialIndex.QuerySegments(volume);
        foreach (var segmentId in selectedSegments)
        {
            var segment = segments[segmentId];
            selectedNodes.Add(segment.StartNodeId);
            selectedNodes.Add(segment.EndNodeId);
        }
        return CreateSnapshotCore(selectedNodes, selectedSegments);
    }

    public IReadOnlyList<SimulationRoadNodeCheckpoint> CreateNodeCheckpoint() => Sort(nodes.Values, static item => item.Id.Value)
        .Select(static item => new SimulationRoadNodeCheckpoint(item.Id, item.Kind, item.Position)).ToArray();
    public IReadOnlyList<SimulationRoadSegmentCheckpoint> CreateSegmentCheckpoint() => Sort(segments.Values, static item => item.Id.Value)
        .Select(static item => new SimulationRoadSegmentCheckpoint(item.Id, item.Kind, item.StartNodeId, item.EndNodeId)).ToArray();
    public IReadOnlyList<SimulationLaneCheckpoint> CreateLaneCheckpoint() => Sort(lanes.Values, static item => item.Id.Value)
        .Select(static item => new SimulationLaneCheckpoint(item.Id, item.SegmentId, item.Direction, item.Order, item.WidthMeters, item.SpeedLimitMetersPerSecond)).ToArray();
    public IReadOnlyList<SimulationLaneConnectionCheckpoint> CreateConnectionCheckpoint() => Sort(connections.Values, static item => item.Id.Value)
        .Select(static item => new SimulationLaneConnectionCheckpoint(item.Id, item.FromLaneId, item.ToLaneId, item.ViaNodeId, item.Movement)).ToArray();
    public IReadOnlyList<SimulationRoadAccessPointCheckpoint> CreateAccessPointCheckpoint() => Sort(accessPoints.Values, static item => item.Id.Value)
        .Select(static item => new SimulationRoadAccessPointCheckpoint(item.Id, item.SegmentId, item.SegmentOffset, item.BuildingId, item.PoiId, item.Mode)).ToArray();

    public void Restore(SimulationCheckpoint checkpoint)
    {
        nodes.Clear(); segments.Clear(); lanes.Clear(); connections.Clear(); accessPoints.Clear(); degreeByNode.Clear();
        foreach (var node in checkpoint.RoadNodes)
        {
            var snapshot = new RoadNodeSnapshot(node.Id, node.Kind, node.Position);
            nodes.Add(node.Id, snapshot); degreeByNode.Add(node.Id, 0); spatialIndex.RegisterNode(node.Id, node.Position);
        }
        foreach (var segment in checkpoint.RoadSegments)
        {
            var snapshot = new RoadSegmentSnapshot(segment.Id, segment.Kind, segment.StartNodeId, segment.EndNodeId);
            segments.Add(segment.Id, snapshot); degreeByNode[segment.StartNodeId]++; degreeByNode[segment.EndNodeId]++;
            spatialIndex.RegisterSegment(segment.Id, nodes[segment.StartNodeId].Position, nodes[segment.EndNodeId].Position);
        }
        foreach (var lane in checkpoint.Lanes) lanes.Add(lane.Id, new LaneSnapshot(lane.Id, lane.SegmentId, lane.Direction, lane.Order, lane.WidthMeters, lane.SpeedLimitMetersPerSecond));
        foreach (var connection in checkpoint.LaneConnections) connections.Add(connection.Id, new LaneConnectionSnapshot(connection.Id, connection.FromLaneId, connection.ToLaneId, connection.ViaNodeId, connection.Movement));
        foreach (var access in checkpoint.RoadAccessPoints) accessPoints.Add(access.Id, new RoadAccessPointSnapshot(access.Id, access.SegmentId, access.SegmentOffset, access.BuildingId, access.PoiId, access.Mode));
        nextNodeId = checkpoint.NextRoadNodeId; nextSegmentId = checkpoint.NextRoadSegmentId; nextLaneId = checkpoint.NextLaneId;
        nextConnectionId = checkpoint.NextLaneConnectionId; nextAccessPointId = checkpoint.NextRoadAccessPointId;
    }

    private RoadNetworkSnapshot CreateSnapshotCore(HashSet<RoadNodeId> selectedNodes, HashSet<RoadSegmentId> selectedSegments)
    {
        var nodeArray = nodes.Values.Where(item => selectedNodes.Contains(item.Id)).OrderBy(item => item.Id.Value).ToArray();
        var segmentArray = segments.Values.Where(item => selectedSegments.Contains(item.Id)).OrderBy(item => item.Id.Value).ToArray();
        var laneArray = lanes.Values.Where(item => selectedSegments.Contains(item.SegmentId)).OrderBy(item => item.Id.Value).ToArray();
        var selectedLanes = laneArray.Select(static item => item.Id).ToHashSet();
        var connectionArray = connections.Values.Where(item => selectedLanes.Contains(item.FromLaneId) && selectedLanes.Contains(item.ToLaneId)).OrderBy(item => item.Id.Value).ToArray();
        var accessArray = accessPoints.Values.Where(item => selectedSegments.Contains(item.SegmentId)).OrderBy(item => item.Id.Value).ToArray();
        return new RoadNetworkSnapshot(nodeArray, segmentArray, laneArray, connectionArray, accessArray);
    }

    private void ValidateSegmentNodes(RoadNodeId startNodeId, RoadNodeId endNodeId, RoadSegmentId? excluding)
    {
        if (startNodeId == endNodeId) throw new ArgumentException("A road segment must connect two distinct road nodes.");
        if (!nodes.ContainsKey(startNodeId)) throw new ArgumentException($"Road node {startNodeId.Value} does not exist.", nameof(startNodeId));
        if (!nodes.ContainsKey(endNodeId)) throw new ArgumentException($"Road node {endNodeId.Value} does not exist.", nameof(endNodeId));
        ValidateAttachment(startNodeId, excluding);
        ValidateAttachment(endNodeId, excluding);
    }

    private void ValidateAttachment(RoadNodeId nodeId, RoadSegmentId? excluding)
    {
        if (nodes[nodeId].Kind == RoadNodeKind.Intersection) return;
        var degree = degreeByNode[nodeId];
        if (excluding is { } segmentId && segments.TryGetValue(segmentId, out var segment) && (segment.StartNodeId == nodeId || segment.EndNodeId == nodeId)) degree--;
        if (degree > 0) throw new InvalidOperationException($"Endpoint road node {nodeId.Value} cannot connect more than one segment; promote it to Intersection first.");
    }

    private void ValidateLane(RoadSegmentId segmentId, LaneDirection direction, ushort order, double widthMeters, double speedLimit, LaneId? excluding)
    {
        if (!segments.ContainsKey(segmentId)) throw new ArgumentException($"Road segment {segmentId.Value} does not exist.", nameof(segmentId));
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!double.IsFinite(widthMeters) || widthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(widthMeters));
        if (!double.IsFinite(speedLimit) || speedLimit <= 0) throw new ArgumentOutOfRangeException(nameof(speedLimit));
        if (lanes.Values.Any(item => item.Id != excluding && item.SegmentId == segmentId && item.Direction == direction && item.Order == order))
            throw new ArgumentException($"Lane order {order} is already used for direction {direction} on road segment {segmentId.Value}.", nameof(order));
    }

    private void ValidateConnection(LaneId fromLaneId, LaneId toLaneId, RoadNodeId viaNodeId, TurnMovement movement, LaneConnectionId? excluding)
    {
        if (fromLaneId == toLaneId) throw new ArgumentException("A lane connection must connect two distinct lanes.");
        if (!lanes.TryGetValue(fromLaneId, out var from)) throw new ArgumentException($"Lane {fromLaneId.Value} does not exist.", nameof(fromLaneId));
        if (!lanes.TryGetValue(toLaneId, out var to)) throw new ArgumentException($"Lane {toLaneId.Value} does not exist.", nameof(toLaneId));
        if (!nodes.TryGetValue(viaNodeId, out var via)) throw new ArgumentException($"Road node {viaNodeId.Value} does not exist.", nameof(viaNodeId));
        if (via.Kind != RoadNodeKind.Intersection) throw new InvalidOperationException($"Lane connections require an Intersection road node; {viaNodeId.Value} is {via.Kind}.");
        if (!Enum.IsDefined(movement)) throw new ArgumentOutOfRangeException(nameof(movement));
        if (GetExitNode(from) != viaNodeId || GetEntryNode(to) != viaNodeId) throw new InvalidOperationException("Lane directions do not enter and exit through the declared intersection node.");
        if (connections.Values.Any(item => item.Id != excluding && item.FromLaneId == fromLaneId && item.ToLaneId == toLaneId && item.ViaNodeId == viaNodeId))
            throw new ArgumentException("An equivalent lane connection already exists.");
    }

    private void ValidateAccessPoint(RoadSegmentId segmentId, double offset, BuildingId? buildingId, PoiId? poiId, RoadAccessMode mode)
    {
        if (!segments.ContainsKey(segmentId)) throw new ArgumentException($"Road segment {segmentId.Value} does not exist.", nameof(segmentId));
        if (!double.IsFinite(offset) || offset < 0 || offset > 1) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Road access point offset must be between zero and one.");
        if (buildingId is null && poiId is null) throw new ArgumentException("A road access point must reference a Building, POI, or both.");
        const RoadAccessMode supported = RoadAccessMode.Motor | RoadAccessMode.Foot;
        if (mode == RoadAccessMode.None || (mode & ~supported) != 0) throw new ArgumentOutOfRangeException(nameof(mode));
    }

    private void ValidateConnectionsForLane(LaneId laneId)
    {
        foreach (var connection in connections.Values.Where(item => item.FromLaneId == laneId || item.ToLaneId == laneId))
            ValidateConnection(connection.FromLaneId, connection.ToLaneId, connection.ViaNodeId, connection.Movement, connection.Id);
    }

    private void ValidateConnectionsForSegment(RoadSegmentId segmentId)
    {
        foreach (var lane in lanes.Values.Where(item => item.SegmentId == segmentId)) ValidateConnectionsForLane(lane.Id);
    }

    private RoadNodeId GetEntryNode(LaneSnapshot lane)
    {
        var segment = segments[lane.SegmentId];
        return lane.Direction == LaneDirection.Forward ? segment.StartNodeId : segment.EndNodeId;
    }

    private RoadNodeId GetExitNode(LaneSnapshot lane)
    {
        var segment = segments[lane.SegmentId];
        return lane.Direction == LaneDirection.Forward ? segment.EndNodeId : segment.StartNodeId;
    }

    private static T[] Sort<T>(IEnumerable<T> source, Func<T, ulong> key) => source.OrderBy(key).ToArray();

    private static void EnsureCapacity(ulong nextId, string entityName)
    {
        if (nextId == ulong.MaxValue) throw new OverflowException($"{entityName} ID capacity has been exhausted.");
    }
}
