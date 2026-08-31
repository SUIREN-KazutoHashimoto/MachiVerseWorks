namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly List<LogisticsCommodityState> _logisticsCommodities = [];
    private readonly Dictionary<CommodityId, LogisticsCommodityState> _logisticsCommodityIndex = [];
    private readonly Dictionary<(EstablishmentId EstablishmentId, CommodityId CommodityId), LogisticsInventoryState> _logisticsInventories = [];
    private readonly List<LogisticsOrderStateData> _logisticsOrders = [];
    private readonly Dictionary<LogisticsOrderId, LogisticsOrderStateData> _logisticsOrderIndex = [];
    private readonly List<LogisticsShipmentStateData> _logisticsShipments = [];
    private readonly Dictionary<ShipmentId, LogisticsShipmentStateData> _logisticsShipmentIndex = [];
    private ulong _nextCommodityId = 1;
    private ulong _nextLogisticsOrderId = 1;
    private ulong _nextShipmentId = 1;
    private ulong _processedLogisticsCycle;
    private ulong _deliveredShipmentCount;

    public int CommodityCount => _logisticsCommodities.Count;
    public int InventoryCount => _logisticsInventories.Count;
    public int LogisticsOrderCount => _logisticsOrders.Count;
    public int ShipmentCount => _logisticsShipments.Count;

    public CommodityId CreateCommodity(CommodityKind kind = CommodityKind.GeneralGoods)
    {
        ValidateEnum(kind, nameof(kind));
        EnsureLogisticsIdCapacity(_nextCommodityId, "Commodity");
        var id = new CommodityId(_nextCommodityId++);
        var state = new LogisticsCommodityState(id, kind);
        _logisticsCommodities.Add(state);
        _logisticsCommodityIndex.Add(id, state);
        return id;
    }

    public void ConfigureInventory(
        EstablishmentId establishmentId,
        CommodityId commodityId,
        RoadAccessPointId roadAccessPointId,
        InventoryRole role,
        double capacity,
        double initialQuantity = 0d,
        double reorderPoint = 0d,
        double targetQuantity = 0d,
        double dailyConsumptionUnits = 0d)
    {
        if (!_economyEstablishmentIndex.TryGetValue(establishmentId, out var establishment))
            throw new ArgumentException($"Establishment {establishmentId.Value} does not exist.", nameof(establishmentId));
        if (!_logisticsCommodityIndex.ContainsKey(commodityId))
            throw new ArgumentException($"Commodity {commodityId.Value} does not exist.", nameof(commodityId));
        ValidateEnum(role, nameof(role));
        ValidateNonNegativeFinite(capacity, nameof(capacity));
        ValidateNonNegativeFinite(initialQuantity, nameof(initialQuantity));
        ValidateNonNegativeFinite(reorderPoint, nameof(reorderPoint));
        ValidateNonNegativeFinite(targetQuantity, nameof(targetQuantity));
        ValidateNonNegativeFinite(dailyConsumptionUnits, nameof(dailyConsumptionUnits));
        if (capacity <= 0d) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Inventory capacity must be greater than zero.");
        if (initialQuantity > capacity || reorderPoint > capacity || targetQuantity > capacity)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Inventory quantity thresholds must not exceed capacity.");
        if (targetQuantity < reorderPoint)
            throw new ArgumentException("Inventory target quantity must be greater than or equal to the reorder point.", nameof(targetQuantity));
        if (!TryGetRoadAccessPointSnapshot(roadAccessPointId, out var accessPoint) || (accessPoint.Mode & RoadAccessMode.Motor) == 0)
            throw new ArgumentException($"Road access point {roadAccessPointId.Value} does not exist or is not motor-accessible.", nameof(roadAccessPointId));
        if (!AccessPointMatchesEstablishment(accessPoint, establishment))
            throw new ArgumentException($"Road access point {roadAccessPointId.Value} is not linked to Establishment {establishmentId.Value}.", nameof(roadAccessPointId));

        var key = (establishmentId, commodityId);
        var observedProduction = TryGetEstablishmentCompany(establishment, out var company) ? company.ProducedUnits : 0d;
        _logisticsInventories[key] = new LogisticsInventoryState(
            establishmentId,
            commodityId,
            role,
            initialQuantity,
            capacity,
            reorderPoint,
            targetQuantity,
            dailyConsumptionUnits,
            roadAccessPointId,
            observedProduction);
    }

    public bool TryGetInventorySnapshot(EstablishmentId establishmentId, CommodityId commodityId, out InventorySnapshot snapshot)
    {
        if (_logisticsInventories.TryGetValue((establishmentId, commodityId), out var state))
        {
            snapshot = CreateInventorySnapshot(state);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetShipmentSnapshot(ShipmentId id, out ShipmentSnapshot snapshot)
    {
        if (_logisticsShipmentIndex.TryGetValue(id, out var state))
        {
            snapshot = CreateShipmentSnapshot(state, Time.TickCount);
            return true;
        }
        snapshot = default;
        return false;
    }

    public LogisticsSnapshot CreateLogisticsSnapshot()
    {
        var commodities = _logisticsCommodities.OrderBy(static item => item.Id.Value)
            .Select(static item => new CommoditySnapshot(item.Id, item.Kind)).ToArray();
        var inventories = _logisticsInventories.Values
            .OrderBy(static item => item.EstablishmentId.Value).ThenBy(static item => item.CommodityId.Value)
            .Select(CreateInventorySnapshot).ToArray();
        var orders = _logisticsOrders.OrderBy(static item => item.Id.Value).Select(static item => new LogisticsOrderSnapshot(
            item.Id, item.DestinationEstablishmentId, item.CommodityId, item.Quantity, item.State, item.CreatedTick, item.ShipmentId)).ToArray();
        var shipments = _logisticsShipments.OrderBy(static item => item.Id.Value)
            .Select(item => CreateShipmentSnapshot(item, Time.TickCount)).ToArray();
        return new LogisticsSnapshot(CreateLogisticsStatistics(), commodities, inventories, orders, shipments);
    }

    public LogisticsStatistics CreateLogisticsStatistics()
    {
        var openOrders = 0;
        var inTransit = 0;
        var delayed = 0;
        var inventoryUnits = 0d;
        var inTransitUnits = 0d;
        foreach (var order in _logisticsOrders)
            if (order.State != LogisticsOrderState.Completed) openOrders++;
        foreach (var inventory in _logisticsInventories.Values) inventoryUnits += inventory.Quantity;
        foreach (var shipment in _logisticsShipments)
        {
            if (shipment.State is ShipmentState.Pickup or ShipmentState.Loading or ShipmentState.InTransit or ShipmentState.Unloading)
            {
                inTransit++;
                inTransitUnits += shipment.Quantity;
                if (shipment.PlannedDeliveryTick != 0 && Time.TickCount > shipment.PlannedDeliveryTick) delayed++;
            }
        }
        return new LogisticsStatistics(
            _logisticsCommodities.Count,
            _logisticsInventories.Count,
            openOrders,
            _logisticsShipments.Count,
            inTransit,
            delayed,
            inventoryUnits,
            inTransitUnits,
            _deliveredShipmentCount,
            _processedLogisticsCycle,
            Time.TickCount);
    }

    private void StepLogistics(SimulationTime nextTime)
    {
        while (_processedLogisticsCycle < _processedEconomicCycle)
        {
            ProcessLogisticsCycle(nextTime.TickCount);
            _processedLogisticsCycle++;
        }
        AdvanceShipments(nextTime.TickCount);
        AllocateOpenOrders(nextTime.TickCount);
    }

    private void ProcessLogisticsCycle(ulong tickCount)
    {
        ReceiveEconomicProduction();
        ConsumeInventory();
        GenerateReplenishmentOrders(tickCount);
    }

    private void ReceiveEconomicProduction()
    {
        foreach (var inventory in _logisticsInventories.Values
                     .Where(static item => item.Role == InventoryRole.Supplier)
                     .OrderBy(static item => item.EstablishmentId.Value).ThenBy(static item => item.CommodityId.Value))
        {
            if (!_economyEstablishmentIndex.TryGetValue(inventory.EstablishmentId, out var establishment)
                || !TryGetEstablishmentCompany(establishment, out var company)) continue;
            var producedDelta = Math.Max(0d, company.ProducedUnits - inventory.ObservedCompanyProducedUnits);
            inventory.ObservedCompanyProducedUnits = company.ProducedUnits;
            if (producedDelta <= 0d) continue;
            inventory.Quantity = Math.Min(inventory.Capacity, inventory.Quantity + producedDelta);
        }
    }

    private void ConsumeInventory()
    {
        foreach (var inventory in _logisticsInventories.Values
                     .Where(static item => item.Role == InventoryRole.Consumer)
                     .OrderBy(static item => item.EstablishmentId.Value).ThenBy(static item => item.CommodityId.Value))
        {
            inventory.Quantity = Math.Max(0d, inventory.Quantity - inventory.DailyConsumptionUnits);
        }
    }

    private void GenerateReplenishmentOrders(ulong tickCount)
    {
        foreach (var inventory in _logisticsInventories.Values
                     .Where(static item => item.Role == InventoryRole.Consumer)
                     .OrderBy(static item => item.EstablishmentId.Value).ThenBy(static item => item.CommodityId.Value))
        {
            if (inventory.Quantity > inventory.ReorderPoint) continue;
            if (_logisticsOrders.Any(item => item.DestinationEstablishmentId == inventory.EstablishmentId
                && item.CommodityId == inventory.CommodityId && item.State != LogisticsOrderState.Completed)) continue;
            var quantity = inventory.TargetQuantity - inventory.Quantity;
            if (quantity < LogisticsDefaults.MinimumOrderQuantity) continue;
            EnsureLogisticsIdCapacity(_nextLogisticsOrderId, "Logistics order");
            var id = new LogisticsOrderId(_nextLogisticsOrderId++);
            var order = new LogisticsOrderStateData(id, inventory.EstablishmentId, inventory.CommodityId, quantity, tickCount);
            _logisticsOrders.Add(order);
            _logisticsOrderIndex.Add(id, order);
        }
    }

    private void AllocateOpenOrders(ulong tickCount)
    {
        foreach (var order in _logisticsOrders.Where(static item => item.State == LogisticsOrderState.Open).OrderBy(static item => item.Id.Value))
        {
            var supplier = _logisticsInventories.Values
                .Where(item => item.Role == InventoryRole.Supplier
                    && item.CommodityId == order.CommodityId
                    && item.EstablishmentId != order.DestinationEstablishmentId
                    && item.Quantity >= order.Quantity)
                .OrderBy(static item => item.EstablishmentId.Value)
                .FirstOrDefault();
            if (supplier is null) continue;
            if (!_logisticsInventories.TryGetValue((order.DestinationEstablishmentId, order.CommodityId), out var destination)) continue;

            supplier.Quantity -= order.Quantity;
            EnsureLogisticsIdCapacity(_nextShipmentId, "Shipment");
            var shipmentId = new ShipmentId(_nextShipmentId++);
            var shipment = new LogisticsShipmentStateData(
                shipmentId,
                order.Id,
                supplier.EstablishmentId,
                order.DestinationEstablishmentId,
                order.CommodityId,
                order.Quantity,
                supplier.RoadAccessPointId,
                destination.RoadAccessPointId,
                tickCount,
                checked(tickCount + LogisticsDefaults.LoadingTicks));
            _logisticsShipments.Add(shipment);
            _logisticsShipmentIndex.Add(shipmentId, shipment);
            order.State = LogisticsOrderState.Allocated;
            order.ShipmentId = shipmentId;
        }
    }

    private void AdvanceShipments(ulong tickCount)
    {
        foreach (var shipment in _logisticsShipments.Where(static item => item.State != ShipmentState.Delivered).OrderBy(static item => item.Id.Value))
        {
            if (shipment.State == ShipmentState.Pickup)
            {
                shipment.State = ShipmentState.Loading;
                continue;
            }

            if (shipment.State == ShipmentState.Loading && tickCount >= shipment.LoadingCompleteTick)
            {
                if (!TryCreateFreightRoute(shipment.PickupAccessPointId, shipment.DeliveryAccessPointId, out var route)) continue;
                shipment.VehicleId = CreateVehicle(
                    route,
                    LogisticsDefaults.FreightVehicleDimensions,
                    LogisticsDefaults.FreightVehiclePerformance,
                    initialSpeedMetersPerSecond: 0d);
                shipment.State = ShipmentState.InTransit;
                shipment.DispatchedTick = tickCount;
                var travelTicks = Math.Max(1UL, checked((ulong)Math.Ceiling(route.EstimatedTravelTimeSeconds * Config.TickRate)));
                shipment.PlannedDeliveryTick = checked(tickCount + travelTicks + LogisticsDefaults.UnloadingTicks);
                continue;
            }

            if (shipment.State == ShipmentState.InTransit && shipment.VehicleId is { } vehicleId
                && TryGetVehicleSnapshot(vehicleId, out var vehicle) && vehicle.State == VehicleMovementState.Arrived)
            {
                shipment.State = ShipmentState.Unloading;
                shipment.UnloadingCompleteTick = checked(tickCount + LogisticsDefaults.UnloadingTicks);
                continue;
            }

            if (shipment.State == ShipmentState.Unloading && tickCount >= shipment.UnloadingCompleteTick)
            {
                if (_logisticsInventories.TryGetValue((shipment.DestinationEstablishmentId, shipment.CommodityId), out var destination))
                    destination.Quantity = Math.Min(destination.Capacity, destination.Quantity + shipment.Quantity);
                shipment.State = ShipmentState.Delivered;
                shipment.DeliveredTick = tickCount;
                _deliveredShipmentCount = checked(_deliveredShipmentCount + 1);
                if (_logisticsOrderIndex.TryGetValue(shipment.OrderId, out var order)) order.State = LogisticsOrderState.Completed;
            }
        }
    }

    private bool TryCreateFreightRoute(RoadAccessPointId pickupAccessPointId, RoadAccessPointId deliveryAccessPointId, out RouteResult route)
    {
        route = null!;
        if (!TryGetRoadAccessPointPosition(pickupAccessPointId, out var origin)
            || !TryGetRoadAccessPointPosition(deliveryAccessPointId, out var destination)) return false;
        try
        {
            route = FindRoadRoute(new RouteRequest(origin, destination, RoutingCostMetric.EstimatedTravelTime));
            return route.Steps.Count > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private bool TryGetRoadAccessPointPosition(RoadAccessPointId id, out WorldPoint position)
    {
        position = default;
        if (!TryGetRoadAccessPointSnapshot(id, out var access)
            || !TryGetRoadSegmentSnapshot(access.SegmentId, out var segment)
            || !TryGetRoadNodeSnapshot(segment.StartNodeId, out var start)
            || !TryGetRoadNodeSnapshot(segment.EndNodeId, out var end)) return false;
        var t = access.SegmentOffset;
        position = new WorldPoint(
            start.Position.X + ((end.Position.X - start.Position.X) * t),
            start.Position.Y + ((end.Position.Y - start.Position.Y) * t),
            start.Position.Z + ((end.Position.Z - start.Position.Z) * t));
        return true;
    }

    private static bool AccessPointMatchesEstablishment(RoadAccessPointSnapshot accessPoint, EconomyEstablishmentState establishment) =>
        (establishment.PoiId is { } poiId && accessPoint.PoiId == poiId)
        || (establishment.BuildingId is { } buildingId && accessPoint.BuildingId == buildingId);

    private bool TryGetEstablishmentCompany(EconomyEstablishmentState establishment, out EconomyCompanyState company) =>
        _economyCompanyIndex.TryGetValue(establishment.CompanyId, out company!);

    private static InventorySnapshot CreateInventorySnapshot(LogisticsInventoryState state) => new(
        state.EstablishmentId,
        state.CommodityId,
        state.Role,
        state.Quantity,
        state.Capacity,
        state.ReorderPoint,
        state.TargetQuantity,
        state.DailyConsumptionUnits,
        state.RoadAccessPointId);

    private static ShipmentSnapshot CreateShipmentSnapshot(LogisticsShipmentStateData state, ulong tickCount) => new(
        state.Id,
        state.OrderId,
        state.SourceEstablishmentId,
        state.DestinationEstablishmentId,
        state.CommodityId,
        state.Quantity,
        state.State,
        state.VehicleId,
        state.PickupAccessPointId,
        state.DeliveryAccessPointId,
        state.CreatedTick,
        state.DispatchedTick,
        state.DeliveredTick,
        state.PlannedDeliveryTick,
        state.PlannedDeliveryTick == 0 || state.State == ShipmentState.Delivered || tickCount <= state.PlannedDeliveryTick ? 0UL : tickCount - state.PlannedDeliveryTick);

    private EconomyCheckpoint CreateEconomyCheckpointWithLogistics() => CreateEconomyCheckpoint() with { Logistics = CreateLogisticsCheckpoint() };

    private LogisticsCheckpoint CreateLogisticsCheckpoint() => new(
        _nextCommodityId,
        _nextLogisticsOrderId,
        _nextShipmentId,
        _processedLogisticsCycle,
        _logisticsCommodities.OrderBy(static item => item.Id.Value).Select(static item => new SimulationCommodityCheckpoint(item.Id, item.Kind)).ToArray(),
        _logisticsInventories.Values.OrderBy(static item => item.EstablishmentId.Value).ThenBy(static item => item.CommodityId.Value).Select(static item => new SimulationInventoryCheckpoint(
            item.EstablishmentId, item.CommodityId, item.Role, item.Quantity, item.Capacity, item.ReorderPoint, item.TargetQuantity, item.DailyConsumptionUnits, item.RoadAccessPointId, item.ObservedCompanyProducedUnits)).ToArray(),
        _logisticsOrders.OrderBy(static item => item.Id.Value).Select(static item => new SimulationLogisticsOrderCheckpoint(
            item.Id, item.DestinationEstablishmentId, item.CommodityId, item.Quantity, item.State, item.CreatedTick, item.ShipmentId)).ToArray(),
        _logisticsShipments.OrderBy(static item => item.Id.Value).Select(static item => new SimulationShipmentCheckpoint(
            item.Id, item.OrderId, item.SourceEstablishmentId, item.DestinationEstablishmentId, item.CommodityId, item.Quantity, item.State, item.VehicleId,
            item.PickupAccessPointId, item.DeliveryAccessPointId, item.CreatedTick, item.DispatchedTick, item.DeliveredTick, item.PlannedDeliveryTick, item.LoadingCompleteTick, item.UnloadingCompleteTick)).ToArray(),
        _deliveredShipmentCount);

    private void RestoreLogistics(LogisticsCheckpoint? checkpoint)
    {
        _logisticsCommodities.Clear();
        _logisticsCommodityIndex.Clear();
        _logisticsInventories.Clear();
        _logisticsOrders.Clear();
        _logisticsOrderIndex.Clear();
        _logisticsShipments.Clear();
        _logisticsShipmentIndex.Clear();
        _nextCommodityId = 1;
        _nextLogisticsOrderId = 1;
        _nextShipmentId = 1;
        _processedLogisticsCycle = 0;
        _deliveredShipmentCount = 0;
        if (checkpoint is null) return;

        foreach (var item in checkpoint.Commodities)
        {
            var state = new LogisticsCommodityState(item.Id, item.Kind);
            _logisticsCommodities.Add(state);
            _logisticsCommodityIndex.Add(state.Id, state);
        }
        foreach (var item in checkpoint.Inventories)
        {
            var state = new LogisticsInventoryState(item.EstablishmentId, item.CommodityId, item.Role, item.Quantity, item.Capacity,
                item.ReorderPoint, item.TargetQuantity, item.DailyConsumptionUnits, item.RoadAccessPointId, item.ObservedCompanyProducedUnits);
            _logisticsInventories.Add((item.EstablishmentId, item.CommodityId), state);
        }
        foreach (var item in checkpoint.Orders)
        {
            var state = new LogisticsOrderStateData(item.Id, item.DestinationEstablishmentId, item.CommodityId, item.Quantity, item.CreatedTick)
            {
                State = item.State,
                ShipmentId = item.ShipmentId,
            };
            _logisticsOrders.Add(state);
            _logisticsOrderIndex.Add(state.Id, state);
        }
        foreach (var item in checkpoint.Shipments)
        {
            var state = new LogisticsShipmentStateData(item.Id, item.OrderId, item.SourceEstablishmentId, item.DestinationEstablishmentId,
                item.CommodityId, item.Quantity, item.PickupAccessPointId, item.DeliveryAccessPointId, item.CreatedTick, item.LoadingCompleteTick)
            {
                State = item.State,
                VehicleId = item.VehicleId,
                DispatchedTick = item.DispatchedTick,
                DeliveredTick = item.DeliveredTick,
                PlannedDeliveryTick = item.PlannedDeliveryTick,
                UnloadingCompleteTick = item.UnloadingCompleteTick,
            };
            _logisticsShipments.Add(state);
            _logisticsShipmentIndex.Add(state.Id, state);
        }
        _nextCommodityId = checkpoint.NextCommodityId;
        _nextLogisticsOrderId = checkpoint.NextOrderId;
        _nextShipmentId = checkpoint.NextShipmentId;
        _processedLogisticsCycle = checkpoint.ProcessedLogisticsCycle;
        _deliveredShipmentCount = checkpoint.DeliveredShipmentCount;
    }

    private static void ValidateLogisticsCheckpoint(SimulationCheckpoint checkpoint)
    {
        var logistics = checkpoint.Economy?.Logistics;
        if (logistics is null) return;
        if (logistics.NextCommodityId == 0 || logistics.NextOrderId == 0 || logistics.NextShipmentId == 0)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Logistics next IDs must be greater than zero.");
        if (logistics.ProcessedLogisticsCycle > (checkpoint.Economy?.ProcessedEconomicCycle ?? 0UL))
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Processed logistics cycle cannot be ahead of Economy.");

        var commodityIds = new HashSet<CommodityId>();
        var maxCommodityId = 0UL;
        foreach (var item in logistics.Commodities)
        {
            if (item.Id.Value == 0 || !commodityIds.Add(item.Id) || !Enum.IsDefined(item.Kind))
                throw new ArgumentException("Logistics contains an invalid or duplicate Commodity.", nameof(checkpoint));
            maxCommodityId = Math.Max(maxCommodityId, item.Id.Value);
        }
        if (logistics.NextCommodityId <= maxCommodityId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Commodity ID must exceed stored IDs.");

        var establishmentIds = (checkpoint.Economy?.Establishments ?? []).Select(static item => item.Id).ToHashSet();
        var accessPointIds = (checkpoint.RoadAccessPoints ?? []).Select(static item => item.Id).ToHashSet();
        var inventoryKeys = new HashSet<(EstablishmentId, CommodityId)>();
        foreach (var item in logistics.Inventories)
        {
            if (!establishmentIds.Contains(item.EstablishmentId) || !commodityIds.Contains(item.CommodityId)
                || !accessPointIds.Contains(item.RoadAccessPointId) || !inventoryKeys.Add((item.EstablishmentId, item.CommodityId))
                || !Enum.IsDefined(item.Role)
                || !IsValidInventoryValue(item.Quantity) || !IsValidInventoryValue(item.Capacity) || item.Capacity <= 0d
                || !IsValidInventoryValue(item.ReorderPoint) || !IsValidInventoryValue(item.TargetQuantity)
                || !IsValidInventoryValue(item.DailyConsumptionUnits) || !IsValidInventoryValue(item.ObservedCompanyProducedUnits)
                || item.Quantity > item.Capacity || item.ReorderPoint > item.TargetQuantity || item.TargetQuantity > item.Capacity)
                throw new ArgumentException("Logistics contains invalid Inventory state.", nameof(checkpoint));
        }

        var orderIds = new HashSet<LogisticsOrderId>();
        var maxOrderId = 0UL;
        foreach (var item in logistics.Orders)
        {
            if (item.Id.Value == 0 || !orderIds.Add(item.Id) || !establishmentIds.Contains(item.DestinationEstablishmentId)
                || !commodityIds.Contains(item.CommodityId) || !double.IsFinite(item.Quantity) || item.Quantity <= 0d || !Enum.IsDefined(item.State))
                throw new ArgumentException("Logistics contains invalid Order state.", nameof(checkpoint));
            maxOrderId = Math.Max(maxOrderId, item.Id.Value);
        }
        if (logistics.NextOrderId <= maxOrderId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Logistics Order ID must exceed stored IDs.");

        var vehicleIds = (checkpoint.Vehicles ?? []).Select(static item => item.Id).ToHashSet();
        var shipmentIds = new HashSet<ShipmentId>();
        var maxShipmentId = 0UL;
        foreach (var item in logistics.Shipments)
        {
            if (item.Id.Value == 0 || !shipmentIds.Add(item.Id) || !orderIds.Contains(item.OrderId)
                || !establishmentIds.Contains(item.SourceEstablishmentId) || !establishmentIds.Contains(item.DestinationEstablishmentId)
                || !commodityIds.Contains(item.CommodityId) || !double.IsFinite(item.Quantity) || item.Quantity <= 0d
                || !Enum.IsDefined(item.State) || !accessPointIds.Contains(item.PickupAccessPointId) || !accessPointIds.Contains(item.DeliveryAccessPointId)
                || (item.VehicleId is { } vehicleId && !vehicleIds.Contains(vehicleId)))
                throw new ArgumentException("Logistics contains invalid Shipment state.", nameof(checkpoint));
            maxShipmentId = Math.Max(maxShipmentId, item.Id.Value);
        }
        if (logistics.NextShipmentId <= maxShipmentId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Shipment ID must exceed stored IDs.");
        foreach (var order in logistics.Orders)
            if (order.ShipmentId is { } shipmentId && !shipmentIds.Contains(shipmentId))
                throw new ArgumentException("Logistics Order references a missing Shipment.", nameof(checkpoint));
    }

    private static bool IsValidInventoryValue(double value) => double.IsFinite(value) && value >= 0d;
    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0d) throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and non-negative.");
    }
    private static void EnsureLogisticsIdCapacity(ulong nextId, string name)
    {
        if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted.");
    }

    private sealed class LogisticsCommodityState(CommodityId id, CommodityKind kind)
    {
        public CommodityId Id { get; } = id;
        public CommodityKind Kind { get; } = kind;
    }

    private sealed class LogisticsInventoryState(
        EstablishmentId establishmentId,
        CommodityId commodityId,
        InventoryRole role,
        double quantity,
        double capacity,
        double reorderPoint,
        double targetQuantity,
        double dailyConsumptionUnits,
        RoadAccessPointId roadAccessPointId,
        double observedCompanyProducedUnits)
    {
        public EstablishmentId EstablishmentId { get; } = establishmentId;
        public CommodityId CommodityId { get; } = commodityId;
        public InventoryRole Role { get; } = role;
        public double Quantity { get; set; } = quantity;
        public double Capacity { get; } = capacity;
        public double ReorderPoint { get; } = reorderPoint;
        public double TargetQuantity { get; } = targetQuantity;
        public double DailyConsumptionUnits { get; } = dailyConsumptionUnits;
        public RoadAccessPointId RoadAccessPointId { get; } = roadAccessPointId;
        public double ObservedCompanyProducedUnits { get; set; } = observedCompanyProducedUnits;
    }

    private sealed class LogisticsOrderStateData(
        LogisticsOrderId id,
        EstablishmentId destinationEstablishmentId,
        CommodityId commodityId,
        double quantity,
        ulong createdTick)
    {
        public LogisticsOrderId Id { get; } = id;
        public EstablishmentId DestinationEstablishmentId { get; } = destinationEstablishmentId;
        public CommodityId CommodityId { get; } = commodityId;
        public double Quantity { get; } = quantity;
        public LogisticsOrderState State { get; set; } = LogisticsOrderState.Open;
        public ulong CreatedTick { get; } = createdTick;
        public ShipmentId? ShipmentId { get; set; }
    }

    private sealed class LogisticsShipmentStateData(
        ShipmentId id,
        LogisticsOrderId orderId,
        EstablishmentId sourceEstablishmentId,
        EstablishmentId destinationEstablishmentId,
        CommodityId commodityId,
        double quantity,
        RoadAccessPointId pickupAccessPointId,
        RoadAccessPointId deliveryAccessPointId,
        ulong createdTick,
        ulong loadingCompleteTick)
    {
        public ShipmentId Id { get; } = id;
        public LogisticsOrderId OrderId { get; } = orderId;
        public EstablishmentId SourceEstablishmentId { get; } = sourceEstablishmentId;
        public EstablishmentId DestinationEstablishmentId { get; } = destinationEstablishmentId;
        public CommodityId CommodityId { get; } = commodityId;
        public double Quantity { get; } = quantity;
        public ShipmentState State { get; set; } = ShipmentState.Pickup;
        public VehicleId? VehicleId { get; set; }
        public RoadAccessPointId PickupAccessPointId { get; } = pickupAccessPointId;
        public RoadAccessPointId DeliveryAccessPointId { get; } = deliveryAccessPointId;
        public ulong CreatedTick { get; } = createdTick;
        public ulong? DispatchedTick { get; set; }
        public ulong? DeliveredTick { get; set; }
        public ulong PlannedDeliveryTick { get; set; }
        public ulong LoadingCompleteTick { get; } = loadingCompleteTick;
        public ulong UnloadingCompleteTick { get; set; }
    }
}
