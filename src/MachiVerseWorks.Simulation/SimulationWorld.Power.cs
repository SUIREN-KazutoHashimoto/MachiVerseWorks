namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly List<PowerNodeState> _powerNodes = [];
    private readonly Dictionary<PowerNodeId, PowerNodeState> _powerNodeIndex = [];
    private readonly List<PowerLineState> _powerLines = [];
    private readonly Dictionary<PowerLineId, PowerLineState> _powerLineIndex = [];
    private readonly List<GeneratorStateData> _powerGenerators = [];
    private readonly Dictionary<GeneratorId, GeneratorStateData> _powerGeneratorIndex = [];
    private readonly List<PowerLoadStateData> _powerLoads = [];
    private readonly Dictionary<PowerLoadId, PowerLoadStateData> _powerLoadIndex = [];
    private readonly IPowerDispatchSolver _powerDispatchSolver;
    private ulong _nextPowerNodeId = 1;
    private ulong _nextPowerLineId = 1;
    private ulong _nextGeneratorId = 1;
    private ulong _nextPowerLoadId = 1;

    public int PowerNodeCount => _powerNodes.Count;
    public int PowerLineCount => _powerLines.Count;
    public int GeneratorCount => _powerGenerators.Count;
    public int PowerLoadCount => _powerLoads.Count;

    public PowerNodeId CreatePowerNode(WorldPoint position, PowerNodeKind kind = PowerNodeKind.Distribution)
    {
        ValidatePoint(position);
        ValidatePowerEnum(kind, nameof(kind));
        EnsurePowerIdCapacity(_nextPowerNodeId, "Power node");
        var id = new PowerNodeId(_nextPowerNodeId++);
        var state = new PowerNodeState(id, kind, position);
        _powerNodes.Add(state);
        _powerNodeIndex.Add(id, state);
        return id;
    }

    public PowerLineId CreatePowerLine(PowerNodeId fromNodeId, PowerNodeId toNodeId, double capacityMegawatts, bool isInService = true)
    {
        if (!_powerNodeIndex.ContainsKey(fromNodeId)) throw new ArgumentException($"Power node {fromNodeId.Value} does not exist.", nameof(fromNodeId));
        if (!_powerNodeIndex.ContainsKey(toNodeId)) throw new ArgumentException($"Power node {toNodeId.Value} does not exist.", nameof(toNodeId));
        if (fromNodeId == toNodeId) throw new ArgumentException("A Power line must connect two different nodes.", nameof(toNodeId));
        ValidatePositiveFinite(capacityMegawatts, nameof(capacityMegawatts));
        EnsurePowerIdCapacity(_nextPowerLineId, "Power line");
        var id = new PowerLineId(_nextPowerLineId++);
        var state = new PowerLineState(id, fromNodeId, toNodeId, capacityMegawatts, isInService);
        _powerLines.Add(state);
        _powerLineIndex.Add(id, state);
        return id;
    }

    public void SetPowerLineInService(PowerLineId id, bool isInService)
    {
        if (!_powerLineIndex.TryGetValue(id, out var line)) throw new ArgumentException($"Power line {id.Value} does not exist.", nameof(id));
        line.IsInService = isInService;
    }

    public GeneratorId CreateGenerator(
        PowerNodeId nodeId,
        double capacityMegawatts,
        GeneratorOperatingState operatingState = GeneratorOperatingState.Online)
    {
        if (!_powerNodeIndex.ContainsKey(nodeId)) throw new ArgumentException($"Power node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidatePositiveFinite(capacityMegawatts, nameof(capacityMegawatts));
        ValidatePowerEnum(operatingState, nameof(operatingState));
        EnsurePowerIdCapacity(_nextGeneratorId, "Generator");
        var id = new GeneratorId(_nextGeneratorId++);
        var state = new GeneratorStateData(id, nodeId, capacityMegawatts, operatingState);
        _powerGenerators.Add(state);
        _powerGeneratorIndex.Add(id, state);
        return id;
    }

    public void SetGeneratorOperatingState(GeneratorId id, GeneratorOperatingState operatingState)
    {
        ValidatePowerEnum(operatingState, nameof(operatingState));
        if (!_powerGeneratorIndex.TryGetValue(id, out var generator)) throw new ArgumentException($"Generator {id.Value} does not exist.", nameof(id));
        generator.OperatingState = operatingState;
    }

    public PowerLoadId CreatePowerLoad(
        PowerNodeId nodeId,
        double baseDemandMegawatts,
        BuildingId? buildingId = null,
        EstablishmentId? establishmentId = null)
    {
        if (!_powerNodeIndex.ContainsKey(nodeId)) throw new ArgumentException($"Power node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidatePositiveFinite(baseDemandMegawatts, nameof(baseDemandMegawatts));
        if (buildingId is null && establishmentId is null)
            throw new ArgumentException("A Power load must reference a Building, an Establishment, or both.", nameof(buildingId));

        if (buildingId is { } requestedBuilding && !TryGetBuildingSnapshot(requestedBuilding, out _))
            throw new ArgumentException($"Building {requestedBuilding.Value} does not exist.", nameof(buildingId));

        if (establishmentId is { } linkedEstablishment)
        {
            if (!_economyEstablishmentIndex.TryGetValue(linkedEstablishment, out var establishment))
                throw new ArgumentException($"Establishment {linkedEstablishment.Value} does not exist.", nameof(establishmentId));
            if (buildingId is { } explicitBuilding && establishment.BuildingId is { } establishmentBuilding && explicitBuilding != establishmentBuilding)
                throw new ArgumentException($"Establishment {linkedEstablishment.Value} belongs to Building {establishmentBuilding.Value}, not Building {explicitBuilding.Value}.", nameof(buildingId));
            buildingId ??= establishment.BuildingId;
        }

        EnsurePowerIdCapacity(_nextPowerLoadId, "Power load");
        var id = new PowerLoadId(_nextPowerLoadId++);
        var state = new PowerLoadStateData(id, nodeId, buildingId, establishmentId, baseDemandMegawatts);
        _powerLoads.Add(state);
        _powerLoadIndex.Add(id, state);
        return id;
    }

    public bool TryGetPowerNodeSnapshot(PowerNodeId id, out PowerNodeSnapshot snapshot)
    {
        if (_powerNodeIndex.TryGetValue(id, out var state))
        {
            snapshot = new PowerNodeSnapshot(state.Id, state.Kind, state.Position);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetPowerLineSnapshot(PowerLineId id, out PowerLineSnapshot snapshot)
    {
        if (_powerLineIndex.TryGetValue(id, out var state))
        {
            snapshot = new PowerLineSnapshot(state.Id, state.FromNodeId, state.ToNodeId, state.CapacityMegawatts, state.IsInService);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetGeneratorSnapshot(GeneratorId id, out GeneratorSnapshot snapshot)
    {
        if (_powerGeneratorIndex.TryGetValue(id, out var state))
        {
            snapshot = CreateGeneratorSnapshot(state);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetPowerLoadSnapshot(PowerLoadId id, out PowerLoadSnapshot snapshot)
    {
        if (_powerLoadIndex.TryGetValue(id, out var state))
        {
            snapshot = CreatePowerLoadSnapshot(state);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool IsBuildingPowered(BuildingId buildingId) => GetBuildingPowerAvailabilityFactor(buildingId) > PowerDefaults.SupplyEpsilonMegawatts;
    public bool IsEstablishmentPowered(EstablishmentId establishmentId) => GetEstablishmentPowerAvailabilityFactor(establishmentId) > PowerDefaults.SupplyEpsilonMegawatts;

    public PowerSnapshot CreatePowerSnapshot()
    {
        var nodes = _powerNodes.OrderBy(static item => item.Id.Value)
            .Select(static item => new PowerNodeSnapshot(item.Id, item.Kind, item.Position)).ToArray();
        var lines = _powerLines.OrderBy(static item => item.Id.Value)
            .Select(static item => new PowerLineSnapshot(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityMegawatts, item.IsInService)).ToArray();
        var generators = _powerGenerators.OrderBy(static item => item.Id.Value).Select(CreateGeneratorSnapshot).ToArray();
        var loads = _powerLoads.OrderBy(static item => item.Id.Value).Select(CreatePowerLoadSnapshot).ToArray();
        return new PowerSnapshot(CreatePowerStatistics(), nodes, lines, generators, loads);
    }

    public PowerStatistics CreatePowerStatistics()
    {
        var outageCount = _powerLoads.Count(static item => item.SupplyState == PowerSupplyState.Outage);
        return new PowerStatistics(
            _powerNodes.Count,
            _powerLines.Count,
            _powerGenerators.Count,
            _powerLoads.Count,
            outageCount,
            _powerGenerators.Sum(static item => item.CapacityMegawatts),
            _powerGenerators.Sum(static item => item.OutputMegawatts),
            _powerLoads.Sum(static item => item.DemandMegawatts),
            _powerLoads.Sum(static item => item.ServedMegawatts),
            _powerLoads.Sum(static item => item.UnservedMegawatts),
            Time.TickCount);
    }

    private void StepPower(SimulationTime nextTime)
    {
        foreach (var load in _powerLoads.OrderBy(static item => item.Id.Value))
            load.DemandMegawatts = CalculatePowerDemand(load, nextTime);

        var request = new PowerDispatchRequest(
            _powerNodes.OrderBy(static item => item.Id.Value).Select(static item => new PowerDispatchNode(item.Id)).ToArray(),
            _powerLines.OrderBy(static item => item.Id.Value).Select(static item => new PowerDispatchLine(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityMegawatts, item.IsInService)).ToArray(),
            _powerGenerators.OrderBy(static item => item.Id.Value).Select(static item => new PowerDispatchGenerator(
                item.Id,
                item.NodeId,
                item.OperatingState == GeneratorOperatingState.Online ? item.CapacityMegawatts : 0d)).ToArray(),
            _powerLoads.OrderBy(static item => item.Id.Value).Select(static item => new PowerDispatchLoad(item.Id, item.NodeId, item.DemandMegawatts)).ToArray());
        var result = _powerDispatchSolver.Solve(request) ?? throw new InvalidOperationException("Power dispatch solver returned no result.");

        var generatorOutputs = result.Generators.ToDictionary(static item => item.GeneratorId, static item => item.OutputMegawatts);
        var servedLoads = result.Loads.ToDictionary(static item => item.LoadId, static item => item.ServedMegawatts);
        foreach (var generator in _powerGenerators)
        {
            var output = generatorOutputs.GetValueOrDefault(generator.Id);
            if (!double.IsFinite(output) || output < 0d || output > generator.CapacityMegawatts + PowerDefaults.SupplyEpsilonMegawatts)
                throw new InvalidOperationException($"Power dispatch solver returned invalid output for Generator {generator.Id.Value}.");
            generator.OutputMegawatts = generator.OperatingState == GeneratorOperatingState.Online ? Math.Min(generator.CapacityMegawatts, output) : 0d;
        }
        foreach (var load in _powerLoads)
        {
            var served = servedLoads.GetValueOrDefault(load.Id);
            if (!double.IsFinite(served) || served < 0d || served > load.DemandMegawatts + PowerDefaults.SupplyEpsilonMegawatts)
                throw new InvalidOperationException($"Power dispatch solver returned invalid served demand for Load {load.Id.Value}.");
            load.ServedMegawatts = Math.Min(load.DemandMegawatts, served);
            load.UnservedMegawatts = Math.Max(0d, load.DemandMegawatts - load.ServedMegawatts);
            load.SupplyState = load.UnservedMegawatts <= PowerDefaults.SupplyEpsilonMegawatts
                ? PowerSupplyState.Supplied
                : load.ServedMegawatts <= PowerDefaults.SupplyEpsilonMegawatts
                    ? PowerSupplyState.Outage
                    : PowerSupplyState.Constrained;
        }
    }

    private double CalculatePowerDemand(PowerLoadStateData load, SimulationTime time)
    {
        var hour = time.Elapsed.TotalHours % 24d;
        if (hour < 0d) hour += 24d;
        var timeFactor = hour switch
        {
            < 6d => 0.55d,
            < 9d => 0.8d,
            < 17d => 1d,
            < 22d => 0.9d,
            _ => 0.65d,
        };

        var useFactor = 1d;
        if (load.BuildingId is { } buildingId && TryGetBuildingSnapshot(buildingId, out var building))
        {
            useFactor *= building.Kind switch
            {
                BuildingKind.Residential => 0.8d,
                BuildingKind.Commercial => 1.1d,
                BuildingKind.Industrial => 1.25d,
                BuildingKind.Civic => 1.05d,
                BuildingKind.MixedUse => 1d,
                _ => 0.9d,
            };
        }

        var activityFactor = 1d;
        if (load.EstablishmentId is { } establishmentId && _economyEstablishmentIndex.TryGetValue(establishmentId, out var establishment))
        {
            if (_economyCompanyIndex.TryGetValue(establishment.CompanyId, out var company))
            {
                useFactor *= company.Sector switch
                {
                    IndustrySector.Manufacturing => 1.35d,
                    IndustrySector.Retail => 1.1d,
                    IndustrySector.Transport => 1.15d,
                    IndustrySector.Public => 1.05d,
                    _ => 1d,
                };
            }

            var jobs = _economyJobs.Where(item => item.EstablishmentId == establishmentId).ToArray();
            if (jobs.Length > 0)
            {
                var required = jobs.Sum(static item => item.RequiredWorkerCount);
                var filled = jobs.Sum(item => GetFilledWorkerCount(item.Id));
                var utilization = required == 0 ? 0d : Math.Min(1d, (double)filled / required);
                activityFactor = 0.6d + (0.4d * utilization);
            }
            else
            {
                activityFactor = 0.75d;
            }
        }

        return load.BaseDemandMegawatts * timeFactor * useFactor * activityFactor;
    }

    private double GetEstablishmentPowerAvailabilityFactor(EstablishmentId establishmentId)
    {
        var direct = _powerLoads.Where(item => item.EstablishmentId == establishmentId).ToArray();
        if (direct.Length > 0) return GetPowerAvailabilityFactor(direct);
        if (_economyEstablishmentIndex.TryGetValue(establishmentId, out var establishment) && establishment.BuildingId is { } buildingId)
            return GetBuildingPowerAvailabilityFactor(buildingId);
        return 1d;
    }

    private double GetBuildingPowerAvailabilityFactor(BuildingId buildingId)
    {
        var loads = _powerLoads.Where(item => item.BuildingId == buildingId).ToArray();
        return loads.Length == 0 ? 1d : GetPowerAvailabilityFactor(loads);
    }

    private static double GetPowerAvailabilityFactor(IReadOnlyList<PowerLoadStateData> loads)
    {
        var demand = loads.Sum(static item => item.DemandMegawatts);
        if (demand <= PowerDefaults.SupplyEpsilonMegawatts) return 1d;
        return Math.Clamp(loads.Sum(static item => item.ServedMegawatts) / demand, 0d, 1d);
    }

    private EconomyCheckpoint CreateEconomyCheckpointWithExtensions() =>
        CreateEconomyCheckpointWithLogistics() with { Power = CreatePowerCheckpoint() };

    private PowerCheckpoint CreatePowerCheckpoint() => new(
        _nextPowerNodeId,
        _nextPowerLineId,
        _nextGeneratorId,
        _nextPowerLoadId,
        _powerNodes.OrderBy(static item => item.Id.Value).Select(static item => new SimulationPowerNodeCheckpoint(item.Id, item.Kind, item.Position)).ToArray(),
        _powerLines.OrderBy(static item => item.Id.Value).Select(static item => new SimulationPowerLineCheckpoint(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityMegawatts, item.IsInService)).ToArray(),
        _powerGenerators.OrderBy(static item => item.Id.Value).Select(static item => new SimulationGeneratorCheckpoint(item.Id, item.NodeId, item.CapacityMegawatts, item.OutputMegawatts, item.OperatingState)).ToArray(),
        _powerLoads.OrderBy(static item => item.Id.Value).Select(static item => new SimulationPowerLoadCheckpoint(
            item.Id, item.NodeId, item.BuildingId, item.EstablishmentId, item.BaseDemandMegawatts, item.DemandMegawatts, item.ServedMegawatts, item.UnservedMegawatts, item.SupplyState)).ToArray());

    private void RestorePower(PowerCheckpoint? checkpoint)
    {
        _powerNodes.Clear();
        _powerNodeIndex.Clear();
        _powerLines.Clear();
        _powerLineIndex.Clear();
        _powerGenerators.Clear();
        _powerGeneratorIndex.Clear();
        _powerLoads.Clear();
        _powerLoadIndex.Clear();
        _nextPowerNodeId = 1;
        _nextPowerLineId = 1;
        _nextGeneratorId = 1;
        _nextPowerLoadId = 1;
        if (checkpoint is null) return;

        foreach (var item in checkpoint.Nodes)
        {
            var state = new PowerNodeState(item.Id, item.Kind, item.Position);
            _powerNodes.Add(state);
            _powerNodeIndex.Add(state.Id, state);
        }
        foreach (var item in checkpoint.Lines)
        {
            var state = new PowerLineState(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityMegawatts, item.IsInService);
            _powerLines.Add(state);
            _powerLineIndex.Add(state.Id, state);
        }
        foreach (var item in checkpoint.Generators)
        {
            var state = new GeneratorStateData(item.Id, item.NodeId, item.CapacityMegawatts, item.OperatingState) { OutputMegawatts = item.OutputMegawatts };
            _powerGenerators.Add(state);
            _powerGeneratorIndex.Add(state.Id, state);
        }
        foreach (var item in checkpoint.Loads)
        {
            var state = new PowerLoadStateData(item.Id, item.NodeId, item.BuildingId, item.EstablishmentId, item.BaseDemandMegawatts)
            {
                DemandMegawatts = item.DemandMegawatts,
                ServedMegawatts = item.ServedMegawatts,
                UnservedMegawatts = item.UnservedMegawatts,
                SupplyState = item.SupplyState,
            };
            _powerLoads.Add(state);
            _powerLoadIndex.Add(state.Id, state);
        }
        _nextPowerNodeId = checkpoint.NextNodeId;
        _nextPowerLineId = checkpoint.NextLineId;
        _nextGeneratorId = checkpoint.NextGeneratorId;
        _nextPowerLoadId = checkpoint.NextLoadId;
    }

    private static void ValidatePowerCheckpoint(SimulationCheckpoint checkpoint)
    {
        var power = checkpoint.Economy?.Power;
        if (power is null) return;
        if (power.NextNodeId == 0 || power.NextLineId == 0 || power.NextGeneratorId == 0 || power.NextLoadId == 0)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Power next IDs must be greater than zero.");

        var nodeIds = new HashSet<PowerNodeId>();
        var maxNodeId = 0UL;
        foreach (var item in power.Nodes)
        {
            if (item.Id.Value == 0 || !nodeIds.Add(item.Id) || !Enum.IsDefined(item.Kind))
                throw new ArgumentException("Power contains an invalid or duplicate node.", nameof(checkpoint));
            ValidatePoint(item.Position);
            maxNodeId = Math.Max(maxNodeId, item.Id.Value);
        }
        if (power.NextNodeId <= maxNodeId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Power node ID must exceed stored IDs.");

        var lineIds = new HashSet<PowerLineId>();
        var maxLineId = 0UL;
        foreach (var item in power.Lines)
        {
            if (item.Id.Value == 0 || !lineIds.Add(item.Id) || !nodeIds.Contains(item.FromNodeId) || !nodeIds.Contains(item.ToNodeId)
                || item.FromNodeId == item.ToNodeId || !IsPositiveFinite(item.CapacityMegawatts))
                throw new ArgumentException("Power contains invalid line state.", nameof(checkpoint));
            maxLineId = Math.Max(maxLineId, item.Id.Value);
        }
        if (power.NextLineId <= maxLineId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Power line ID must exceed stored IDs.");

        var generatorIds = new HashSet<GeneratorId>();
        var maxGeneratorId = 0UL;
        foreach (var item in power.Generators)
        {
            if (item.Id.Value == 0 || !generatorIds.Add(item.Id) || !nodeIds.Contains(item.NodeId)
                || !IsPositiveFinite(item.CapacityMegawatts) || !IsNonNegativeFinite(item.OutputMegawatts)
                || item.OutputMegawatts > item.CapacityMegawatts + PowerDefaults.SupplyEpsilonMegawatts || !Enum.IsDefined(item.OperatingState))
                throw new ArgumentException("Power contains invalid Generator state.", nameof(checkpoint));
            maxGeneratorId = Math.Max(maxGeneratorId, item.Id.Value);
        }
        if (power.NextGeneratorId <= maxGeneratorId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Generator ID must exceed stored IDs.");

        var buildingIds = (checkpoint.Buildings ?? []).Select(static item => item.Id).ToHashSet();
        var establishmentIds = (checkpoint.Economy?.Establishments ?? []).Select(static item => item.Id).ToHashSet();
        var loadIds = new HashSet<PowerLoadId>();
        var maxLoadId = 0UL;
        foreach (var item in power.Loads)
        {
            if (item.Id.Value == 0 || !loadIds.Add(item.Id) || !nodeIds.Contains(item.NodeId)
                || (item.BuildingId is null && item.EstablishmentId is null)
                || (item.BuildingId is { } buildingId && !buildingIds.Contains(buildingId))
                || (item.EstablishmentId is { } establishmentId && !establishmentIds.Contains(establishmentId))
                || !IsPositiveFinite(item.BaseDemandMegawatts) || !IsNonNegativeFinite(item.DemandMegawatts)
                || !IsNonNegativeFinite(item.ServedMegawatts) || !IsNonNegativeFinite(item.UnservedMegawatts)
                || item.ServedMegawatts > item.DemandMegawatts + PowerDefaults.SupplyEpsilonMegawatts
                || Math.Abs((item.ServedMegawatts + item.UnservedMegawatts) - item.DemandMegawatts) > 1e-6
                || !Enum.IsDefined(item.SupplyState))
                throw new ArgumentException("Power contains invalid Load state.", nameof(checkpoint));
            maxLoadId = Math.Max(maxLoadId, item.Id.Value);
        }
        if (power.NextLoadId <= maxLoadId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Power load ID must exceed stored IDs.");
    }

    private static GeneratorSnapshot CreateGeneratorSnapshot(GeneratorStateData state) =>
        new(state.Id, state.NodeId, state.CapacityMegawatts, state.OutputMegawatts, state.OperatingState);

    private static PowerLoadSnapshot CreatePowerLoadSnapshot(PowerLoadStateData state) =>
        new(state.Id, state.NodeId, state.BuildingId, state.EstablishmentId, state.BaseDemandMegawatts, state.DemandMegawatts, state.ServedMegawatts, state.UnservedMegawatts, state.SupplyState);

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!IsPositiveFinite(value)) throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and greater than zero.");
    }
    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0d;
    private static bool IsNonNegativeFinite(double value) => double.IsFinite(value) && value >= 0d;
    private static void ValidatePowerEnum<T>(T value, string parameterName) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(parameterName, value, "Power enum value is not defined.");
    }
    private static void EnsurePowerIdCapacity(ulong nextId, string name)
    {
        if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted.");
    }

    private sealed class PowerNodeState(PowerNodeId id, PowerNodeKind kind, WorldPoint position)
    {
        public PowerNodeId Id { get; } = id;
        public PowerNodeKind Kind { get; } = kind;
        public WorldPoint Position { get; } = position;
    }

    private sealed class PowerLineState(PowerLineId id, PowerNodeId fromNodeId, PowerNodeId toNodeId, double capacityMegawatts, bool isInService)
    {
        public PowerLineId Id { get; } = id;
        public PowerNodeId FromNodeId { get; } = fromNodeId;
        public PowerNodeId ToNodeId { get; } = toNodeId;
        public double CapacityMegawatts { get; } = capacityMegawatts;
        public bool IsInService { get; set; } = isInService;
    }

    private sealed class GeneratorStateData(GeneratorId id, PowerNodeId nodeId, double capacityMegawatts, GeneratorOperatingState operatingState)
    {
        public GeneratorId Id { get; } = id;
        public PowerNodeId NodeId { get; } = nodeId;
        public double CapacityMegawatts { get; } = capacityMegawatts;
        public double OutputMegawatts { get; set; }
        public GeneratorOperatingState OperatingState { get; set; } = operatingState;
    }

    private sealed class PowerLoadStateData(PowerLoadId id, PowerNodeId nodeId, BuildingId? buildingId, EstablishmentId? establishmentId, double baseDemandMegawatts)
    {
        public PowerLoadId Id { get; } = id;
        public PowerNodeId NodeId { get; } = nodeId;
        public BuildingId? BuildingId { get; } = buildingId;
        public EstablishmentId? EstablishmentId { get; } = establishmentId;
        public double BaseDemandMegawatts { get; } = baseDemandMegawatts;
        public double DemandMegawatts { get; set; }
        public double ServedMegawatts { get; set; }
        public double UnservedMegawatts { get; set; }
        public PowerSupplyState SupplyState { get; set; } = PowerSupplyState.Supplied;
    }
}
