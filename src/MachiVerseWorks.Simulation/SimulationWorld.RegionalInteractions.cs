namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    public RegionalInteractionSnapshot CreateRegionalInteractionSnapshot()
    {
        EnsurePersistentRegionalEvolution();
        return CreateRegionalInteractionSnapshot(_persistentRegionalEvolution!.Settlements);
    }

    private RegionalInteractionSnapshot CreateRegionalInteractionSnapshot(
        IReadOnlyList<SettlementEvolutionState> settlements)
    {
        var commuting = new Dictionary<(SettlementId From, SettlementId To), int>();
        foreach (var employment in _economyEmployments.Values.OrderBy(static item => item.PersonId.Value))
        {
            if (!TryGetPersonSnapshot(employment.PersonId, out var person)
                || !TryGetJobSnapshot(employment.JobId, out var job)
                || !TryGetEstablishmentSnapshot(job.EstablishmentId, out var establishment)
                || !TryResolveRegionalEndpointPosition(person.Residence, out var home)
                || !TryResolveRegionalEndpointPosition(establishment.Location, out var work))
            {
                continue;
            }
            var from = FindNearestSettlement(settlements, home);
            var to = FindNearestSettlement(settlements, work);
            if (from is null || to is null || from.SettlementId == to.SettlementId) continue;
            var key = (from.SettlementId, to.SettlementId);
            commuting[key] = commuting.GetValueOrDefault(key) + 1;
        }

        var freight = new Dictionary<(SettlementId From, SettlementId To, CommodityId Commodity), FreightAccumulator>();
        foreach (var shipment in CreateLogisticsSnapshot().Shipments.OrderBy(static item => item.Id.Value))
        {
            if (!TryGetEstablishmentSnapshot(shipment.SourceEstablishmentId, out var sourceEstablishment)
                || !TryGetEstablishmentSnapshot(shipment.DestinationEstablishmentId, out var destinationEstablishment)
                || !TryResolveRegionalEndpointPosition(sourceEstablishment.Location, out var sourcePoint)
                || !TryResolveRegionalEndpointPosition(destinationEstablishment.Location, out var destinationPoint))
            {
                continue;
            }
            var from = FindNearestSettlement(settlements, sourcePoint);
            var to = FindNearestSettlement(settlements, destinationPoint);
            if (from is null || to is null || from.SettlementId == to.SettlementId) continue;
            var key = (from.SettlementId, to.SettlementId, shipment.CommodityId);
            var accumulator = freight.GetValueOrDefault(key);
            accumulator.Quantity += shipment.Quantity;
            accumulator.ShipmentCount++;
            if (shipment.State == ShipmentState.Delivered) accumulator.DeliveredQuantity += shipment.Quantity;
            freight[key] = accumulator;
        }

        return new RegionalInteractionSnapshot(
            Time.TickCount,
            commuting.OrderBy(static item => item.Key.From.Value).ThenBy(static item => item.Key.To.Value)
                .Select(static item => new RegionalCommutingFlow(item.Key.From, item.Key.To, item.Value)).ToArray(),
            freight.OrderBy(static item => item.Key.From.Value).ThenBy(static item => item.Key.To.Value).ThenBy(static item => item.Key.Commodity.Value)
                .Select(static item => new RegionalFreightFlow(
                    item.Key.From,
                    item.Key.To,
                    item.Key.Commodity,
                    item.Value.Quantity,
                    item.Value.ShipmentCount,
                    item.Value.DeliveredQuantity)).ToArray());
    }

    private struct FreightAccumulator
    {
        public double Quantity;
        public int ShipmentCount;
        public double DeliveredQuantity;
    }
}
