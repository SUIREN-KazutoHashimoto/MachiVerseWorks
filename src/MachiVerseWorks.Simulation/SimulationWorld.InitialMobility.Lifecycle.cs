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

    private void RestoreInitialMobilityCheckpoint(SimulationCheckpoint checkpoint)
    {
        var storedPedestrians = (checkpoint.Pedestrians ?? Array.Empty<SimulationPedestrianCheckpoint>())
            .Select(static item => item.Id)
            .ToHashSet();
        foreach (var pedestrianId in checkpoint.InitialMobilityPedestrianIds ?? Array.Empty<PedestrianId>())
        {
            if (pedestrianId.Value == 0 || !storedPedestrians.Contains(pedestrianId) || !_initialMobilityPedestrianIds.Add(pedestrianId))
                throw new ArgumentException($"Initial mobility Pedestrian ID {pedestrianId.Value} is zero, duplicated, or missing from the checkpoint.", nameof(checkpoint));
        }

        var storedVehicles = (checkpoint.Vehicles ?? Array.Empty<SimulationVehicleCheckpoint>())
            .Select(static item => item.Id)
            .ToHashSet();
        foreach (var vehicleId in checkpoint.InitialMobilityVehicleIds ?? Array.Empty<VehicleId>())
        {
            if (vehicleId.Value == 0 || !storedVehicles.Contains(vehicleId) || !_initialMobilityVehicleIds.Add(vehicleId))
                throw new ArgumentException($"Initial mobility Vehicle ID {vehicleId.Value} is zero, duplicated, or missing from the checkpoint.", nameof(checkpoint));
        }
    }
}
