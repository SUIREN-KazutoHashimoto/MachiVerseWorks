using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed class SimulationWorld
{
    private readonly AgentStore _agents = new();
    private readonly SpatialIndex _spatialIndex;
    private DeterministicRandom _random;

    public SimulationWorld(SimulationConfig? config = null)
    {
        Config = config ?? new SimulationConfig();
        _spatialIndex = new SpatialIndex(Config.SpatialCellSize);
        _random = new DeterministicRandom(Config.Seed);
        Time = default;
    }

    public SimulationConfig Config { get; }

    public SimulationTime Time { get; private set; }

    public int ActiveAgentCount => _agents.ActiveCount;

    public int TotalCreatedAgentCount => _agents.TotalCreatedCount;

    public AgentId CreateAgent(WorldPoint position)
    {
        return CreateAgent(position, NextVelocity());
    }

    public AgentId CreateAgent(WorldPoint position, WorldVector velocity)
    {
        ValidatePoint(position);
        ValidateVector(velocity);
        return _agents.Add(position, velocity, _spatialIndex);
    }

    public AgentId[] CreateAgents(int count, WorldRect spawnArea)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Agent count cannot be negative.");
        }

        var ids = new AgentId[count];
        for (var index = 0; index < ids.Length; index++)
        {
            var position = new WorldPoint(
                _random.NextDouble(spawnArea.MinX, spawnArea.MaxX),
                _random.NextDouble(spawnArea.MinY, spawnArea.MaxY));
            ids[index] = CreateAgent(position, NextVelocity());
        }

        return ids;
    }

    public bool RemoveAgent(AgentId id)
    {
        return _agents.Remove(id, _spatialIndex);
    }

    public void Step()
    {
        _agents.Step(Config.TickDurationSeconds, _spatialIndex);
        Time = Time.Advance(Config.TickDuration);
    }

    public bool TryGetAgentSnapshot(AgentId id, out AgentSnapshot snapshot)
    {
        return _agents.TryGetSnapshot(id, Time.TickCount, out snapshot);
    }

    public AgentSnapshot[] CreateSnapshot(WorldRect area)
    {
        return _agents.CreateSnapshot(area, _spatialIndex, Time.TickCount);
    }

    private WorldVector NextVelocity()
    {
        return new WorldVector(
            _random.NextDouble(-1d, 1d),
            _random.NextDouble(-1d, 1d));
    }

    private static void ValidatePoint(WorldPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(point), "World coordinates must be finite.");
        }
    }

    private static void ValidateVector(WorldVector vector)
    {
        if (!double.IsFinite(vector.X) || !double.IsFinite(vector.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(vector), "Velocity components must be finite.");
        }
    }
}
