namespace MachiVerseWorks.Simulation;

internal sealed class RadioEmissionSpatialIndex
{
    private readonly double _cellSize;
    private readonly Dictionary<SpatialCell, HashSet<RadioEmissionId>> _cells = [];
    private readonly Dictionary<RadioEmissionId, SpatialCell> _emissionCells = [];

    public RadioEmissionSpatialIndex(double cellSize)
    {
        if (!double.IsFinite(cellSize) || cellSize <= 0d) throw new ArgumentOutOfRangeException(nameof(cellSize));
        _cellSize = cellSize;
    }

    public void Add(RadioEmissionId id, WorldPoint position)
    {
        if (id.Value == 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (_emissionCells.ContainsKey(id)) throw new ArgumentException($"Radio emission {id.Value} is already indexed.", nameof(id));
        var cell = SpatialGrid.ToCell(position, _cellSize);
        if (!_cells.TryGetValue(cell, out var items))
        {
            items = [];
            _cells.Add(cell, items);
        }
        items.Add(id);
        _emissionCells.Add(id, cell);
    }

    public void Remove(RadioEmissionId id)
    {
        if (!_emissionCells.Remove(id, out var cell)) return;
        if (!_cells.TryGetValue(cell, out var items)) return;
        items.Remove(id);
        if (items.Count == 0) _cells.Remove(cell);
    }

    public void Clear()
    {
        _cells.Clear();
        _emissionCells.Clear();
    }

    public RadioEmissionId[] Query(WorldVolume volume)
    {
        var minimum = SpatialGrid.ToCell(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ), _cellSize);
        var maximum = SpatialGrid.ToCell(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ), _cellSize);
        var result = new HashSet<RadioEmissionId>();
        for (var x = minimum.X; x <= maximum.X; x++)
        {
            for (var y = minimum.Y; y <= maximum.Y; y++)
            {
                for (var z = minimum.Z; z <= maximum.Z; z++)
                {
                    if (_cells.TryGetValue(new SpatialCell(x, y, z), out var items)) result.UnionWith(items);
                    if (z == int.MaxValue) break;
                }
                if (y == int.MaxValue) break;
            }
            if (x == int.MaxValue) break;
        }
        return result.OrderBy(static id => id.Value).ToArray();
    }
}
