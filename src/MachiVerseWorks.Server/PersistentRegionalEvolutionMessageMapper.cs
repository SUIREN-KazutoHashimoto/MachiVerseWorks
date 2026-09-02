using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

public static class PersistentRegionalEvolutionMessageMapper
{
    public static PersistentRegionalEvolutionSnapshotMessage ToProtocol(
        PersistentRegionalEvolutionSnapshot snapshot,
        RegionalInteractionSnapshot interactions)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(interactions);
        return new PersistentRegionalEvolutionSnapshotMessage(
            snapshot.CurrentYear,
            snapshot.TickCount,
            snapshot.Settlements.Select(static item => new ProtocolSettlementEvolution(
                item.SettlementId.Value, item.Center.X, item.Center.Y, item.Center.Z, item.Population, item.Jobs,
                item.ServiceIndex, item.Density, item.Accessibility, item.InfluenceRadiusMeters,
                (byte)item.Scale, (byte)item.Trend, item.IsActive, item.EstablishedYear, item.DormantSinceYear)).ToArray(),
            snapshot.Parcels.Select(static item => new ProtocolParcelEvolution(
                item.ParcelId.Value, item.SettlementId.Value, item.DevelopmentDemand, item.LandValue,
                (byte)item.DevelopmentState, item.BuildingId?.Value ?? 0UL)).ToArray(),
            snapshot.Buildings.Select(static item => new ProtocolBuildingLifecycle(
                item.BuildingId.Value, item.ParcelId.Value, (byte)item.Use, item.BuiltYear, item.LastChangedYear,
                item.Condition, item.Occupancy, item.Capacity, (byte)item.Status)).ToArray(),
            snapshot.ServiceCatchments.Select(static item => new ProtocolServiceCatchment(
                item.SettlementId.Value, (byte)item.Kind, item.RadiusMeters, item.Coverage)).ToArray(),
            snapshot.InfrastructureDemands.Select(static item => new ProtocolInfrastructureDemand(
                item.SettlementId.Value, (byte)item.Kind, item.Demand, item.Reason)).ToArray(),
            snapshot.Relations.Select(static item => new ProtocolRegionalRelation(
                item.Id.Value, item.FromSettlementId.Value, item.ToSettlementId.Value, (byte)item.Kind,
                item.Strength, item.IsActive, item.SinceYear)).ToArray(),
            snapshot.Events.Select(static item => new ProtocolRegionalEvolutionEvent(
                item.Id.Value, item.Year, (byte)item.Kind, item.SettlementId.Value,
                item.BuildingId?.Value ?? 0UL, item.Reason)).ToArray(),
            interactions.CommutingFlows.Select(static item => new ProtocolRegionalCommutingFlow(
                item.FromSettlementId.Value, item.ToSettlementId.Value, item.WorkerCount)).ToArray(),
            interactions.FreightFlows.Select(static item => new ProtocolRegionalFreightFlow(
                item.FromSettlementId.Value, item.ToSettlementId.Value, item.CommodityId.Value,
                item.Quantity, item.ShipmentCount, item.DeliveredQuantity)).ToArray());
    }
}
