namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private ulong _nextCompletedLogisticsPruneTick;

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
            ProcessLogisticsCycle(nextTime.TickCount);
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
}
