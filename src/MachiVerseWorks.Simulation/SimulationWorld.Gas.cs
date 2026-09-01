namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly List<GasNodeState> _gasNodes = [];
    private readonly Dictionary<GasNodeId, GasNodeState> _gasNodeIndex = [];
    private readonly List<GasPipelineState> _gasPipelines = [];
    private readonly Dictionary<GasPipelineId, GasPipelineState> _gasPipelineIndex = [];
    private readonly List<GasSourceStateData> _gasSources = [];
    private readonly Dictionary<GasSourceId, GasSourceStateData> _gasSourceIndex = [];
    private readonly List<GasImportTerminalStateData> _gasImportTerminals = [];
    private readonly Dictionary<GasImportTerminalId, GasImportTerminalStateData> _gasImportTerminalIndex = [];
    private readonly List<GasStorageStateData> _gasStorages = [];
    private readonly Dictionary<GasStorageId, GasStorageStateData> _gasStorageIndex = [];
    private readonly List<GasServicePointStateData> _gasServicePoints = [];
    private readonly Dictionary<GasServicePointId, GasServicePointStateData> _gasServicePointIndex = [];
    private readonly IGasSupplySolver _gasSupplySolver;
    private ulong _nextGasNodeId = 1;
    private ulong _nextGasPipelineId = 1;
    private ulong _nextGasSourceId = 1;
    private ulong _nextGasImportTerminalId = 1;
    private ulong _nextGasStorageId = 1;
    private ulong _nextGasServicePointId = 1;

    public int GasNodeCount => _gasNodes.Count;
    public int GasPipelineCount => _gasPipelines.Count;
    public int GasServicePointCount => _gasServicePoints.Count;

    public GasNodeId CreateGasNode(WorldPoint position, GasNodeKind kind = GasNodeKind.Distribution)
    {
        ValidatePoint(position);
        ValidateGasEnum(kind, nameof(kind));
        EnsureGasIdCapacity(_nextGasNodeId, "Gas node");
        var id = new GasNodeId(_nextGasNodeId++);
        var state = new GasNodeState(id, kind, position);
        _gasNodes.Add(state);
        _gasNodeIndex.Add(id, state);
        return id;
    }

    public GasPipelineId CreateGasPipeline(GasNodeId fromNodeId, GasNodeId toNodeId, double capacityCubicMetersPerDay, bool isInService = true)
    {
        if (!_gasNodeIndex.ContainsKey(fromNodeId) || !_gasNodeIndex.ContainsKey(toNodeId) || fromNodeId == toNodeId)
            throw new ArgumentException("Gas pipeline references invalid nodes.");
        ValidateGasPositiveFinite(capacityCubicMetersPerDay, nameof(capacityCubicMetersPerDay));
        EnsureGasIdCapacity(_nextGasPipelineId, "Gas pipeline");
        var id = new GasPipelineId(_nextGasPipelineId++);
        var state = new GasPipelineState(id, fromNodeId, toNodeId, capacityCubicMetersPerDay, isInService);
        _gasPipelines.Add(state);
        _gasPipelineIndex.Add(id, state);
        return id;
    }

    public GasSourceId CreateGasSource(GasNodeId nodeId, double capacityCubicMetersPerDay, GasOperatingState operatingState = GasOperatingState.Online)
    {
        ValidateGasFacility(nodeId, capacityCubicMetersPerDay, operatingState);
        EnsureGasIdCapacity(_nextGasSourceId, "Gas source");
        var id = new GasSourceId(_nextGasSourceId++);
        var state = new GasSourceStateData(id, nodeId, capacityCubicMetersPerDay, operatingState);
        _gasSources.Add(state);
        _gasSourceIndex.Add(id, state);
        return id;
    }

    public GasImportTerminalId CreateGasImportTerminal(GasNodeId nodeId, double capacityCubicMetersPerDay, GasOperatingState operatingState = GasOperatingState.Online)
    {
        ValidateGasFacility(nodeId, capacityCubicMetersPerDay, operatingState);
        EnsureGasIdCapacity(_nextGasImportTerminalId, "Gas import terminal");
        var id = new GasImportTerminalId(_nextGasImportTerminalId++);
        var state = new GasImportTerminalStateData(id, nodeId, capacityCubicMetersPerDay, operatingState);
        _gasImportTerminals.Add(state);
        _gasImportTerminalIndex.Add(id, state);
        return id;
    }

    public GasStorageId CreateGasStorage(
        GasNodeId nodeId,
        double capacityCubicMeters,
        double initialStoredCubicMeters,
        double releaseCapacityCubicMetersPerDay,
        GasOperatingState operatingState = GasOperatingState.Online)
    {
        if (!_gasNodeIndex.ContainsKey(nodeId)) throw new ArgumentException($"Gas node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidateGasPositiveFinite(capacityCubicMeters, nameof(capacityCubicMeters));
        ValidateGasNonNegativeFinite(initialStoredCubicMeters, nameof(initialStoredCubicMeters));
        ValidateGasPositiveFinite(releaseCapacityCubicMetersPerDay, nameof(releaseCapacityCubicMetersPerDay));
        if (initialStoredCubicMeters > capacityCubicMeters) throw new ArgumentOutOfRangeException(nameof(initialStoredCubicMeters));
        ValidateGasEnum(operatingState, nameof(operatingState));
        EnsureGasIdCapacity(_nextGasStorageId, "Gas storage");
        var id = new GasStorageId(_nextGasStorageId++);
        var state = new GasStorageStateData(id, nodeId, capacityCubicMeters, initialStoredCubicMeters, releaseCapacityCubicMetersPerDay, operatingState);
        _gasStorages.Add(state);
        _gasStorageIndex.Add(id, state);
        return id;
    }

    public GasServicePointId CreatePipedGasServicePoint(
        GasNodeId nodeId,
        double baseDemandCubicMetersPerDay,
        BuildingId? buildingId = null,
        EstablishmentId? establishmentId = null)
    {
        if (!_gasNodeIndex.ContainsKey(nodeId)) throw new ArgumentException($"Gas node {nodeId.Value} does not exist.", nameof(nodeId));
        return CreateGasServicePointCore(nodeId, buildingId, establishmentId, GasDeliveryMode.Piped, null, baseDemandCubicMetersPerDay);
    }

    public GasServicePointId CreateDeliveredGasServicePoint(
        EstablishmentId establishmentId,
        CommodityId commodityId,
        double baseDemandCubicMetersPerDay,
        BuildingId? buildingId = null)
    {
        if (!_logisticsCommodityIndex.TryGetValue(commodityId, out var commodity) || commodity.Kind != CommodityKind.Gas)
            throw new ArgumentException($"Commodity {commodityId.Value} does not exist or is not Gas.", nameof(commodityId));
        if (!_logisticsInventories.TryGetValue((establishmentId, commodityId), out var inventory) || inventory.Role != InventoryRole.Consumer)
            throw new ArgumentException("Delivered Gas requires a consumer Logistics inventory for the Establishment and Gas commodity.", nameof(commodityId));
        return CreateGasServicePointCore(null, buildingId, establishmentId, GasDeliveryMode.Delivered, commodityId, baseDemandCubicMetersPerDay);
    }

    public void SetGasPipelineInService(GasPipelineId id, bool isInService)
    {
        if (!_gasPipelineIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Gas pipeline {id.Value} does not exist.", nameof(id));
        item.IsInService = isInService;
    }

    public void SetGasSourceOperatingState(GasSourceId id, GasOperatingState state)
    {
        ValidateGasEnum(state, nameof(state));
        if (!_gasSourceIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Gas source {id.Value} does not exist.", nameof(id));
        item.OperatingState = state;
    }

    public void SetGasImportTerminalOperatingState(GasImportTerminalId id, GasOperatingState state)
    {
        ValidateGasEnum(state, nameof(state));
        if (!_gasImportTerminalIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Gas import terminal {id.Value} does not exist.", nameof(id));
        item.OperatingState = state;
    }

    public void SetGasStorageOperatingState(GasStorageId id, GasOperatingState state)
    {
        ValidateGasEnum(state, nameof(state));
        if (!_gasStorageIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Gas storage {id.Value} does not exist.", nameof(id));
        item.OperatingState = state;
    }

    public bool TryGetGasServicePointSnapshot(GasServicePointId id, out GasServicePointSnapshot snapshot)
    {
        if (_gasServicePointIndex.TryGetValue(id, out var item))
        {
            snapshot = CreateGasServicePointSnapshot(item);
            return true;
        }
        snapshot = default;
        return false;
    }

    public GasNodeSnapshot[] QueryGasNodes(WorldVolume volume) =>
        _gasNodes
            .Where(item => volume.Contains(item.Position))
            .OrderBy(static item => item.Id.Value)
            .Select(static item => new GasNodeSnapshot(item.Id, item.Kind, item.Position))
            .ToArray();

    public GasSnapshot CreateGasSnapshot() => new(
        CreateGasStatistics(),
        _gasNodes.OrderBy(static item => item.Id.Value).Select(static item => new GasNodeSnapshot(item.Id, item.Kind, item.Position)).ToArray(),
        _gasPipelines.OrderBy(static item => item.Id.Value).Select(static item => new GasPipelineSnapshot(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
        _gasSources.OrderBy(static item => item.Id.Value).Select(static item => new GasSourceSnapshot(item.Id, item.NodeId, item.CapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _gasImportTerminals.OrderBy(static item => item.Id.Value).Select(static item => new GasImportTerminalSnapshot(item.Id, item.NodeId, item.CapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _gasStorages.OrderBy(static item => item.Id.Value).Select(static item => new GasStorageSnapshot(item.Id, item.NodeId, item.CapacityCubicMeters, item.StoredCubicMeters, item.ReleaseCapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _gasServicePoints.OrderBy(static item => item.Id.Value).Select(CreateGasServicePointSnapshot).ToArray());

    public GasStatistics CreateGasStatistics() => new(
        _gasNodes.Count,
        _gasPipelines.Count,
        _gasSources.Count,
        _gasImportTerminals.Count,
        _gasStorages.Count,
        _gasServicePoints.Count,
        _gasServicePoints.Count(static item => item.DeliveryMode == GasDeliveryMode.Piped),
        _gasServicePoints.Count(static item => item.DeliveryMode == GasDeliveryMode.Delivered),
        _gasServicePoints.Count(static item => item.ServiceState == GasServiceState.Unavailable),
        _gasSources.Where(static item => item.OperatingState == GasOperatingState.Online).Sum(static item => item.CapacityCubicMetersPerDay)
            + _gasImportTerminals.Where(static item => item.OperatingState == GasOperatingState.Online).Sum(static item => item.CapacityCubicMetersPerDay)
            + _gasStorages.Where(static item => item.OperatingState == GasOperatingState.Online).Sum(static item => Math.Min(item.ReleaseCapacityCubicMetersPerDay, item.StoredCubicMeters * EconomyDefaults.TicksPerEconomicDay)),
        _gasServicePoints.Sum(static item => item.DemandCubicMetersPerDay),
        _gasServicePoints.Sum(static item => item.ServedCubicMetersPerDay),
        _gasServicePoints.Sum(static item => item.UnservedCubicMetersPerDay),
        _gasStorages.Sum(static item => item.StoredCubicMeters),
        Time.TickCount);

    private void StepGas(SimulationTime nextTime)
    {
        var demandContext = CreateGasDemandContext();
        foreach (var point in _gasServicePoints)
            point.DemandCubicMetersPerDay = CalculateGasDemand(point, nextTime, demandContext);

        var request = new GasSupplyRequest(
            _gasNodes.Select(static item => new GasSupplyNode(item.Id)).ToArray(),
            _gasPipelines.Select(static item => new GasSupplyPipeline(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
            _gasSources.Select(static item => new GasSupplySource(item.Id, item.NodeId, item.OperatingState == GasOperatingState.Online ? item.CapacityCubicMetersPerDay : 0d)).ToArray(),
            _gasImportTerminals.Select(static item => new GasSupplyImportTerminal(item.Id, item.NodeId, item.OperatingState == GasOperatingState.Online ? item.CapacityCubicMetersPerDay : 0d)).ToArray(),
            _gasStorages.Select(static item => new GasSupplyStorage(item.Id, item.NodeId,
                item.OperatingState == GasOperatingState.Online ? Math.Min(item.ReleaseCapacityCubicMetersPerDay, item.StoredCubicMeters * EconomyDefaults.TicksPerEconomicDay) : 0d)).ToArray(),
            _gasServicePoints.Where(static item => item.DeliveryMode == GasDeliveryMode.Piped && item.NodeId is not null)
                .Select(static item => new GasSupplyLoad(item.Id, item.NodeId!.Value, item.DemandCubicMetersPerDay)).ToArray());
        var result = _gasSupplySolver.Solve(request);

        foreach (var item in _gasSources) item.OutputCubicMetersPerDay = 0d;
        foreach (var item in _gasImportTerminals) item.OutputCubicMetersPerDay = 0d;
        foreach (var item in _gasStorages) item.OutputCubicMetersPerDay = 0d;
        foreach (var dispatch in result.Sources)
            if (_gasSourceIndex.TryGetValue(dispatch.Id, out var item)) item.OutputCubicMetersPerDay = Math.Max(0d, dispatch.OutputCubicMetersPerDay);
        foreach (var dispatch in result.ImportTerminals)
            if (_gasImportTerminalIndex.TryGetValue(dispatch.Id, out var item)) item.OutputCubicMetersPerDay = Math.Max(0d, dispatch.OutputCubicMetersPerDay);
        foreach (var dispatch in result.Storages)
            if (_gasStorageIndex.TryGetValue(dispatch.Id, out var item)) item.OutputCubicMetersPerDay = Math.Max(0d, dispatch.OutputCubicMetersPerDay);
        foreach (var storage in _gasStorages)
            storage.StoredCubicMeters = Math.Max(0d, storage.StoredCubicMeters - (storage.OutputCubicMetersPerDay / EconomyDefaults.TicksPerEconomicDay));

        var pipedDispatch = result.Loads.ToDictionary(static item => item.Id, static item => item.ServedCubicMetersPerDay);
        foreach (var point in _gasServicePoints)
        {
            var served = point.DeliveryMode == GasDeliveryMode.Piped
                ? pipedDispatch.GetValueOrDefault(point.Id)
                : GetDeliveredGasAvailability(point);
            ApplyGasServiceResult(point, served);
        }
    }

    private GasDemandContext CreateGasDemandContext()
    {
        var residentsByBuilding = new Dictionary<BuildingId, int>();
        for (var index = 0; index < _population.PersonCount; index++)
        {
            if (_population.GetPersonAt(index).Residence.BuildingId is not { } buildingId) continue;
            residentsByBuilding[buildingId] = residentsByBuilding.GetValueOrDefault(buildingId) + 1;
        }

        var requiredWorkersByEstablishment = new Dictionary<EstablishmentId, int>();
        foreach (var job in _economyJobs)
            requiredWorkersByEstablishment[job.EstablishmentId] = checked(requiredWorkersByEstablishment.GetValueOrDefault(job.EstablishmentId) + job.RequiredWorkerCount);

        var filledWorkersByEstablishment = new Dictionary<EstablishmentId, int>();
        foreach (var employment in _economyEmployments.Values)
        {
            if (!_economyJobIndex.TryGetValue(employment.JobId, out var job)) continue;
            filledWorkersByEstablishment[job.EstablishmentId] = filledWorkersByEstablishment.GetValueOrDefault(job.EstablishmentId) + 1;
        }
        return new GasDemandContext(residentsByBuilding, requiredWorkersByEstablishment, filledWorkersByEstablishment);
    }

    private double CalculateGasDemand(GasServicePointStateData point, SimulationTime time, GasDemandContext context)
    {
        var hour = time.Elapsed.TotalHours % 24d;
        if (hour < 0d) hour += 24d;
        var timeFactor = hour switch
        {
            < 6d => 0.8d,
            < 9d => 1.2d,
            < 17d => 0.9d,
            < 22d => 1.25d,
            _ => 0.9d,
        };

        var useFactor = 1d;
        if (point.BuildingId is { } buildingId && TryGetBuildingSnapshot(buildingId, out var building))
        {
            useFactor *= building.Kind switch
            {
                BuildingKind.Residential => 1.1d,
                BuildingKind.Commercial => 0.9d,
                BuildingKind.Industrial => 1.45d,
                BuildingKind.Civic => 1.05d,
                BuildingKind.MixedUse => 1.15d,
                _ => 1d,
            };
            var residents = context.ResidentsByBuilding.GetValueOrDefault(buildingId);
            if (residents > 0) useFactor *= Math.Min(4d, 1d + (residents * 0.15d));
        }

        if (point.EstablishmentId is { } establishmentId
            && _economyEstablishmentIndex.TryGetValue(establishmentId, out var establishment)
            && _economyCompanyIndex.TryGetValue(establishment.CompanyId, out var company))
        {
            useFactor *= company.Sector switch
            {
                IndustrySector.Manufacturing => 1.5d,
                IndustrySector.Retail => 1.05d,
                IndustrySector.Services => 0.85d,
                IndustrySector.Transport => 1.1d,
                IndustrySector.Public => 1.05d,
                _ => 1d,
            };
            var required = context.RequiredWorkersByEstablishment.GetValueOrDefault(establishmentId);
            if (required > 0)
            {
                var filled = context.FilledWorkersByEstablishment.GetValueOrDefault(establishmentId);
                useFactor *= 0.65d + (0.35d * Math.Min(1d, (double)filled / required));
            }
        }
        return point.BaseDemandCubicMetersPerDay * timeFactor * useFactor;
    }

    private double GetDeliveredGasAvailability(GasServicePointStateData point)
    {
        if (point.EstablishmentId is not { } establishmentId || point.CommodityId is not { } commodityId) return 0d;
        if (!_logisticsInventories.TryGetValue((establishmentId, commodityId), out var inventory)) return 0d;
        return Math.Min(point.DemandCubicMetersPerDay, Math.Max(0d, inventory.Quantity));
    }

    private static void ApplyGasServiceResult(GasServicePointStateData point, double served)
    {
        point.ServedCubicMetersPerDay = Math.Clamp(served, 0d, point.DemandCubicMetersPerDay);
        point.UnservedCubicMetersPerDay = Math.Max(0d, point.DemandCubicMetersPerDay - point.ServedCubicMetersPerDay);
        point.ServiceState = point.ServedCubicMetersPerDay <= GasDefaults.FlowEpsilonCubicMetersPerDay
            ? GasServiceState.Unavailable
            : point.UnservedCubicMetersPerDay <= GasDefaults.FlowEpsilonCubicMetersPerDay
                ? GasServiceState.Supplied
                : GasServiceState.Constrained;
    }

    private double GetEstablishmentGasAvailabilityFactor(EstablishmentId establishmentId)
    {
        var points = _gasServicePoints.Where(item => item.EstablishmentId == establishmentId).ToArray();
        if (points.Length == 0 && _economyEstablishmentIndex.TryGetValue(establishmentId, out var establishment) && establishment.BuildingId is { } buildingId)
            points = _gasServicePoints.Where(item => item.BuildingId == buildingId).ToArray();
        if (points.Length == 0) return 1d;
        var demand = points.Sum(static item => item.DemandCubicMetersPerDay);
        return demand <= GasDefaults.FlowEpsilonCubicMetersPerDay ? 1d : Math.Clamp(points.Sum(static item => item.ServedCubicMetersPerDay) / demand, 0d, 1d);
    }

    private GasServicePointId CreateGasServicePointCore(
        GasNodeId? nodeId,
        BuildingId? buildingId,
        EstablishmentId? establishmentId,
        GasDeliveryMode deliveryMode,
        CommodityId? commodityId,
        double baseDemandCubicMetersPerDay)
    {
        ValidateGasPositiveFinite(baseDemandCubicMetersPerDay, nameof(baseDemandCubicMetersPerDay));
        ValidateGasEnum(deliveryMode, nameof(deliveryMode));
        if (buildingId is null && establishmentId is null)
            throw new ArgumentException("A Gas service point must reference a Building, an Establishment, or both.", nameof(buildingId));
        ValidateGasConsumerReferences(ref buildingId, establishmentId);
        EnsureGasIdCapacity(_nextGasServicePointId, "Gas service point");
        var id = new GasServicePointId(_nextGasServicePointId++);
        var state = new GasServicePointStateData(id, nodeId, buildingId, establishmentId, deliveryMode, commodityId, baseDemandCubicMetersPerDay);
        _gasServicePoints.Add(state);
        _gasServicePointIndex.Add(id, state);
        return id;
    }

    private void ValidateGasFacility(GasNodeId nodeId, double capacity, GasOperatingState operatingState)
    {
        if (!_gasNodeIndex.ContainsKey(nodeId)) throw new ArgumentException($"Gas node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidateGasPositiveFinite(capacity, nameof(capacity));
        ValidateGasEnum(operatingState, nameof(operatingState));
    }

    private void ValidateGasConsumerReferences(ref BuildingId? buildingId, EstablishmentId? establishmentId)
    {
        if (buildingId is { } explicitBuilding && !TryGetBuildingSnapshot(explicitBuilding, out _))
            throw new ArgumentException($"Building {explicitBuilding.Value} does not exist.", nameof(buildingId));
        if (establishmentId is not { } establishmentValue) return;
        if (!_economyEstablishmentIndex.TryGetValue(establishmentValue, out var establishment))
            throw new ArgumentException($"Establishment {establishmentValue.Value} does not exist.", nameof(establishmentId));
        if (buildingId is { } buildingValue && establishment.BuildingId is { } establishmentBuilding && buildingValue != establishmentBuilding)
            throw new ArgumentException("Establishment belongs to a different Building.", nameof(buildingId));
        buildingId ??= establishment.BuildingId;
    }

    private EconomyCheckpoint CreateEconomyCheckpointWithGas() =>
        CreateEconomyCheckpointWithWaterSewer() with { Gas = CreateGasCheckpoint() };

    private GasCheckpoint CreateGasCheckpoint() => new(
        _nextGasNodeId,
        _nextGasPipelineId,
        _nextGasSourceId,
        _nextGasImportTerminalId,
        _nextGasStorageId,
        _nextGasServicePointId,
        _gasNodes.OrderBy(static item => item.Id.Value).Select(static item => new SimulationGasNodeCheckpoint(item.Id, item.Kind, item.Position)).ToArray(),
        _gasPipelines.OrderBy(static item => item.Id.Value).Select(static item => new SimulationGasPipelineCheckpoint(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
        _gasSources.OrderBy(static item => item.Id.Value).Select(static item => new SimulationGasSourceCheckpoint(item.Id, item.NodeId, item.CapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _gasImportTerminals.OrderBy(static item => item.Id.Value).Select(static item => new SimulationGasImportTerminalCheckpoint(item.Id, item.NodeId, item.CapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _gasStorages.OrderBy(static item => item.Id.Value).Select(static item => new SimulationGasStorageCheckpoint(item.Id, item.NodeId, item.CapacityCubicMeters, item.StoredCubicMeters, item.ReleaseCapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _gasServicePoints.OrderBy(static item => item.Id.Value).Select(static item => new SimulationGasServicePointCheckpoint(item.Id, item.NodeId, item.BuildingId, item.EstablishmentId, item.DeliveryMode, item.CommodityId, item.BaseDemandCubicMetersPerDay, item.DemandCubicMetersPerDay, item.ServedCubicMetersPerDay, item.UnservedCubicMetersPerDay, item.ServiceState)).ToArray());

    private void RestoreGas(GasCheckpoint? checkpoint)
    {
        _gasNodes.Clear(); _gasNodeIndex.Clear(); _gasPipelines.Clear(); _gasPipelineIndex.Clear();
        _gasSources.Clear(); _gasSourceIndex.Clear(); _gasImportTerminals.Clear(); _gasImportTerminalIndex.Clear();
        _gasStorages.Clear(); _gasStorageIndex.Clear(); _gasServicePoints.Clear(); _gasServicePointIndex.Clear();
        _nextGasNodeId = _nextGasPipelineId = _nextGasSourceId = _nextGasImportTerminalId = _nextGasStorageId = _nextGasServicePointId = 1;
        if (checkpoint is null) return;
        foreach (var item in checkpoint.Nodes) { var state = new GasNodeState(item.Id, item.Kind, item.Position); _gasNodes.Add(state); _gasNodeIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.Pipelines) { var state = new GasPipelineState(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService); _gasPipelines.Add(state); _gasPipelineIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.Sources) { var state = new GasSourceStateData(item.Id, item.NodeId, item.CapacityCubicMetersPerDay, item.OperatingState) { OutputCubicMetersPerDay = item.OutputCubicMetersPerDay }; _gasSources.Add(state); _gasSourceIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.ImportTerminals) { var state = new GasImportTerminalStateData(item.Id, item.NodeId, item.CapacityCubicMetersPerDay, item.OperatingState) { OutputCubicMetersPerDay = item.OutputCubicMetersPerDay }; _gasImportTerminals.Add(state); _gasImportTerminalIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.Storages) { var state = new GasStorageStateData(item.Id, item.NodeId, item.CapacityCubicMeters, item.StoredCubicMeters, item.ReleaseCapacityCubicMetersPerDay, item.OperatingState) { OutputCubicMetersPerDay = item.OutputCubicMetersPerDay }; _gasStorages.Add(state); _gasStorageIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.ServicePoints) { var state = new GasServicePointStateData(item.Id, item.NodeId, item.BuildingId, item.EstablishmentId, item.DeliveryMode, item.CommodityId, item.BaseDemandCubicMetersPerDay) { DemandCubicMetersPerDay = item.DemandCubicMetersPerDay, ServedCubicMetersPerDay = item.ServedCubicMetersPerDay, UnservedCubicMetersPerDay = item.UnservedCubicMetersPerDay, ServiceState = item.ServiceState }; _gasServicePoints.Add(state); _gasServicePointIndex.Add(state.Id, state); }
        _nextGasNodeId = checkpoint.NextNodeId; _nextGasPipelineId = checkpoint.NextPipelineId; _nextGasSourceId = checkpoint.NextSourceId;
        _nextGasImportTerminalId = checkpoint.NextImportTerminalId; _nextGasStorageId = checkpoint.NextStorageId; _nextGasServicePointId = checkpoint.NextServicePointId;
    }

    private static void ValidateGasCheckpoint(SimulationCheckpoint checkpoint)
    {
        var gas = checkpoint.Economy?.Gas;
        if (gas is null) return;
        if (gas.NextNodeId == 0 || gas.NextPipelineId == 0 || gas.NextSourceId == 0 || gas.NextImportTerminalId == 0 || gas.NextStorageId == 0 || gas.NextServicePointId == 0)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Gas next IDs must be greater than zero.");
        var nodes = new HashSet<GasNodeId>();
        var maximumNode = 0UL;
        foreach (var item in gas.Nodes)
        {
            if (item.Id.Value == 0 || !nodes.Add(item.Id) || !Enum.IsDefined(item.Kind)) throw new ArgumentException("Gas contains invalid nodes.", nameof(checkpoint));
            ValidatePoint(item.Position); maximumNode = Math.Max(maximumNode, item.Id.Value);
        }
        if (gas.NextNodeId <= maximumNode) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Gas node ID must exceed stored IDs.");
        ValidateGasCheckpointIds(gas.Pipelines.Select(static item => item.Id.Value), gas.NextPipelineId, "pipeline");
        foreach (var item in gas.Pipelines)
            if (!nodes.Contains(item.FromNodeId) || !nodes.Contains(item.ToNodeId) || item.FromNodeId == item.ToNodeId || !IsPositiveFinite(item.CapacityCubicMetersPerDay)) throw new ArgumentException("Gas contains invalid pipelines.", nameof(checkpoint));
        ValidateGasCheckpointIds(gas.Sources.Select(static item => item.Id.Value), gas.NextSourceId, "source");
        foreach (var item in gas.Sources)
            if (!nodes.Contains(item.NodeId) || !IsPositiveFinite(item.CapacityCubicMetersPerDay) || !IsNonNegativeFinite(item.OutputCubicMetersPerDay) || item.OutputCubicMetersPerDay > item.CapacityCubicMetersPerDay + GasDefaults.FlowEpsilonCubicMetersPerDay || !Enum.IsDefined(item.OperatingState)) throw new ArgumentException("Gas contains invalid sources.", nameof(checkpoint));
        ValidateGasCheckpointIds(gas.ImportTerminals.Select(static item => item.Id.Value), gas.NextImportTerminalId, "import terminal");
        foreach (var item in gas.ImportTerminals)
            if (!nodes.Contains(item.NodeId) || !IsPositiveFinite(item.CapacityCubicMetersPerDay) || !IsNonNegativeFinite(item.OutputCubicMetersPerDay) || item.OutputCubicMetersPerDay > item.CapacityCubicMetersPerDay + GasDefaults.FlowEpsilonCubicMetersPerDay || !Enum.IsDefined(item.OperatingState)) throw new ArgumentException("Gas contains invalid import terminals.", nameof(checkpoint));
        ValidateGasCheckpointIds(gas.Storages.Select(static item => item.Id.Value), gas.NextStorageId, "storage");
        foreach (var item in gas.Storages)
            if (!nodes.Contains(item.NodeId) || !IsPositiveFinite(item.CapacityCubicMeters) || !IsNonNegativeFinite(item.StoredCubicMeters) || item.StoredCubicMeters > item.CapacityCubicMeters || !IsPositiveFinite(item.ReleaseCapacityCubicMetersPerDay) || !IsNonNegativeFinite(item.OutputCubicMetersPerDay) || !Enum.IsDefined(item.OperatingState)) throw new ArgumentException("Gas contains invalid storages.", nameof(checkpoint));
        ValidateGasCheckpointIds(gas.ServicePoints.Select(static item => item.Id.Value), gas.NextServicePointId, "service point");
        var buildings = checkpoint.Buildings.Select(static item => item.Id).ToHashSet();
        var establishments = (checkpoint.Economy?.Establishments ?? []).Select(static item => item.Id).ToHashSet();
        var gasCommodities = (checkpoint.Economy?.Logistics?.Commodities ?? []).Where(static item => item.Kind == CommodityKind.Gas).Select(static item => item.Id).ToHashSet();
        foreach (var item in gas.ServicePoints)
        {
            var invalidMode = item.DeliveryMode switch
            {
                GasDeliveryMode.Piped => item.NodeId is null || !nodes.Contains(item.NodeId.Value) || item.CommodityId is not null,
                GasDeliveryMode.Delivered => item.NodeId is not null || item.EstablishmentId is null || item.CommodityId is null || !gasCommodities.Contains(item.CommodityId.Value),
                _ => true,
            };
            if (invalidMode || (item.BuildingId is null && item.EstablishmentId is null)
                || (item.BuildingId is { } buildingId && !buildings.Contains(buildingId))
                || (item.EstablishmentId is { } establishmentId && !establishments.Contains(establishmentId))
                || !IsPositiveFinite(item.BaseDemandCubicMetersPerDay) || !IsNonNegativeFinite(item.DemandCubicMetersPerDay)
                || !IsNonNegativeFinite(item.ServedCubicMetersPerDay) || !IsNonNegativeFinite(item.UnservedCubicMetersPerDay)
                || item.ServedCubicMetersPerDay > item.DemandCubicMetersPerDay + GasDefaults.FlowEpsilonCubicMetersPerDay
                || !Enum.IsDefined(item.ServiceState)) throw new ArgumentException("Gas contains invalid service points.", nameof(checkpoint));
        }
    }

    private static void ValidateGasCheckpointIds(IEnumerable<ulong> ids, ulong nextId, string name)
    {
        var seen = new HashSet<ulong>(); var maximum = 0UL;
        foreach (var id in ids) { if (id == 0 || !seen.Add(id)) throw new ArgumentException($"Gas {name} IDs are invalid or duplicated."); maximum = Math.Max(maximum, id); }
        if (nextId <= maximum) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next Gas {name} ID must exceed stored IDs.");
    }

    private static GasServicePointSnapshot CreateGasServicePointSnapshot(GasServicePointStateData item) => new(
        item.Id, item.NodeId, item.BuildingId, item.EstablishmentId, item.DeliveryMode, item.CommodityId,
        item.BaseDemandCubicMetersPerDay, item.DemandCubicMetersPerDay, item.ServedCubicMetersPerDay, item.UnservedCubicMetersPerDay, item.ServiceState);

    private static void ValidateGasPositiveFinite(double value, string name) { if (!IsPositiveFinite(value)) throw new ArgumentOutOfRangeException(name, value, "Value must be finite and greater than zero."); }
    private static void ValidateGasNonNegativeFinite(double value, string name) { if (!IsNonNegativeFinite(value)) throw new ArgumentOutOfRangeException(name, value, "Value must be finite and non-negative."); }
    private static void ValidateGasEnum<T>(T value, string name) where T : struct, Enum { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(name, value, "Gas enum value is not defined."); }
    private static void EnsureGasIdCapacity(ulong nextId, string name) { if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted."); }

    private readonly record struct GasDemandContext(
        IReadOnlyDictionary<BuildingId, int> ResidentsByBuilding,
        IReadOnlyDictionary<EstablishmentId, int> RequiredWorkersByEstablishment,
        IReadOnlyDictionary<EstablishmentId, int> FilledWorkersByEstablishment);
}
