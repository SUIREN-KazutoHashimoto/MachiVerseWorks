using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationRuntime
{
    private readonly object _gate = new();
    private readonly SimulationWorld _world;

    public SimulationRuntime(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _world = new SimulationWorld(new SimulationConfig(
            options.TickRate,
            options.Seed,
            options.SpatialCellSize));

        if (options.InitialAgentCount > 0)
        {
            _world.CreateAgents(
                options.InitialAgentCount,
                new WorldRect(
                    options.SpawnMinX,
                    options.SpawnMinY,
                    options.SpawnMaxX,
                    options.SpawnMaxY));
        }
    }

    public int TickRate => _world.Config.TickRate;

    public ulong TickCount
    {
        get
        {
            lock (_gate)
            {
                return _world.Time.TickCount;
            }
        }
    }

    public int ActiveAgentCount
    {
        get
        {
            lock (_gate)
            {
                return _world.ActiveAgentCount;
            }
        }
    }

    public void Step()
    {
        lock (_gate)
        {
            _world.Step();
        }
    }

    public AgentSnapshot[] CreateSnapshot(WorldRect area)
    {
        lock (_gate)
        {
            return _world.CreateSnapshot(area);
        }
    }
}
