namespace MachiVerseWorks.Simulation;

public readonly record struct CommodityId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct LogisticsOrderId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct ShipmentId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum CommodityKind : byte
{
    GeneralGoods = 0,
    Food = 1,
    IndustrialGoods = 2,
    Gas = 3,
}

public enum InventoryRole : byte
{
    Buffer = 0,
    Supplier = 1,
    Consumer = 2,
}

public enum LogisticsOrderState : byte
{
    Open = 0,
    Allocated = 1,
    Completed = 2,
}

public enum ShipmentState : byte
{
    Pickup = 0,
    Loading = 1,
    InTransit = 2,
    Unloading = 3,
    Delivered = 4,
}

public readonly record struct CommoditySnapshot(CommodityId Id, CommodityKind Kind);

public readonly record struct InventorySnapshot(
    EstablishmentId EstablishmentId,
    CommodityId CommodityId,
    InventoryRole Role,
    double Quantity,
    double Capacity,
    double ReorderPoint,
    double TargetQuantity,
    double DailyConsumptionUnits,
    RoadAccessPointId RoadAccessPointId);

public readonly record struct LogisticsOrderSnapshot(
    LogisticsOrderId Id,
    EstablishmentId DestinationEstablishmentId,
    CommodityId CommodityId,
    double Quantity,
    LogisticsOrderState State,
    ulong CreatedTick,
    ShipmentId? ShipmentId);

public readonly record struct ShipmentSnapshot(
    ShipmentId Id,
    LogisticsOrderId OrderId,
    EstablishmentId SourceEstablishmentId,
    EstablishmentId DestinationEstablishmentId,
    CommodityId CommodityId,
    double Quantity,
    ShipmentState State,
    VehicleId? VehicleId,
    RoadAccessPointId PickupAccessPointId,
    RoadAccessPointId DeliveryAccessPointId,
    ulong CreatedTick,
    ulong? DispatchedTick,
    ulong? DeliveredTick,
    ulong PlannedDeliveryTick,
    ulong DelayTicks);

public readonly record struct LogisticsStatistics(
    int CommodityCount,
    int InventoryCount,
    int OpenOrderCount,
    int ShipmentCount,
    int InTransitShipmentCount,
    int DelayedShipmentCount,
    double InventoryUnits,
    double InTransitUnits,
    ulong DeliveredShipmentCount,
    ulong LogisticsCycle,
    ulong TickCount);

public sealed record LogisticsSnapshot(
    LogisticsStatistics Statistics,
    IReadOnlyList<CommoditySnapshot> Commodities,
    IReadOnlyList<InventorySnapshot> Inventories,
    IReadOnlyList<LogisticsOrderSnapshot> Orders,
    IReadOnlyList<ShipmentSnapshot> Shipments);

public static class LogisticsDefaults
{
    public const ulong LoadingTicks = 2;
    public const ulong UnloadingTicks = 2;
    public const double MinimumOrderQuantity = 1d;
    public const double DefaultDailyConsumptionUnits = 5d;
    public static VehicleDimensions FreightVehicleDimensions => new(8d, 2.5d, 3.2d);
    public static VehiclePerformance FreightVehiclePerformance => new(22.2222222222d, 1.5d, 3.5d, 3d, 2d);
}

public sealed record LogisticsCheckpoint(
    ulong NextCommodityId,
    ulong NextOrderId,
    ulong NextShipmentId,
    ulong ProcessedLogisticsCycle,
    IReadOnlyList<SimulationCommodityCheckpoint> Commodities,
    IReadOnlyList<SimulationInventoryCheckpoint> Inventories,
    IReadOnlyList<SimulationLogisticsOrderCheckpoint> Orders,
    IReadOnlyList<SimulationShipmentCheckpoint> Shipments,
    ulong DeliveredShipmentCount);

public readonly record struct SimulationCommodityCheckpoint(CommodityId Id, CommodityKind Kind);

public readonly record struct SimulationInventoryCheckpoint(
    EstablishmentId EstablishmentId,
    CommodityId CommodityId,
    InventoryRole Role,
    double Quantity,
    double Capacity,
    double ReorderPoint,
    double TargetQuantity,
    double DailyConsumptionUnits,
    RoadAccessPointId RoadAccessPointId,
    double ObservedCompanyProducedUnits);

public readonly record struct SimulationLogisticsOrderCheckpoint(
    LogisticsOrderId Id,
    EstablishmentId DestinationEstablishmentId,
    CommodityId CommodityId,
    double Quantity,
    LogisticsOrderState State,
    ulong CreatedTick,
    ShipmentId? ShipmentId);

public readonly record struct SimulationShipmentCheckpoint(
    ShipmentId Id,
    LogisticsOrderId OrderId,
    EstablishmentId SourceEstablishmentId,
    EstablishmentId DestinationEstablishmentId,
    CommodityId CommodityId,
    double Quantity,
    ShipmentState State,
    VehicleId? VehicleId,
    RoadAccessPointId PickupAccessPointId,
    RoadAccessPointId DeliveryAccessPointId,
    ulong CreatedTick,
    ulong? DispatchedTick,
    ulong? DeliveredTick,
    ulong PlannedDeliveryTick,
    ulong LoadingCompleteTick,
    ulong UnloadingCompleteTick);
