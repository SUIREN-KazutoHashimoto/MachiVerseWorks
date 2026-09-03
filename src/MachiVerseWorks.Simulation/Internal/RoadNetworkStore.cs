namespace MachiVerseWorks.Simulation.Internal;

internal sealed class RoadNetworkStore
{
    private readonly Dictionary<RoadNodeId, RoadNodeSnapshot> nodes = [];
    private readonly Dictionary<RoadSegmentId, RoadSegmentSnapshot> segments = [];
    private readonly Dictionary<LaneId, LaneSnapshot> lanes = [];
    private readonly Dictionary<LaneConnectionId, LaneConnectionSnapshot> connections = [];
    private readonly Dictionary<RoadAccessPointId, RoadAccessPointSnapshot> accessPoints = [];
    private readonly Dictionary<BuildingId, List<RoadAccessPointId>> accessPointIdsByBuilding = [];
    private readonly Dictionary<PoiId, List<RoadAccessPointId>> accessPointIdsByPoi = [];
    private readonly Dictionary<RoadNodeId, int> degreeByNode = [];
    private readonly Dictionary<RoadNodeId, HashSet<RoadSegmentId>> incidentSegmentIdsByNode = [];
    private readonly RoadSpatialIndex spatialIndex;
    private ulong nextNodeId = 1, nextSegmentId = 1, nextLaneId = 1, nextConnectionId = 1, nextAccessPointId = 1;
    public RoadNetworkStore(double cellSize) => spatialIndex = new RoadSpatialIndex(cellSize);
    public int NodeCount => nodes.Count; public int SegmentCount => segments.Count; public int LaneCount => lanes.Count; public int ConnectionCount => connections.Count; public int AccessPointCount => accessPoints.Count;
    public ulong NextNodeId => nextNodeId; public ulong NextSegmentId => nextSegmentId; public ulong NextLaneId => nextLaneId; public ulong NextConnectionId => nextConnectionId; public ulong NextAccessPointId => nextAccessPointId;

    public RoadNodeId AddNode(WorldPoint position, RoadNodeKind kind) { EnsureCapacity(nextNodeId, "Road node"); spatialIndex.ValidatePosition(position); var id = new RoadNodeId(nextNodeId++); nodes.Add(id, new(id, kind, position)); degreeByNode.Add(id, 0); incidentSegmentIdsByNode.Add(id, []); spatialIndex.RegisterNode(id, position); return id; }
    public bool UpdateNode(RoadNodeId id, WorldPoint position, RoadNodeKind kind)
    {
        if (!nodes.ContainsKey(id)) return false;
        spatialIndex.ValidatePosition(position);
        if (kind == RoadNodeKind.Endpoint && degreeByNode[id] > 1) throw new InvalidOperationException($"Road node {id.Value} has multiple incident segments and must remain an intersection.");
        if (kind == RoadNodeKind.Endpoint && connections.Values.Any(x => x.ViaNodeId == id)) throw new InvalidOperationException($"Road node {id.Value} must remain an intersection while lane connections reference it.");
        nodes[id] = new(id, kind, position); spatialIndex.UpdateNode(id, position);
        foreach (var segmentId in incidentSegmentIdsByNode[id]) { var s = segments[segmentId]; spatialIndex.UpdateSegment(s.Id, nodes[s.StartNodeId].Position, nodes[s.EndNodeId].Position); }
        return true;
    }
    public bool RemoveNode(RoadNodeId id) { if (!nodes.ContainsKey(id)) return false; if (degreeByNode[id] != 0) throw new InvalidOperationException($"Road node {id.Value} cannot be removed while segments reference it."); if (connections.Values.Any(x => x.ViaNodeId == id)) throw new InvalidOperationException($"Road node {id.Value} cannot be removed while lane connections reference it."); spatialIndex.RemoveNode(id); degreeByNode.Remove(id); incidentSegmentIdsByNode.Remove(id); return nodes.Remove(id); }

