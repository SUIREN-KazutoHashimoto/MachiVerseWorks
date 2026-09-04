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

            // Initial Agent grounding only needs the authoritative terrain elevation. Avoid TerrainSurface.Sample(),
            // which also computes normals, slope, hydrology and material and is unnecessarily expensive for large bootstraps.
            var surface = GetTerrainPartition(snapshot.Position).Surface;
            var groundedPosition = new WorldPoint(
                snapshot.Position.X,
                snapshot.Position.Y,
                surface.SampleHeight(snapshot.Position.X, snapshot.Position.Y));
            if (!_agents.Update(id, groundedPosition, snapshot.Velocity, _spatialIndex))
                throw new InvalidOperationException($"Newly created Agent {id.Value} could not be updated for terrain grounding.");
        }
        return ids;
    }
}
