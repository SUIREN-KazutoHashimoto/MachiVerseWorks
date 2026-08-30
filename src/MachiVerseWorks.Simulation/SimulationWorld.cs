using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly AgentStore _agents = new();
    private readonly SpatialIndex _spatialIndex;
    private DeterministicRandom _random;

    public SimulationWorld(SimulationConfig? config = null)
    {
        Config = config ?? new SimulationConfig();
        _spatialIndex = new SpatialIndex(Config.SpatialCellSize);
        _roads = new RoadNetworkStore(Config.SpatialCellSize);
        _random = new DeterministicRandom(Config.Seed);
        Time = default;
    }

    public SimulationConfig Config { get; }
    public SimulationTime Time { get; private set; }
    public int ActiveAgentCount => _agents.ActiveCount;
    public int TotalCreatedAgentCount => _agents.TotalCreatedCount;

    public AgentId CreateAgent(WorldPoint position)
    {
        ValidatePoint(position); _spatialIndex.ValidatePosition(position); _agents.EnsureCapacity(1); return CreateAgent(position, NextVelocity());
    }

    public AgentId CreateAgent(WorldPoint position, WorldVector velocity)
    {
        ValidatePoint(position); ValidateVector(velocity); return _agents.Add(position, velocity, _spatialIndex);
    }

    public AgentId[] CreateAgents(int count, WorldVolume spawnVolume)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Agent count cannot be negative.");
        _spatialIndex.ValidatePosition(new WorldPoint(spawnVolume.MinX, spawnVolume.MinY, spawnVolume.MinZ));
        _spatialIndex.ValidatePosition(new WorldPoint(spawnVolume.MaxX, spawnVolume.MaxY, spawnVolume.MaxZ));
        _agents.EnsureCapacity(count);
        var ids = new AgentId[count];
        for (var index = 0; index < ids.Length; index++)
        {
            var position = new WorldPoint(NextCoordinate(spawnVolume.MinX, spawnVolume.MaxX), NextCoordinate(spawnVolume.MinY, spawnVolume.MaxY), NextCoordinate(spawnVolume.MinZ, spawnVolume.MaxZ));
            ids[index] = CreateAgent(position, NextVelocity());
        }
        return ids;
    }

    public bool RemoveAgent(AgentId id) => _agents.Remove(id, _spatialIndex);

    public void Step()
    {
        var nextTime = Time.Advance(Config.TickDuration);
        _agents.Step(Config.TickDurationSeconds, _spatialIndex);
        StepPedestrians(Config.TickDurationSeconds);
        Time = nextTime;
    }

    public bool TryGetAgentSnapshot(AgentId id, out AgentSnapshot snapshot) => _agents.TryGetSnapshot(id, Time.TickCount, out snapshot);
    public AgentSnapshot[] CreateSnapshot(WorldVolume volume) => _agents.CreateSnapshot(volume, _spatialIndex, Time.TickCount);

    public SimulationCheckpoint CreateCheckpoint() => new(
        Config.TickRate, Config.Seed, Config.SpatialCellSize, Time.TickCount, Time.Elapsed.Ticks, _random.State,
        _agents.NextId, _agents.CreateCheckpoint(),
        _buildings.NextId, _buildings.CreateCheckpoint(),
        _pois.NextId, _pois.CreateCheckpoint(),
        _roads.NextNodeId, _roads.CreateNodeCheckpoint(),
        _roads.NextSegmentId, _roads.CreateSegmentCheckpoint(),
        _roads.NextLaneId, _roads.CreateLaneCheckpoint(),
        _roads.NextConnectionId, _roads.CreateConnectionCheckpoint(),
        _roads.NextAccessPointId, _roads.CreateAccessPointCheckpoint(),
        _pedestrians.NextId, _pedestrians.CreateCheckpoint());

    public static SimulationWorld RestoreCheckpoint(SimulationCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(checkpoint.Agents); ArgumentNullException.ThrowIfNull(checkpoint.Buildings); ArgumentNullException.ThrowIfNull(checkpoint.Pois);
        ArgumentNullException.ThrowIfNull(checkpoint.RoadNodes); ArgumentNullException.ThrowIfNull(checkpoint.RoadSegments); ArgumentNullException.ThrowIfNull(checkpoint.Lanes);
        ArgumentNullException.ThrowIfNull(checkpoint.LaneConnections); ArgumentNullException.ThrowIfNull(checkpoint.RoadAccessPoints);
        if (checkpoint.ElapsedTicks < 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.ElapsedTicks, "Simulation elapsed time cannot be negative.");
        if (checkpoint.NextAgentId == 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextAgentId, "Next Agent ID must be greater than zero.");
        var seenAgentIds = new HashSet<ulong>(checkpoint.Agents.Count); var maximumAgentId = 0UL;
        foreach (var agent in checkpoint.Agents)
        {
            if (agent.Id.Value == 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), agent.Id.Value, "Agent IDs must be greater than zero.");
            if (!seenAgentIds.Add(agent.Id.Value)) throw new ArgumentException($"Duplicate Agent ID {agent.Id.Value}.", nameof(checkpoint));
            ValidatePoint(agent.Position); ValidateVector(agent.Velocity); maximumAgentId = Math.Max(maximumAgentId, agent.Id.Value);
        }
        if (checkpoint.NextAgentId <= maximumAgentId) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextAgentId, "Next Agent ID must be greater than every stored Agent ID.");

        var config = new SimulationConfig(checkpoint.TickRate, checkpoint.Seed, checkpoint.SpatialCellSize);
        ValidateUrbanObjectCheckpoint(checkpoint, config.SpatialCellSize);
        ValidateRoadNetworkCheckpoint(checkpoint, config.SpatialCellSize);
        ValidatePedestrianCheckpoint(checkpoint);
        var restoredTime = new SimulationTime(checkpoint.TickCount, TimeSpan.FromTicks(checkpoint.ElapsedTicks));
        try { _ = restoredTime.Advance(config.TickDuration); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(checkpoint), "Simulation time must allow at least one additional tick."); }
        var expectedElapsedTicks = CalculateExpectedElapsedTicks(checkpoint.TickCount, config.TickDuration);
        if (checkpoint.ElapsedTicks != expectedElapsedTicks) throw new ArgumentException($"Elapsed time {checkpoint.ElapsedTicks} does not match tick count {checkpoint.TickCount} and tick rate {checkpoint.TickRate}.", nameof(checkpoint));
        var world = new SimulationWorld(config) { Time = restoredTime, _random = new DeterministicRandom(checkpoint.RandomState) };
        world._agents.Restore(checkpoint.Agents, checkpoint.NextAgentId, world._spatialIndex);
        world.RestoreUrbanObjects(checkpoint);
        world._roads.Restore(checkpoint);
        world.EnsurePedestrianNetwork();
        world._pedestrians.Restore(checkpoint.Pedestrians ?? Array.Empty<SimulationPedestrianCheckpoint>(), checkpoint.NextPedestrianId, world._pedestrianNetwork);
        return world;
    }

    private double NextCoordinate(double minimum, double maximum) => minimum == maximum ? minimum : _random.NextDouble(minimum, maximum);
    private WorldVector NextVelocity() => new(_random.NextDouble(-1d, 1d), _random.NextDouble(-1d, 1d), 0d);

    private static long CalculateExpectedElapsedTicks(ulong tickCount, TimeSpan tickDuration)
    {
        var ticks = tickDuration.Ticks; if (ticks == 0) return 0; var maximum = (ulong)(long.MaxValue / ticks);
        if (tickCount > maximum) throw new ArgumentOutOfRangeException(nameof(tickCount), tickCount, "Tick count cannot be represented by the configured elapsed-time range.");
        return (long)(tickCount * (ulong)ticks);
    }

    private static void ValidatePoint(WorldPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z)) throw new ArgumentOutOfRangeException(nameof(point), "World coordinates must be finite.");
    }

    private static void ValidateVector(WorldVector vector)
    {
        if (!double.IsFinite(vector.X) || !double.IsFinite(vector.Y) || !double.IsFinite(vector.Z)) throw new ArgumentOutOfRangeException(nameof(vector), "Velocity components must be finite.");
    }
}
