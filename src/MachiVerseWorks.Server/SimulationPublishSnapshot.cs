using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationPublishSnapshot
{
    private readonly PublishedEntitySpatialIndex<AgentSnapshot> _agents;
    private readonly PublishedEntitySpatialIndex<PedestrianSnapshot> _pedestrians;
    private readonly PublishedEntitySpatialIndex<VehicleSnapshot> _vehicles;
    private readonly PublishedEntitySpatialIndex<IntersectionControllerSnapshot> _intersections;

    public SimulationPublishSnapshot(
        ulong tickCount,
        double spatialCellSize,
        AgentSnapshot[] agents,
        PedestrianSnapshot[] pedestrians,
        RoadNetworkReadModel roadNetwork)
        : this(
            tickCount,
            spatialCellSize,
            agents,
            pedestrians,
            [],
            new IntersectionControlSnapshot([], tickCount),
            roadNetwork)
    {
    }

    public SimulationPublishSnapshot(
        ulong tickCount,
        double spatialCellSize,
        AgentSnapshot[] agents,
        PedestrianSnapshot[] pedestrians,
        VehicleSnapshot[] vehicles,
        IntersectionControlSnapshot intersectionControl,
        RoadNetworkReadModel roadNetwork)
    {
        TickCount = tickCount;
        SpatialCellSize = spatialCellSize;
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(pedestrians);
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(intersectionControl);
        RoadNetwork = roadNetwork ?? throw new ArgumentNullException(nameof(roadNetwork));
        _agents = new PublishedEntitySpatialIndex<AgentSnapshot>(agents, spatialCellSize, static item => item.Position);
        _pedestrians = new PublishedEntitySpatialIndex<PedestrianSnapshot>(pedestrians, spatialCellSize, static item => item.Position);
        _vehicles = new PublishedEntitySpatialIndex<VehicleSnapshot>(vehicles, spatialCellSize, static item => item.Position);
        _intersections = new PublishedEntitySpatialIndex<IntersectionControllerSnapshot>(
            intersectionControl.Controllers.ToArray(),
            spatialCellSize,
            item => roadNetwork.GetNodePosition(item.IntersectionNodeId));
    }

    public ulong TickCount { get; }
    public double SpatialCellSize { get; }
    public RoadNetworkReadModel RoadNetwork { get; }

    public EntityPublishSnapshot QueryEntities(WorldVolume volume) => new(
        TickCount,
        _agents.Query(volume),
        _pedestrians.Query(volume),
        _vehicles.Query(volume),
        _intersections.Query(volume));

    public SubscriptionPublishSnapshot Query(WorldVolume volume)
    {
        var entities = QueryEntities(volume);
        return new SubscriptionPublishSnapshot(
            entities.TickCount,
            entities.Agents,
            entities.Pedestrians,
            RoadNetwork.Query(volume));
    }
}

internal sealed record EntityPublishSnapshot(
    ulong TickCount,
    AgentSnapshot[] Agents,
    PedestrianSnapshot[] Pedestrians,
    VehicleSnapshot[] Vehicles,
    IntersectionControllerSnapshot[] Intersections);

internal sealed record SubscriptionPublishSnapshot(
    ulong TickCount,
    AgentSnapshot[] Agents,
    PedestrianSnapshot[] Pedestrians,
    RoadNetworkSnapshot RoadNetwork);

internal sealed class RoadNetworkReadModel
{
    private readonly RoadNetworkSnapshot _snapshot;
    private readonly Dictionary<RoadNodeId, RoadNodeSnapshot> _nodes;

    public RoadNetworkReadModel(ulong revision, RoadNetworkSnapshot snapshot)
    {
        Revision = revision;
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _nodes = new Dictionary<RoadNodeId, RoadNodeSnapshot>(snapshot.Nodes.Count);
        foreach (var node in snapshot.Nodes) _nodes.Add(node.Id, node);
    }

    public ulong Revision { get; }

    public WorldPoint GetNodePosition(RoadNodeId id)
    {
        if (!_nodes.TryGetValue(id, out var node))
            throw new InvalidOperationException($"Road read model is missing node {id.Value}.");
        return node.Position;
    }

