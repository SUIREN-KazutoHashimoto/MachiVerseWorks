using MachiVerseWorks.Persistence;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationRuntime
{
    private readonly object _gate = new();
    private readonly SimulationWorld _world;

    public SimulationRuntime(ServerOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        var savePath = configuration["Simulation:SavePath"];
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            using var stream = File.OpenRead(Path.GetFullPath(savePath));
            _world = WorldSaveSerializer.Load(stream);
            return;
        }

        _world = new SimulationWorld(new SimulationConfig(options.TickRate, options.Seed, options.SpatialCellSize));
        if (options.InitialAgentCount > 0)
        {
            _world.CreateAgents(
                options.InitialAgentCount,
                new WorldVolume(
                    options.SpawnMinX,
                    options.SpawnMinY,
                    options.SpawnMinZ,
                    options.SpawnMaxX,
                    options.SpawnMaxY,
                    options.SpawnMaxZ));
        }

        if (bool.TryParse(configuration["Simulation:PedestrianFixture"], out var pedestrianFixture) && pedestrianFixture)
        {
            SeedPedestrianFixture(_world);
        }
    }

    public int TickRate => _world.Config.TickRate;
    public double SpatialCellSize => _world.Config.SpatialCellSize;
    public ulong TickCount { get { lock (_gate) return _world.Time.TickCount; } }
    public int ActiveAgentCount { get { lock (_gate) return _world.ActiveAgentCount; } }
    public int ActivePedestrianCount { get { lock (_gate) return _world.ActivePedestrianCount; } }
    public int RoadSegmentCount { get { lock (_gate) return _world.RoadSegmentCount; } }
    public void Step() { lock (_gate) _world.Step(); }
    public AgentSnapshot[] CreateSnapshot(WorldVolume volume) { lock (_gate) return _world.CreateSnapshot(volume); }
    public PedestrianSnapshot[] CreatePedestrianSnapshot(WorldVolume volume) { lock (_gate) return _world.CreatePedestrianSnapshot(volume); }
    public RoadNetworkSnapshot CreateRoadNetworkSnapshot(WorldVolume volume) { lock (_gate) return _world.CreateRoadNetworkSnapshot(volume); }

    private static void SeedPedestrianFixture(SimulationWorld world)
    {
        var originBuilding = world.CreateBuilding(new WorldVolume(-21d, -1d, 0d, -19d, 1d, 2d), BuildingKind.Residential);
        var destinationBuilding = world.CreateBuilding(new WorldVolume(19d, -1d, 0d, 21d, 1d, 2d), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(-20d, 0d, 0d));
        var crossing = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection);
        var end = world.CreateRoadNode(new WorldPoint(20d, 0d, 0d));
        var firstSegment = world.CreateRoadSegment(start, crossing, RoadKind.Local);
        var secondSegment = world.CreateRoadSegment(crossing, end, RoadKind.Local);
        world.CreateRoadAccessPoint(firstSegment, 0d, originBuilding, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(secondSegment, 1d, destinationBuilding, mode: RoadAccessMode.Foot);
        world.CreatePedestrian(
            new TripRequest(new TripRequestId(1), TripEndpoint.ForBuilding(originBuilding), TripEndpoint.ForBuilding(destinationBuilding), TravelMode.Foot),
            walkingSpeedMetersPerSecond: 4d);
    }
}