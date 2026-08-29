namespace MachiVerseWorks.Simulation.Internal;

internal sealed class PoiStore
{
    private readonly Dictionary<PoiId, PoiSnapshot> items = [];
    private ulong nextId = 1;

    public int Count => items.Count;

    public ulong NextId => nextId;

    public PoiId Add(PoiKind kind, WorldPoint position, BuildingId? buildingId)
    {
        if (nextId == ulong.MaxValue)
        {
            throw new OverflowException("POI ID capacity has been exhausted.");
        }

        var id = new PoiId(nextId++);
        items.Add(id, new PoiSnapshot(id, kind, position, buildingId));
        return id;
    }

    public bool Remove(PoiId id)
    {
        return items.Remove(id);
    }

    public bool TryGetSnapshot(PoiId id, out PoiSnapshot snapshot)
    {
        return items.TryGetValue(id, out snapshot);
    }

    public bool ContainsBuildingReference(BuildingId buildingId)
    {
        foreach (var poi in items.Values)
        {
            if (poi.BuildingId == buildingId)
            {
                return true;
            }
        }

        return false;
    }

    public PoiSnapshot[] CreateSnapshot()
    {
        var result = items.Values.ToArray();
        Array.Sort(result, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        return result;
    }

    public IReadOnlyList<SimulationPoiCheckpoint> CreateCheckpoint()
    {
        var snapshots = CreateSnapshot();
        var result = new SimulationPoiCheckpoint[snapshots.Length];
        for (var index = 0; index < result.Length; index++)
        {
            var snapshot = snapshots[index];
            result[index] = new SimulationPoiCheckpoint(
                snapshot.Id,
                snapshot.Kind,
                snapshot.Position,
                snapshot.BuildingId);
        }

        return result;
    }

    public void Restore(IReadOnlyList<SimulationPoiCheckpoint> pois, ulong restoredNextId)
    {
        items.Clear();
        foreach (var poi in pois)
        {
            items.Add(
                poi.Id,
                new PoiSnapshot(poi.Id, poi.Kind, poi.Position, poi.BuildingId));
        }

        nextId = restoredNextId;
    }
}
