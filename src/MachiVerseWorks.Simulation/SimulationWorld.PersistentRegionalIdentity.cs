namespace MachiVerseWorks.Simulation;

public readonly record struct PersistentRegionalObservationIdentity(
    int CurrentYear,
    ulong EconomicCycle,
    ulong LogisticsCycle,
    int EmploymentCount,
    int ShipmentCount,
    ulong DeliveredShipmentCount);

public sealed partial class SimulationWorld
{
    public bool TryGetPersistentRegionalObservationIdentity(out PersistentRegionalObservationIdentity identity)
    {
        if (_persistentRegionalEvolution is null)
        {
            identity = default;
            return false;
        }

        identity = new PersistentRegionalObservationIdentity(
            _persistentRegionalEvolution.CurrentYear,
            _processedEconomicCycle,
            _processedLogisticsCycle,
            _economyEmployments.Count,
            _logisticsShipments.Count,
            _deliveredShipmentCount);
        return true;
    }
}
