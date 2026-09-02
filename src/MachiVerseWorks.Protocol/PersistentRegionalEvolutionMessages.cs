namespace MachiVerseWorks.Protocol;

public sealed record ProtocolSettlementEvolution(
    ulong SettlementId,
    double X,
    double Y,
    double Z,
    int Population,
    int Jobs,
    double ServiceIndex,
    double Density,
    double Accessibility,
    double InfluenceRadiusMeters,
    byte Scale,
    byte Trend,
    bool IsActive,
    int EstablishedYear,
    int? DormantSinceYear);

public sealed record ProtocolParcelEvolution(
    ulong ParcelId,
    ulong SettlementId,
    double DevelopmentDemand,
    double LandValue,
    byte DevelopmentState,
    ulong BuildingId);

public sealed record ProtocolBuildingLifecycle(
    ulong BuildingId,
    ulong ParcelId,
    byte Use,
    int BuiltYear,
    int LastChangedYear,
    double Condition,
    double Occupancy,
    int Capacity,
    byte Status);

public sealed record ProtocolServiceCatchment(
    ulong SettlementId,
    byte Kind,
    double RadiusMeters,
    double Coverage);

public sealed record ProtocolInfrastructureDemand(
    ulong SettlementId,
    byte Kind,
    double Demand,
    string Reason);

public sealed record ProtocolRegionalRelation(
    ulong RelationId,
    ulong FromSettlementId,
    ulong ToSettlementId,
    byte Kind,
    double Strength,
    bool IsActive,
    int SinceYear);

public sealed record ProtocolRegionalEvolutionEvent(
    ulong EventId,
    int Year,
    byte Kind,
    ulong SettlementId,
    ulong BuildingId,
    string Reason);

public sealed record ProtocolRegionalCommutingFlow(
    ulong FromSettlementId,
    ulong ToSettlementId,
    int WorkerCount);

public sealed record ProtocolRegionalFreightFlow(
    ulong FromSettlementId,
    ulong ToSettlementId,
    ulong CommodityId,
    double Quantity,
    int ShipmentCount,
    double DeliveredQuantity);

public sealed record PersistentRegionalEvolutionSnapshotMessage(
    int CurrentYear,
    ulong TickCount,
    IReadOnlyList<ProtocolSettlementEvolution> Settlements,
    IReadOnlyList<ProtocolParcelEvolution> Parcels,
    IReadOnlyList<ProtocolBuildingLifecycle> Buildings,
    IReadOnlyList<ProtocolServiceCatchment> ServiceCatchments,
    IReadOnlyList<ProtocolInfrastructureDemand> InfrastructureDemands,
    IReadOnlyList<ProtocolRegionalRelation> Relations,
    IReadOnlyList<ProtocolRegionalEvolutionEvent> Events,
    IReadOnlyList<ProtocolRegionalCommutingFlow> CommutingFlows,
    IReadOnlyList<ProtocolRegionalFreightFlow> FreightFlows) : IProtocolMessage
{
    public MessageType Type => MessageType.PersistentRegionalEvolutionSnapshot;
}
