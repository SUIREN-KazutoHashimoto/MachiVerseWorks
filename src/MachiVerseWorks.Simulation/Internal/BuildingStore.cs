namespace MachiVerseWorks.Simulation.Internal;

internal sealed class BuildingStore
{
    private readonly Dictionary<BuildingId, BuildingSnapshot> items = [];
    private ulong nextId = 1;

    public int Count => items.Count;
    public ulong NextId => nextId;

    public BuildingId Add(BuildingKind kind, WorldVolume bounds)
    {
        if (nextId == ulong.MaxValue) throw new OverflowException("Building ID capacity has been exhausted.");
        var id = new BuildingId(nextId++);
        items.Add(id, new BuildingSnapshot(id, kind, bounds));
        return id;
    }

    public bool Update(BuildingId id, BuildingKind kind, WorldVolume bounds)
    {
        if (!items.ContainsKey(id)) return false;
        items[id] = new BuildingSnapshot(id, kind, bounds);
        return true;
    }

    public bool Contains(BuildingId id) => items.ContainsKey(id);
    public bool Remove(BuildingId id) => items.Remove(id);
    public bool TryGetSnapshot(BuildingId id, out BuildingSnapshot snapshot) => items.TryGetValue(id, out snapshot);

    public BuildingSnapshot[] CreateSnapshot()
    {
        var result = items.Values.ToArray();
        Array.Sort(result, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        return result;
    }

    public IReadOnlyList<SimulationBuildingCheckpoint> CreateCheckpoint()
    {
        var snapshots = CreateSnapshot();
        var result = new SimulationBuildingCheckpoint[snapshots.Length];
        for (var index = 0; index < result.Length; index++)
        {
            var snapshot = snapshots[index];
            result[index] = new SimulationBuildingCheckpoint(snapshot.Id, snapshot.Kind, snapshot.Bounds);
        }
        return result;
    }

    public void Restore(IReadOnlyList<SimulationBuildingCheckpoint> buildings, ulong restoredNextId)
    {
        items.Clear();
        foreach (var building in buildings) items.Add(building.Id, new BuildingSnapshot(building.Id, building.Kind, building.Bounds));
        nextId = restoredNextId;
    }
}
