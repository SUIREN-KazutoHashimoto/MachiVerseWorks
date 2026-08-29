using System.Runtime.InteropServices;

namespace MachiVerseWorks.Simulation.Internal;

internal sealed class AgentStore
{
    private readonly List<AgentState> _states = [];
    private readonly Dictionary<AgentId, int> _indexById = [];
    private WorldPoint[] _stepOriginalPositions = [];
    private ulong _nextId = 1;

    public int ActiveCount { get; private set; }

    public int TotalCreatedCount => _states.Count;

    public ulong NextId => _nextId;

    public AgentId Add(WorldPoint position, WorldVector velocity, SpatialIndex spatialIndex)
    {
        spatialIndex.ValidatePosition(position);
        var id = new AgentId(_nextId);
        var nextId = checked(_nextId + 1);

        var state = new AgentState(id, position, velocity);
        var index = _states.Count;
        _states.Add(state);
        _indexById.Add(id, index);
        spatialIndex.Register(id, position);
        ActiveCount++;
        _nextId = nextId;

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
        EnsureStepRollbackCapacity(states.Length);
        var lastCommittedIndex = -1;

        try
        {
            for (var index = 0; index < states.Length; index++)
            {
                ref var state = ref states[index];
                if (!state.IsActive)
                {
                    continue;
                }

                _stepOriginalPositions[index] = state.Position;
                var nextPosition = new WorldPoint(
                    state.Position.X + (state.Velocity.X * tickDurationSeconds),
                    state.Position.Y + (state.Velocity.Y * tickDurationSeconds),
                    state.Position.Z + (state.Velocity.Z * tickDurationSeconds));

                spatialIndex.Update(state.Id, nextPosition);
                state.Position = nextPosition;
                lastCommittedIndex = index;
            }
        }
        catch
        {
            RollBackStep(states, lastCommittedIndex, spatialIndex);
            throw;
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

    public AgentSnapshot[] CreateSnapshot(WorldVolume volume, SpatialIndex spatialIndex, ulong tickCount)
    {
        var candidates = spatialIndex.Query(volume);
        var snapshots = new List<AgentSnapshot>(candidates.Count);

        foreach (var id in candidates)
        {
            if (!_indexById.TryGetValue(id, out var index))
            {
                continue;
            }

            var state = _states[index];
            if (state.IsActive && volume.Contains(state.Position))
            {
                snapshots.Add(new AgentSnapshot(state.Id, state.Position, state.Velocity, tickCount));
            }
        }

        return snapshots.ToArray();
    }

    public SimulationAgentCheckpoint[] CreateCheckpoint()
    {
        var checkpoint = new SimulationAgentCheckpoint[_states.Count];
        for (var index = 0; index < _states.Count; index++)
        {
            var state = _states[index];
            checkpoint[index] = new SimulationAgentCheckpoint(
                state.Id,
                state.Position,
                state.Velocity,
                state.IsActive);
        }

        return checkpoint;
    }

    public void Restore(
        IReadOnlyList<SimulationAgentCheckpoint> agents,
        ulong nextId,
        SpatialIndex spatialIndex)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(spatialIndex);

        if (_states.Count != 0 || _indexById.Count != 0 || ActiveCount != 0)
        {
            throw new InvalidOperationException("Agent store must be empty before restore.");
        }

        for (var index = 0; index < agents.Count; index++)
        {
            var checkpoint = agents[index];
            var state = new AgentState(checkpoint.Id, checkpoint.Position, checkpoint.Velocity)
            {
                IsActive = checkpoint.IsActive,
            };
            _states.Add(state);

            if (!state.IsActive)
            {
                continue;
            }

            _indexById.Add(state.Id, index);
            spatialIndex.Register(state.Id, state.Position);
            ActiveCount++;
        }

        _nextId = nextId;
    }

    private void EnsureStepRollbackCapacity(int requiredLength)
    {
        if (_stepOriginalPositions.Length >= requiredLength)
        {
            return;
        }

        var newLength = _stepOriginalPositions.Length == 0 ? 4 : _stepOriginalPositions.Length;
        while (newLength < requiredLength)
        {
            if (newLength > Array.MaxLength / 2)
            {
                newLength = requiredLength;
                break;
            }

            newLength *= 2;
        }

        Array.Resize(ref _stepOriginalPositions, newLength);
    }

    private void RollBackStep(
        Span<AgentState> states,
        int lastCommittedIndex,
        SpatialIndex spatialIndex)
    {
        for (var index = lastCommittedIndex; index >= 0; index--)
        {
            ref var state = ref states[index];
            if (!state.IsActive)
            {
                continue;
            }

            var originalPosition = _stepOriginalPositions[index];
            spatialIndex.Update(state.Id, originalPosition);
            state.Position = originalPosition;
        }
    }
}