    public RoadSegmentId AddSegment(RoadNodeId start, RoadNodeId end, RoadKind kind) { EnsureCapacity(nextSegmentId, "Road segment"); ValidateSegmentNodes(start, end, null); var id = new RoadSegmentId(nextSegmentId++); segments.Add(id, new(id, kind, start, end)); degreeByNode[start]++; degreeByNode[end]++; AttachSegment(id, start, end); spatialIndex.RegisterSegment(id, nodes[start].Position, nodes[end].Position); return id; }
    public bool UpdateSegment(RoadSegmentId id, RoadNodeId start, RoadNodeId end, RoadKind kind)
    {
        if (!segments.TryGetValue(id, out var previous)) return false; ValidateSegmentNodes(start, end, id);
        degreeByNode[previous.StartNodeId]--; degreeByNode[previous.EndNodeId]--; degreeByNode[start]++; degreeByNode[end]++; segments[id] = new(id, kind, start, end); spatialIndex.UpdateSegment(id, nodes[start].Position, nodes[end].Position);
        try { ValidateConnectionsForSegment(id); }
        catch { spatialIndex.UpdateSegment(id, nodes[previous.StartNodeId].Position, nodes[previous.EndNodeId].Position); segments[id] = previous; degreeByNode[start]--; degreeByNode[end]--; degreeByNode[previous.StartNodeId]++; degreeByNode[previous.EndNodeId]++; throw; }
        DetachSegment(id, previous.StartNodeId, previous.EndNodeId); AttachSegment(id, start, end);
        return true;
    }
    public bool RemoveSegment(RoadSegmentId id) { if (!segments.TryGetValue(id, out var s)) return false; if (lanes.Values.Any(l => l.SegmentId == id)) throw new InvalidOperationException($"Road segment {id.Value} cannot be removed while lanes reference it."); if (accessPoints.Values.Any(a => a.SegmentId == id)) throw new InvalidOperationException($"Road segment {id.Value} cannot be removed while access points reference it."); spatialIndex.RemoveSegment(id); degreeByNode[s.StartNodeId]--; degreeByNode[s.EndNodeId]--; DetachSegment(id, s.StartNodeId, s.EndNodeId); return segments.Remove(id); }

    public LaneId AddLane(RoadSegmentId segment, LaneDirection direction, ushort order, double width, double speed) { EnsureCapacity(nextLaneId, "Lane"); ValidateLane(segment, direction, order, width, speed, null); var id = new LaneId(nextLaneId++); lanes.Add(id, new(id, segment, direction, order, width, speed)); return id; }
    public bool UpdateLane(LaneId id, RoadSegmentId segment, LaneDirection direction, ushort order, double width, double speed) { if (!lanes.TryGetValue(id, out var previous)) return false; ValidateLane(segment, direction, order, width, speed, id); lanes[id] = new(id, segment, direction, order, width, speed); try { ValidateConnectionsForLane(id); } catch { lanes[id] = previous; throw; } return true; }
    public bool RemoveLane(LaneId id) { if (!lanes.ContainsKey(id)) return false; if (connections.Values.Any(x => x.FromLaneId == id || x.ToLaneId == id)) throw new InvalidOperationException($"Lane {id.Value} cannot be removed while lane connections reference it."); return lanes.Remove(id); }

    public LaneConnectionId AddConnection(LaneId from, LaneId to, RoadNodeId via, TurnMovement movement) { EnsureCapacity(nextConnectionId, "Lane connection"); ValidateConnection(from, to, via, movement, null); var id = new LaneConnectionId(nextConnectionId++); connections.Add(id, new(id, from, to, via, movement)); return id; }
    public bool UpdateConnection(LaneConnectionId id, LaneId from, LaneId to, RoadNodeId via, TurnMovement movement) { if (!connections.ContainsKey(id)) return false; ValidateConnection(from, to, via, movement, id); connections[id] = new(id, from, to, via, movement); return true; }
    public bool RemoveConnection(LaneConnectionId id) => connections.Remove(id);

