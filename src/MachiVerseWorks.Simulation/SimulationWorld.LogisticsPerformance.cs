namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private ulong _nextCompletedLogisticsPruneTick;
    private readonly Dictionary<CompanyId, LogisticsSupplierCycleBucket> _logisticsSupplierCycleBuckets = [];
    private readonly List<CompanyId> _activeLogisticsSupplierCompanies = [];
    private readonly List<LogisticsInventoryState> _logisticsConsumerCycleInventories = [];
    private ulong _logisticsCycleWorkingSetGeneration;

    private void StepLogisticsOptimized(SimulationTime nextTime)
    {
        var deliveredBefore = _deliveredShipmentCount;
        if (_nextCompletedLogisticsPruneTick == 0 || nextTime.TickCount >= _nextCompletedLogisticsPruneTick)
        {
            PruneCompletedLogisticsHistory(nextTime.TickCount);
            _nextCompletedLogisticsPruneTick = CalculateNextCompletedLogisticsPruneTick(nextTime.TickCount);
        }

        while (_processedLogisticsCycle < _processedEconomicCycle)
        {
            ProcessLogisticsCycleOptimized(nextTime.TickCount);
            _processedLogisticsCycle++;
        }
        AdvanceShipments(nextTime.TickCount);
        AllocateOpenOrders(nextTime.TickCount);

        if (_deliveredShipmentCount != deliveredBefore)
        {
            var expiryTick = AddLogisticsRetentionWindow(nextTime.TickCount);
            if (_nextCompletedLogisticsPruneTick == 0 || expiryTick < _nextCompletedLogisticsPruneTick)
                _nextCompletedLogisticsPruneTick = expiryTick;
        }
    }

    private void ProcessLogisticsCycleOptimized(ulong tickCount)
    {
        PrepareLogisticsCycleWorkingSets();
        ReceiveEconomicProductionFromWorkingSet();
        ConsumeInventoryFromWorkingSet();
        GenerateReplenishmentOrdersFromWorkingSet(tickCount);
    }

    private void PrepareLogisticsCycleWorkingSets()
    {
        if (_logisticsCycleWorkingSetGeneration == ulong.MaxValue)
        {
            foreach (var bucket in _logisticsSupplierCycleBuckets.Values) bucket.Generation = 0;
            _logisticsCycleWorkingSetGeneration = 1;
        }
        else
        {
            _logisticsCycleWorkingSetGeneration++;
        }

        var generation = _logisticsCycleWorkingSetGeneration;
        _activeLogisticsSupplierCompanies.Clear();
        _logisticsConsumerCycleInventories.Clear();

        foreach (var inventory in _logisticsInventories.Values)
        {
            if (inventory.Role == InventoryRole.Consumer)
            {
                _logisticsConsumerCycleInventories.Add(inventory);
                continue;
            }
            if (inventory.Role != InventoryRole.Supplier
                || !_economyEstablishmentIndex.TryGetValue(inventory.EstablishmentId, out var establishment)
                || !_economyCompanyIndex.ContainsKey(establishment.CompanyId))
                continue;

            if (!_logisticsSupplierCycleBuckets.TryGetValue(establishment.CompanyId, out var bucket))
            {
                bucket = new LogisticsSupplierCycleBucket();
                _logisticsSupplierCycleBuckets.Add(establishment.CompanyId, bucket);
            }
            if (bucket.Generation != generation)
            {
                bucket.Generation = generation;
                bucket.Inventories.Clear();
                _activeLogisticsSupplierCompanies.Add(establishment.CompanyId);
            }
            bucket.Inventories.Add(inventory);
        }

        _activeLogisticsSupplierCompanies.Sort(static (left, right) => left.Value.CompareTo(right.Value));
        _logisticsConsumerCycleInventories.Sort(CompareLogisticsInventories);
    }

    private void ReceiveEconomicProductionFromWorkingSet()
    {
        var generation = _logisticsCycleWorkingSetGeneration;
        for (var companyIndex = 0; companyIndex < _activeLogisticsSupplierCompanies.Count; companyIndex++)
        {
            var companyId = _activeLogisticsSupplierCompanies[companyIndex];
            var bucket = _logisticsSupplierCycleBuckets[companyId];
            if (bucket.Generation != generation || bucket.Inventories.Count == 0) continue;
            bucket.Inventories.Sort(CompareLogisticsInventories);

            var company = _economyCompanyIndex[companyId];
            var observedProduction = bucket.Inventories[0].ObservedCompanyProducedUnits;
            for (var inventoryIndex = 1; inventoryIndex < bucket.Inventories.Count; inventoryIndex++)
                observedProduction = Math.Min(observedProduction, bucket.Inventories[inventoryIndex].ObservedCompanyProducedUnits);

            var remainingProduction = Math.Max(0d, company.ProducedUnits - observedProduction);
            for (var inventoryIndex = 0; inventoryIndex < bucket.Inventories.Count; inventoryIndex++)
                bucket.Inventories[inventoryIndex].ObservedCompanyProducedUnits = company.ProducedUnits;

            for (var inventoryIndex = 0; inventoryIndex < bucket.Inventories.Count && remainingProduction > 0d; inventoryIndex++)
            {
                var inventory = bucket.Inventories[inventoryIndex];
                var availableCapacity = Math.Max(0d, inventory.Capacity - inventory.Quantity);
                var accepted = Math.Min(availableCapacity, remainingProduction);
                inventory.Quantity += accepted;
                remainingProduction -= accepted;
            }
        }
    }

    private void ConsumeInventoryFromWorkingSet()
    {
        for (var inventoryIndex = 0; inventoryIndex < _logisticsConsumerCycleInventories.Count; inventoryIndex++)
        {
            var inventory = _logisticsConsumerCycleInventories[inventoryIndex];
            inventory.Quantity = Math.Max(0d, inventory.Quantity - inventory.DailyConsumptionUnits);
        }
    }

    private void GenerateReplenishmentOrdersFromWorkingSet(ulong tickCount)
    {
        for (var inventoryIndex = 0; inventoryIndex < _logisticsConsumerCycleInventories.Count; inventoryIndex++)
        {
            var inventory = _logisticsConsumerCycleInventories[inventoryIndex];
            if (inventory.Quantity > inventory.ReorderPoint) continue;
            var activeKey = (inventory.EstablishmentId, inventory.CommodityId);
            if (_activeLogisticsOrderKeys.Contains(activeKey)) continue;
            var quantity = inventory.TargetQuantity - inventory.Quantity;
            if (quantity < LogisticsDefaults.MinimumOrderQuantity) continue;
            EnsureLogisticsIdCapacity(_nextLogisticsOrderId, "Logistics order");
            var id = new LogisticsOrderId(_nextLogisticsOrderId++);
            var order = new LogisticsOrderStateData(id, inventory.EstablishmentId, inventory.CommodityId, quantity, tickCount);
            _logisticsOrders.Add(order);
            _logisticsOrderIndex.Add(id, order);
            if (!_activeLogisticsOrderKeys.Add(activeKey))
                throw new InvalidOperationException("An active Logistics Order already exists for the destination inventory.");
        }
    }

    private static int CompareLogisticsInventories(LogisticsInventoryState left, LogisticsInventoryState right)
    {
        var establishmentComparison = left.EstablishmentId.Value.CompareTo(right.EstablishmentId.Value);
        return establishmentComparison != 0
            ? establishmentComparison
            : left.CommodityId.Value.CompareTo(right.CommodityId.Value);
    }

    private ulong CalculateNextCompletedLogisticsPruneTick(ulong tickCount)
    {
        var next = ulong.MaxValue;
        for (var index = 0; index < _logisticsShipments.Count; index++)
        {
            var shipment = _logisticsShipments[index];
            if (shipment.State != ShipmentState.Delivered || shipment.DeliveredTick is not { } deliveredTick) continue;
            var expiryTick = AddLogisticsRetentionWindow(deliveredTick);
            if (expiryTick > tickCount && expiryTick < next) next = expiryTick;
        }
        return next;
    }

    private ulong AddLogisticsRetentionWindow(ulong deliveredTick)
    {
        var windowTicks = _persistentRegionalEvolutionOptions.TicksPerYear;
        if (windowTicks == ulong.MaxValue || deliveredTick >= ulong.MaxValue - windowTicks)
            return ulong.MaxValue;
        return deliveredTick + windowTicks + 1UL;
    }

    private sealed class LogisticsSupplierCycleBucket
    {
        public ulong Generation { get; set; }
        public List<LogisticsInventoryState> Inventories { get; } = [];
    }
}
