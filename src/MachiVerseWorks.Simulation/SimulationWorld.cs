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

    public SimulationCheckpoint CreateCheckpoint()
    {
        return new SimulationCheckpoint(
            Config.TickRate,
            Config.Seed,
            Config.SpatialCellSize,
            Time.TickCount,
            Time.Elapsed.Ticks,
            _random.State,
            _agents.NextId,
            _agents.CreateCheckpoint());
    }

    public static SimulationWorld RestoreCheckpoint(SimulationCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(checkpoint.Agents);

        if (checkpoint.ElapsedTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                checkpoint.ElapsedTicks,
                "Simulation elapsed time cannot be negative.");
        }

        if (checkpoint.NextAgentId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                checkpoint.NextAgentId,
                "Next Agent ID must be greater than zero.");
        }

        var seenAgentIds = new HashSet<ulong>(checkpoint.Agents.Count);
        var maximumAgentId = 0UL;
        foreach (var agent in checkpoint.Agents)
        {
            if (agent.Id.Value == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(checkpoint),
                    agent.Id.Value,
                    "Agent IDs must be greater than zero.");
            }

            if (!seenAgentIds.Add(agent.Id.Value))
            {
                throw new ArgumentException($"Duplicate Agent ID {agent.Id.Value}.", nameof(checkpoint));
            }

            ValidatePoint(agent.Position);
            ValidateVector(agent.Velocity);
            maximumAgentId = Math.Max(maximumAgentId, agent.Id.Value);
        }

        if (checkpoint.NextAgentId <= maximumAgentId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                checkpoint.NextAgentId,
                "Next Agent ID must be greater than every stored Agent ID.");
        }

        var config = new SimulationConfig(
            checkpoint.TickRate,
            checkpoint.Seed,
            checkpoint.SpatialCellSize);
        var world = new SimulationWorld(config)
        {
            Time = new SimulationTime(checkpoint.TickCount, TimeSpan.FromTicks(checkpoint.ElapsedTicks)),
            _random = new DeterministicRandom(checkpoint.RandomState),
        };
        world._agents.Restore(checkpoint.Agents, checkpoint.NextAgentId, world._spatialIndex);
        return world;
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