    public RoadAccessPointId AddAccessPoint(RoadSegmentId segment, double offset, BuildingId? building, PoiId? poi, RoadAccessMode mode)
    {
        EnsureCapacity(nextAccessPointId, "Road access point"); ValidateAccessPoint(segment, offset, building, poi, mode);
        var id = new RoadAccessPointId(nextAccessPointId++); var snapshot = new RoadAccessPointSnapshot(id, segment, offset, building, poi, mode); accessPoints.Add(id, snapshot); AttachAccessPoint(snapshot); return id;
    }
    public bool UpdateAccessPoint(RoadAccessPointId id, RoadSegmentId segment, double offset, BuildingId? building, PoiId? poi, RoadAccessMode mode)
    {
        if (!accessPoints.TryGetValue(id, out var previous)) return false;
        ValidateAccessPoint(segment, offset, building, poi, mode);
        var updated = new RoadAccessPointSnapshot(id, segment, offset, building, poi, mode);
        DetachAccessPoint(previous); accessPoints[id] = updated; AttachAccessPoint(updated); return true;
    }
    public bool RemoveAccessPoint(RoadAccessPointId id)
    {
        if (!accessPoints.TryGetValue(id, out var previous)) return false;
        DetachAccessPoint(previous); return accessPoints.Remove(id);
    }
    public bool ContainsBuildingReference(BuildingId id) => accessPointIdsByBuilding.ContainsKey(id);
    public bool ContainsPoiReference(PoiId id) => accessPointIdsByPoi.ContainsKey(id);
    public bool TryGetNode(RoadNodeId id, out RoadNodeSnapshot snapshot) => nodes.TryGetValue(id, out snapshot); public bool TryGetSegment(RoadSegmentId id, out RoadSegmentSnapshot snapshot) => segments.TryGetValue(id, out snapshot); public bool TryGetLane(LaneId id, out LaneSnapshot snapshot) => lanes.TryGetValue(id, out snapshot); public bool TryGetConnection(LaneConnectionId id, out LaneConnectionSnapshot snapshot) => connections.TryGetValue(id, out snapshot); public bool TryGetAccessPoint(RoadAccessPointId id, out RoadAccessPointSnapshot snapshot) => accessPoints.TryGetValue(id, out snapshot);

