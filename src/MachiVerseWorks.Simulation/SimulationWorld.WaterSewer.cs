namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly List<WaterNodeState> _waterNodes = [];
    private readonly Dictionary<WaterNodeId, WaterNodeState> _waterNodeIndex = [];
    private readonly List<WaterPipeState> _waterPipes = [];
    private readonly Dictionary<WaterPipeId, WaterPipeState> _waterPipeIndex = [];
    private readonly List<SewerNodeState> _sewerNodes = [];
    private readonly Dictionary<SewerNodeId, SewerNodeState> _sewerNodeIndex = [];
    private readonly List<SewerPipeState> _sewerPipes = [];
    private readonly Dictionary<SewerPipeId, SewerPipeState> _sewerPipeIndex = [];
    private readonly List<WaterSourceStateData> _waterSources = [];
    private readonly Dictionary<WaterSourceId, WaterSourceStateData> _waterSourceIndex = [];
    private readonly List<ReservoirStateData> _reservoirs = [];
    private readonly Dictionary<ReservoirId, ReservoirStateData> _reservoirIndex = [];
    private readonly List<PumpStateData> _utilityPumps = [];
    private readonly Dictionary<PumpId, PumpStateData> _utilityPumpIndex = [];
    private readonly List<SewageTreatmentPlantStateData> _treatmentPlants = [];
    private readonly Dictionary<SewageTreatmentPlantId, SewageTreatmentPlantStateData> _treatmentPlantIndex = [];
    private readonly List<WaterSewerServicePointStateData> _waterSewerServicePoints = [];
    private readonly Dictionary<WaterSewerServicePointId, WaterSewerServicePointStateData> _waterSewerServicePointIndex = [];
    private readonly Dictionary<SpatialCell, List<WaterNodeId>> _waterNodeSpatialIndex = [];
    private readonly Dictionary<SpatialCell, List<SewerNodeId>> _sewerNodeSpatialIndex = [];
    private readonly IWaterSupplySolver _waterSupplySolver;
    private readonly ISewerSolver _sewerSolver;
    private ulong _nextWaterNodeId = 1;
    private ulong _nextWaterPipeId = 1;
    private ulong _nextSewerNodeId = 1;
    private ulong _nextSewerPipeId = 1;
    private ulong _nextWaterSourceId = 1;
    private ulong _nextReservoirId = 1;
    private ulong _nextPumpId = 1;
    private ulong _nextTreatmentPlantId = 1;
    private ulong _nextWaterSewerServicePointId = 1;

    public int WaterNodeCount => _waterNodes.Count;
    public int WaterPipeCount => _waterPipes.Count;
    public int SewerNodeCount => _sewerNodes.Count;
    public int SewerPipeCount => _sewerPipes.Count;
    public int WaterSewerServicePointCount => _waterSewerServicePoints.Count;

    public WaterNodeId CreateWaterNode(WorldPoint position, WaterNodeKind kind = WaterNodeKind.Distribution)
    {
        ValidatePoint(position); ValidateWaterSewerEnum(kind, nameof(kind)); EnsureWaterSewerIdCapacity(_nextWaterNodeId, "Water node");
        var id = new WaterNodeId(_nextWaterNodeId++); var state = new WaterNodeState(id, kind, position);
        _waterNodes.Add(state); _waterNodeIndex.Add(id, state); AddSpatial(_waterNodeSpatialIndex, position, id); return id;
    }

    public SewerNodeId CreateSewerNode(WorldPoint position, SewerNodeKind kind = SewerNodeKind.Collection)
    {
        ValidatePoint(position); ValidateWaterSewerEnum(kind, nameof(kind)); EnsureWaterSewerIdCapacity(_nextSewerNodeId, "Sewer node");
        var id = new SewerNodeId(_nextSewerNodeId++); var state = new SewerNodeState(id, kind, position);
        _sewerNodes.Add(state); _sewerNodeIndex.Add(id, state); AddSpatial(_sewerNodeSpatialIndex, position, id); return id;
    }

    public WaterPipeId CreateWaterPipe(WaterNodeId fromNodeId, WaterNodeId toNodeId, double capacityCubicMetersPerDay, bool isInService = true)
    {
        ValidateWaterPipeReferences(fromNodeId, toNodeId); ValidatePositiveFinite(capacityCubicMetersPerDay, nameof(capacityCubicMetersPerDay)); EnsureWaterSewerIdCapacity(_nextWaterPipeId, "Water pipe");
        var id = new WaterPipeId(_nextWaterPipeId++); var state = new WaterPipeState(id, fromNodeId, toNodeId, capacityCubicMetersPerDay, isInService);
        _waterPipes.Add(state); _waterPipeIndex.Add(id, state); return id;
    }

    public SewerPipeId CreateSewerPipe(SewerNodeId fromNodeId, SewerNodeId toNodeId, double capacityCubicMetersPerDay, bool isInService = true)
    {
        ValidateSewerPipeReferences(fromNodeId, toNodeId); ValidatePositiveFinite(capacityCubicMetersPerDay, nameof(capacityCubicMetersPerDay)); EnsureWaterSewerIdCapacity(_nextSewerPipeId, "Sewer pipe");
        var id = new SewerPipeId(_nextSewerPipeId++); var state = new SewerPipeState(id, fromNodeId, toNodeId, capacityCubicMetersPerDay, isInService);
        _sewerPipes.Add(state); _sewerPipeIndex.Add(id, state); return id;
    }

    public void SetWaterPipeInService(WaterPipeId id, bool isInService)
    {
        if (!_waterPipeIndex.TryGetValue(id, out var pipe)) throw new ArgumentException($"Water pipe {id.Value} does not exist.", nameof(id)); pipe.IsInService = isInService;
    }

    public void SetSewerPipeInService(SewerPipeId id, bool isInService)
    {
        if (!_sewerPipeIndex.TryGetValue(id, out var pipe)) throw new ArgumentException($"Sewer pipe {id.Value} does not exist.", nameof(id)); pipe.IsInService = isInService;
    }

    public WaterSourceId CreateWaterSource(WaterNodeId nodeId, double capacityCubicMetersPerDay, UtilityOperatingState operatingState = UtilityOperatingState.Online)
    {
        if (!_waterNodeIndex.ContainsKey(nodeId)) throw new ArgumentException($"Water node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidatePositiveFinite(capacityCubicMetersPerDay, nameof(capacityCubicMetersPerDay)); ValidateWaterSewerEnum(operatingState, nameof(operatingState)); EnsureWaterSewerIdCapacity(_nextWaterSourceId, "Water source");
        var id = new WaterSourceId(_nextWaterSourceId++); var state = new WaterSourceStateData(id, nodeId, capacityCubicMetersPerDay, operatingState);
        _waterSources.Add(state); _waterSourceIndex.Add(id, state); return id;
    }

    public ReservoirId CreateReservoir(WaterNodeId nodeId, double releaseCapacityCubicMetersPerDay, UtilityOperatingState operatingState = UtilityOperatingState.Online)
    {
        if (!_waterNodeIndex.ContainsKey(nodeId)) throw new ArgumentException($"Water node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidatePositiveFinite(releaseCapacityCubicMetersPerDay, nameof(releaseCapacityCubicMetersPerDay)); ValidateWaterSewerEnum(operatingState, nameof(operatingState)); EnsureWaterSewerIdCapacity(_nextReservoirId, "Reservoir");
        var id = new ReservoirId(_nextReservoirId++); var state = new ReservoirStateData(id, nodeId, releaseCapacityCubicMetersPerDay, operatingState);
        _reservoirs.Add(state); _reservoirIndex.Add(id, state); return id;
    }

    public PumpId CreateWaterPump(WaterNodeId nodeId, double capacityCubicMetersPerDay, PowerLoadId? powerLoadId = null, UtilityOperatingState operatingState = UtilityOperatingState.Online) =>
        CreatePumpCore(PumpNetworkKind.Water, nodeId, null, capacityCubicMetersPerDay, powerLoadId, operatingState);

    public PumpId CreateSewerPump(SewerNodeId nodeId, double capacityCubicMetersPerDay, PowerLoadId? powerLoadId = null, UtilityOperatingState operatingState = UtilityOperatingState.Online) =>
        CreatePumpCore(PumpNetworkKind.Sewer, null, nodeId, capacityCubicMetersPerDay, powerLoadId, operatingState);

    public SewageTreatmentPlantId CreateSewageTreatmentPlant(SewerNodeId nodeId, double capacityCubicMetersPerDay, PowerLoadId? powerLoadId = null, UtilityOperatingState operatingState = UtilityOperatingState.Online)
    {
        if (!_sewerNodeIndex.ContainsKey(nodeId)) throw new ArgumentException($"Sewer node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidatePowerLoadReference(powerLoadId); ValidatePositiveFinite(capacityCubicMetersPerDay, nameof(capacityCubicMetersPerDay)); ValidateWaterSewerEnum(operatingState, nameof(operatingState)); EnsureWaterSewerIdCapacity(_nextTreatmentPlantId, "Treatment plant");
        var id = new SewageTreatmentPlantId(_nextTreatmentPlantId++); var state = new SewageTreatmentPlantStateData(id, nodeId, powerLoadId, capacityCubicMetersPerDay, operatingState);
        _treatmentPlants.Add(state); _treatmentPlantIndex.Add(id, state); return id;
    }

    public WaterSewerServicePointId CreateWaterSewerServicePoint(WaterNodeId waterNodeId, SewerNodeId sewerNodeId, double baseWaterDemandCubicMetersPerDay, BuildingId? buildingId = null, EstablishmentId? establishmentId = null, double wastewaterReturnRatio = WaterSewerDefaults.WastewaterReturnRatio)
    {
        if (!_waterNodeIndex.ContainsKey(waterNodeId)) throw new ArgumentException($"Water node {waterNodeId.Value} does not exist.", nameof(waterNodeId));
        if (!_sewerNodeIndex.ContainsKey(sewerNodeId)) throw new ArgumentException($"Sewer node {sewerNodeId.Value} does not exist.", nameof(sewerNodeId));
        ValidatePositiveFinite(baseWaterDemandCubicMetersPerDay, nameof(baseWaterDemandCubicMetersPerDay));
        if (!double.IsFinite(wastewaterReturnRatio) || wastewaterReturnRatio < 0d || wastewaterReturnRatio > 1d) throw new ArgumentOutOfRangeException(nameof(wastewaterReturnRatio));
        if (buildingId is null && establishmentId is null) throw new ArgumentException("A Water/Sewer service point must reference a Building, an Establishment, or both.", nameof(buildingId));
        ValidateWaterSewerConsumerReferences(ref buildingId, establishmentId);
        EnsureWaterSewerIdCapacity(_nextWaterSewerServicePointId, "Water/Sewer service point");
        var id = new WaterSewerServicePointId(_nextWaterSewerServicePointId++); var state = new WaterSewerServicePointStateData(id, waterNodeId, sewerNodeId, buildingId, establishmentId, baseWaterDemandCubicMetersPerDay, wastewaterReturnRatio);
        _waterSewerServicePoints.Add(state); _waterSewerServicePointIndex.Add(id, state); return id;
    }

    public void SetWaterSourceOperatingState(WaterSourceId id, UtilityOperatingState state) { ValidateWaterSewerEnum(state, nameof(state)); if (!_waterSourceIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Water source {id.Value} does not exist.", nameof(id)); item.OperatingState = state; }
    public void SetReservoirOperatingState(ReservoirId id, UtilityOperatingState state) { ValidateWaterSewerEnum(state, nameof(state)); if (!_reservoirIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Reservoir {id.Value} does not exist.", nameof(id)); item.OperatingState = state; }
    public void SetPumpOperatingState(PumpId id, UtilityOperatingState state) { ValidateWaterSewerEnum(state, nameof(state)); if (!_utilityPumpIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Pump {id.Value} does not exist.", nameof(id)); item.OperatingState = state; }
    public void SetSewageTreatmentPlantOperatingState(SewageTreatmentPlantId id, UtilityOperatingState state) { ValidateWaterSewerEnum(state, nameof(state)); if (!_treatmentPlantIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Treatment plant {id.Value} does not exist.", nameof(id)); item.OperatingState = state; }

    public bool TryGetWaterSewerServicePointSnapshot(WaterSewerServicePointId id, out WaterSewerServicePointSnapshot snapshot)
    {
        if (_waterSewerServicePointIndex.TryGetValue(id, out var state)) { snapshot = CreateServicePointSnapshot(state); return true; } snapshot = default; return false;
    }

    public WaterNodeSnapshot[] QueryWaterNodes(WorldVolume volume) => QuerySpatial(_waterNodeSpatialIndex, volume).Distinct().OrderBy(static id => id.Value).Select(id => { var n = _waterNodeIndex[id]; return new WaterNodeSnapshot(n.Id, n.Kind, n.Position); }).Where(item => volume.Contains(item.Position)).ToArray();
    public SewerNodeSnapshot[] QuerySewerNodes(WorldVolume volume) => QuerySpatial(_sewerNodeSpatialIndex, volume).Distinct().OrderBy(static id => id.Value).Select(id => { var n = _sewerNodeIndex[id]; return new SewerNodeSnapshot(n.Id, n.Kind, n.Position); }).Where(item => volume.Contains(item.Position)).ToArray();

    public WaterSewerSnapshot CreateWaterSewerSnapshot() => new(
        CreateWaterSewerStatistics(),
        _waterNodes.OrderBy(static item => item.Id.Value).Select(static item => new WaterNodeSnapshot(item.Id, item.Kind, item.Position)).ToArray(),
        _waterPipes.OrderBy(static item => item.Id.Value).Select(static item => new WaterPipeSnapshot(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
        _sewerNodes.OrderBy(static item => item.Id.Value).Select(static item => new SewerNodeSnapshot(item.Id, item.Kind, item.Position)).ToArray(),
        _sewerPipes.OrderBy(static item => item.Id.Value).Select(static item => new SewerPipeSnapshot(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
        _waterSources.OrderBy(static item => item.Id.Value).Select(CreateWaterSourceSnapshot).ToArray(),
        _reservoirs.OrderBy(static item => item.Id.Value).Select(CreateReservoirSnapshot).ToArray(),
        _utilityPumps.OrderBy(static item => item.Id.Value).Select(CreatePumpSnapshot).ToArray(),
        _treatmentPlants.OrderBy(static item => item.Id.Value).Select(CreateTreatmentPlantSnapshot).ToArray(),
        _waterSewerServicePoints.OrderBy(static item => item.Id.Value).Select(CreateServicePointSnapshot).ToArray());

    public WaterSewerStatistics CreateWaterSewerStatistics() => new(
        _waterNodes.Count, _waterPipes.Count, _sewerNodes.Count, _sewerPipes.Count, _waterSources.Count, _reservoirs.Count, _utilityPumps.Count, _treatmentPlants.Count, _waterSewerServicePoints.Count,
        _waterSewerServicePoints.Count(static item => item.WaterState == WaterServiceState.Unavailable),
        _waterSewerServicePoints.Count(static item => item.SewerState == SewerServiceState.Unavailable),
        _waterSewerServicePoints.Count(static item => item.SewerState == SewerServiceState.Overflow),
        _waterSources.Sum(static item => item.CapacityCubicMetersPerDay) + _reservoirs.Sum(static item => item.ReleaseCapacityCubicMetersPerDay),
        _waterSewerServicePoints.Sum(static item => item.WaterDemandCubicMetersPerDay),
        _waterSewerServicePoints.Sum(static item => item.WaterServedCubicMetersPerDay),
        _waterSewerServicePoints.Sum(static item => item.WastewaterGeneratedCubicMetersPerDay),
        _waterSewerServicePoints.Sum(static item => item.WastewaterProcessedCubicMetersPerDay),
        _waterSewerServicePoints.Sum(static item => item.WastewaterOverflowCubicMetersPerDay), Time.TickCount);

    private void StepWaterSewer(SimulationTime nextTime)
    {
        foreach (var point in _waterSewerServicePoints) point.WaterDemandCubicMetersPerDay = CalculateWaterDemand(point, nextTime);
        var waterResult = _waterSupplySolver.Solve(new WaterSupplyRequest(
            _waterNodes.Select(static item => new WaterSupplyNode(item.Id)).ToArray(),
            _waterPipes.Select(static item => new WaterSupplyPipe(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
            _waterSources.Select(item => new WaterSupplySource(item.Id, item.NodeId, IsFacilityAvailable(item.OperatingState, null) ? item.CapacityCubicMetersPerDay : 0d)).ToArray(),
            _reservoirs.Select(item => new WaterSupplyReservoir(item.Id, item.NodeId, IsFacilityAvailable(item.OperatingState, null) ? item.ReleaseCapacityCubicMetersPerDay : 0d)).ToArray(),
            _utilityPumps.Where(static item => item.NetworkKind == PumpNetworkKind.Water).Select(item => new WaterSupplyPump(item.Id, item.WaterNodeId!.Value, IsFacilityAvailable(item.OperatingState, item.PowerLoadId) ? item.CapacityCubicMetersPerDay : 0d)).ToArray(),
            _waterSewerServicePoints.Select(static item => new WaterSupplyLoad(item.Id, item.WaterNodeId, item.WaterDemandCubicMetersPerDay)).ToArray()));

        var sourceOutputs = waterResult.Sources.ToDictionary(static item => item.Id, static item => item.OutputCubicMetersPerDay);
        var reservoirOutputs = waterResult.Reservoirs.ToDictionary(static item => item.Id, static item => item.OutputCubicMetersPerDay);
        var pumpOutputs = waterResult.Pumps.ToDictionary(static item => item.Id, static item => item.ThroughputCubicMetersPerDay);
        var waterLoads = waterResult.Loads.ToDictionary(static item => item.Id, static item => item.ServedCubicMetersPerDay);
        foreach (var source in _waterSources) source.OutputCubicMetersPerDay = Math.Min(source.CapacityCubicMetersPerDay, sourceOutputs.GetValueOrDefault(source.Id));
        foreach (var reservoir in _reservoirs) reservoir.OutputCubicMetersPerDay = Math.Min(reservoir.ReleaseCapacityCubicMetersPerDay, reservoirOutputs.GetValueOrDefault(reservoir.Id));
        foreach (var pump in _utilityPumps.Where(static item => item.NetworkKind == PumpNetworkKind.Water)) pump.ThroughputCubicMetersPerDay = Math.Min(pump.CapacityCubicMetersPerDay, pumpOutputs.GetValueOrDefault(pump.Id));
        foreach (var point in _waterSewerServicePoints)
        {
            point.WaterServedCubicMetersPerDay = Math.Clamp(waterLoads.GetValueOrDefault(point.Id), 0d, point.WaterDemandCubicMetersPerDay);
            point.WaterUnservedCubicMetersPerDay = Math.Max(0d, point.WaterDemandCubicMetersPerDay - point.WaterServedCubicMetersPerDay);
            point.WaterState = point.WaterUnservedCubicMetersPerDay <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay ? WaterServiceState.Supplied : point.WaterServedCubicMetersPerDay <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay ? WaterServiceState.Unavailable : WaterServiceState.Constrained;
            point.WastewaterGeneratedCubicMetersPerDay = point.WaterServedCubicMetersPerDay * point.WastewaterReturnRatio;
        }

        var sewerResult = _sewerSolver.Solve(new SewerFlowRequest(
            _sewerNodes.Select(static item => new SewerFlowNode(item.Id)).ToArray(),
            _sewerPipes.Select(static item => new SewerFlowPipe(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
            _utilityPumps.Where(static item => item.NetworkKind == PumpNetworkKind.Sewer).Select(item => new SewerFlowPump(item.Id, item.SewerNodeId!.Value, IsFacilityAvailable(item.OperatingState, item.PowerLoadId) ? item.CapacityCubicMetersPerDay : 0d)).ToArray(),
            _treatmentPlants.Select(item => new SewerFlowTreatment(item.Id, item.NodeId, IsFacilityAvailable(item.OperatingState, item.PowerLoadId) ? item.CapacityCubicMetersPerDay : 0d)).ToArray(),
            _waterSewerServicePoints.Select(static item => new SewerFlowLoad(item.Id, item.SewerNodeId, item.WastewaterGeneratedCubicMetersPerDay)).ToArray()));
        var sewerPumpOutputs = sewerResult.Pumps.ToDictionary(static item => item.Id, static item => item.ThroughputCubicMetersPerDay);
        var treatmentOutputs = sewerResult.Treatments.ToDictionary(static item => item.Id, static item => item.ProcessedCubicMetersPerDay);
        var sewerLoads = sewerResult.Loads.ToDictionary(static item => item.Id, static item => item.ProcessedCubicMetersPerDay);
        foreach (var pump in _utilityPumps.Where(static item => item.NetworkKind == PumpNetworkKind.Sewer)) pump.ThroughputCubicMetersPerDay = Math.Min(pump.CapacityCubicMetersPerDay, sewerPumpOutputs.GetValueOrDefault(pump.Id));
        foreach (var plant in _treatmentPlants) plant.ProcessedCubicMetersPerDay = Math.Min(plant.CapacityCubicMetersPerDay, treatmentOutputs.GetValueOrDefault(plant.Id));
        foreach (var point in _waterSewerServicePoints)
        {
            point.WastewaterProcessedCubicMetersPerDay = Math.Clamp(sewerLoads.GetValueOrDefault(point.Id), 0d, point.WastewaterGeneratedCubicMetersPerDay);
            point.WastewaterOverflowCubicMetersPerDay = Math.Max(0d, point.WastewaterGeneratedCubicMetersPerDay - point.WastewaterProcessedCubicMetersPerDay);
            point.SewerState = point.WastewaterGeneratedCubicMetersPerDay <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay || point.WastewaterOverflowCubicMetersPerDay <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay ? SewerServiceState.Available : point.WastewaterProcessedCubicMetersPerDay <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay ? SewerServiceState.Unavailable : SewerServiceState.Overflow;
        }
    }

    private double CalculateWaterDemand(WaterSewerServicePointStateData point, SimulationTime time)
    {
        var hour = time.Elapsed.TotalHours % 24d; if (hour < 0d) hour += 24d;
        var timeFactor = hour switch { < 6d => 0.65d, < 9d => 1.15d, < 17d => 0.95d, < 22d => 1.1d, _ => 0.75d };
        var useFactor = 1d;
        if (point.BuildingId is { } buildingId && TryGetBuildingSnapshot(buildingId, out var building))
        {
            useFactor *= building.Kind switch { BuildingKind.Residential => 1d, BuildingKind.Commercial => 0.9d, BuildingKind.Industrial => 1.35d, BuildingKind.Civic => 1.15d, BuildingKind.MixedUse => 1.1d, _ => 1d };
            var residents = 0;
            for (var index = 0; index < _population.PersonCount; index++) if (_population.GetPersonAt(index).Residence.BuildingId == buildingId) residents++;
            if (residents > 0) useFactor *= Math.Min(5d, 1d + (residents * 0.2d));
        }
        if (point.EstablishmentId is { } establishmentId && _economyEstablishmentIndex.TryGetValue(establishmentId, out var establishment) && _economyCompanyIndex.TryGetValue(establishment.CompanyId, out var company))
        {
            useFactor *= company.Sector switch { IndustrySector.Manufacturing => 1.4d, IndustrySector.Retail => 1.05d, IndustrySector.Services => 0.9d, IndustrySector.Transport => 1.1d, IndustrySector.Public => 1.15d, _ => 1d };
            var required = _economyJobs.Where(item => item.EstablishmentId == establishmentId).Sum(static item => item.RequiredWorkerCount);
            if (required > 0) useFactor *= 0.6d + (0.4d * Math.Min(1d, (double)_economyJobs.Where(item => item.EstablishmentId == establishmentId).Sum(item => GetFilledWorkerCount(item.Id)) / required));
        }
        return point.BaseWaterDemandCubicMetersPerDay * timeFactor * useFactor;
    }

    private double GetEstablishmentWaterSewerAvailabilityFactor(EstablishmentId establishmentId)
    {
        var direct = _waterSewerServicePoints.Where(item => item.EstablishmentId == establishmentId).ToArray();
        if (direct.Length == 0 && _economyEstablishmentIndex.TryGetValue(establishmentId, out var establishment) && establishment.BuildingId is { } buildingId) direct = _waterSewerServicePoints.Where(item => item.BuildingId == buildingId).ToArray();
        if (direct.Length == 0) return 1d;
        var demand = direct.Sum(static item => item.WaterDemandCubicMetersPerDay); var water = demand <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay ? 1d : direct.Sum(static item => item.WaterServedCubicMetersPerDay) / demand;
        var wastewater = direct.Sum(static item => item.WastewaterGeneratedCubicMetersPerDay); var sewer = wastewater <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay ? 1d : direct.Sum(static item => item.WastewaterProcessedCubicMetersPerDay) / wastewater;
        return Math.Clamp(Math.Min(water, sewer), 0d, 1d);
    }

    private EconomyCheckpoint CreateEconomyCheckpointWithWaterSewer() => CreateEconomyCheckpointWithExtensions() with { WaterSewer = CreateWaterSewerCheckpoint() };

    private WaterSewerCheckpoint CreateWaterSewerCheckpoint() => new(
        _nextWaterNodeId, _nextWaterPipeId, _nextSewerNodeId, _nextSewerPipeId, _nextWaterSourceId, _nextReservoirId, _nextPumpId, _nextTreatmentPlantId, _nextWaterSewerServicePointId,
        _waterNodes.Select(static item => new SimulationWaterNodeCheckpoint(item.Id, item.Kind, item.Position)).ToArray(),
        _waterPipes.Select(static item => new SimulationWaterPipeCheckpoint(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
        _sewerNodes.Select(static item => new SimulationSewerNodeCheckpoint(item.Id, item.Kind, item.Position)).ToArray(),
        _sewerPipes.Select(static item => new SimulationSewerPipeCheckpoint(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray(),
        _waterSources.Select(static item => new SimulationWaterSourceCheckpoint(item.Id, item.NodeId, item.CapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _reservoirs.Select(static item => new SimulationReservoirCheckpoint(item.Id, item.NodeId, item.ReleaseCapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _utilityPumps.Select(static item => new SimulationPumpCheckpoint(item.Id, item.NetworkKind, item.WaterNodeId, item.SewerNodeId, item.PowerLoadId, item.CapacityCubicMetersPerDay, item.ThroughputCubicMetersPerDay, item.OperatingState)).ToArray(),
        _treatmentPlants.Select(static item => new SimulationSewageTreatmentPlantCheckpoint(item.Id, item.NodeId, item.PowerLoadId, item.CapacityCubicMetersPerDay, item.ProcessedCubicMetersPerDay, item.OperatingState)).ToArray(),
        _waterSewerServicePoints.Select(static item => new SimulationWaterSewerServicePointCheckpoint(item.Id, item.WaterNodeId, item.SewerNodeId, item.BuildingId, item.EstablishmentId, item.BaseWaterDemandCubicMetersPerDay, item.WastewaterReturnRatio, item.WaterDemandCubicMetersPerDay, item.WaterServedCubicMetersPerDay, item.WaterUnservedCubicMetersPerDay, item.WaterState, item.WastewaterGeneratedCubicMetersPerDay, item.WastewaterProcessedCubicMetersPerDay, item.WastewaterOverflowCubicMetersPerDay, item.SewerState)).ToArray());

    private void RestoreWaterSewer(WaterSewerCheckpoint? checkpoint)
    {
        ClearWaterSewer(); if (checkpoint is null) return;
        foreach (var item in checkpoint.WaterNodes) { var state = new WaterNodeState(item.Id, item.Kind, item.Position); _waterNodes.Add(state); _waterNodeIndex.Add(state.Id, state); AddSpatial(_waterNodeSpatialIndex, state.Position, state.Id); }
        foreach (var item in checkpoint.WaterPipes) { var state = new WaterPipeState(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService); _waterPipes.Add(state); _waterPipeIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.SewerNodes) { var state = new SewerNodeState(item.Id, item.Kind, item.Position); _sewerNodes.Add(state); _sewerNodeIndex.Add(state.Id, state); AddSpatial(_sewerNodeSpatialIndex, state.Position, state.Id); }
        foreach (var item in checkpoint.SewerPipes) { var state = new SewerPipeState(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityCubicMetersPerDay, item.IsInService); _sewerPipes.Add(state); _sewerPipeIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.WaterSources) { var state = new WaterSourceStateData(item.Id, item.NodeId, item.CapacityCubicMetersPerDay, item.OperatingState) { OutputCubicMetersPerDay = item.OutputCubicMetersPerDay }; _waterSources.Add(state); _waterSourceIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.Reservoirs) { var state = new ReservoirStateData(item.Id, item.NodeId, item.ReleaseCapacityCubicMetersPerDay, item.OperatingState) { OutputCubicMetersPerDay = item.OutputCubicMetersPerDay }; _reservoirs.Add(state); _reservoirIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.Pumps) { var state = new PumpStateData(item.Id, item.NetworkKind, item.WaterNodeId, item.SewerNodeId, item.PowerLoadId, item.CapacityCubicMetersPerDay, item.OperatingState) { ThroughputCubicMetersPerDay = item.ThroughputCubicMetersPerDay }; _utilityPumps.Add(state); _utilityPumpIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.TreatmentPlants) { var state = new SewageTreatmentPlantStateData(item.Id, item.NodeId, item.PowerLoadId, item.CapacityCubicMetersPerDay, item.OperatingState) { ProcessedCubicMetersPerDay = item.ProcessedCubicMetersPerDay }; _treatmentPlants.Add(state); _treatmentPlantIndex.Add(state.Id, state); }
        foreach (var item in checkpoint.ServicePoints) { var state = new WaterSewerServicePointStateData(item.Id, item.WaterNodeId, item.SewerNodeId, item.BuildingId, item.EstablishmentId, item.BaseWaterDemandCubicMetersPerDay, item.WastewaterReturnRatio) { WaterDemandCubicMetersPerDay = item.WaterDemandCubicMetersPerDay, WaterServedCubicMetersPerDay = item.WaterServedCubicMetersPerDay, WaterUnservedCubicMetersPerDay = item.WaterUnservedCubicMetersPerDay, WaterState = item.WaterState, WastewaterGeneratedCubicMetersPerDay = item.WastewaterGeneratedCubicMetersPerDay, WastewaterProcessedCubicMetersPerDay = item.WastewaterProcessedCubicMetersPerDay, WastewaterOverflowCubicMetersPerDay = item.WastewaterOverflowCubicMetersPerDay, SewerState = item.SewerState }; _waterSewerServicePoints.Add(state); _waterSewerServicePointIndex.Add(state.Id, state); }
        _nextWaterNodeId = checkpoint.NextWaterNodeId; _nextWaterPipeId = checkpoint.NextWaterPipeId; _nextSewerNodeId = checkpoint.NextSewerNodeId; _nextSewerPipeId = checkpoint.NextSewerPipeId; _nextWaterSourceId = checkpoint.NextWaterSourceId; _nextReservoirId = checkpoint.NextReservoirId; _nextPumpId = checkpoint.NextPumpId; _nextTreatmentPlantId = checkpoint.NextTreatmentPlantId; _nextWaterSewerServicePointId = checkpoint.NextServicePointId;
    }

    private static void ValidateWaterSewerCheckpoint(SimulationCheckpoint checkpoint)
    {
        var utility = checkpoint.Economy?.WaterSewer; if (utility is null) return;
        if (utility.NextWaterNodeId == 0 || utility.NextWaterPipeId == 0 || utility.NextSewerNodeId == 0 || utility.NextSewerPipeId == 0 || utility.NextWaterSourceId == 0 || utility.NextReservoirId == 0 || utility.NextPumpId == 0 || utility.NextTreatmentPlantId == 0 || utility.NextServicePointId == 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Water/Sewer next IDs must be greater than zero.");
        var waterNodes = ValidateNodes(utility.WaterNodes, utility.NextWaterNodeId); var sewerNodes = ValidateNodes(utility.SewerNodes, utility.NextSewerNodeId);
        ValidatePipes(utility.WaterPipes, waterNodes, utility.NextWaterPipeId); ValidatePipes(utility.SewerPipes, sewerNodes, utility.NextSewerPipeId);
        var powerLoads = (checkpoint.Economy?.Power?.Loads ?? []).Select(static item => item.Id).ToHashSet();
        ValidateFacilities(utility, waterNodes, sewerNodes, powerLoads);
        var buildings = checkpoint.Buildings.Select(static item => item.Id).ToHashSet(); var establishments = (checkpoint.Economy?.Establishments ?? []).Select(static item => item.Id).ToHashSet(); var ids = new HashSet<WaterSewerServicePointId>(); var maxId = 0UL;
        foreach (var item in utility.ServicePoints)
        {
            if (item.Id.Value == 0 || !ids.Add(item.Id) || !waterNodes.Contains(item.WaterNodeId) || !sewerNodes.Contains(item.SewerNodeId) || (item.BuildingId is null && item.EstablishmentId is null) || (item.BuildingId is { } b && !buildings.Contains(b)) || (item.EstablishmentId is { } e && !establishments.Contains(e)) || !IsPositiveFinite(item.BaseWaterDemandCubicMetersPerDay) || !double.IsFinite(item.WastewaterReturnRatio) || item.WastewaterReturnRatio < 0d || item.WastewaterReturnRatio > 1d || !IsNonNegativeFinite(item.WaterDemandCubicMetersPerDay) || !IsNonNegativeFinite(item.WaterServedCubicMetersPerDay) || !IsNonNegativeFinite(item.WaterUnservedCubicMetersPerDay) || !IsNonNegativeFinite(item.WastewaterGeneratedCubicMetersPerDay) || !IsNonNegativeFinite(item.WastewaterProcessedCubicMetersPerDay) || !IsNonNegativeFinite(item.WastewaterOverflowCubicMetersPerDay) || !Enum.IsDefined(item.WaterState) || !Enum.IsDefined(item.SewerState)) throw new ArgumentException("Water/Sewer contains invalid Service Point state.", nameof(checkpoint));
            maxId = Math.Max(maxId, item.Id.Value);
        }
        if (utility.NextServicePointId <= maxId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Water/Sewer service point ID must exceed stored IDs.");
    }

    private static HashSet<WaterNodeId> ValidateNodes(IReadOnlyList<SimulationWaterNodeCheckpoint> nodes, ulong nextId) { var ids = new HashSet<WaterNodeId>(); var max = 0UL; foreach (var item in nodes) { if (item.Id.Value == 0 || !ids.Add(item.Id) || !Enum.IsDefined(item.Kind)) throw new ArgumentException("Water contains invalid nodes."); ValidatePoint(item.Position); max = Math.Max(max, item.Id.Value); } if (nextId <= max) throw new ArgumentOutOfRangeException(nameof(nextId)); return ids; }
    private static HashSet<SewerNodeId> ValidateNodes(IReadOnlyList<SimulationSewerNodeCheckpoint> nodes, ulong nextId) { var ids = new HashSet<SewerNodeId>(); var max = 0UL; foreach (var item in nodes) { if (item.Id.Value == 0 || !ids.Add(item.Id) || !Enum.IsDefined(item.Kind)) throw new ArgumentException("Sewer contains invalid nodes."); ValidatePoint(item.Position); max = Math.Max(max, item.Id.Value); } if (nextId <= max) throw new ArgumentOutOfRangeException(nameof(nextId)); return ids; }
    private static void ValidatePipes(IReadOnlyList<SimulationWaterPipeCheckpoint> pipes, HashSet<WaterNodeId> nodes, ulong nextId) { var ids = new HashSet<WaterPipeId>(); var max = 0UL; foreach (var item in pipes) { if (item.Id.Value == 0 || !ids.Add(item.Id) || !nodes.Contains(item.FromNodeId) || !nodes.Contains(item.ToNodeId) || item.FromNodeId == item.ToNodeId || !IsPositiveFinite(item.CapacityCubicMetersPerDay)) throw new ArgumentException("Water contains invalid pipes."); max = Math.Max(max, item.Id.Value); } if (nextId <= max) throw new ArgumentOutOfRangeException(nameof(nextId)); }
    private static void ValidatePipes(IReadOnlyList<SimulationSewerPipeCheckpoint> pipes, HashSet<SewerNodeId> nodes, ulong nextId) { var ids = new HashSet<SewerPipeId>(); var max = 0UL; foreach (var item in pipes) { if (item.Id.Value == 0 || !ids.Add(item.Id) || !nodes.Contains(item.FromNodeId) || !nodes.Contains(item.ToNodeId) || item.FromNodeId == item.ToNodeId || !IsPositiveFinite(item.CapacityCubicMetersPerDay)) throw new ArgumentException("Sewer contains invalid pipes."); max = Math.Max(max, item.Id.Value); } if (nextId <= max) throw new ArgumentOutOfRangeException(nameof(nextId)); }

    private static void ValidateFacilities(WaterSewerCheckpoint utility, HashSet<WaterNodeId> waterNodes, HashSet<SewerNodeId> sewerNodes, HashSet<PowerLoadId> powerLoads)
    {
        ValidateFacilityIds(utility.WaterSources.Select(static item => item.Id.Value), utility.NextWaterSourceId, "Water source"); foreach (var item in utility.WaterSources) if (!waterNodes.Contains(item.NodeId) || !IsPositiveFinite(item.CapacityCubicMetersPerDay) || !IsNonNegativeFinite(item.OutputCubicMetersPerDay) || !Enum.IsDefined(item.OperatingState)) throw new ArgumentException("Invalid Water source.");
        ValidateFacilityIds(utility.Reservoirs.Select(static item => item.Id.Value), utility.NextReservoirId, "Reservoir"); foreach (var item in utility.Reservoirs) if (!waterNodes.Contains(item.NodeId) || !IsPositiveFinite(item.ReleaseCapacityCubicMetersPerDay) || !IsNonNegativeFinite(item.OutputCubicMetersPerDay) || !Enum.IsDefined(item.OperatingState)) throw new ArgumentException("Invalid Reservoir.");
        ValidateFacilityIds(utility.Pumps.Select(static item => item.Id.Value), utility.NextPumpId, "Pump"); foreach (var item in utility.Pumps) if (!Enum.IsDefined(item.NetworkKind) || (item.NetworkKind == PumpNetworkKind.Water ? item.WaterNodeId is not { } w || !waterNodes.Contains(w) || item.SewerNodeId is not null : item.SewerNodeId is not { } s || !sewerNodes.Contains(s) || item.WaterNodeId is not null) || (item.PowerLoadId is { } p && !powerLoads.Contains(p)) || !IsPositiveFinite(item.CapacityCubicMetersPerDay) || !IsNonNegativeFinite(item.ThroughputCubicMetersPerDay) || !Enum.IsDefined(item.OperatingState)) throw new ArgumentException("Invalid Pump.");
        ValidateFacilityIds(utility.TreatmentPlants.Select(static item => item.Id.Value), utility.NextTreatmentPlantId, "Treatment plant"); foreach (var item in utility.TreatmentPlants) if (!sewerNodes.Contains(item.NodeId) || (item.PowerLoadId is { } p && !powerLoads.Contains(p)) || !IsPositiveFinite(item.CapacityCubicMetersPerDay) || !IsNonNegativeFinite(item.ProcessedCubicMetersPerDay) || !Enum.IsDefined(item.OperatingState)) throw new ArgumentException("Invalid Treatment plant.");
    }

    private static void ValidateFacilityIds(IEnumerable<ulong> values, ulong nextId, string name) { var ids = new HashSet<ulong>(); var max = 0UL; foreach (var id in values) { if (id == 0 || !ids.Add(id)) throw new ArgumentException($"{name} IDs are invalid or duplicated."); max = Math.Max(max, id); } if (nextId <= max) throw new ArgumentOutOfRangeException(nameof(nextId)); }
    private PumpId CreatePumpCore(PumpNetworkKind kind, WaterNodeId? waterNodeId, SewerNodeId? sewerNodeId, double capacity, PowerLoadId? powerLoadId, UtilityOperatingState operatingState) { ValidateWaterSewerEnum(kind, nameof(kind)); if (kind == PumpNetworkKind.Water && (waterNodeId is null || !_waterNodeIndex.ContainsKey(waterNodeId.Value))) throw new ArgumentException("Water Pump requires an existing Water node.", nameof(waterNodeId)); if (kind == PumpNetworkKind.Sewer && (sewerNodeId is null || !_sewerNodeIndex.ContainsKey(sewerNodeId.Value))) throw new ArgumentException("Sewer Pump requires an existing Sewer node.", nameof(sewerNodeId)); ValidatePositiveFinite(capacity, nameof(capacity)); ValidatePowerLoadReference(powerLoadId); ValidateWaterSewerEnum(operatingState, nameof(operatingState)); EnsureWaterSewerIdCapacity(_nextPumpId, "Pump"); var id = new PumpId(_nextPumpId++); var state = new PumpStateData(id, kind, waterNodeId, sewerNodeId, powerLoadId, capacity, operatingState); _utilityPumps.Add(state); _utilityPumpIndex.Add(id, state); return id; }
    private void ValidatePowerLoadReference(PowerLoadId? id) { if (id is { } value && !_powerLoadIndex.ContainsKey(value)) throw new ArgumentException($"Power load {value.Value} does not exist.", nameof(id)); }
    private bool IsFacilityAvailable(UtilityOperatingState state, PowerLoadId? powerLoadId) { if (state != UtilityOperatingState.Online) return false; if (powerLoadId is null) return true; if (!_powerLoadIndex.TryGetValue(powerLoadId.Value, out var load)) return false; return load.DemandMegawatts <= PowerDefaults.SupplyEpsilonMegawatts || load.ServedMegawatts > PowerDefaults.SupplyEpsilonMegawatts; }
    private void ValidateWaterSewerConsumerReferences(ref BuildingId? buildingId, EstablishmentId? establishmentId) { if (buildingId is { } b && !TryGetBuildingSnapshot(b, out _)) throw new ArgumentException($"Building {b.Value} does not exist.", nameof(buildingId)); if (establishmentId is { } e) { if (!_economyEstablishmentIndex.TryGetValue(e, out var est)) throw new ArgumentException($"Establishment {e.Value} does not exist.", nameof(establishmentId)); if (buildingId is { } explicitBuilding && est.BuildingId is { } estBuilding && explicitBuilding != estBuilding) throw new ArgumentException("Establishment belongs to a different Building.", nameof(buildingId)); buildingId ??= est.BuildingId; } }
    private void ValidateWaterPipeReferences(WaterNodeId from, WaterNodeId to) { if (!_waterNodeIndex.ContainsKey(from) || !_waterNodeIndex.ContainsKey(to) || from == to) throw new ArgumentException("Water pipe references invalid nodes."); }
    private void ValidateSewerPipeReferences(SewerNodeId from, SewerNodeId to) { if (!_sewerNodeIndex.ContainsKey(from) || !_sewerNodeIndex.ContainsKey(to) || from == to) throw new ArgumentException("Sewer pipe references invalid nodes."); }
    private static void ValidateWaterSewerEnum<T>(T value, string name) where T : struct, Enum { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(name, value, "Water/Sewer enum value is not defined."); }
    private static void EnsureWaterSewerIdCapacity(ulong nextId, string name) { if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted."); }
    private void AddSpatial<T>(Dictionary<SpatialCell, List<T>> index, WorldPoint point, T id) { var cell = SpatialGrid.ToCell(point, Config.SpatialCellSize); if (!index.TryGetValue(cell, out var items)) index[cell] = items = []; items.Add(id); }
    private IEnumerable<T> QuerySpatial<T>(Dictionary<SpatialCell, List<T>> index, WorldVolume volume) { var min = SpatialGrid.ToCell(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ), Config.SpatialCellSize); var max = SpatialGrid.ToCell(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ), Config.SpatialCellSize); for (var x = min.X; x <= max.X; x++) for (var y = min.Y; y <= max.Y; y++) for (var z = min.Z; z <= max.Z; z++) if (index.TryGetValue(new SpatialCell(x, y, z), out var items)) foreach (var item in items) yield return item; }
    private void ClearWaterSewer() { _waterNodes.Clear(); _waterNodeIndex.Clear(); _waterPipes.Clear(); _waterPipeIndex.Clear(); _sewerNodes.Clear(); _sewerNodeIndex.Clear(); _sewerPipes.Clear(); _sewerPipeIndex.Clear(); _waterSources.Clear(); _waterSourceIndex.Clear(); _reservoirs.Clear(); _reservoirIndex.Clear(); _utilityPumps.Clear(); _utilityPumpIndex.Clear(); _treatmentPlants.Clear(); _treatmentPlantIndex.Clear(); _waterSewerServicePoints.Clear(); _waterSewerServicePointIndex.Clear(); _waterNodeSpatialIndex.Clear(); _sewerNodeSpatialIndex.Clear(); _nextWaterNodeId = _nextWaterPipeId = _nextSewerNodeId = _nextSewerPipeId = _nextWaterSourceId = _nextReservoirId = _nextPumpId = _nextTreatmentPlantId = _nextWaterSewerServicePointId = 1; }

    private static WaterSourceSnapshot CreateWaterSourceSnapshot(WaterSourceStateData s) => new(s.Id, s.NodeId, s.CapacityCubicMetersPerDay, s.OutputCubicMetersPerDay, s.OperatingState);
    private static ReservoirSnapshot CreateReservoirSnapshot(ReservoirStateData s) => new(s.Id, s.NodeId, s.ReleaseCapacityCubicMetersPerDay, s.OutputCubicMetersPerDay, s.OperatingState);
    private static PumpSnapshot CreatePumpSnapshot(PumpStateData s) => new(s.Id, s.NetworkKind, s.WaterNodeId, s.SewerNodeId, s.PowerLoadId, s.CapacityCubicMetersPerDay, s.ThroughputCubicMetersPerDay, s.OperatingState);
    private static SewageTreatmentPlantSnapshot CreateTreatmentPlantSnapshot(SewageTreatmentPlantStateData s) => new(s.Id, s.NodeId, s.PowerLoadId, s.CapacityCubicMetersPerDay, s.ProcessedCubicMetersPerDay, s.OperatingState);
    private static WaterSewerServicePointSnapshot CreateServicePointSnapshot(WaterSewerServicePointStateData s) => new(s.Id, s.WaterNodeId, s.SewerNodeId, s.BuildingId, s.EstablishmentId, s.BaseWaterDemandCubicMetersPerDay, s.WastewaterReturnRatio, s.WaterDemandCubicMetersPerDay, s.WaterServedCubicMetersPerDay, s.WaterUnservedCubicMetersPerDay, s.WaterState, s.WastewaterGeneratedCubicMetersPerDay, s.WastewaterProcessedCubicMetersPerDay, s.WastewaterOverflowCubicMetersPerDay, s.SewerState);

    private sealed class WaterNodeState(WaterNodeId id, WaterNodeKind kind, WorldPoint position) { public WaterNodeId Id { get; } = id; public WaterNodeKind Kind { get; } = kind; public WorldPoint Position { get; } = position; }
    private sealed class SewerNodeState(SewerNodeId id, SewerNodeKind kind, WorldPoint position) { public SewerNodeId Id { get; } = id; public SewerNodeKind Kind { get; } = kind; public WorldPoint Position { get; } = position; }
    private sealed class WaterPipeState(WaterPipeId id, WaterNodeId from, WaterNodeId to, double capacity, bool service) { public WaterPipeId Id { get; } = id; public WaterNodeId FromNodeId { get; } = from; public WaterNodeId ToNodeId { get; } = to; public double CapacityCubicMetersPerDay { get; } = capacity; public bool IsInService { get; set; } = service; }
    private sealed class SewerPipeState(SewerPipeId id, SewerNodeId from, SewerNodeId to, double capacity, bool service) { public SewerPipeId Id { get; } = id; public SewerNodeId FromNodeId { get; } = from; public SewerNodeId ToNodeId { get; } = to; public double CapacityCubicMetersPerDay { get; } = capacity; public bool IsInService { get; set; } = service; }
    private sealed class WaterSourceStateData(WaterSourceId id, WaterNodeId node, double capacity, UtilityOperatingState state) { public WaterSourceId Id { get; } = id; public WaterNodeId NodeId { get; } = node; public double CapacityCubicMetersPerDay { get; } = capacity; public double OutputCubicMetersPerDay { get; set; } public UtilityOperatingState OperatingState { get; set; } = state; }
    private sealed class ReservoirStateData(ReservoirId id, WaterNodeId node, double capacity, UtilityOperatingState state) { public ReservoirId Id { get; } = id; public WaterNodeId NodeId { get; } = node; public double ReleaseCapacityCubicMetersPerDay { get; } = capacity; public double OutputCubicMetersPerDay { get; set; } public UtilityOperatingState OperatingState { get; set; } = state; }
    private sealed class PumpStateData(PumpId id, PumpNetworkKind kind, WaterNodeId? waterNode, SewerNodeId? sewerNode, PowerLoadId? powerLoad, double capacity, UtilityOperatingState state) { public PumpId Id { get; } = id; public PumpNetworkKind NetworkKind { get; } = kind; public WaterNodeId? WaterNodeId { get; } = waterNode; public SewerNodeId? SewerNodeId { get; } = sewerNode; public PowerLoadId? PowerLoadId { get; } = powerLoad; public double CapacityCubicMetersPerDay { get; } = capacity; public double ThroughputCubicMetersPerDay { get; set; } public UtilityOperatingState OperatingState { get; set; } = state; }
    private sealed class SewageTreatmentPlantStateData(SewageTreatmentPlantId id, SewerNodeId node, PowerLoadId? powerLoad, double capacity, UtilityOperatingState state) { public SewageTreatmentPlantId Id { get; } = id; public SewerNodeId NodeId { get; } = node; public PowerLoadId? PowerLoadId { get; } = powerLoad; public double CapacityCubicMetersPerDay { get; } = capacity; public double ProcessedCubicMetersPerDay { get; set; } public UtilityOperatingState OperatingState { get; set; } = state; }
    private sealed class WaterSewerServicePointStateData(WaterSewerServicePointId id, WaterNodeId waterNode, SewerNodeId sewerNode, BuildingId? building, EstablishmentId? establishment, double baseDemand, double returnRatio) { public WaterSewerServicePointId Id { get; } = id; public WaterNodeId WaterNodeId { get; } = waterNode; public SewerNodeId SewerNodeId { get; } = sewerNode; public BuildingId? BuildingId { get; } = building; public EstablishmentId? EstablishmentId { get; } = establishment; public double BaseWaterDemandCubicMetersPerDay { get; } = baseDemand; public double WastewaterReturnRatio { get; } = returnRatio; public double WaterDemandCubicMetersPerDay { get; set; } public double WaterServedCubicMetersPerDay { get; set; } public double WaterUnservedCubicMetersPerDay { get; set; } public WaterServiceState WaterState { get; set; } = WaterServiceState.Supplied; public double WastewaterGeneratedCubicMetersPerDay { get; set; } public double WastewaterProcessedCubicMetersPerDay { get; set; } public double WastewaterOverflowCubicMetersPerDay { get; set; } public SewerServiceState SewerState { get; set; } = SewerServiceState.Available; }
}
