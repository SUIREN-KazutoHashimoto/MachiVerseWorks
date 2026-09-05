namespace MachiVerseWorks.Simulation.Internal;

internal sealed class PedestrianSpatialIndex
{
    private readonly double cellSize;
    private readonly Dictionary<SpatialCell, HashSet<PedestrianId>> pedestriansByCell = [];
    private readonly Dictionary<PedestrianId, SpatialCell> cellByPedestrian = [];

    public PedestrianSpatialIndex(double cellSize)
    {
        this.cellSize = cellSize;
    }

    public void ValidatePosition(WorldPoint position)
    {
        _ = SpatialGrid.ToCell(position, cellSize);
    }

    public void Register(PedestrianId id, WorldPoint position)
    {
        var cell = SpatialGrid.ToCell(position, cellSize);
        GetOrCreateCell(cell).Add(id);
        cellByPedestrian.Add(id, cell);
    }

    public bool Remove(PedestrianId id)
    {
        if (!cellByPedestrian.Remove(id, out var cell)) return false;
        RemoveFromCell(cell, id);
        return true;
    }

    public void Update(PedestrianId id, WorldPoint position)
    {
        if (!cellByPedestrian.TryGetValue(id, out var previousCell))
            throw new InvalidOperationException($"Pedestrian {id.Value} is not registered in the spatial index.");

        var nextCell = SpatialGrid.ToCell(position, cellSize);
        if (previousCell == nextCell) return;
        RemoveFromCell(previousCell, id);
        GetOrCreateCell(nextCell).Add(id);
        cellByPedestrian[id] = nextCell;
    }

    public List<PedestrianId> Query(WorldVolume volume)
    {
        var minCell = SpatialGrid.ToCell(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ), cellSize);
        var maxCell = SpatialGrid.ToCell(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ), cellSize);
        if (pedestriansByCell.Count == 0) return [];

        return ShouldScanOccupiedCells(minCell, maxCell)
            ? QueryOccupiedCells(minCell, maxCell)
            : QueryCellRange(minCell, maxCell);
    }

    private bool ShouldScanOccupiedCells(SpatialCell minCell, SpatialCell maxCell)
    {
        var occupiedCellCount = (ulong)pedestriansByCell.Count;
        var spanX = CellSpan(minCell.X, maxCell.X);
        var spanY = CellSpan(minCell.Y, maxCell.Y);
        var spanZ = CellSpan(minCell.Z, maxCell.Z);
        if (spanX > occupiedCellCount) return true;
        if (spanY > occupiedCellCount / spanX) return true;
        var spanXY = spanX * spanY;
        return spanZ > occupiedCellCount / spanXY;
    }

    private List<PedestrianId> QueryOccupiedCells(SpatialCell minCell, SpatialCell maxCell)
    {
        var result = new List<PedestrianId>();
        foreach (var entry in pedestriansByCell)
        {
            var cell = entry.Key;
            if (cell.X < minCell.X || cell.X > maxCell.X ||
                cell.Y < minCell.Y || cell.Y > maxCell.Y ||
                cell.Z < minCell.Z || cell.Z > maxCell.Z)
                continue;
            result.AddRange(entry.Value);
        }
        return result;
    }

    private List<PedestrianId> QueryCellRange(SpatialCell minCell, SpatialCell maxCell)
    {
        var result = new List<PedestrianId>();
        for (var cellZ = minCell.Z; cellZ <= maxCell.Z; cellZ++)
        {
            for (var cellY = minCell.Y; cellY <= maxCell.Y; cellY++)
            {
                for (var cellX = minCell.X; cellX <= maxCell.X; cellX++)
                {
                    if (pedestriansByCell.TryGetValue(new SpatialCell(cellX, cellY, cellZ), out var pedestrians)) result.AddRange(pedestrians);
                    if (cellX == int.MaxValue) break;
                }
                if (cellY == int.MaxValue) break;
            }
            if (cellZ == int.MaxValue) break;
        }
        return result;
    }

    private static ulong CellSpan(int minimum, int maximum) => (ulong)((long)maximum - minimum) + 1UL;

    private HashSet<PedestrianId> GetOrCreateCell(SpatialCell cell)
    {
        if (pedestriansByCell.TryGetValue(cell, out var pedestrians)) return pedestrians;
        pedestrians = [];
        pedestriansByCell.Add(cell, pedestrians);
        return pedestrians;
    }

    private void RemoveFromCell(SpatialCell cell, PedestrianId id)
    {
        if (!pedestriansByCell.TryGetValue(cell, out var pedestrians)) return;
        pedestrians.Remove(id);
        if (pedestrians.Count == 0) pedestriansByCell.Remove(cell);
    }
}