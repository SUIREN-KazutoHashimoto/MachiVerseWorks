namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    public WorldEnvironmentSnapshot CreateDetailedWorldEnvironmentSnapshot(
        WorldVolume volume,
        int sampleColumns = 8,
        int sampleRows = 8,
        int maximumFeatures = 128)
    {
        var snapshot = CreateWorldEnvironmentSnapshot(volume, sampleColumns, sampleRows, maximumFeatures);
        var terrainSamples = snapshot.Samples
            .Select(item => QueryTerrainSurface(item.Position.X, item.Position.Y))
            .ToArray();
        return snapshot with { TerrainSamples = terrainSamples };
    }
}
