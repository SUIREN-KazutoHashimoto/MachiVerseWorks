namespace MachiVerseWorks.Simulation.Internal;

internal sealed class RoadSpatialIndex
{
    private const ulong MaximumIndexedSegmentCells = 4_096;
    private const double CoarseCellScale = 64d;
    private readonly double cellSize;
    private readonly double coarseCellSize;
    private readonly Dictionary<SpatialCell, HashSet<RoadNodeId>> nodesByCell = [];
    private readonly Dictionary<RoadNodeId, SpatialCell> cellByNode = [];
    private readonly Dictionary<SpatialCell, HashSet<RoadSegmentId>> segmentsByCell = [];
    private readonly Dictionary<RoadSegmentId, SpatialCell[]> cellsBySegment = [];
    private readonly Dictionary<SpatialCell, HashSet<RoadSegmentId>> coarseSegmentsByCell = [];
    private readonly Dictionary<RoadSegmentId, SpatialCell[]> coarseCellsBySegment = [];
    private readonly Dictionary<RoadSegmentId, WorldVolume> boundsBySegment = [];
    private readonly HashSet<RoadSegmentId> ultraLargeSegments = [];

    public RoadSpatialIndex(double cellSize)
    {
        this.cellSize = cellSize;
        coarseCellSize = checked(cellSize * CoarseCellScale);
    }

    public void ValidatePosition(WorldPoint position) => _ = SpatialGrid.ToCell(position, cellSize);

    public void RegisterNode(RoadNodeId id, WorldPoint position)
    {
        var cell = SpatialGrid.ToCell(position, cellSize);
        GetOrCreate(nodesByCell, cell).Add(id);
        cellByNode.Add(id, cell);
    }

    public void UpdateNode(RoadNodeId id, WorldPoint position)
    {
        var nextCell = SpatialGrid.ToCell(position, cellSize);
        if (!cellByNode.TryGetValue(id, out var previousCell))
        {
            throw new InvalidOperationException($"Road node {id.Value} is not registered in the spatial index.");
        }

        if (previousCell == nextCell) return;
        RemoveFromCell(nodesByCell, previousCell, id);
        GetOrCreate(nodesByCell, nextCell).Add(id);
        cellByNode[id] = nextCell;
    }

    public void RemoveNode(RoadNodeId id)
    {
        if (!cellByNode.Remove(id, out var cell)) return;
        RemoveFromCell(nodesByCell, cell, id);
    }

    public void RegisterSegment(RoadSegmentId id, WorldPoint start, WorldPoint end)
    {
        var bounds = CreateBounds(start, end);
        boundsBySegment.Add(id, bounds);
        var (low, high) = GetCellRange(bounds, cellSize);
        var count = CellCount(low, high);
        if (count <= MaximumIndexedSegmentCells)
        {
            var cells = RegisterCells(segmentsByCell, id, low, high, count);
            cellsBySegment.Add(id, cells);
            coarseCellsBySegment.Add(id, []);
            return;
        }

        cellsBySegment.Add(id, []);
        var (coarseLow, coarseHigh) = GetCellRange(bounds, coarseCellSize);
        var coarseCount = CellCount(coarseLow, coarseHigh);
        if (coarseCount <= MaximumIndexedSegmentCells)
        {
            coarseCellsBySegment.Add(id, RegisterCells(coarseSegmentsByCell, id, coarseLow, coarseHigh, coarseCount));
            return;
        }

        coarseCellsBySegment.Add(id, []);
        ultraLargeSegments.Add(id);
    }

    public void UpdateSegment(RoadSegmentId id, WorldPoint start, WorldPoint end)
    {
        RemoveSegment(id);
        RegisterSegment(id, start, end);
    }

    public void RemoveSegment(RoadSegmentId id)
    {
        if (cellsBySegment.Remove(id, out var cells))
        {
            foreach (var cell in cells) RemoveFromCell(segmentsByCell, cell, id);
        }
        if (coarseCellsBySegment.Remove(id, out var coarseCells))
        {
            foreach (var cell in coarseCells) RemoveFromCell(coarseSegmentsByCell, cell, id);
        }
        ultraLargeSegments.Remove(id);
        boundsBySegment.Remove(id);
    }

    public HashSet<RoadNodeId> QueryNodes(WorldVolume volume)
    {
        var result = new HashSet<RoadNodeId>();
        var (low, high) = GetCellRange(volume, cellSize);
        QueryCells(nodesByCell, low, high, result);
        return result;
    }

    public HashSet<RoadSegmentId> QuerySegments(WorldVolume volume)
    {
        var candidates = new HashSet<RoadSegmentId>(ultraLargeSegments);
        var (low, high) = GetCellRange(volume, cellSize);
        QueryCells(segmentsByCell, low, high, candidates);
        var (coarseLow, coarseHigh) = GetCellRange(volume, coarseCellSize);
        QueryCells(coarseSegmentsByCell, coarseLow, coarseHigh, candidates);
        candidates.RemoveWhere(id => !boundsBySegment.TryGetValue(id, out var bounds) || !Intersects(bounds, volume));
        return candidates;
    }

