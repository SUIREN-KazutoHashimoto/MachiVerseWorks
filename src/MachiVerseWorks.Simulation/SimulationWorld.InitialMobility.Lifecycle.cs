namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private void RetireInitialVehiclesForRoadTopologyMutation()
    {
        foreach (var vehicleId in _initialMobilityVehicleIds.ToArray())
        {
            _ = RemoveVehicleCore(vehicleId);
            _initialMobilityVehicleIds.Remove(vehicleId);
        }
    }
}
