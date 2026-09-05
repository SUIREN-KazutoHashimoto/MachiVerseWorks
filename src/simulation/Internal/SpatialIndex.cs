namespace MachiVerseWorks.Simulation.Internal;

internal sealed class SpatialIndex
{
    private readonly double _cellSize;
    private readonly Dictionary<SpatialCell, HashSet<AgentId>> _agentsByCell = [];
    private readonly Dictionary<AgentId, SpatialCell> _cellByAgent = [];

    public SpatialIndex(double cellSize)
    {
        _cellSize = cellSize;
    }

    public void ValidatePosition(WorldPoint position)
    {
        _ = SpatialGrid.ToCell(position, _cellSize);
    }

    public void Register(AgentId id, WorldPoint position)
    {
        var cell = SpatialGrid.ToCell(position, _cellSize);
        GetOrCreateCell(cell).Add(id);
        _cellByAgent.Add(id, cell);
    }

    public bool Remove(AgentId id)
    {
        if (!_cellByAgent.Remove(id, out var cell))
        {
            return false;
        }

        RemoveFromCell(cell, id);
        return true;
    }

    public void Update(AgentId id, WorldPoint position)
    {
        if (!_cellByAgent.TryGetValue(id, out var previousCell))
        {
            throw new InvalidOperationException($"Agent {id} is not registered in the spatial index.");
        }

        var nextCell = SpatialGrid.ToCell(position, _cellSize);
        if (previousCell == nextCell)
        {
            return;
        }

        RemoveFromCell(previousCell, id);
        GetOrCreateCell(nextCell).Add(id);
        _cellByAgent[id] = nextCell;
    }

    public List<AgentId> Query(WorldVolume volume)
    {
        var minCell = SpatialGrid.ToCell(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ), _cellSize);
        var maxCell = SpatialGrid.ToCell(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ), _cellSize);
        if (_agentsByCell.Count == 0)
        {
            return [];
        }

        return ShouldScanOccupiedCells(minCell, maxCell)
            ? QueryOccupiedCells(minCell, maxCell)
            : QueryCellRange(minCell, maxCell);
    }

    private bool ShouldScanOccupiedCells(SpatialCell minCell, SpatialCell maxCell)
    {
        var occupiedCellCount = (ulong)_agentsByCell.Count;
        var spanX = CellSpan(minCell.X, maxCell.X);
        var spanY = CellSpan(minCell.Y, maxCell.Y);
        var spanZ = CellSpan(minCell.Z, maxCell.Z);

        if (spanX > occupiedCellCount)
        {
            return true;
        }

        if (spanY > occupiedCellCount / spanX)
        {
            return true;
        }

        var spanXY = spanX * spanY;
        return spanZ > occupiedCellCount / spanXY;
    }

    private List<AgentId> QueryOccupiedCells(SpatialCell minCell, SpatialCell maxCell)
    {
        var result = new List<AgentId>();
        foreach (var entry in _agentsByCell)
        {
            var cell = entry.Key;
            if (cell.X < minCell.X || cell.X > maxCell.X ||
                cell.Y < minCell.Y || cell.Y > maxCell.Y ||
                cell.Z < minCell.Z || cell.Z > maxCell.Z)
            {
                continue;
            }

            result.AddRange(entry.Value);
        }

        return result;
    }

    private List<AgentId> QueryCellRange(SpatialCell minCell, SpatialCell maxCell)
    {
        var result = new List<AgentId>();
        for (var cellZ = minCell.Z; cellZ <= maxCell.Z; cellZ++)
        {
            for (var cellY = minCell.Y; cellY <= maxCell.Y; cellY++)
            {
                for (var cellX = minCell.X; cellX <= maxCell.X; cellX++)
                {
                    if (_agentsByCell.TryGetValue(new SpatialCell(cellX, cellY, cellZ), out var agents))
                    {
                        result.AddRange(agents);
                    }

                    if (cellX == int.MaxValue)
                    {
                        break;
                    }
                }

                if (cellY == int.MaxValue)
                {
                    break;
                }
            }

            if (cellZ == int.MaxValue)
            {
                break;
            }
        }

        return result;
    }

    private static ulong CellSpan(int minimum, int maximum)
    {
        return (ulong)((long)maximum - minimum) + 1UL;
    }

    private HashSet<AgentId> GetOrCreateCell(SpatialCell cell)
    {
        if (_agentsByCell.TryGetValue(cell, out var agents))
        {
            return agents;
        }

        agents = [];
        _agentsByCell.Add(cell, agents);
        return agents;
    }

    private void RemoveFromCell(SpatialCell cell, AgentId id)
    {
        if (!_agentsByCell.TryGetValue(cell, out var agents))
        {
            return;
        }

        agents.Remove(id);
        if (agents.Count == 0)
        {
            _agentsByCell.Remove(cell);
        }
    }
}
