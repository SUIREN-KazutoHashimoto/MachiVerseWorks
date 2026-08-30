namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    public AgentSnapshot[] CreateAllAgentSnapshots()
    {
        var checkpoints = _agents.CreateCheckpoint();
        var result = new List<AgentSnapshot>(ActiveAgentCount);
        foreach (var checkpoint in checkpoints)
        {
            if (!checkpoint.IsActive) continue;
            result.Add(new AgentSnapshot(checkpoint.Id, checkpoint.Position, checkpoint.Velocity, Time.TickCount));
        }
        return result.ToArray();
    }

    public PedestrianSnapshot[] CreateAllPedestrianSnapshots()
    {
        var checkpoints = _pedestrians.CreateCheckpoint();
        var result = new PedestrianSnapshot[checkpoints.Count];
        for (var index = 0; index < checkpoints.Count; index++)
        {
            if (!_pedestrians.TryGetSnapshot(checkpoints[index].Id, Time.TickCount, out result[index]))
                throw new InvalidOperationException($"Pedestrian {checkpoints[index].Id.Value} disappeared while creating an atomic snapshot.");
        }
        return result;
    }

    public VehicleSnapshot[] CreateAllVehicleSnapshots() => _vehicles.CreateAllSnapshots(Time.TickCount);
}
