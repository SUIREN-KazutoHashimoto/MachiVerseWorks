namespace MachiVerseWorks.Simulation;

public sealed record RegionalCommutingFlow(
    SettlementId FromSettlementId,
    SettlementId ToSettlementId,
    int WorkerCount);

public sealed record RegionalFreightFlow(
    SettlementId FromSettlementId,
    SettlementId ToSettlementId,
    CommodityId CommodityId,
    double Quantity,
    int ShipmentCount,
    double DeliveredQuantity);

public sealed record RegionalInteractionSnapshot(
    ulong TickCount,
    IReadOnlyList<RegionalCommutingFlow> CommutingFlows,
    IReadOnlyList<RegionalFreightFlow> FreightFlows);
