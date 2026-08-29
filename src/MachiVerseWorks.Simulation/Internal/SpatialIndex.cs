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