    public RoadNetworkSnapshot Query(WorldVolume volume)
    {
        var selectedNodes = new HashSet<RoadNodeId>();
        var selectedSegments = new HashSet<RoadSegmentId>();

        foreach (var node in _snapshot.Nodes)
        {
            if (volume.Contains(node.Position)) selectedNodes.Add(node.Id);
        }

        foreach (var segment in _snapshot.Segments)
        {
            if (!_nodes.TryGetValue(segment.StartNodeId, out var start) || !_nodes.TryGetValue(segment.EndNodeId, out var end))
                throw new InvalidOperationException($"Road read model segment {segment.Id.Value} references a missing node.");
            if (!SegmentIntersectsVolume(start.Position, end.Position, volume)) continue;
            selectedSegments.Add(segment.Id);
            selectedNodes.Add(segment.StartNodeId);
            selectedNodes.Add(segment.EndNodeId);
        }

        var nodes = _snapshot.Nodes.Where(item => selectedNodes.Contains(item.Id)).ToArray();
        var segments = _snapshot.Segments.Where(item => selectedSegments.Contains(item.Id)).ToArray();
        var lanes = _snapshot.Lanes.Where(item => selectedSegments.Contains(item.SegmentId)).ToArray();
        var laneIds = lanes.Select(static item => item.Id).ToHashSet();
        var connections = _snapshot.Connections.Where(item => laneIds.Contains(item.FromLaneId) && laneIds.Contains(item.ToLaneId)).ToArray();
        var accessPoints = _snapshot.AccessPoints.Where(item => selectedSegments.Contains(item.SegmentId)).ToArray();
        return new RoadNetworkSnapshot(nodes, segments, lanes, connections, accessPoints);
    }

    private static bool SegmentIntersectsVolume(WorldPoint start, WorldPoint end, WorldVolume volume)
    {
        var minX = Math.Min(start.X, end.X);
        var minY = Math.Min(start.Y, end.Y);
        var minZ = Math.Min(start.Z, end.Z);
        var maxX = Math.Max(start.X, end.X);
        var maxY = Math.Max(start.Y, end.Y);
        var maxZ = Math.Max(start.Z, end.Z);
        return maxX >= volume.MinX && minX <= volume.MaxX
            && maxY >= volume.MinY && minY <= volume.MaxY
            && maxZ >= volume.MinZ && minZ <= volume.MaxZ;
    }
}

internal sealed class PublishedEntitySpatialIndex<T>
{
    private readonly T[] _items;
    private readonly Func<T, WorldPoint> _positionSelector;
    private readonly double _cellSize;
    private readonly Dictionary<CellKey, int[]> _indicesByCell;

    public PublishedEntitySpatialIndex(T[] items, double cellSize, Func<T, WorldPoint> positionSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        _positionSelector = positionSelector ?? throw new ArgumentNullException(nameof(positionSelector));
        if (!double.IsFinite(cellSize) || cellSize <= 0d) throw new ArgumentOutOfRangeException(nameof(cellSize));
        _items = items;
        _cellSize = cellSize;

        var builders = new Dictionary<CellKey, List<int>>();
        for (var index = 0; index < items.Length; index++)
        {
            var key = ToCell(positionSelector(items[index]));
            if (!builders.TryGetValue(key, out var list))
            {
                list = [];
                builders.Add(key, list);
            }
            list.Add(index);
        }

        _indicesByCell = new Dictionary<CellKey, int[]>(builders.Count);
        foreach (var entry in builders) _indicesByCell.Add(entry.Key, entry.Value.ToArray());
    }

    public T[] Query(WorldVolume volume)
    {
        if (_items.Length == 0) return [];
        var min = ToCell(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ));
        var max = ToCell(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ));
        var result = new List<T>();

        var rangeCellCount = CalculateRangeCellCount(min, max);
        if (rangeCellCount <= (ulong)_indicesByCell.Count * 2UL)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    for (var z = min.Z; z <= max.Z; z++)
                    {
                        AddCell(new CellKey(x, y, z), volume, result);
                        if (z == long.MaxValue) break;
                    }
                    if (y == long.MaxValue) break;
                }
                if (x == long.MaxValue) break;
            }
        }
        else
        {
            foreach (var entry in _indicesByCell)
            {
                var key = entry.Key;
                if (key.X < min.X || key.X > max.X || key.Y < min.Y || key.Y > max.Y || key.Z < min.Z || key.Z > max.Z) continue;
                AddIndices(entry.Value, volume, result);
            }
        }

        return result.ToArray();
    }

    private void AddCell(CellKey key, WorldVolume volume, List<T> result)
    {
        if (_indicesByCell.TryGetValue(key, out var indices)) AddIndices(indices, volume, result);
    }

    private void AddIndices(int[] indices, WorldVolume volume, List<T> result)
    {
        foreach (var index in indices)
        {
            var item = _items[index];
            if (volume.Contains(_positionSelector(item))) result.Add(item);
        }
    }

    private CellKey ToCell(WorldPoint point) => new(
        checked((long)Math.Floor(point.X / _cellSize)),
        checked((long)Math.Floor(point.Y / _cellSize)),
        checked((long)Math.Floor(point.Z / _cellSize)));

    private static ulong CalculateRangeCellCount(CellKey min, CellKey max)
    {
        var x = (UInt128)((Int128)max.X - min.X + 1);
        var y = (UInt128)((Int128)max.Y - min.Y + 1);
        var z = (UInt128)((Int128)max.Z - min.Z + 1);
        var total = x * y * z;
        return total > ulong.MaxValue ? ulong.MaxValue : (ulong)total;
    }

    private readonly record struct CellKey(long X, long Y, long Z);
}
