namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    /// <summary>
    /// Creates agents using the normal deterministic 3D spawn sequence, then snaps their authoritative
    /// positions to the primary terrain surface at each generated X/Y coordinate.
    /// </summary>
    public AgentId[] CreateGroundedAgents(int count, WorldVolume spawnVolume)
    {
        var ids = CreateAgents(count, spawnVolume);
        foreach (var id in ids)
        {
            if (!_agents.TryGetSnapshot(id, Time.TickCount, out var snapshot))
                throw new InvalidOperationException($"Newly created Agent {id.Value} could not be read for terrain grounding.");

            var groundedPosition = SnapToGround(snapshot.Position);
            if (!_agents.Update(id, groundedPosition, snapshot.Velocity, _spatialIndex))
                throw new InvalidOperationException($"Newly created Agent {id.Value} could not be updated for terrain grounding.");
        }
        return ids;
    }
}
