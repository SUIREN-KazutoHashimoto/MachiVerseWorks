using MachiVerseWorks.Persistence;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationRuntime
{
    private readonly object _gate = new();
    private SimulationWorld _world;
    private bool _paused;
    private bool _pedestrianFixturePending;
    private bool _roadTrafficFixturePending;
    private bool _trafficFixturePending;
    private bool _populationFixturePending;
    private bool _railwayFixturePending;
    private bool _railwayOperationsFixturePending;
    private bool _multimodalTransitFixturePending;
    private bool _economyFixturePending;
    private RoadNetworkReadModel? _roadReadModel;
    private RailwayInfrastructureReadModel? _railwayReadModel;
    private ulong _roadRevision = 1;
    private ulong _railwayRevision = 1;
    private ulong _observationGeneration = 1;
    private ulong _observationRevision = 1;

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
            _world.CreateAgents(options.InitialAgentCount, new WorldVolume(options.SpawnMinX, options.SpawnMinY, options.SpawnMinZ, options.SpawnMaxX, options.SpawnMaxY, options.SpawnMaxZ));
        _pedestrianFixturePending = ReadFixture(configuration, "Simulation:PedestrianFixture");
        _roadTrafficFixturePending = ReadFixture(configuration, "Simulation:RoadTrafficFixture");
        _trafficFixturePending = ReadFixture(configuration, "Simulation:TrafficFixture");
        _populationFixturePending = ReadFixture(configuration, "Simulation:PopulationFixture");
        _railwayFixturePending = ReadFixture(configuration, "Simulation:RailwayFixture");
        _railwayOperationsFixturePending = ReadFixture(configuration, "Simulation:RailwayOperationsFixture");
        _multimodalTransitFixturePending = ReadFixture(configuration, "Simulation:MultimodalTransitFixture");
        _economyFixturePending = ReadFixture(configuration, "Simulation:EconomyFixture");
    }

    public int TickRate { get { lock (_gate) return _world.Config.TickRate; } }
    public TimeSpan TickInterval { get { lock (_gate) return TimeSpan.FromSeconds(_world.Config.TickDurationSeconds); } }
    public double SpatialCellSize { get { lock (_gate) return _world.Config.SpatialCellSize; } }
    public ulong TickCount { get { lock (_gate) return _world.Time.TickCount; } }
    public ulong ObservationGeneration { get { lock (_gate) return _observationGeneration; } }
    public ulong ObservationRevision { get { lock (_gate) return _observationRevision; } }
    public bool IsPaused { get { lock (_gate) return _paused; } }
    public int ActiveAgentCount { get { lock (_gate) return _world.ActiveAgentCount; } }
    public int ActivePedestrianCount { get { lock (_gate) return _world.ActivePedestrianCount; } }
    public int ActiveVehicleCount { get { lock (_gate) return _world.ActiveVehicleCount; } }
    public int RoadSegmentCount { get { lock (_gate) return _world.RoadSegmentCount; } }
    public int TrackSegmentCount { get { lock (_gate) return _world.TrackSegmentCount; } }
    public int HouseholdCount { get { lock (_gate) { EnsureFixtures(); return _world.HouseholdCount; } } }
    public int PersonCount { get { lock (_gate) { EnsureFixtures(); return _world.PersonCount; } } }
    internal ulong RoadRevision { get { lock (_gate) return _roadRevision; } }
    internal ulong RailwayRevision { get { lock (_gate) return _railwayRevision; } }

    public void Step()
    {
        lock (_gate)
        {
            EnsureFixtures();
            if (_paused) return;
            _world.Step();
            AdvanceObservationRevision();
        }
    }

    public bool Pause() { lock (_gate) { if (_paused) return false; _paused = true; return true; } }
    public bool Resume() { lock (_gate) { if (!_paused) return false; _paused = false; return true; } }
    public ulong StepPaused(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        lock (_gate)
        {
            EnsureFixtures();
            if (!_paused) throw new InvalidOperationException("Simulation must be paused before manual stepping.");
            for (var index = 0; index < count; index++)
            {
                _world.Step();
                AdvanceObservationRevision();
            }
            return _world.Time.TickCount;
        }
    }

    public T Read<T>(Func<SimulationWorld, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate) { EnsureFixtures(); return operation(_world); }
    }

    public T Mutate<T>(Func<SimulationWorld, T> operation, bool roadTopologyChanged = false, bool railwayTopologyChanged = false)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            EnsureFixtures();
            var result = operation(_world);
            var changed = !((roadTopologyChanged || railwayTopologyChanged) && result is bool booleanResult && !booleanResult);
            if (!changed) return result;
            if (roadTopologyChanged) { _roadRevision = checked(_roadRevision + 1); _roadReadModel = null; }
            if (railwayTopologyChanged) { _railwayRevision = checked(_railwayRevision + 1); _railwayReadModel = null; }
            AdvanceObservationRevision();
            return result;
        }
    }

    public SimulationCheckpoint CaptureCheckpoint() { lock (_gate) { EnsureFixtures(); return _world.CreateCheckpoint(); } }
    public void ReplaceWorld(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        lock (_gate)
        {
            _world = world;
            _observationGeneration = checked(_observationGeneration + 1);
            _observationRevision = 1;
            _roadRevision = checked(_roadRevision + 1);
            _railwayRevision = checked(_railwayRevision + 1);
            _roadReadModel = null;
            _railwayReadModel = null;
            _pedestrianFixturePending = _roadTrafficFixturePending = _trafficFixturePending = _populationFixturePending = false;
            _railwayFixturePending = _railwayOperationsFixturePending = _multimodalTransitFixturePending = _economyFixturePending = false;
        }
    }

    public AgentSnapshot[] CreateSnapshot(WorldVolume volume) { lock (_gate) return _world.CreateSnapshot(volume); }
    public PedestrianSnapshot[] CreatePedestrianSnapshot(WorldVolume volume) { lock (_gate) { EnsureFixtures(); return _world.CreatePedestrianSnapshot(volume); } }
    public RoadNetworkSnapshot CreateRoadNetworkSnapshot(WorldVolume volume) { lock (_gate) { EnsureFixtures(); return _world.CreateRoadNetworkSnapshot(volume); } }
    public RailwayInfrastructureSnapshot CreateRailwayInfrastructureSnapshot(WorldVolume volume) { lock (_gate) { EnsureFixtures(); return _world.CreateRailwayInfrastructureSnapshot(volume); } }
    public PopulationStatistics CreatePopulationStatistics() { lock (_gate) { EnsureFixtures(); return _world.CreatePopulationStatistics(); } }
    public bool TryGetPersonSnapshot(PersonId id, out PersonSnapshot snapshot) { lock (_gate) { EnsureFixtures(); return _world.TryGetPersonSnapshot(id, out snapshot); } }

    public PopulationPublishSnapshot CapturePopulationPublishSnapshot(IReadOnlySet<ulong> inspectedPersonIds)
    {
        ArgumentNullException.ThrowIfNull(inspectedPersonIds);
        lock (_gate)
        {
            EnsureFixtures();
            var statistics = _world.CreatePopulationStatistics();
            var persons = new Dictionary<ulong, PersonSnapshot>(inspectedPersonIds.Count);
            foreach (var personId in inspectedPersonIds)
                if (_world.TryGetPersonSnapshot(new PersonId(personId), out var person)) persons.Add(personId, person);
            return new PopulationPublishSnapshot(_observationGeneration, _observationRevision, _world.Time.TickCount, statistics, persons);
        }
    }

    public VersionedObservation<WorldEnvironmentSnapshot> CaptureWorldEnvironmentSnapshot(WorldVolume volume)
    {
        WorldEnvironmentConfig config;
        ulong tickCount;
        ulong generation;
        ulong revision;
        lock (_gate)
        {
            EnsureFixtures();
            config = _world.WorldEnvironment;
            tickCount = _world.Time.TickCount;
            generation = _observationGeneration;
            revision = _observationRevision;
        }
        return new VersionedObservation<WorldEnvironmentSnapshot>(
            generation,
            revision,
            SimulationWorld.CreateDetachedDetailedWorldEnvironmentSnapshot(config, tickCount, volume));
    }

    public SimulationPublishSnapshot CapturePublishSnapshot() => CapturePublishSnapshot(null);

    public SimulationPublishSnapshot CapturePublishSnapshot(WorldVolume volume) => CapturePublishSnapshot((WorldVolume?)volume);

    private SimulationPublishSnapshot CapturePublishSnapshot(WorldVolume? volume)
    {
        ulong tickCount; ulong observationGeneration; ulong observationRevision; AgentSnapshot[] agents; PedestrianSnapshot[] pedestrians; VehicleSnapshot[] vehicles; TrainSnapshot[] trains; RailwayOperationsSnapshot railwayOperations; MultimodalTransitSnapshot multimodalTransit; IntersectionControlSnapshot intersectionControl; RoadNetworkReadModel roadReadModel; RailwayInfrastructureReadModel railwayReadModel; double spatialCellSize;
        lock (_gate)
        {
            EnsureFixtures();
            tickCount = _world.Time.TickCount; observationGeneration = _observationGeneration; observationRevision = _observationRevision; spatialCellSize = _world.Config.SpatialCellSize;
            if (volume is { } selectedVolume)
            {
                agents = _world.CreateSnapshot(selectedVolume);
                pedestrians = _world.CreatePedestrianSnapshot(selectedVolume);
                vehicles = _world.CreateVehicleSnapshot(selectedVolume);
                trains = _world.CreateTrainSnapshot().Where(item => selectedVolume.Contains(item.Position)).ToArray();
            }
            else
            {
                agents = _world.CreateAllAgentSnapshots();
                pedestrians = _world.CreateAllPedestrianSnapshots();
                vehicles = _world.CreateAllVehicleSnapshots();
                trains = _world.CreateTrainSnapshot();
            }
            railwayOperations = _world.CreateRailwayOperationsSnapshot(); multimodalTransit = _world.CreateMultimodalTransitSnapshot(); intersectionControl = _world.CreateIntersectionControlSnapshot();
            _roadReadModel ??= new RoadNetworkReadModel(_roadRevision, _world.CreateRoadNetworkSnapshot());
            _railwayReadModel ??= new RailwayInfrastructureReadModel(_railwayRevision, _world.CreateRailwayInfrastructureSnapshot());
            roadReadModel = _roadReadModel; railwayReadModel = _railwayReadModel;
        }
        return new SimulationPublishSnapshot(tickCount, spatialCellSize, agents, pedestrians, vehicles, intersectionControl, roadReadModel, railwayReadModel, trains, railwayOperations, multimodalTransit, observationGeneration, observationRevision);
    }

    private static bool ReadFixture(IConfiguration configuration, string key) => bool.TryParse(configuration[key], out var value) && value;

    private void EnsureFixtures()
    {
        var changed = false;
        if (_pedestrianFixturePending) { SeedPedestrianFixture(_world); _pedestrianFixturePending = false; _roadRevision = checked(_roadRevision + 1); _roadReadModel = null; changed = true; }
        if (_roadTrafficFixturePending) { SeedRoadTrafficFixture(_world); _roadTrafficFixturePending = false; _roadRevision = checked(_roadRevision + 1); _roadReadModel = null; changed = true; }
        if (_trafficFixturePending) { SeedTrafficFixture(_world); _trafficFixturePending = false; _roadRevision = checked(_roadRevision + 1); _roadReadModel = null; changed = true; }
        if (_populationFixturePending) { SeedPopulationFixture(_world); _populationFixturePending = false; changed = true; }
        if (_railwayFixturePending) { RailwayInfrastructureFixtures.SeedDeterministic(_world); _railwayFixturePending = false; _railwayRevision = checked(_railwayRevision + 1); _railwayReadModel = null; changed = true; }
        if (_railwayOperationsFixturePending) { RailwayOperationsFixtures.SeedDeterministic(_world); _railwayOperationsFixturePending = false; _railwayRevision = checked(_railwayRevision + 1); _railwayReadModel = null; changed = true; }
        if (_multimodalTransitFixturePending) { MultimodalTransitFixtures.SeedDeterministic(_world); _multimodalTransitFixturePending = false; _roadRevision = checked(_roadRevision + 1); _railwayRevision = checked(_railwayRevision + 1); _roadReadModel = null; _railwayReadModel = null; changed = true; }
        if (_economyFixturePending) { SeedEconomyFixture(_world); _economyFixturePending = false; changed = true; }
        if (changed) AdvanceObservationRevision();
    }

    private void AdvanceObservationRevision() => _observationRevision = checked(_observationRevision + 1);

    private static void SeedPopulationFixture(SimulationWorld world)
    {
        var home = world.CreateBuilding(new WorldVolume(-2d, -2d, 0d, 2d, 2d, 4d), BuildingKind.Residential);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        world.CreatePerson(household, new PersonDemographics(35, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
    }

    private static void SeedEconomyFixture(SimulationWorld world)
    {
        var home = world.CreateBuilding(new WorldVolume(-4d, -4d, 0d, 4d, 4d, 4d), BuildingKind.Residential);
        var shop = world.CreateBuilding(new WorldVolume(16d, -4d, 0d, 24d, 4d, 4d), BuildingKind.Commercial);
        var poi = world.CreatePoi(new WorldPoint(20d, 0d, 0d), PoiKind.Retail, shop);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(household, new PersonDemographics(35, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        world.SetHouseholdCashBalance(household, 1_000);
        var company = world.CreateCompany(IndustrySector.Retail, 100_000, 10d);
        var establishment = world.CreateEstablishment(company, shop, poi);
        var job = world.CreateJob(establishment, 2, 500);
        world.AssignEmployment(person, job);
    }

    private static void SeedPedestrianFixture(SimulationWorld world)
    {
        var originBuilding = world.CreateBuilding(new WorldVolume(-21d, -1d, 0d, -19d, 1d, 2d), BuildingKind.Residential);
        var destinationBuilding = world.CreateBuilding(new WorldVolume(19d, -1d, 0d, 21d, 1d, 2d), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(-20d, 0d, 0d)); var crossing = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection); var end = world.CreateRoadNode(new WorldPoint(20d, 0d, 0d));
        var firstSegment = world.CreateRoadSegment(start, crossing, RoadKind.Local); var secondSegment = world.CreateRoadSegment(crossing, end, RoadKind.Local);
        world.CreateRoadAccessPoint(firstSegment, 0d, originBuilding, mode: RoadAccessMode.Foot); world.CreateRoadAccessPoint(secondSegment, 1d, destinationBuilding, mode: RoadAccessMode.Foot);
        world.CreatePedestrian(new TripRequest(new TripRequestId(1), TripEndpoint.ForBuilding(originBuilding), TripEndpoint.ForBuilding(destinationBuilding), TravelMode.Foot), walkingSpeedMetersPerSecond: 4d);
    }

    private static void SeedRoadTrafficFixture(SimulationWorld world)
    {
        const double startX = -30d, endX = 30d, distanceMeters = endX - startX, speedLimit = 10d;
        var routes = new IReadOnlyList<RouteLaneStep>[3];
        for (var index = 0; index < routes.Length; index++)
        {
            var y = (index - 1) * 12d; var start = world.CreateRoadNode(new WorldPoint(startX, y, 0d)); var end = world.CreateRoadNode(new WorldPoint(endX, y, 0d)); var segment = world.CreateRoadSegment(start, end, RoadKind.Local); var lane = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: speedLimit);
            routes[index] = [new RouteLaneStep(lane, segment, 0d, 1d, distanceMeters, distanceMeters / speedLimit, null)];
        }
        foreach (var route in routes) world.CreateVehicle(route, initialSpeedMetersPerSecond: 8d);
    }

    private static void SeedTrafficFixture(SimulationWorld world)
    {
        const double armLength = 30d, speedLimit = 10d;
        var center = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d), RoadNodeKind.Intersection);
        var west = CreateTrafficArm(world, center, new WorldPoint(-armLength, 0d, 0d), speedLimit); var east = CreateTrafficArm(world, center, new WorldPoint(armLength, 0d, 0d), speedLimit); var south = CreateTrafficArm(world, center, new WorldPoint(0d, -armLength, 0d), speedLimit); var north = CreateTrafficArm(world, center, new WorldPoint(0d, armLength, 0d), speedLimit);
        var routes = new[] { CreateTrafficRoute(world, center, west, east, TurnMovement.Straight, speedLimit), CreateTrafficRoute(world, center, east, west, TurnMovement.Straight, speedLimit), CreateTrafficRoute(world, center, south, north, TurnMovement.Straight, speedLimit), CreateTrafficRoute(world, center, north, south, TurnMovement.Straight, speedLimit) };
        foreach (var route in routes) world.CreateVehicle(route, initialSpeedMetersPerSecond: 8d);
    }

    private static TrafficArm CreateTrafficArm(SimulationWorld world, RoadNodeId center, WorldPoint endpointPosition, double speedLimit)
    {
        var endpoint = world.CreateRoadNode(endpointPosition); var segment = world.CreateRoadSegment(center, endpoint, RoadKind.Local); var outbound = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: speedLimit); var inbound = world.CreateLane(segment, LaneDirection.Reverse, 0, speedLimitMetersPerSecond: speedLimit); return new TrafficArm(segment, inbound, outbound);
    }

    private static IReadOnlyList<RouteLaneStep> CreateTrafficRoute(SimulationWorld world, RoadNodeId center, TrafficArm from, TrafficArm to, TurnMovement turnMovement, double speedLimit)
    {
        var connection = world.CreateLaneConnection(from.InboundLaneId, to.OutboundLaneId, center, turnMovement);
        return [new RouteLaneStep(from.InboundLaneId, from.SegmentId, 1d, 0d, 30d, 30d / speedLimit, connection), new RouteLaneStep(to.OutboundLaneId, to.SegmentId, 0d, 1d, 30d, 30d / speedLimit, null)];
    }

    private readonly record struct TrafficArm(RoadSegmentId SegmentId, LaneId InboundLaneId, LaneId OutboundLaneId);
}