    public RoadAccessPointSnapshot[] GetAccessPoints(TripEndpoint endpoint, RoadAccessMode mode)
    {
        List<RoadAccessPointId>? ids = endpoint.BuildingId is { } buildingId
            ? accessPointIdsByBuilding.GetValueOrDefault(buildingId)
            : endpoint.PoiId is { } poiId ? accessPointIdsByPoi.GetValueOrDefault(poiId) : null;
        if (ids is null || ids.Count == 0) return [];
        var result = new List<RoadAccessPointSnapshot>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            var access = accessPoints[ids[index]];
            if ((access.Mode & mode) != 0) result.Add(access);
        }
        return result.ToArray();
    }

    public bool TryGetAccessPointPosition(RoadAccessPointId id, out WorldPoint position)
    {
        if (!accessPoints.TryGetValue(id, out var access)
            || !segments.TryGetValue(access.SegmentId, out var segment)
            || !nodes.TryGetValue(segment.StartNodeId, out var start)
            || !nodes.TryGetValue(segment.EndNodeId, out var end))
        {
            position = default;
            return false;
        }
        var offset = access.SegmentOffset;
        position = new WorldPoint(
            start.Position.X + ((end.Position.X - start.Position.X) * offset),
            start.Position.Y + ((end.Position.Y - start.Position.Y) * offset),
            start.Position.Z + ((end.Position.Z - start.Position.Z) * offset));
        return true;
    }

    public IEnumerable<RoadSegmentSnapshot> GetIncidentSegments(RoadNodeId id)
    {
        if (!incidentSegmentIdsByNode.TryGetValue(id, out var segmentIds)) yield break;
        foreach (var segmentId in segmentIds) yield return segments[segmentId];
    }
    public RoadNetworkSnapshot CreateSnapshot() => CreateSnapshotCore(nodes.Keys.ToHashSet(), segments.Keys.ToHashSet());
    public RoadNetworkSnapshot CreateSnapshot(WorldVolume volume) { var selectedNodes = spatialIndex.QueryNodes(volume); var selectedSegments = spatialIndex.QuerySegments(volume); foreach (var id in selectedSegments) { var s = segments[id]; selectedNodes.Add(s.StartNodeId); selectedNodes.Add(s.EndNodeId); } return CreateSnapshotCore(selectedNodes, selectedSegments); }

    public IReadOnlyList<SimulationRoadNodeCheckpoint> CreateNodeCheckpoint() => nodes.Values.OrderBy(x => x.Id.Value).Select(static x => new SimulationRoadNodeCheckpoint(x.Id, x.Kind, x.Position)).ToArray();
    public IReadOnlyList<SimulationRoadSegmentCheckpoint> CreateSegmentCheckpoint() => segments.Values.OrderBy(x => x.Id.Value).Select(static x => new SimulationRoadSegmentCheckpoint(x.Id, x.Kind, x.StartNodeId, x.EndNodeId)).ToArray();
    public IReadOnlyList<SimulationLaneCheckpoint> CreateLaneCheckpoint() => lanes.Values.OrderBy(x => x.Id.Value).Select(static x => new SimulationLaneCheckpoint(x.Id, x.SegmentId, x.Direction, x.Order, x.WidthMeters, x.SpeedLimitMetersPerSecond)).ToArray();
    public IReadOnlyList<SimulationLaneConnectionCheckpoint> CreateConnectionCheckpoint() => connections.Values.OrderBy(x => x.Id.Value).Select(static x => new SimulationLaneConnectionCheckpoint(x.Id, x.FromLaneId, x.ToLaneId, x.ViaNodeId, x.Movement)).ToArray();
    public IReadOnlyList<SimulationRoadAccessPointCheckpoint> CreateAccessPointCheckpoint() => accessPoints.Values.OrderBy(x => x.Id.Value).Select(static x => new SimulationRoadAccessPointCheckpoint(x.Id, x.SegmentId, x.SegmentOffset, x.BuildingId, x.PoiId, x.Mode)).ToArray();
    public void Restore(SimulationCheckpoint c)
    {
        nodes.Clear(); segments.Clear(); lanes.Clear(); connections.Clear(); accessPoints.Clear(); accessPointIdsByBuilding.Clear(); accessPointIdsByPoi.Clear(); degreeByNode.Clear(); incidentSegmentIdsByNode.Clear();
        foreach (var n in c.RoadNodes) { nodes.Add(n.Id, new(n.Id, n.Kind, n.Position)); degreeByNode.Add(n.Id, 0); incidentSegmentIdsByNode.Add(n.Id, []); spatialIndex.RegisterNode(n.Id, n.Position); }
        foreach (var s in c.RoadSegments) { segments.Add(s.Id, new(s.Id, s.Kind, s.StartNodeId, s.EndNodeId)); degreeByNode[s.StartNodeId]++; degreeByNode[s.EndNodeId]++; AttachSegment(s.Id, s.StartNodeId, s.EndNodeId); spatialIndex.RegisterSegment(s.Id, nodes[s.StartNodeId].Position, nodes[s.EndNodeId].Position); }
        foreach (var x in c.Lanes) lanes.Add(x.Id, new(x.Id, x.SegmentId, x.Direction, x.Order, x.WidthMeters, x.SpeedLimitMetersPerSecond));
        foreach (var x in c.LaneConnections) connections.Add(x.Id, new(x.Id, x.FromLaneId, x.ToLaneId, x.ViaNodeId, x.Movement));
        foreach (var x in c.RoadAccessPoints) { var access = new RoadAccessPointSnapshot(x.Id, x.SegmentId, x.SegmentOffset, x.BuildingId, x.PoiId, x.Mode); accessPoints.Add(x.Id, access); AttachAccessPoint(access); }
        nextNodeId = c.NextRoadNodeId; nextSegmentId = c.NextRoadSegmentId; nextLaneId = c.NextLaneId; nextConnectionId = c.NextLaneConnectionId; nextAccessPointId = c.NextRoadAccessPointId;
    }

    private RoadNetworkSnapshot CreateSnapshotCore(HashSet<RoadNodeId> selectedNodes, HashSet<RoadSegmentId> selectedSegments)
    {
        var nodeArray = nodes.Values.Where(x => selectedNodes.Contains(x.Id)).OrderBy(x => x.Id.Value).ToArray(); var segmentArray = segments.Values.Where(x => selectedSegments.Contains(x.Id)).OrderBy(x => x.Id.Value).ToArray(); var laneArray = lanes.Values.Where(x => selectedSegments.Contains(x.SegmentId)).OrderBy(x => x.Id.Value).ToArray(); var selectedLanes = laneArray.Select(static x => x.Id).ToHashSet(); var connectionArray = connections.Values.Where(x => selectedLanes.Contains(x.FromLaneId) && selectedLanes.Contains(x.ToLaneId)).OrderBy(x => x.Id.Value).ToArray(); var accessArray = accessPoints.Values.Where(x => selectedSegments.Contains(x.SegmentId)).OrderBy(x => x.Id.Value).ToArray(); return new(nodeArray, segmentArray, laneArray, connectionArray, accessArray);
    }
    private void ValidateSegmentNodes(RoadNodeId start, RoadNodeId end, RoadSegmentId? excluding) { if (start == end) throw new ArgumentException("A road segment must connect two distinct road nodes."); if (!nodes.ContainsKey(start)) throw new ArgumentException($"Road node {start.Value} does not exist.", nameof(start)); if (!nodes.ContainsKey(end)) throw new ArgumentException($"Road node {end.Value} does not exist.", nameof(end)); ValidateAttachment(start, excluding); ValidateAttachment(end, excluding); }
    private void ValidateAttachment(RoadNodeId node, RoadSegmentId? excluding) { if (nodes[node].Kind == RoadNodeKind.Intersection) return; var degree = degreeByNode[node]; if (excluding is { } id && segments.TryGetValue(id, out var s) && (s.StartNodeId == node || s.EndNodeId == node)) degree--; if (degree > 0) throw new InvalidOperationException($"Endpoint road node {node.Value} cannot connect more than one segment; promote it to Intersection first."); }
    private void ValidateLane(RoadSegmentId segment, LaneDirection direction, ushort order, double width, double speed, LaneId? excluding)
    {
        if (!segments.ContainsKey(segment)) throw new ArgumentException($"Road segment {segment.Value} does not exist.", nameof(segment));
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!double.IsFinite(width) || width <= 0d)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Lane width must be finite and greater than zero.");
        if (!double.IsFinite(speed) || speed <= 0d) throw new ArgumentOutOfRangeException(nameof(speed));
        if (lanes.Values.Any(x => x.Id != excluding && x.SegmentId == segment && x.Direction == direction && x.Order == order))
            throw new ArgumentException($"Lane order {order} is already used for direction {direction} on road segment {segment.Value}.", nameof(order));

        var totalWidth = width;
        foreach (var lane in lanes.Values)
        {
            if (lane.Id == excluding || lane.SegmentId != segment || lane.Direction != direction) continue;
            if (lane.WidthMeters > double.MaxValue - totalWidth)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Total lane width for one segment direction must remain representable as a finite double.");
            totalWidth += lane.WidthMeters;
        }
    }
    private void ValidateConnection(LaneId fromId, LaneId toId, RoadNodeId viaId, TurnMovement movement, LaneConnectionId? excluding) { if (fromId == toId) throw new ArgumentException("A lane connection must connect two distinct lanes."); if (!lanes.TryGetValue(fromId, out var from)) throw new ArgumentException($"Lane {fromId.Value} does not exist.", nameof(fromId)); if (!lanes.TryGetValue(toId, out var to)) throw new ArgumentException($"Lane {toId.Value} does not exist.", nameof(toId)); if (!nodes.TryGetValue(viaId, out var via)) throw new ArgumentException($"Road node {viaId.Value} does not exist.", nameof(viaId)); if (via.Kind != RoadNodeKind.Intersection) throw new InvalidOperationException($"Lane connections require an Intersection road node; {viaId.Value} is {via.Kind}."); if (!Enum.IsDefined(movement)) throw new ArgumentOutOfRangeException(nameof(movement)); if (GetExitNode(from) != viaId || GetEntryNode(to) != viaId) throw new InvalidOperationException("Lane directions do not enter and exit through the declared intersection node."); if (connections.Values.Any(x => x.Id != excluding && x.FromLaneId == fromId && x.ToLaneId == toId && x.ViaNodeId == viaId)) throw new ArgumentException("An equivalent lane connection already exists."); }
    private void ValidateAccessPoint(RoadSegmentId segment, double offset, BuildingId? building, PoiId? poi, RoadAccessMode mode) { if (!segments.ContainsKey(segment)) throw new ArgumentException($"Road segment {segment.Value} does not exist.", nameof(segment)); if (!double.IsFinite(offset) || offset < 0 || offset > 1) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Road access point offset must be between zero and one."); if (building is null && poi is null) throw new ArgumentException("A road access point must reference a Building, POI, or both."); const RoadAccessMode supported = RoadAccessMode.Motor | RoadAccessMode.Foot; if (mode == RoadAccessMode.None || (mode & ~supported) != 0) throw new ArgumentOutOfRangeException(nameof(mode)); }
    private void ValidateConnectionsForLane(LaneId id) { foreach (var x in connections.Values.Where(x => x.FromLaneId == id || x.ToLaneId == id)) ValidateConnection(x.FromLaneId, x.ToLaneId, x.ViaNodeId, x.Movement, x.Id); }
    private void ValidateConnectionsForSegment(RoadSegmentId id) { foreach (var lane in lanes.Values.Where(x => x.SegmentId == id)) ValidateConnectionsForLane(lane.Id); }
    private void AttachSegment(RoadSegmentId segmentId, RoadNodeId start, RoadNodeId end) { incidentSegmentIdsByNode[start].Add(segmentId); incidentSegmentIdsByNode[end].Add(segmentId); }
    private void DetachSegment(RoadSegmentId segmentId, RoadNodeId start, RoadNodeId end) { incidentSegmentIdsByNode[start].Remove(segmentId); incidentSegmentIdsByNode[end].Remove(segmentId); }
    private RoadNodeId GetEntryNode(LaneSnapshot lane) { var s = segments[lane.SegmentId]; return lane.Direction == LaneDirection.Forward ? s.StartNodeId : s.EndNodeId; }
    private RoadNodeId GetExitNode(LaneSnapshot lane) { var s = segments[lane.SegmentId]; return lane.Direction == LaneDirection.Forward ? s.EndNodeId : s.StartNodeId; }

    private void AttachAccessPoint(RoadAccessPointSnapshot access)
    {
        if (access.BuildingId is { } buildingId) AddAccessIndex(accessPointIdsByBuilding, buildingId, access.Id);
        if (access.PoiId is { } poiId) AddAccessIndex(accessPointIdsByPoi, poiId, access.Id);
    }

    private void DetachAccessPoint(RoadAccessPointSnapshot access)
    {
        if (access.BuildingId is { } buildingId) RemoveAccessIndex(accessPointIdsByBuilding, buildingId, access.Id);
        if (access.PoiId is { } poiId) RemoveAccessIndex(accessPointIdsByPoi, poiId, access.Id);
    }

    private static void AddAccessIndex<T>(Dictionary<T, List<RoadAccessPointId>> index, T key, RoadAccessPointId id) where T : notnull
    {
        if (!index.TryGetValue(key, out var list)) { list = []; index.Add(key, list); }
        var position = list.BinarySearch(id, RoadAccessPointIdComparer.Instance);
        if (position < 0) list.Insert(~position, id);
    }

    private static void RemoveAccessIndex<T>(Dictionary<T, List<RoadAccessPointId>> index, T key, RoadAccessPointId id) where T : notnull
    {
        if (!index.TryGetValue(key, out var list)) return;
        list.Remove(id);
        if (list.Count == 0) index.Remove(key);
    }

    private sealed class RoadAccessPointIdComparer : IComparer<RoadAccessPointId>
    {
        public static RoadAccessPointIdComparer Instance { get; } = new();
        public int Compare(RoadAccessPointId left, RoadAccessPointId right) => left.Value.CompareTo(right.Value);
    }

    private static void EnsureCapacity(ulong nextId, string name) { if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted."); }
}
