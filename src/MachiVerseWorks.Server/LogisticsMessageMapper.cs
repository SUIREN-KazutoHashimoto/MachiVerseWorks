using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class LogisticsMessageMapper
{
    private const int MaximumDebugEntries = 256;

    public static LogisticsSnapshotMessage Create(LogisticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var statistics = snapshot.Statistics;
        var protocolStatistics = new ProtocolLogisticsStatistics(
            checked((uint)statistics.CommodityCount),
            checked((uint)statistics.InventoryCount),
            checked((uint)statistics.OpenOrderCount),
            checked((uint)statistics.ShipmentCount),
            checked((uint)statistics.InTransitShipmentCount),
            checked((uint)statistics.DelayedShipmentCount),
            statistics.InventoryUnits,
            statistics.InTransitUnits,
            statistics.DeliveredShipmentCount,
            statistics.LogisticsCycle,
            statistics.TickCount);

        var inventories = snapshot.Inventories.Take(MaximumDebugEntries).Select(static item => new ProtocolInventory(
            item.EstablishmentId.Value,
            item.CommodityId.Value,
            item.Quantity,
            item.Capacity)).ToArray();
        var shipments = snapshot.Shipments
            .OrderBy(static item => item.State == ShipmentState.Delivered ? 1 : 0)
            .ThenByDescending(static item => item.Id.Value)
            .Take(MaximumDebugEntries)
            .Select(static item => new ProtocolShipment(
                item.Id.Value,
                item.OrderId.Value,
                item.SourceEstablishmentId.Value,
                item.DestinationEstablishmentId.Value,
                item.CommodityId.Value,
                item.Quantity,
                (ProtocolShipmentState)item.State,
                item.VehicleId?.Value ?? 0UL,
                item.DelayTicks)).ToArray();
        return new LogisticsSnapshotMessage(protocolStatistics, Array.AsReadOnly(inventories), Array.AsReadOnly(shipments));
    }
}
