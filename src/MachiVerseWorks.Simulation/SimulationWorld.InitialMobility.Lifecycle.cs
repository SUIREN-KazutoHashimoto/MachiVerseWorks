namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private int CountStoredInitialPedestrians()
    {
        var count = 0;
        foreach (var pedestrianId in _initialMobilityPedestrianIds)
        {
            if (_pedestrians.TryGetSnapshot(pedestrianId, Time.TickCount, out _)) count++;
        }
        return count;
    }

    private int CountStoredInitialVehicles()
    {
        var count = 0;
        foreach (var vehicleId in _initialMobilityVehicleIds)
        {
            if (_vehicles.TryGetSnapshot(vehicleId, Time.TickCount, out _)) count++;
        }
        return count;
    }

    private void EnsurePedestrianNetworkMutable()
    {
        if (_pedestrians.Count > CountStoredInitialPedestrians())
            throw new InvalidOperationException("Road topology cannot be changed while stored Pedestrians reference derived routes. Remove them before mutating the walk network.");
    }

    private void RetireInitialVehiclesForRoadTopologyMutation()
    {
        foreach (var vehicleId in _initialMobilityVehicleIds.ToArray())
        {
            _ = RemoveVehicleCore(vehicleId);
            _initialMobilityVehicleIds.Remove(vehicleId);
        }
    }

    private void RetireTransientInitialMobilityForCheckpoint() => RetireInitialMobilityForRoadTopologyMutation();
}
