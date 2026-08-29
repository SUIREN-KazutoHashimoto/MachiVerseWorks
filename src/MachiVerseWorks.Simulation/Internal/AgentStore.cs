using System.Runtime.InteropServices;

namespace MachiVerseWorks.Simulation.Internal;

internal sealed class AgentStore
{
    private readonly List<AgentState> _states = [];
    private readonly Dictionary<AgentId, int> _indexById = [];
    private ulong _nextId = 1;

    public int ActiveCount { get; private set; }

    public int TotalCreatedCount => _states.Count;

    public AgentId Add(WorldPoint position, WorldVector velocity, SpatialIndex spatialIndex)
    {
        var id = new AgentId(_nextId);
        _nextId = checked(_nextId + 1);

        var state = new AgentState(id, position, velocity);
        var index = _states.Count;
        _states.Add(state);
        _indexById.Add(id, index);
        spatialIndex.Register(id, position);
        ActiveCount++;

        return id;
    }

    public bool Remove(AgentId id, SpatialIndex spatialIndex)
    {
        if (!_indexById.Remove(id, out var index))
        {
            return false;
        }

        var states = CollectionsMarshal.AsSpan(_states);
        ref var state = ref states[index];
        state.IsActive = false;
        spatialIndex.Remove(id);
        ActiveCount--;
        return true;
    }

    public void Step(double tickDurationSeconds, SpatialIndex spatialIndex)
    {
        var states = CollectionsMarshal.AsSpan(_states);

        for (var index = 0; index < states.Length; index++)
        {
            ref var state = ref states[index];
            if (!state.IsActive)
            {
                continue;
            }

            var nextPosition = new WorldPoint(
                state.Position.X + (state.Velocity.X * tickDurationSeconds),
                state.Position.Y + (state.Velocity.Y * tickDurationSeconds));

            state.Position = nextPosition;
            spatialIndex.Update(state.Id, nextPosition);
        }
    }

    public bool TryGetSnapshot(AgentId id, ulong tickCount, out AgentSnapshot snapshot)
    {
        if (!_indexById.TryGetValue(id, out var index))
        {
            snapshot = default;
            return false;
        }

        var state = _states[index];
        if (!state.IsActive)
        {
            snapshot = default;
            return false;
        }

        snapshot = new AgentSnapshot(state.Id, state.Position, state.Velocity, tickCount);
        return true;
    }

    public AgentSnapshot[] CreateSnapshot(WorldRect area, SpatialIndex spatialIndex, ulong tickCount)
    {
        var candidates = spatialIndex.Query(area);
        var snapshots = new List<AgentSnapshot>(candidates.Count);

        foreach (var id in candidates)
        {
            if (!_indexById.TryGetValue(id, out var index))
            {
                continue;
            }

            var state = _states[index];
            if (state.IsActive && area.Contains(state.Position))
            {
                snapshots.Add(new AgentSnapshot(state.Id, state.Position, state.Velocity, tickCount));
            }
        }

        return snapshots.ToArray();
    }
}
