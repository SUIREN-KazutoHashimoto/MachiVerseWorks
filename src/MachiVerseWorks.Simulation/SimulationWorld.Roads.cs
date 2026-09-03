using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly RoadNetworkStore _roads;

    public int RoadNodeCount => _roads.NodeCount;
    public int RoadSegmentCount => _roads.SegmentCount;
    public int LaneCount => _roads.LaneCount;
    public int LaneConnectionCount => _roads.ConnectionCount;
    public int RoadAccessPointCount => _roads.AccessPointCount;

    public RoadNodeId CreateRoadNode(WorldPoint position, RoadNodeKind kind = RoadNodeKind.Endpoint)
    {
        ValidatePoint(position);
        ValidateEnum(kind, nameof(kind));
        EnsureRoadTopologyMutable();
        InvalidatePedestrianNetwork();
        var id = _roads.AddNode(position, kind);
        InvalidateRouting();
        return id;
    }

    public bool UpdateRoadNode(RoadNodeId id, WorldPoint position, RoadNodeKind kind)
    {
        ValidatePoint(position);
        ValidateEnum(kind, nameof(kind));
        ValidateIncidentRoadSegmentGeometry(id, position);
        EnsureRoadTopologyMutable();
        InvalidatePedestrianNetwork();
        var updated = _roads.UpdateNode(id, position, kind);
        if (updated) InvalidateRouting();
        return updated;
    }

    public bool RemoveRoadNode(RoadNodeId id)
    {
        EnsureRoadTopologyMutable();
        InvalidatePedestrianNetwork();
        var removed = _roads.RemoveNode(id);
        if (removed) InvalidateRouting();
        return removed;
    }

    public RoadSegmentId CreateRoadSegment(RoadNodeId startNodeId, RoadNodeId endNodeId, RoadKind kind = RoadKind.Local)
    {
        ValidateEnum(kind, nameof(kind));
        ValidateRoadSegmentGeometry(startNodeId, endNodeId);
        EnsureRoadTopologyMutable();
        InvalidatePedestrianNetwork();
        var id = _roads.AddSegment(startNodeId, endNodeId, kind);
        InvalidateRouting();
        return id;
    }

    public bool UpdateRoadSegment(RoadSegmentId id, RoadNodeId startNodeId, RoadNodeId endNodeId, RoadKind kind)
    {
        ValidateEnum(kind, nameof(kind));
        ValidateRoadSegmentGeometry(startNodeId, endNodeId);
        EnsureRoadTopologyMutable();
        InvalidatePedestrianNetwork();
        var updated = _roads.UpdateSegment(id, startNodeId, endNodeId, kind);
        if (updated) InvalidateRouting();
        return updated;
    }

    public bool RemoveRoadSegment(RoadSegmentId id)
    {
        EnsureRoadTopologyMutable();
        InvalidatePedestrianNetwork();
        var removed = _roads.RemoveSegment(id);
        if (removed) InvalidateRouting();
        return removed;
    }

    public LaneId CreateLane(RoadSegmentId segmentId, LaneDirection direction, ushort order, double widthMeters = 3.5d, double speedLimitMetersPerSecond = 13.8888888889d)
    {
        EnsureRoadTopologyMutable();
        var id = _roads.AddLane(segmentId, direction, order, widthMeters, speedLimitMetersPerSecond);
        InvalidateRouting();
        return id;
    }

    public bool UpdateLane(LaneId id, RoadSegmentId segmentId, LaneDirection direction, ushort order, double widthMeters, double speedLimitMetersPerSecond)
    {
        EnsureRoadTopologyMutable();
        var updated = _roads.UpdateLane(id, segmentId, direction, order, widthMeters, speedLimitMetersPerSecond);
        if (updated) InvalidateRouting();
        return updated;
    }

    public bool RemoveLane(LaneId id)
    {
        if (_multimodalTransit.ContainsLaneReference(id))
            throw new InvalidOperationException($"Lane {id.Value} cannot be removed while a Multimodal Transit stop references it.");
        EnsureRoadTopologyMutable();
        var removed = _roads.RemoveLane(id);
        if (removed) InvalidateRouting();
        return removed;
    }

    public LaneConnectionId CreateLaneConnection(LaneId fromLaneId, LaneId toLaneId, RoadNodeId viaNodeId, TurnMovement movement = TurnMovement.Unspecified)
    {
        EnsureRoadTopologyMutable();
        var id = _roads.AddConnection(fromLaneId, toLaneId, viaNodeId, movement);
        InvalidateRouting();
        return id;
    }

    public bool UpdateLaneConnection(LaneConnectionId id, LaneId fromLaneId, LaneId toLaneId, RoadNodeId viaNodeId, TurnMovement movement)
    {
        EnsureRoadTopologyMutable();
        var updated = _roads.UpdateConnection(id, fromLaneId, toLaneId, viaNodeId, movement);
        if (updated) InvalidateRouting();
        return updated;
    }

    public bool RemoveLaneConnection(LaneConnectionId id)
    {
        EnsureRoadTopologyMutable();
        var removed = _roads.RemoveConnection(id);
        if (removed) InvalidateRouting();
        return removed;
    }

    public RoadAccessPointId CreateRoadAccessPoint(RoadSegmentId segmentId, double segmentOffset, BuildingId? buildingId = null, PoiId? poiId = null, RoadAccessMode mode = RoadAccessMode.Motor)
    {
        ValidateAccessReferences(buildingId, poiId); InvalidatePedestrianNetwork(); return _roads.AddAccessPoint(segmentId, segmentOffset, buildingId, poiId, mode);
    }

    public bool UpdateRoadAccessPoint(RoadAccessPointId id, RoadSegmentId segmentId, double segmentOffset, BuildingId? buildingId, PoiId? poiId, RoadAccessMode mode)
    {
        ValidateAccessReferences(buildingId, poiId);
        if (_railway.ContainsRoadAccessPointReference(id) && (mode & RoadAccessMode.Foot) == 0)
            throw new InvalidOperationException($"Road access point {id.Value} must remain walkable while a Platform access point references it.");
        if (ContainsLogisticsRoadAccessPointReference(id))
            throw new InvalidOperationException($"Road access point {id.Value} cannot be updated while Logistics inventory or shipment state references it.");
        InvalidatePedestrianNetwork();
        return _roads.UpdateAccessPoint(id, segmentId, segmentOffset, buildingId, poiId, mode);
    }

    public bool RemoveRoadAccessPoint(RoadAccessPointId id)
    {
        if (_railway.ContainsRoadAccessPointReference(id))
            throw new InvalidOperationException($"Road access point {id.Value} cannot be removed while a Platform access point references it.");
        if (ContainsLogisticsRoadAccessPointReference(id))
            throw new InvalidOperationException($"Road access point {id.Value} cannot be removed while Logistics inventory or shipment state references it.");
        InvalidatePedestrianNetwork();
        return _roads.RemoveAccessPoint(id);
    }

    public RoadNetworkSnapshot CreateRoadNetworkSnapshot() => _roads.CreateSnapshot();
    public RoadNetworkSnapshot CreateRoadNetworkSnapshot(WorldVolume volume)
    {
        _spatialIndex.ValidatePosition(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ));
        _spatialIndex.ValidatePosition(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ));
        return _roads.CreateSnapshot(volume);
    }

    public bool TryGetRoadNodeSnapshot(RoadNodeId id, out RoadNodeSnapshot snapshot) => _roads.TryGetNode(id, out snapshot);
    public bool TryGetRoadSegmentSnapshot(RoadSegmentId id, out RoadSegmentSnapshot snapshot) => _roads.TryGetSegment(id, out snapshot);
    public bool TryGetLaneSnapshot(LaneId id, out LaneSnapshot snapshot) => _roads.TryGetLane(id, out snapshot);
    public bool TryGetLaneConnectionSnapshot(LaneConnectionId id, out LaneConnectionSnapshot snapshot) => _roads.TryGetConnection(id, out snapshot);
    public bool TryGetRoadAccessPointSnapshot(RoadAccessPointId id, out RoadAccessPointSnapshot snapshot) => _roads.TryGetAccessPoint(id, out snapshot);

    private void EnsureRoadTopologyMutable()
    {
        if (_vehicles.Count > 0)
            throw new InvalidOperationException("Road topology cannot be changed while stored Vehicles reference derived routes. Remove them before mutating Road nodes, segments, lanes, or lane connections.");
    }

    private void ValidateRoadSegmentGeometry(RoadNodeId startNodeId, RoadNodeId endNodeId)
    {
        if (!_roads.TryGetNode(startNodeId, out var start) || !_roads.TryGetNode(endNodeId, out var end)) return;
        if (start.Position == end.Position)
            throw new ArgumentException("A road segment must have non-zero 3D length.");
    }

    private void ValidateIncidentRoadSegmentGeometry(RoadNodeId nodeId, WorldPoint position)
    {
        if (!_roads.TryGetNode(nodeId, out _)) return;
        foreach (var segment in _roads.GetIncidentSegments(nodeId))
        {
            var otherNodeId = segment.StartNodeId == nodeId ? segment.EndNodeId : segment.StartNodeId;
            if (_roads.TryGetNode(otherNodeId, out var other) && position == other.Position)
                throw new ArgumentException("Updating the road node would create a zero-length road segment.", nameof(position));
        }
    }

    private void ValidateAccessReferences(BuildingId? buildingId, PoiId? poiId)
    {
        if (buildingId is { } linkedBuilding && !_buildings.Contains(linkedBuilding)) throw new ArgumentException($"Building {linkedBuilding.Value} does not exist.", nameof(buildingId));
        if (poiId is { } linkedPoi && !_pois.TryGetSnapshot(linkedPoi, out _)) throw new ArgumentException($"POI {linkedPoi.Value} does not exist.", nameof(poiId));
    }

    private static void ValidateRoadNetworkCheckpoint(SimulationCheckpoint checkpoint, double cellSize)
    {
        ValidateNextId(checkpoint.NextRoadNodeId, checkpoint.RoadNodes.Select(static item => item.Id.Value), "Road node");
        ValidateNextId(checkpoint.NextRoadSegmentId, checkpoint.RoadSegments.Select(static item => item.Id.Value), "Road segment");
        ValidateNextId(checkpoint.NextLaneId, checkpoint.Lanes.Select(static item => item.Id.Value), "Lane");
        ValidateNextId(checkpoint.NextLaneConnectionId, checkpoint.LaneConnections.Select(static item => item.Id.Value), "Lane connection");
        ValidateNextId(checkpoint.NextRoadAccessPointId, checkpoint.RoadAccessPoints.Select(static item => item.Id.Value), "Road access point");

        var nodes = new Dictionary<RoadNodeId, SimulationRoadNodeCheckpoint>();
        foreach (var node in checkpoint.RoadNodes)
        {
            if (node.Id.Value == 0 || !nodes.TryAdd(node.Id, node)) throw new ArgumentException($"Road node ID {node.Id.Value} is zero or duplicated.", nameof(checkpoint));
            ValidateEnum(node.Kind, nameof(checkpoint)); _ = SpatialGrid.ToCell(node.Position, cellSize);
        }

        var segments = new Dictionary<RoadSegmentId, SimulationRoadSegmentCheckpoint>();
        var degree = nodes.Keys.ToDictionary(static id => id, static _ => 0);
        foreach (var segment in checkpoint.RoadSegments)
        {
            if (segment.Id.Value == 0 || !segments.TryAdd(segment.Id, segment)) throw new ArgumentException($"Road segment ID {segment.Id.Value} is zero or duplicated.", nameof(checkpoint));
            ValidateEnum(segment.Kind, nameof(checkpoint));
            if (segment.StartNodeId == segment.EndNodeId
                || !nodes.TryGetValue(segment.StartNodeId, out var startNode)
                || !nodes.TryGetValue(segment.EndNodeId, out var endNode))
                throw new ArgumentException($"Road segment {segment.Id.Value} has invalid node references.", nameof(checkpoint));
            if (startNode.Position == endNode.Position) throw new ArgumentException($"Road segment {segment.Id.Value} has zero-length geometry.", nameof(checkpoint));
            degree[segment.StartNodeId]++; degree[segment.EndNodeId]++;
        }
        foreach (var entry in degree) if (nodes[entry.Key].Kind == RoadNodeKind.Endpoint && entry.Value > 1) throw new ArgumentException($"Endpoint road node {entry.Key.Value} has degree {entry.Value}.", nameof(checkpoint));

        var lanes = new Dictionary<LaneId, SimulationLaneCheckpoint>();
        var laneOrders = new HashSet<(RoadSegmentId, LaneDirection, ushort)>();
        var laneWidthsByDirection = new Dictionary<(RoadSegmentId SegmentId, LaneDirection Direction), double>();
        foreach (var lane in checkpoint.Lanes)
        {
            if (lane.Id.Value == 0 || !lanes.TryAdd(lane.Id, lane)) throw new ArgumentException($"Lane ID {lane.Id.Value} is zero or duplicated.", nameof(checkpoint));
            ValidateEnum(lane.Direction, nameof(checkpoint));
            if (!segments.ContainsKey(lane.SegmentId)
                || !double.IsFinite(lane.WidthMeters)
                || lane.WidthMeters <= 0d
                || lane.WidthMeters > RoadNetworkStore.MaximumLaneWidthMeters
                || !double.IsFinite(lane.SpeedLimitMetersPerSecond)
                || lane.SpeedLimitMetersPerSecond <= 0d)
            {
                throw new ArgumentException($"Lane {lane.Id.Value} is invalid.", nameof(checkpoint));
            }
            if (!laneOrders.Add((lane.SegmentId, lane.Direction, lane.Order))) throw new ArgumentException("Lane order is duplicated within a segment and direction.", nameof(checkpoint));

            var key = (lane.SegmentId, lane.Direction);
            var totalWidth = laneWidthsByDirection.GetValueOrDefault(key) + lane.WidthMeters;
            if (!double.IsFinite(totalWidth) || totalWidth > RoadNetworkStore.MaximumDirectionalRoadwayWidthMeters)
                throw new ArgumentException($"Lane widths for Road segment {lane.SegmentId.Value} direction {lane.Direction} exceed the supported roadway width.", nameof(checkpoint));
            laneWidthsByDirection[key] = totalWidth;
        }

        var connectionIds = new HashSet<LaneConnectionId>();
        var connectionKeys = new HashSet<(LaneId, LaneId, RoadNodeId)>();
        foreach (var connection in checkpoint.LaneConnections)
        {
            if (connection.Id.Value == 0 || !connectionIds.Add(connection.Id)) throw new ArgumentException($"Lane connection ID {connection.Id.Value} is zero or duplicated.", nameof(checkpoint));
            ValidateEnum(connection.Movement, nameof(checkpoint));
            if (!lanes.TryGetValue(connection.FromLaneId, out var from) || !lanes.TryGetValue(connection.ToLaneId, out var to) || connection.FromLaneId == connection.ToLaneId || !nodes.TryGetValue(connection.ViaNodeId, out var via) || via.Kind != RoadNodeKind.Intersection) throw new ArgumentException($"Lane connection {connection.Id.Value} has invalid references.", nameof(checkpoint));
            if (ExitNode(from, segments) != connection.ViaNodeId || EntryNode(to, segments) != connection.ViaNodeId) throw new ArgumentException($"Lane connection {connection.Id.Value} directions do not meet at its via node.", nameof(checkpoint));
            if (!connectionKeys.Add((connection.FromLaneId, connection.ToLaneId, connection.ViaNodeId))) throw new ArgumentException("Equivalent lane connection is duplicated.", nameof(checkpoint));
        }

        var buildings = checkpoint.Buildings.Select(static item => item.Id).ToHashSet();
        var pois = checkpoint.Pois.Select(static item => item.Id).ToHashSet();
        var accessIds = new HashSet<RoadAccessPointId>();
        foreach (var access in checkpoint.RoadAccessPoints)
        {
            if (access.Id.Value == 0 || !accessIds.Add(access.Id) || !segments.ContainsKey(access.SegmentId) || !double.IsFinite(access.SegmentOffset) || access.SegmentOffset < 0 || access.SegmentOffset > 1 || (access.BuildingId is null && access.PoiId is null)) throw new ArgumentException($"Road access point {access.Id.Value} is invalid.", nameof(checkpoint));
            if (access.BuildingId is { } building && !buildings.Contains(building)) throw new ArgumentException($"Road access point {access.Id.Value} references missing Building {building.Value}.", nameof(checkpoint));
            if (access.PoiId is { } poi && !pois.Contains(poi)) throw new ArgumentException($"Road access point {access.Id.Value} references missing POI {poi.Value}.", nameof(checkpoint));
            const RoadAccessMode supported = RoadAccessMode.Motor | RoadAccessMode.Foot;
            if (access.Mode == RoadAccessMode.None || (access.Mode & ~supported) != 0) throw new ArgumentException($"Road access point {access.Id.Value} has an invalid access mode.", nameof(checkpoint));
        }
    }

    private static RoadNodeId EntryNode(SimulationLaneCheckpoint lane, Dictionary<RoadSegmentId, SimulationRoadSegmentCheckpoint> segments)
    {
        var segment = segments[lane.SegmentId]; return lane.Direction == LaneDirection.Forward ? segment.StartNodeId : segment.EndNodeId;
    }

    private static RoadNodeId ExitNode(SimulationLaneCheckpoint lane, Dictionary<RoadSegmentId, SimulationRoadSegmentCheckpoint> segments)
    {
        var segment = segments[lane.SegmentId]; return lane.Direction == LaneDirection.Forward ? segment.EndNodeId : segment.StartNodeId;
    }

    private static void ValidateNextId(ulong nextId, IEnumerable<ulong> ids, string entityName)
    {
        if (nextId == 0) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next {entityName} ID must be greater than zero.");
        var maximum = 0UL;
        foreach (var id in ids) maximum = Math.Max(maximum, id);
        if (nextId <= maximum) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next {entityName} ID must be greater than every stored ID.");
    }

    private static void ValidateEnum<T>(T value, string parameterName) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(parameterName, value, $"{typeof(T).Name} value is not defined.");
    }
}
