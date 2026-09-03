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

    public static WorldEnvironmentSnapshot CreateDetachedDetailedWorldEnvironmentSnapshot(
        WorldEnvironmentConfig config,
        ulong tickCount,
        WorldVolume volume,
        int sampleColumns = 8,
        int sampleRows = 8,
        int maximumFeatures = 128)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (sampleColumns is <= 0 or > 32) throw new ArgumentOutOfRangeException(nameof(sampleColumns));
        if (sampleRows is <= 0 or > 32) throw new ArgumentOutOfRangeException(nameof(sampleRows));

        var generator = new WorldEnvironmentGenerator(config);
        var samples = new RegionalEnvironmentSample[checked(sampleColumns * sampleRows)];
        var index = 0;
        for (var row = 0; row < sampleRows; row++)
        {
            var y = volume.Depth == 0d ? volume.MinY : volume.MinY + ((row + 0.5d) / sampleRows * volume.Depth);
            for (var column = 0; column < sampleColumns; column++)
            {
                var x = volume.Width == 0d ? volume.MinX : volume.MinX + ((column + 0.5d) / sampleColumns * volume.Width);
                samples[index++] = generator.Sample(new WorldPoint(x, y, 0d));
            }
        }

        var features = generator.DetectGeographicFeatures(volume, maximumFeatures);
        var toponyms = features.Select(generator.CreateToponym).OrderBy(static item => item.Id.Value).ToArray();
        var snapshot = new WorldEnvironmentSnapshot(config, volume, samples, features, toponyms, tickCount);
        var surface = new TerrainSurface(generator, volume);
        var terrainSamples = samples
            .Select(item => surface.Sample(item.Position.X, item.Position.Y))
            .ToArray();
        return snapshot with { TerrainSamples = terrainSamples };
    }
}