    private static SpatialCell[] RegisterCells(
        Dictionary<SpatialCell, HashSet<RoadSegmentId>> target,
        RoadSegmentId id,
        SpatialCell low,
        SpatialCell high,
        ulong count)
    {
        var cells = new SpatialCell[(int)count];
        var offset = 0;
        ForEachCell(low, high, cell =>
        {
            cells[offset++] = cell;
            GetOrCreate(target, cell).Add(id);
        });
        return cells;
    }

    private static (SpatialCell Low, SpatialCell High) GetCellRange(WorldVolume bounds, double size)
    {
        var minCell = SpatialGrid.ToCell(new WorldPoint(bounds.MinX, bounds.MinY, bounds.MinZ), size);
        var maxCell = SpatialGrid.ToCell(new WorldPoint(bounds.MaxX, bounds.MaxY, bounds.MaxZ), size);
        return (
            new SpatialCell(Math.Min(minCell.X, maxCell.X), Math.Min(minCell.Y, maxCell.Y), Math.Min(minCell.Z, maxCell.Z)),
            new SpatialCell(Math.Max(minCell.X, maxCell.X), Math.Max(minCell.Y, maxCell.Y), Math.Max(minCell.Z, maxCell.Z)));
    }

    private static WorldVolume CreateBounds(WorldPoint start, WorldPoint end) => new(
        Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z),
        Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));

    private static bool Intersects(WorldVolume left, WorldVolume right) =>
        left.MaxX >= right.MinX && left.MinX <= right.MaxX &&
        left.MaxY >= right.MinY && left.MinY <= right.MaxY &&
        left.MaxZ >= right.MinZ && left.MinZ <= right.MaxZ;

    private static ulong CellCount(SpatialCell low, SpatialCell high)
    {
        var x = (ulong)((long)high.X - low.X) + 1;
        var y = (ulong)((long)high.Y - low.Y) + 1;
        if (x > MaximumIndexedSegmentCells || y > MaximumIndexedSegmentCells / x) return MaximumIndexedSegmentCells + 1;
        var xy = x * y;
        var z = (ulong)((long)high.Z - low.Z) + 1;
        return z > MaximumIndexedSegmentCells / xy ? MaximumIndexedSegmentCells + 1 : xy * z;
    }

    private static void QueryCells<T>(Dictionary<SpatialCell, HashSet<T>> source, SpatialCell low, SpatialCell high, HashSet<T> result)
        where T : notnull
    {
        if (source.Count == 0) return;
        var occupied = (ulong)source.Count;
        var x = (ulong)((long)high.X - low.X) + 1;
        var y = (ulong)((long)high.Y - low.Y) + 1;
        var scanOccupied = x > occupied || y > occupied / Math.Max(x, 1UL);
        if (!scanOccupied)
        {
            var xy = x * y;
            var z = (ulong)((long)high.Z - low.Z) + 1;
            scanOccupied = z > occupied / Math.Max(xy, 1UL);
        }

        if (scanOccupied)
        {
            foreach (var entry in source)
            {
                var cell = entry.Key;
                if (cell.X < low.X || cell.X > high.X || cell.Y < low.Y || cell.Y > high.Y || cell.Z < low.Z || cell.Z > high.Z) continue;
                result.UnionWith(entry.Value);
            }
            return;
        }

        ForEachCell(low, high, cell =>
        {
            if (source.TryGetValue(cell, out var items)) result.UnionWith(items);
        });
    }

    private static void ForEachCell(SpatialCell low, SpatialCell high, Action<SpatialCell> action)
    {
        for (var z = low.Z; z <= high.Z; z++)
        {
            for (var y = low.Y; y <= high.Y; y++)
            {
                for (var x = low.X; x <= high.X; x++)
                {
                    action(new SpatialCell(x, y, z));
                    if (x == int.MaxValue) break;
                }
                if (y == int.MaxValue) break;
            }
            if (z == int.MaxValue) break;
        }
    }

    private static HashSet<T> GetOrCreate<T>(Dictionary<SpatialCell, HashSet<T>> source, SpatialCell cell)
        where T : notnull
    {
        if (source.TryGetValue(cell, out var items)) return items;
        items = [];
        source.Add(cell, items);
        return items;
    }

    private static void RemoveFromCell<T>(Dictionary<SpatialCell, HashSet<T>> source, SpatialCell cell, T id)
        where T : notnull
    {
        if (!source.TryGetValue(cell, out var items)) return;
        items.Remove(id);
        if (items.Count == 0) source.Remove(cell);
    }
}
