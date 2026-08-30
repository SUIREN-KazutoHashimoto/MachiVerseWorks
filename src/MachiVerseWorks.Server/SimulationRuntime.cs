using MachiVerseWorks.Persistence;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationRuntime
{
    private readonly object _gate = new();
    private readonly SimulationWorld _world;
    private bool _pedestrianFixturePending;
    private bool _trafficFixturePending;
    private RoadNetworkReadModel? _roadReadModel;

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

        _pedestrianFixturePending = bool.TryParse(configuration["Simulation:PedestrianFixture"], out var pedestrianFixture) && pedestrianFixture;
        _trafficFixturePending = bool.TryParse(configuration["Simulation:TrafficFixture"], out var trafficFixture) && trafficFixture;
    }

    public int TickRate => _world.Config.TickRate;
    public TimeSpan TickInterval => TimeSpan.FromSeconds(_world.Config.TickDurationSeconds);
    public double SpatialCellSize => _world.Config.SpatialCellSize;
    public ulong TickCount { get { lock (_gate) return _world.Time.TickCount; } }
    public int ActiveAgentCount { get { lock (_gate) return _world.ActiveAgentCount; } }
    public int ActivePedestrianCount { get { lock (_gate) return _world.ActivePedestrianCount; } }
    public int ActiveVehicleCount { get { lock (_gate) return _world.ActiveVehicleCount; } }
    public int RoadSegmentCount { get { lock (_gate) return _world.RoadSegmentCount; } }
    public int HouseholdCount { get { lock (_gate) return _world.HouseholdCount; } }
    public int PersonCount { get { lock (_gate) return _world.PersonCount; } }

    public void Step()
    {
        lock (_gate) _world.Step();
    }

    public AgentSnapshot[] CreateSnapshot(WorldVolume volume)
    {
        lock (_gate) return _world.CreateSnapshot(volume);
    }

    public PedestrianSnapshot[] CreatePedestrianSnapshot(WorldVolume volume)
    {
        lock (_gate)
        {
            EnsureFixtures();
            return _world.CreatePedestrianSnapshot(volume);
        }
    }

    public RoadNetworkSnapshot CreateRoadNetworkSnapshot(WorldVolume volume)
    {
        lock (_gate)
        {
            EnsureFixtures();
            return _world.CreateRoadNetworkSnapshot(volume);
        }
    }

    public PopulationStatistics CreatePopulationStatistics()
    {
        lock (_gate) return _world.CreatePopulationStatistics();
    }

    public bool TryGetPersonSnapshot(PersonId id, out PersonSnapshot snapshot)
    {
        lock (_gate) return _world.TryGetPersonSnapshot(id, out snapshot);
    }

    public SimulationPublishSnapshot CapturePublishSnapshot()
    {
        ulong tickCount;
        AgentSnapshot[] agents;
        PedestrianSnapshot[] pedestrians;
        VehicleSnapshot[] vehicles;
        IntersectionControlSnapshot intersectionControl;
        RoadNetworkReadModel roadReadModel;
        lock (_gate)
        {
            EnsureFixtures();
            tickCount = _world.Time.TickCount;
            agents = _world.CreateAllAgentSnapshots();
            pedestrians = _world.CreateAllPedestrianSnapshots();
            vehicles = _world.CreateAllVehicleSnapshots();
            intersectionControl = _world.CreateIntersectionControlSnapshot();
            _roadReadModel ??= new RoadNetworkReadModel(1, _world.CreateRoadNetworkSnapshot());
            roadReadModel = _roadReadModel;
        }

        return new SimulationPublishSnapshot(
            tickCount,
            SpatialCellSize,
            agents,
            pedestrians,
            vehicles,
            intersectionControl,
            roadReadModel);
    }

    private void EnsureFixtures()
    {
        if (_pedestrianFixturePending)
        {
            SeedPedestrianFixture(_world);
            _pedestrianFixturePending = false;
            _roadReadModel = null;
        }
        if (_trafficFixturePending)
        {
            SeedTrafficFixture(_world);
            _trafficFixturePending = false;
            _roadReadModel = null;
        }
    }

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

    private static void SeedTrafficFixture(SimulationWorld world)
    {
        const double armLength = 30d;
        const double speedLimit = 10d;
        var center = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection);
        var west = CreateTrafficArm(world, center, new WorldPoint(-armLength, 0d, 0d), speedLimit);
        var east = CreateTrafficArm(world, center, new WorldPoint(armLength, 0d, 0d), speedLimit);
        var south = CreateTrafficArm(world, center, new WorldPoint(0d, -armLength, 0d), speedLimit);
        var north = CreateTrafficArm(world, center, new WorldPoint(0d, armLength, 0d), speedLimit);

        AddTrafficVehicle(world, center, west, east, TurnMovement.Straight, speedLimit);
        AddTrafficVehicle(world, center, east, west, TurnMovement.Straight, speedLimit);
        AddTrafficVehicle(world, center, south, north, TurnMovement.Straight, speedLimit);
        AddTrafficVehicle(world, center, north, south, TurnMovement.Straight, speedLimit);
    }

    private static TrafficArm CreateTrafficArm(
        SimulationWorld world,
        RoadNodeId center,
        WorldPoint endpointPosition,
        double speedLimit)
    {
        var endpoint = world.CreateRoadNode(endpointPosition);
        var segment = world.CreateRoadSegment(center, endpoint, RoadKind.Local);
        var outbound = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: speedLimit);
        var inbound = world.CreateLane(segment, LaneDirection.Reverse, 0, speedLimitMetersPerSecond: speedLimit);
        return new TrafficArm(segment, inbound, outbound);
    }

    private static void AddTrafficVehicle(
        SimulationWorld world,
        RoadNodeId center,
        TrafficArm from,
        TrafficArm to,
        TurnMovement turnMovement,
        double speedLimit)
    {
        var connection = world.CreateLaneConnection(from.InboundLaneId, to.OutboundLaneId, center, turnMovement);
        world.CreateVehicle(
        [
            new RouteLaneStep(from.InboundLaneId, from.SegmentId, 1d, 0d, 30d, 30d / speedLimit, connection),
            new RouteLaneStep(to.OutboundLaneId, to.SegmentId, 0d, 1d, 30d, 30d / speedLimit, null),
        ],
        initialSpeedMetersPerSecond: 8d);
    }

    private readonly record struct TrafficArm(RoadSegmentId SegmentId, LaneId InboundLaneId, LaneId OutboundLaneId);
}
