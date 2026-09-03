using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly AgentStore _agents = new();
    private readonly SpatialIndex _spatialIndex;
    private DeterministicRandom _random;
    private bool _isFaulted;

    public SimulationWorld(
        SimulationConfig? config = null,
        IPowerDispatchSolver? powerDispatchSolver = null,
        IWaterSupplySolver? waterSupplySolver = null,
        ISewerSolver? sewerSolver = null,
        IGasSupplySolver? gasSupplySolver = null,
        IOpticalRoutingSolver? opticalRoutingSolver = null,
        IRadioPropagationSolver? radioPropagationSolver = null)
    {
        Config = config ?? new SimulationConfig();
        _spatialIndex = new SpatialIndex(Config.SpatialCellSize);
        _pedestrianSpatialIndex = new PedestrianSpatialIndex(Config.SpatialCellSize);
        _roads = new RoadNetworkStore(Config.SpatialCellSize);
        _railway = new RailwayInfrastructureStore();
        _powerDispatchSolver = powerDispatchSolver ?? new CapacityPowerDispatchSolver();
        _waterSupplySolver = CreateWaterSupplySolver(waterSupplySolver);
        _sewerSolver = CreateSewerSolver(sewerSolver);
        _gasSupplySolver = new ValidatingGasSupplySolver(gasSupplySolver ?? new CapacityGasSupplySolver());
        _opticalRoutingSolver = opticalRoutingSolver ?? new CapacityOpticalRoutingSolver();
        _radioPropagationSolver = radioPropagationSolver ?? new DeterministicRadioPropagationSolver();
        _random = new DeterministicRandom(Config.Seed);
        Time = default;
    }

    public SimulationConfig Config { get; }
    public SimulationTime Time { get; private set; }
    public int ActiveAgentCount => _agents.ActiveCount;
    public int TotalCreatedAgentCount => _agents.TotalCreatedCount;
    public bool IsFaulted => _isFaulted;

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
        if (_isFaulted)
            throw new InvalidOperationException("Simulation world is faulted because a previous Step failed and cannot be stepped again.");

        try
        {
            var nextTime = Time.Advance(Config.TickRate);
            _agents.Step(Config.TickDurationSeconds, _spatialIndex);
            CapturePowerProductionBaselines();
            StepPower(nextTime);
            StepWaterSewerTransactional(nextTime);
            StepGas(nextTime);
            StepOptical(nextTime);
            StepRadio(nextTime);
            StepEconomy(nextTime);
            ApplyPowerOperationalConstraints();
            StepLogistics(nextTime);
            StepPersistentRegionalEvolution(nextTime);
            PlanPopulationAndEconomyTrips(nextTime);
            StepVehicles(Config.TickDurationSeconds, nextTime.TickCount);
            StepRailwayOperations(Config.TickDurationSeconds, nextTime.TickCount);
            StepPedestrians(Config.TickDurationSeconds);
            StepMultimodalTransit(nextTime.TickCount);
            CompletePopulationTrips();
            Time = nextTime;
        }
        catch
        {
            _isFaulted = true;
            throw;
        }
    }

    public bool TryGetAgentSnapshot(AgentId id, out AgentSnapshot snapshot) => _agents.TryGetSnapshot(id, Time.TickCount, out snapshot);
    public AgentSnapshot[] CreateSnapshot(WorldVolume volume) => _agents.CreateSnapshot(volume, _spatialIndex, Time.TickCount);

    public SimulationCheckpoint CreateCheckpoint()
    {
        if (_isFaulted)
            throw new InvalidOperationException("A faulted Simulation world cannot be checkpointed because its domain state may represent a partial tick.");
        EnsurePedestrianNetwork();
        var railwayOperations = _railwayOperations?.CreateSnapshot();
        var economy = CreateEconomyCheckpointWithRadio() with
        {
            WorldEnvironment = CreateWorldEnvironmentCheckpoint(),
            RegionalGeneration = CreateRegionalGenerationCheckpoint(),
            RegionalEvolution = CreatePersistentRegionalEvolutionCheckpoint(),
        };
        return new SimulationCheckpoint(
            Config.TickRate, Config.Seed, Config.SpatialCellSize, Time.TickCount, Time.Elapsed.Ticks, _random.State,
            _agents.NextId, _agents.CreateCheckpoint(),
            _buildings.NextId, _buildings.CreateCheckpoint(),
            _pois.NextId, _pois.CreateCheckpoint(),
            _roads.NextNodeId, _roads.CreateNodeCheckpoint(),
            _roads.NextSegmentId, _roads.CreateSegmentCheckpoint(),
            _roads.NextLaneId, _roads.CreateLaneCheckpoint(),
            _roads.NextConnectionId, _roads.CreateConnectionCheckpoint(),
            _roads.NextAccessPointId, _roads.CreateAccessPointCheckpoint(),
            _pedestrians.NextId, _pedestrians.CreateCheckpoint(), _pedestrianNetwork.CreateCrossingCheckpoint(),
            _vehicles.NextId, _vehicles.CreateCheckpoint(),
            _population.NextHouseholdId, _population.CreateHouseholdCheckpoint(),
            _population.NextPersonId, _population.CreatePersonCheckpoint(), _population.NextTripRequestId,
            _railway.NextNodeId, _railway.CreateNodeCheckpoint(),
            _railway.NextSegmentId, _railway.CreateSegmentCheckpoint(),
            _railway.NextConnectionId, _railway.CreateConnectionCheckpoint(),
            _railway.NextBlockId, _railway.CreateBlockCheckpoint(),
            _railway.NextStationId, _railway.CreateStationCheckpoint(),
            _railway.NextPlatformId, _railway.CreatePlatformCheckpoint(),
            _railway.NextPlatformAccessPointId, _railway.CreatePlatformAccessPointCheckpoint(),
            _railway.NextDepotId, _railway.CreateDepotCheckpoint(),
            _railwayOperations?.NextFormationId ?? 1UL, railwayOperations?.Formations ?? Array.Empty<TrainFormationSnapshot>(),
            _railwayOperations?.NextRouteId ?? 1UL, railwayOperations?.Routes ?? Array.Empty<RailwayRouteSnapshot>(),
            _railwayOperations?.NextTimetableId ?? 1UL, railwayOperations?.Timetables ?? Array.Empty<TimetableSnapshot>(),
            _railwayOperations?.NextServiceId ?? 1UL, railwayOperations?.Services ?? Array.Empty<RailwayServiceSnapshot>(),
            _railwayOperations?.NextTrainId ?? 1UL, railwayOperations?.Trains ?? Array.Empty<TrainSnapshot>(),
            _multimodalTransit.CreateCheckpoint(Time.TickCount),
            economy,
            _agents.TotalCreatedCount);
    }

    public static SimulationWorld RestoreCheckpoint(SimulationCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(checkpoint.Agents); ArgumentNullException.ThrowIfNull(checkpoint.Buildings); ArgumentNullException.ThrowIfNull(checkpoint.Pois);
        ArgumentNullException.ThrowIfNull(checkpoint.RoadNodes); ArgumentNullException.ThrowIfNull(checkpoint.RoadSegments); ArgumentNullException.ThrowIfNull(checkpoint.Lanes);
        ArgumentNullException.ThrowIfNull(checkpoint.LaneConnections); ArgumentNullException.ThrowIfNull(checkpoint.RoadAccessPoints);
        if (checkpoint.ElapsedTicks < 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.ElapsedTicks, "Simulation elapsed time cannot be negative.");
        if (checkpoint.NextAgentId == 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextAgentId, "Next Agent ID must be greater than zero.");
        ArgumentOutOfRangeException.ThrowIfNegative(checkpoint.TotalCreatedAgentCount);
        var seenAgentIds = new HashSet<ulong>(checkpoint.Agents.Count); var maximumAgentId = 0UL;
        foreach (var agent in checkpoint.Agents)
        {
            if (agent.Id.Value == 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), agent.Id.Value, "Agent IDs must be greater than zero.");
            if (!seenAgentIds.Add(agent.Id.Value)) throw new ArgumentException($"Duplicate Agent ID {agent.Id.Value}.", nameof(checkpoint));
            ValidatePoint(agent.Position); ValidateVector(agent.Velocity); maximumAgentId = Math.Max(maximumAgentId, agent.Id.Value);
        }
        if (checkpoint.NextAgentId <= maximumAgentId) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextAgentId, "Next Agent ID must be greater than every stored Agent ID.");

        var worldEnvironment = checkpoint.Economy?.WorldEnvironment;
        var config = new SimulationConfig(checkpoint.TickRate, checkpoint.Seed, checkpoint.SpatialCellSize, worldEnvironment?.Config);
        ValidateUrbanObjectCheckpoint(checkpoint, config.SpatialCellSize);
        ValidateRoadNetworkCheckpoint(checkpoint, config.SpatialCellSize);
        ValidatePedestrianCheckpoint(checkpoint);
        ValidateVehicleCheckpoint(checkpoint);
        ValidatePopulationCheckpoint(checkpoint);
        ValidateRailwayCheckpoint(checkpoint, config.SpatialCellSize);
        ValidateRailwayOperationsCheckpoint(checkpoint);
        ValidateEconomyCheckpoint(checkpoint);
        ValidateLogisticsCheckpoint(checkpoint);
        ValidatePowerCheckpoint(checkpoint);
        ValidateWaterSewerCheckpoint(checkpoint);
        ValidateGasCheckpoint(checkpoint);
        ValidateDeliveredGasCheckpointInvariants(checkpoint);
        ValidateOpticalCheckpoint(checkpoint);
        ValidateRadioCheckpoint(checkpoint);
        ValidateWorldEnvironmentCheckpoint(checkpoint);
        ValidateRegionalGenerationCheckpoint(checkpoint);
        ValidatePersistentRegionalEvolutionCheckpoint(checkpoint);
        var expectedElapsedTicks = CalculateExpectedElapsedTicks(checkpoint.TickCount, config.TickRate);
        if (checkpoint.ElapsedTicks != expectedElapsedTicks
            && (!TryCalculateLegacyElapsedTicks(checkpoint.TickCount, config.TickRate, out var legacyElapsedTicks)
                || checkpoint.ElapsedTicks != legacyElapsedTicks))
        {
            throw new ArgumentException($"Elapsed time {checkpoint.ElapsedTicks} does not match tick count {checkpoint.TickCount} and tick rate {checkpoint.TickRate}.", nameof(checkpoint));
        }

        var restoredTime = new SimulationTime(checkpoint.TickCount, TimeSpan.FromTicks(expectedElapsedTicks));
        try { _ = restoredTime.Advance(config.TickRate); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(checkpoint), "Simulation time must allow at least one additional tick."); }
        var world = new SimulationWorld(config) { Time = restoredTime, _random = new DeterministicRandom(checkpoint.RandomState) };
        world._agents.Restore(checkpoint.Agents, checkpoint.NextAgentId, ResolveTotalCreatedAgentCount(checkpoint), world._spatialIndex);
        world.RestoreUrbanObjects(checkpoint);
        world._roads.Restore(checkpoint);
        world._railway.Restore(checkpoint);
        world.RestoreRailwayOperations(checkpoint);
        world.EnsureRoadTrafficTopology();
        world._vehicles.Restore(checkpoint.Vehicles ?? Array.Empty<SimulationVehicleCheckpoint>(), checkpoint.NextVehicleId, world._roadTrafficTopology);
        world.EnsurePedestrianNetwork();
        world._pedestrianNetwork.RestoreCrossingPermissions(checkpoint.PedestrianCrossings ?? Array.Empty<SimulationPedestrianCrossingCheckpoint>());
        world._pedestrians.Restore(
            checkpoint.Pedestrians ?? Array.Empty<SimulationPedestrianCheckpoint>(),
            checkpoint.NextPedestrianId,
            world._pedestrianNetwork,
            world._pedestrianSpatialIndex);
        world._population.Restore(
            checkpoint.Households ?? Array.Empty<SimulationHouseholdCheckpoint>(),
            checkpoint.NextHouseholdId,
            checkpoint.Persons ?? Array.Empty<SimulationPersonCheckpoint>(),
            checkpoint.NextPersonId,
            checkpoint.NextTripRequestId);
        world.RestoreEconomy(checkpoint.Economy);
        world.RestoreLogistics(checkpoint.Economy?.Logistics);
        world.RestorePower(checkpoint.Economy?.Power);
        world.RestoreWaterSewer(checkpoint.Economy?.WaterSewer);
        world.RestoreGas(checkpoint.Economy?.Gas);
        world.RestoreOptical(checkpoint.Economy?.Optical);
        world.RestoreRadio(checkpoint.Economy?.Radio);
        world.RestoreWorldEnvironment(worldEnvironment);
        world.RestoreRegionalGeneration(checkpoint.Economy?.RegionalGeneration);
        world.RestorePersistentRegionalEvolution(checkpoint.Economy?.RegionalEvolution);
        world._multimodalTransit.Restore(checkpoint.MultimodalTransit);
        ValidateMultimodalTransitCheckpointReferences(checkpoint);
        return world;
    }

    private static int ResolveTotalCreatedAgentCount(SimulationCheckpoint checkpoint)
    {
        if (checkpoint.TotalCreatedAgentCount > 0) return Math.Max(checkpoint.TotalCreatedAgentCount, checkpoint.Agents.Count);
        var activeCount = checkpoint.Agents.Count(static item => item.IsActive);
        var issuedCount = checkpoint.NextAgentId - 1;
        if (issuedCount <= int.MaxValue && checkpoint.Agents.Count == (int)issuedCount)
            return checkpoint.Agents.Count;
        return activeCount;
    }

    private double NextCoordinate(double minimum, double maximum) => minimum == maximum ? minimum : _random.NextDouble(minimum, maximum);
    private WorldVector NextVelocity() => new(_random.NextDouble(-1d, 1d), _random.NextDouble(-1d, 1d), 0d);

    private static long CalculateExpectedElapsedTicks(ulong tickCount, int tickRate)
    {
        try
        {
            return SimulationTime.CalculateElapsedTicks(tickCount, tickRate);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount), tickCount, "Tick count cannot be represented by the configured elapsed-time range.");
        }
    }

    private static bool TryCalculateLegacyElapsedTicks(ulong tickCount, int tickRate, out long elapsedTicks)
    {
        var legacyTickDurationTicks = TimeSpan.FromSeconds(1d / tickRate).Ticks;
        var total = (UInt128)tickCount * (ulong)legacyTickDurationTicks;
        if (total > long.MaxValue)
        {
            elapsedTicks = 0;
            return false;
        }

        elapsedTicks = (long)total;
        return true;
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
