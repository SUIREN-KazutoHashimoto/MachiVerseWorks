namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly List<OpticalNodeState> _opticalNodes = [];
    private readonly Dictionary<OpticalNodeId, OpticalNodeState> _opticalNodeIndex = [];
    private readonly List<FiberCableState> _fiberCables = [];
    private readonly Dictionary<FiberCableId, FiberCableState> _fiberCableIndex = [];
    private readonly List<OpticalEquipmentState> _opticalEquipment = [];
    private readonly Dictionary<OpticalEquipmentId, OpticalEquipmentState> _opticalEquipmentIndex = [];
    private readonly List<OpticalBackhaulState> _opticalBackhauls = [];
    private readonly Dictionary<OpticalBackhaulId, OpticalBackhaulState> _opticalBackhaulIndex = [];
    private readonly List<OpticalDemandState> _opticalDemands = [];
    private readonly Dictionary<OpticalDemandId, OpticalDemandState> _opticalDemandIndex = [];
    private readonly IOpticalRoutingSolver _opticalRoutingSolver;
    private ulong _nextOpticalNodeId = 1;
    private ulong _nextFiberCableId = 1;
    private ulong _nextOpticalEquipmentId = 1;
    private ulong _nextOpticalBackhaulId = 1;
    private ulong _nextOpticalDemandId = 1;

    public int OpticalNodeCount => _opticalNodes.Count;
    public int FiberCableCount => _fiberCables.Count;
    public int OpticalEquipmentCount => _opticalEquipment.Count;
    public int OpticalBackhaulCount => _opticalBackhauls.Count;
    public int OpticalDemandCount => _opticalDemands.Count;

    public OpticalNodeId CreateOpticalNode(WorldPoint position, OpticalNodeKind kind = OpticalNodeKind.Distribution)
    {
        ValidatePoint(position);
        ValidateOpticalEnum(kind, nameof(kind));
        EnsureOpticalIdCapacity(_nextOpticalNodeId, "Optical node");
        var id = new OpticalNodeId(_nextOpticalNodeId++);
        var state = new OpticalNodeState(id, kind, position);
        _opticalNodes.Add(state);
        _opticalNodeIndex.Add(id, state);
        return id;
    }

    public FiberCableId CreateFiberCable(
        OpticalNodeId fromNodeId,
        OpticalNodeId toNodeId,
        double capacityGigabitsPerSecond,
        bool isInService = true)
    {
        if (!_opticalNodeIndex.ContainsKey(fromNodeId) || !_opticalNodeIndex.ContainsKey(toNodeId) || fromNodeId == toNodeId)
            throw new ArgumentException("Fiber cable references invalid Optical nodes.");
        ValidateOpticalPositiveFinite(capacityGigabitsPerSecond, nameof(capacityGigabitsPerSecond));
        EnsureOpticalIdCapacity(_nextFiberCableId, "Fiber cable");
        var id = new FiberCableId(_nextFiberCableId++);
        var state = new FiberCableState(id, fromNodeId, toNodeId, capacityGigabitsPerSecond, isInService);
        _fiberCables.Add(state);
        _fiberCableIndex.Add(id, state);
        return id;
    }

    public OpticalEquipmentId CreateOpticalEquipment(
        OpticalNodeId nodeId,
        OpticalEquipmentKind kind,
        double capacityGigabitsPerSecond,
        BuildingId? buildingId = null,
        EstablishmentId? establishmentId = null,
        bool requiresPower = true,
        bool isInService = true)
    {
        if (!_opticalNodeIndex.ContainsKey(nodeId))
            throw new ArgumentException($"Optical node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidateOpticalEnum(kind, nameof(kind));
        ValidateOpticalPositiveFinite(capacityGigabitsPerSecond, nameof(capacityGigabitsPerSecond));
        ValidateOpticalConsumerReferences(ref buildingId, establishmentId, allowEmpty: true);
        EnsureOpticalIdCapacity(_nextOpticalEquipmentId, "Optical equipment");
        var id = new OpticalEquipmentId(_nextOpticalEquipmentId++);
        var state = new OpticalEquipmentState(
            id,
            nodeId,
            kind,
            buildingId,
            establishmentId,
            capacityGigabitsPerSecond,
            requiresPower,
            isInService);
        _opticalEquipment.Add(state);
        _opticalEquipmentIndex.Add(id, state);
        return id;
    }

    public OpticalBackhaulId CreateOpticalBackhaul(
        OpticalNodeId nodeId,
        double capacityGigabitsPerSecond,
        bool isInService = true)
    {
        if (!_opticalNodeIndex.ContainsKey(nodeId))
            throw new ArgumentException($"Optical node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidateOpticalPositiveFinite(capacityGigabitsPerSecond, nameof(capacityGigabitsPerSecond));
        EnsureOpticalIdCapacity(_nextOpticalBackhaulId, "Optical backhaul");
        var id = new OpticalBackhaulId(_nextOpticalBackhaulId++);
        var state = new OpticalBackhaulState(id, nodeId, capacityGigabitsPerSecond, isInService);
        _opticalBackhauls.Add(state);
        _opticalBackhaulIndex.Add(id, state);
        return id;
    }

    public OpticalDemandId CreateBuildingOpticalDemand(
        OpticalNodeId nodeId,
        BuildingId buildingId,
        double baseDemandGigabitsPerSecond) =>
        CreateOpticalDemandCore(nodeId, OpticalDemandKind.Building, buildingId, null, baseDemandGigabitsPerSecond);

    public OpticalDemandId CreateOfficeOpticalDemand(
        OpticalNodeId nodeId,
        EstablishmentId establishmentId,
        double baseDemandGigabitsPerSecond) =>
        CreateOpticalDemandCore(nodeId, OpticalDemandKind.Office, null, establishmentId, baseDemandGigabitsPerSecond);

    public OpticalDemandId CreateDataCenterOpticalDemand(
        OpticalNodeId nodeId,
        BuildingId buildingId,
        double baseDemandGigabitsPerSecond,
        EstablishmentId? establishmentId = null) =>
        CreateOpticalDemandCore(nodeId, OpticalDemandKind.DataCenter, buildingId, establishmentId, baseDemandGigabitsPerSecond);

    public OpticalDemandId CreateRadioBackhaulDemand(
        OpticalNodeId nodeId,
        double baseDemandGigabitsPerSecond) =>
        CreateOpticalDemandCore(nodeId, OpticalDemandKind.RadioBackhaul, null, null, baseDemandGigabitsPerSecond);

    public void SetFiberCableInService(FiberCableId id, bool isInService)
    {
        if (!_fiberCableIndex.TryGetValue(id, out var cable))
            throw new ArgumentException($"Fiber cable {id.Value} does not exist.", nameof(id));
        cable.IsInService = isInService;
    }

    public void SetOpticalEquipmentInService(OpticalEquipmentId id, bool isInService)
    {
        if (!_opticalEquipmentIndex.TryGetValue(id, out var equipment))
            throw new ArgumentException($"Optical equipment {id.Value} does not exist.", nameof(id));
        equipment.IsInService = isInService;
    }

    public void SetOpticalBackhaulInService(OpticalBackhaulId id, bool isInService)
    {
        if (!_opticalBackhaulIndex.TryGetValue(id, out var backhaul))
            throw new ArgumentException($"Optical backhaul {id.Value} does not exist.", nameof(id));
        backhaul.IsInService = isInService;
    }

    public bool TryGetOpticalDemandSnapshot(OpticalDemandId id, out OpticalDemandSnapshot snapshot)
    {
        if (_opticalDemandIndex.TryGetValue(id, out var demand))
        {
            snapshot = CreateOpticalDemandSnapshot(demand);
            return true;
        }
        snapshot = default;
        return false;
    }

    public OpticalNodeSnapshot[] QueryOpticalNodes(WorldVolume volume) =>
        _opticalNodes
            .Where(item => volume.Contains(item.Position))
            .OrderBy(static item => item.Id.Value)
            .Select(static item => new OpticalNodeSnapshot(item.Id, item.Kind, item.Position))
            .ToArray();

    public OpticalSnapshot CreateOpticalSnapshot() => new(
        CreateOpticalStatistics(),
        _opticalNodes.OrderBy(static item => item.Id.Value)
            .Select(static item => new OpticalNodeSnapshot(item.Id, item.Kind, item.Position)).ToArray(),
        _fiberCables.OrderBy(static item => item.Id.Value).Select(CreateFiberCableSnapshot).ToArray(),
        _opticalEquipment.OrderBy(static item => item.Id.Value).Select(CreateOpticalEquipmentSnapshot).ToArray(),
        _opticalBackhauls.OrderBy(static item => item.Id.Value).Select(CreateOpticalBackhaulSnapshot).ToArray(),
        _opticalDemands.OrderBy(static item => item.Id.Value).Select(CreateOpticalDemandSnapshot).ToArray());

    public OpticalStatistics CreateOpticalStatistics()
    {
        var allocated = SimulationNumeric.SaturatingDoubleSum(_opticalDemands, static item => item.AllocatedGigabitsPerSecond);
        var peakFiberUtilization = _fiberCables.Count == 0
            ? 0d
            : _fiberCables.Max(static item => CalculateOpticalUtilization(item.LoadGigabitsPerSecond, item.CapacityGigabitsPerSecond));
        return new OpticalStatistics(
            _opticalNodes.Count,
            _fiberCables.Count,
            _opticalEquipment.Count,
            _opticalBackhauls.Count,
            _opticalDemands.Count,
            _opticalDemands.Count(static item => item.AllocatedGigabitsPerSecond > OpticalDefaults.BandwidthEpsilonGigabitsPerSecond),
            _opticalDemands.Count(static item => item.QualityState == OpticalQualityState.Congested),
            _opticalDemands.Count(static item => item.QualityState == OpticalQualityState.Degraded),
            _opticalDemands.Count(static item => item.QualityState == OpticalQualityState.Unavailable),
            SimulationNumeric.SaturatingDoubleSum(_opticalBackhauls.Where(static item => item.IsInService), static item => item.CapacityGigabitsPerSecond),
            SimulationNumeric.SaturatingDoubleSum(_opticalDemands, static item => item.DemandGigabitsPerSecond),
            allocated,
            peakFiberUtilization,
            Time.TickCount);
    }

    private void StepOptical(SimulationTime nextTime)
    {
        var calculatedDemands = _opticalDemands
            .Select(demand => (DemandState: demand, Value: CalculateOpticalDemand(demand, nextTime)))
            .ToArray();
        foreach (var equipment in _opticalEquipment)
            equipment.IsPowered = IsOpticalEquipmentPowered(equipment);
        foreach (var item in calculatedDemands) item.DemandState.DemandGigabitsPerSecond = item.Value;

        var equipmentByNode = _opticalEquipment
            .GroupBy(static item => item.NodeId)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var nodes = _opticalNodes
            .OrderBy(static item => item.Id.Value)
            .Select(item => new OpticalRoutingNode(item.Id, IsOpticalNodeAvailable(item.Id, equipmentByNode)))
            .ToArray();
        var endpointCapacity = _opticalDemands
            .Select(static item => item.NodeId)
            .Distinct()
            .OrderBy(static item => item.Value)
            .Select(nodeId => new OpticalRoutingEndpoint(
                nodeId,
                CalculateEndpointCapacity(nodeId, equipmentByNode),
                CalculateEndpointCapacity(nodeId, equipmentByNode) > OpticalDefaults.BandwidthEpsilonGigabitsPerSecond))
            .ToArray();
        var backhauls = _opticalBackhauls
            .OrderBy(static item => item.Id.Value)
            .Select(item =>
            {
                var equipmentCapacity = CalculateBackhaulEquipmentCapacity(item.NodeId, equipmentByNode);
                var capacity = Math.Min(item.CapacityGigabitsPerSecond, equipmentCapacity);
                return new OpticalRoutingBackhaul(
                    item.Id,
                    item.NodeId,
                    Math.Max(OpticalDefaults.BandwidthEpsilonGigabitsPerSecond, capacity),
                    item.IsInService && capacity > OpticalDefaults.BandwidthEpsilonGigabitsPerSecond);
            })
            .ToArray();
        var request = new OpticalRoutingRequest(
            nodes,
            _fiberCables.OrderBy(static item => item.Id.Value)
                .Select(static item => new OpticalRoutingCable(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityGigabitsPerSecond, item.IsInService))
                .ToArray(),
            endpointCapacity,
            backhauls,
            _opticalDemands.OrderBy(static item => item.Id.Value)
                .Select(static item => new OpticalRoutingDemand(item.Id, item.NodeId, item.DemandGigabitsPerSecond, GetOpticalDemandPriority(item.Kind)))
                .ToArray());
        var result = _opticalRoutingSolver.Solve(request)
            ?? throw new InvalidOperationException("Optical routing solver returned no result.");
        ApplyOpticalRoutingResult(result);
    }

    private void ApplyOpticalRoutingResult(OpticalRoutingResult result)
    {
        ArgumentNullException.ThrowIfNull(result.Demands);
        ArgumentNullException.ThrowIfNull(result.FiberCables);
        ArgumentNullException.ThrowIfNull(result.Backhauls);
        var routes = new Dictionary<OpticalDemandId, OpticalDemandRouteResult>();
        foreach (var route in result.Demands)
        {
            if (!_opticalDemandIndex.ContainsKey(route.DemandId) || !routes.TryAdd(route.DemandId, route))
                throw new InvalidOperationException("Optical routing solver returned an unknown or duplicate Demand.");
            if (route.RouteCableIds is null) throw new InvalidOperationException("Optical routing solver returned a null route.");
        }
        var cableLoads = new Dictionary<FiberCableId, double>();
        foreach (var load in result.FiberCables)
        {
            if (!_fiberCableIndex.TryGetValue(load.FiberCableId, out var cable) || !cableLoads.TryAdd(load.FiberCableId, load.LoadGigabitsPerSecond))
                throw new InvalidOperationException("Optical routing solver returned an unknown or duplicate FiberCable.");
            if (!double.IsFinite(load.LoadGigabitsPerSecond) || load.LoadGigabitsPerSecond < 0d || load.LoadGigabitsPerSecond > cable.CapacityGigabitsPerSecond + OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
                throw new InvalidOperationException($"Optical routing solver returned invalid load for FiberCable {load.FiberCableId.Value}.");
        }
        var backhaulLoads = new Dictionary<OpticalBackhaulId, double>();
        foreach (var load in result.Backhauls)
        {
            if (!_opticalBackhaulIndex.TryGetValue(load.BackhaulId, out var backhaul) || !backhaulLoads.TryAdd(load.BackhaulId, load.AllocatedGigabitsPerSecond))
                throw new InvalidOperationException("Optical routing solver returned an unknown or duplicate Backhaul.");
            if (!double.IsFinite(load.AllocatedGigabitsPerSecond) || load.AllocatedGigabitsPerSecond < 0d || load.AllocatedGigabitsPerSecond > backhaul.CapacityGigabitsPerSecond + OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
                throw new InvalidOperationException($"Optical routing solver returned invalid load for Backhaul {load.BackhaulId.Value}.");
        }
        foreach (var demand in _opticalDemands)
        {
            if (!routes.TryGetValue(demand.Id, out var route)) continue;
            if (!double.IsFinite(route.AllocatedGigabitsPerSecond) || route.AllocatedGigabitsPerSecond < 0d || route.AllocatedGigabitsPerSecond > demand.DemandGigabitsPerSecond + OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
                throw new InvalidOperationException($"Optical routing solver returned invalid allocation for Demand {demand.Id.Value}.");
            if (!IsValidOpticalRouteTopology(
                    demand.NodeId,
                    route.BackhaulId,
                    route.AllocatedGigabitsPerSecond,
                    route.RouteCableIds,
                    id => _opticalBackhaulIndex.TryGetValue(id, out var backhaul) ? backhaul.NodeId : null,
                    id => _fiberCableIndex.TryGetValue(id, out var cable)
                        ? new OpticalRouteCableValidation(cable.FromNodeId, cable.ToNodeId, cable.IsInService)
                        : null,
                    requireInService: true))
                throw new InvalidOperationException($"Optical routing solver returned a disconnected or inconsistent route for Demand {demand.Id.Value}.");
        }

        foreach (var cable in _fiberCables)
        {
            var load = cableLoads.GetValueOrDefault(cable.Id);
            cable.LoadGigabitsPerSecond = cable.IsInService ? Math.Min(cable.CapacityGigabitsPerSecond, load) : 0d;
        }
        foreach (var backhaul in _opticalBackhauls)
        {
            var load = backhaulLoads.GetValueOrDefault(backhaul.Id);
            backhaul.AllocatedGigabitsPerSecond = backhaul.IsInService ? Math.Min(backhaul.CapacityGigabitsPerSecond, load) : 0d;
        }
        foreach (var demand in _opticalDemands)
        {
            if (!routes.TryGetValue(demand.Id, out var route)) route = new OpticalDemandRouteResult(demand.Id, null, 0d, Array.Empty<FiberCableId>());
            demand.AllocatedGigabitsPerSecond = Math.Min(demand.DemandGigabitsPerSecond, route.AllocatedGigabitsPerSecond);
            demand.BackhaulId = demand.AllocatedGigabitsPerSecond > OpticalDefaults.BandwidthEpsilonGigabitsPerSecond ? route.BackhaulId : null;
            demand.RouteCableIds = demand.AllocatedGigabitsPerSecond > OpticalDefaults.BandwidthEpsilonGigabitsPerSecond ? route.RouteCableIds.ToArray() : Array.Empty<FiberCableId>();
        }
        foreach (var demand in _opticalDemands) demand.QualityState = CalculateOpticalQuality(demand);
    }

    private static bool IsValidOpticalRouteTopology(
        OpticalNodeId demandNodeId,
        OpticalBackhaulId? backhaulId,
        double allocatedGigabitsPerSecond,
        IReadOnlyList<FiberCableId>? routeCableIds,
        Func<OpticalBackhaulId, OpticalNodeId?> resolveBackhaulNode,
        Func<FiberCableId, OpticalRouteCableValidation?> resolveCable,
        bool requireInService)
    {
        if (routeCableIds is null) return false;
        if (allocatedGigabitsPerSecond <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
            return backhaulId is null && routeCableIds.Count == 0;
        if (backhaulId is not { } selectedBackhaul || resolveBackhaulNode(selectedBackhaul) is not { } cursor)
            return false;

        var seen = new HashSet<FiberCableId>();
        foreach (var cableId in routeCableIds)
        {
            if (!seen.Add(cableId) || resolveCable(cableId) is not { } cable || (requireInService && !cable.IsInService))
                return false;
            if (cable.FromNodeId == cursor) cursor = cable.ToNodeId;
            else if (cable.ToNodeId == cursor) cursor = cable.FromNodeId;
            else return false;
        }
        return cursor == demandNodeId;
    }

    private readonly record struct OpticalRouteCableValidation(
        OpticalNodeId FromNodeId,
        OpticalNodeId ToNodeId,
        bool IsInService);

    private OpticalQualityState CalculateOpticalQuality(OpticalDemandState demand)
    {
        if (demand.AllocatedGigabitsPerSecond <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
            return OpticalQualityState.Unavailable;
        if (demand.AllocatedGigabitsPerSecond + OpticalDefaults.BandwidthEpsilonGigabitsPerSecond < demand.DemandGigabitsPerSecond)
            return OpticalQualityState.Degraded;

        var utilization = 0d;
        foreach (var cableId in demand.RouteCableIds)
        {
            var cable = _fiberCableIndex[cableId];
            utilization = Math.Max(utilization, CalculateOpticalUtilization(cable.LoadGigabitsPerSecond, cable.CapacityGigabitsPerSecond));
        }
        if (demand.BackhaulId is { } backhaulId)
        {
            var backhaul = _opticalBackhaulIndex[backhaulId];
            utilization = Math.Max(utilization, CalculateOpticalUtilization(backhaul.AllocatedGigabitsPerSecond, backhaul.CapacityGigabitsPerSecond));
        }
        return utilization >= OpticalDefaults.CongestionThreshold
            ? OpticalQualityState.Congested
            : OpticalQualityState.Healthy;
    }

    private bool IsOpticalEquipmentPowered(OpticalEquipmentState equipment)
    {
        if (!equipment.RequiresPower) return true;
        if (equipment.EstablishmentId is { } establishmentId) return IsEstablishmentPowered(establishmentId);
        if (equipment.BuildingId is { } buildingId) return IsBuildingPowered(buildingId);
        return true;
    }

    private static bool IsOpticalNodeAvailable(
        OpticalNodeId nodeId,
        IReadOnlyDictionary<OpticalNodeId, OpticalEquipmentState[]> equipmentByNode)
    {
        if (!equipmentByNode.TryGetValue(nodeId, out var equipment) || equipment.Length == 0) return true;
        return equipment.Any(static item => item.IsInService && item.IsPowered);
    }

    private static double CalculateEndpointCapacity(
        OpticalNodeId nodeId,
        IReadOnlyDictionary<OpticalNodeId, OpticalEquipmentState[]> equipmentByNode)
    {
        if (!equipmentByNode.TryGetValue(nodeId, out var equipment)) return 0d;
        return SimulationNumeric.SaturatingDoubleSum(
            equipment.Where(static item => item.IsInService && item.IsPowered && IsEndpointEquipment(item.Kind)),
            static item => item.CapacityGigabitsPerSecond);
    }

    private static double CalculateBackhaulEquipmentCapacity(
        OpticalNodeId nodeId,
        IReadOnlyDictionary<OpticalNodeId, OpticalEquipmentState[]> equipmentByNode)
    {
        if (!equipmentByNode.TryGetValue(nodeId, out var equipment)) return 0d;
        return SimulationNumeric.SaturatingDoubleSum(
            equipment.Where(static item => item.IsInService && item.IsPowered && IsBackhaulEquipment(item.Kind)),
            static item => item.CapacityGigabitsPerSecond);
    }

    private static bool IsEndpointEquipment(OpticalEquipmentKind kind) =>
        kind is OpticalEquipmentKind.Onu or OpticalEquipmentKind.Switch or OpticalEquipmentKind.Router;

    private static bool IsBackhaulEquipment(OpticalEquipmentKind kind) =>
        kind is OpticalEquipmentKind.Olt or OpticalEquipmentKind.Router;

    private double CalculateOpticalDemand(OpticalDemandState demand, SimulationTime time)
    {
        var hour = time.Elapsed.TotalHours % 24d;
        if (hour < 0d) hour += 24d;
        var timeFactor = demand.Kind switch
        {
            OpticalDemandKind.Building => hour is >= 18d or < 8d ? 1.1d : 0.85d,
            OpticalDemandKind.Office => hour is >= 8d and < 19d ? 1.15d : 0.45d,
            OpticalDemandKind.DataCenter => 1d,
            OpticalDemandKind.RadioBackhaul => hour is >= 7d and < 23d ? 1.05d : 0.8d,
            _ => 1d,
        };
        var useFactor = 1d;
        if (demand.BuildingId is { } buildingId && TryGetBuildingSnapshot(buildingId, out var building))
        {
            useFactor *= building.Kind switch
            {
                BuildingKind.Residential => 0.8d,
                BuildingKind.Commercial => 1.15d,
                BuildingKind.Industrial => 1.05d,
                BuildingKind.Civic => 0.9d,
                BuildingKind.MixedUse => 1d,
                _ => 1d,
            };
        }
        if (demand.EstablishmentId is { } establishmentId
            && _economyEstablishmentIndex.TryGetValue(establishmentId, out var establishment)
            && _economyCompanyIndex.TryGetValue(establishment.CompanyId, out var company))
        {
            useFactor *= company.Sector switch
            {
                IndustrySector.Retail => 1.05d,
                IndustrySector.Services => 1.15d,
                IndustrySector.Manufacturing => 0.95d,
                IndustrySector.Transport => 1.1d,
                IndustrySector.Public => 1d,
                _ => 1d,
            };
        }
        if (demand.Kind == OpticalDemandKind.DataCenter) useFactor *= 1.25d;
        return SimulationNumeric.SaturatingMultiplyNonNegative(demand.BaseDemandGigabitsPerSecond, timeFactor, useFactor);
    }

    private OpticalDemandId CreateOpticalDemandCore(
        OpticalNodeId nodeId,
        OpticalDemandKind kind,
        BuildingId? buildingId,
        EstablishmentId? establishmentId,
        double baseDemandGigabitsPerSecond)
    {
        if (!_opticalNodeIndex.ContainsKey(nodeId))
            throw new ArgumentException($"Optical node {nodeId.Value} does not exist.", nameof(nodeId));
        ValidateOpticalEnum(kind, nameof(kind));
        ValidateOpticalPositiveFinite(baseDemandGigabitsPerSecond, nameof(baseDemandGigabitsPerSecond));
        ValidateOpticalConsumerReferences(ref buildingId, establishmentId, allowEmpty: kind == OpticalDemandKind.RadioBackhaul);
        EnsureOpticalIdCapacity(_nextOpticalDemandId, "Optical demand");
        var id = new OpticalDemandId(_nextOpticalDemandId++);
        var state = new OpticalDemandState(id, nodeId, kind, buildingId, establishmentId, baseDemandGigabitsPerSecond);
        _opticalDemands.Add(state);
        _opticalDemandIndex.Add(id, state);
        return id;
    }

    private void ValidateOpticalConsumerReferences(ref BuildingId? buildingId, EstablishmentId? establishmentId, bool allowEmpty)
    {
        if (!allowEmpty && buildingId is null && establishmentId is null)
            throw new ArgumentException("Optical consumer must reference a Building, an Establishment, or both.", nameof(buildingId));
        if (buildingId is { } explicitBuilding && !TryGetBuildingSnapshot(explicitBuilding, out _))
            throw new ArgumentException($"Building {explicitBuilding.Value} does not exist.", nameof(buildingId));
        if (establishmentId is not { } establishmentValue) return;
        if (!_economyEstablishmentIndex.TryGetValue(establishmentValue, out var establishment))
            throw new ArgumentException($"Establishment {establishmentValue.Value} does not exist.", nameof(establishmentId));
        if (buildingId is { } buildingValue
            && establishment.BuildingId is { } establishmentBuilding
            && buildingValue != establishmentBuilding)
            throw new ArgumentException("Establishment belongs to a different Building.", nameof(buildingId));
        buildingId ??= establishment.BuildingId;
    }

    private static byte GetOpticalDemandPriority(OpticalDemandKind kind) => kind switch
    {
        OpticalDemandKind.DataCenter => 4,
        OpticalDemandKind.RadioBackhaul => 3,
        OpticalDemandKind.Office => 2,
        OpticalDemandKind.Building => 1,
        _ => 0,
    };

    private static double CalculateOpticalUtilization(double load, double capacity) =>
        capacity <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond ? 0d : Math.Clamp(load / capacity, 0d, 1d);

    private FiberCableSnapshot CreateFiberCableSnapshot(FiberCableState item)
    {
        var utilization = CalculateOpticalUtilization(item.LoadGigabitsPerSecond, item.CapacityGigabitsPerSecond);
        return new FiberCableSnapshot(
            item.Id,
            item.FromNodeId,
            item.ToNodeId,
            item.CapacityGigabitsPerSecond,
            item.LoadGigabitsPerSecond,
            utilization,
            item.IsInService,
            item.IsInService && utilization >= OpticalDefaults.CongestionThreshold);
    }

    private OpticalEquipmentSnapshot CreateOpticalEquipmentSnapshot(OpticalEquipmentState item) => new(
        item.Id,
        item.NodeId,
        item.Kind,
        item.BuildingId,
        item.EstablishmentId,
        item.CapacityGigabitsPerSecond,
        item.RequiresPower,
        item.IsInService,
        item.IsPowered,
        item.IsInService && item.IsPowered);

    private OpticalBackhaulSnapshot CreateOpticalBackhaulSnapshot(OpticalBackhaulState item)
    {
        var equipment = _opticalEquipment.Where(candidate => candidate.NodeId == item.NodeId && IsBackhaulEquipment(candidate.Kind)).ToArray();
        var operational = item.IsInService && equipment.Any(static candidate => candidate.IsInService && candidate.IsPowered);
        return new OpticalBackhaulSnapshot(
            item.Id,
            item.NodeId,
            item.CapacityGigabitsPerSecond,
            item.AllocatedGigabitsPerSecond,
            CalculateOpticalUtilization(item.AllocatedGigabitsPerSecond, item.CapacityGigabitsPerSecond),
            item.IsInService,
            operational);
    }

    private OpticalDemandSnapshot CreateOpticalDemandSnapshot(OpticalDemandState item) => new(
        item.Id,
        item.NodeId,
        item.Kind,
        item.BuildingId,
        item.EstablishmentId,
        item.BaseDemandGigabitsPerSecond,
        item.DemandGigabitsPerSecond,
        item.AllocatedGigabitsPerSecond,
        item.QualityState,
        item.BackhaulId,
        item.RouteCableIds.ToArray());
}
