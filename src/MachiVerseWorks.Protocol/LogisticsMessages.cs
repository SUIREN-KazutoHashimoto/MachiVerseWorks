namespace MachiVerseWorks.Protocol;

public enum ProtocolShipmentState : byte
{
    Pickup = 0,
    Loading = 1,
    InTransit = 2,
    Unloading = 3,
    Delivered = 4,
}

public readonly record struct ProtocolLogisticsStatistics(
    uint CommodityCount,
    uint InventoryCount,
    uint OpenOrderCount,
    uint ShipmentCount,
    uint InTransitShipmentCount,
    uint DelayedShipmentCount,
    double InventoryUnits,
    double InTransitUnits,
    ulong DeliveredShipmentCount,
    ulong LogisticsCycle,
    ulong TickCount);

public readonly record struct ProtocolInventory(
    ulong EstablishmentId,
    ulong CommodityId,
    double Quantity,
    double Capacity);

public readonly record struct ProtocolShipment(
    ulong ShipmentId,
    ulong OrderId,
    ulong SourceEstablishmentId,
    ulong DestinationEstablishmentId,
    ulong CommodityId,
    double Quantity,
    ProtocolShipmentState State,
    ulong VehicleId,
    ulong DelayTicks);

public sealed record LogisticsSnapshotMessage(
    ProtocolLogisticsStatistics Statistics,
    IReadOnlyList<ProtocolInventory> Inventories,
    IReadOnlyList<ProtocolShipment> Shipments) : IProtocolMessage
{
    public MessageType Type => MessageType.LogisticsSnapshot;
}
