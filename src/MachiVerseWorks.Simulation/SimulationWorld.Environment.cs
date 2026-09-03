namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private const double DefaultTerrainPartitionSizeMeters = 16_384d;
    private WorldEnvironmentGenerator? _worldEnvironmentGenerator;
    private readonly Dictionary<TerrainPartitionId, TerrainPartition> _terrainPartitions = [];
    private readonly Dictionary<GeographicFeatureId, GeographicFeature> _geographicFeatures = [];
    private readonly Dictionary<ToponymId, NaturalToponym> _naturalToponyms = [];
    private readonly Dictionary<GeographicFeatureId, NaturalToponym> _derivedNaturalToponyms = [];

    public WorldEnvironmentConfig WorldEnvironment => Config.WorldEnvironment;
    public RegionalEnvironmentSample QueryEnvironment(WorldPoint position) => EnvironmentGenerator.Sample(position);
    public IReadOnlyList<SettlementCandidateRegion> SelectSettlementCandidates(WorldVolume volume, int count) => EnvironmentGenerator.SelectSettlementCandidates(volume, count);
    public TerrainPartition GetTerrainPartition(WorldPoint position) => GetTerrainPartition(position.X, position.Y);

    public TerrainPartition GetTerrainPartition(double x, double y)
    {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x), x, "Terrain coordinates must be finite.");
        if (!double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y), y, "Terrain coordinates must be finite.");
        var partitionSize = Math.Max(DefaultTerrainPartitionSizeMeters, Config.WorldEnvironment.TerrainDetailScaleMeters * 32d);
        var partitionX = checked((long)Math.Floor(x / partitionSize));
        var partitionY = checked((long)Math.Floor(y / partitionSize));
        var id = new TerrainPartitionId(partitionX, partitionY);
        if (_terrainPartitions.TryGetValue(id, out var existing)) return existing;
        var minX = partitionX * partitionSize;
        var minY = partitionY * partitionSize;
        var bounds = new WorldVolume(minX, minY, Config.WorldEnvironment.SeaLevelMeters - 12_000d, minX + partitionSize, minY + partitionSize, Config.WorldEnvironment.SeaLevelMeters + 12_000d);
        var partition = new TerrainPartition(id, bounds, new TerrainSurface(EnvironmentGenerator, bounds));
        _terrainPartitions.Add(id, partition);
        return partition;
    }

    public TerrainSurfaceSample QueryTerrainSurface(double x, double y) => GetTerrainPartition(x, y).Surface.Sample(x, y);
    public TerrainVolumeSample QueryTerrainVolume(WorldPoint position) => GetTerrainPartition(position).Volume.Sample(position);
    public IReadOnlyList<TerrainSurfaceIntersection> QueryTerrainSurfaces(double x, double y, double minimumZ, double maximumZ) => GetTerrainPartition(x, y).Volume.GetSurfaces(x, y, minimumZ, maximumZ);
    public WorldPoint SnapToGround(WorldPoint position)
    {
        var partition = GetTerrainPartition(position);
        return TerrainConstraintEvaluator.SnapToGround(partition.Surface, position);
    }

    public TerrainConstraintResult EvaluateTerrainConstraint(WorldVolume footprint, TerrainConstraintKind kind)
    {
        var centerX = (footprint.MinX + footprint.MaxX) * 0.5d;
        var centerY = (footprint.MinY + footprint.MaxY) * 0.5d;
        var centerPartition = GetTerrainPartition(centerX, centerY);
        if (footprint.MinX >= centerPartition.Bounds.MinX && footprint.MaxX <= centerPartition.Bounds.MaxX
            && footprint.MinY >= centerPartition.Bounds.MinY && footprint.MaxY <= centerPartition.Bounds.MaxY)
        {
            return TerrainConstraintEvaluator.Evaluate(centerPartition.Surface, centerPartition.Volume, footprint, kind);
        }

        var margin = Math.Max(4d, Config.WorldEnvironment.TerrainDetailScaleMeters / 16d);
        var bounds = new WorldVolume(
            footprint.MinX - margin,
            footprint.MinY - margin,
            Config.WorldEnvironment.SeaLevelMeters - 12_000d,
            footprint.MaxX + margin,
            footprint.MaxY + margin,
            Config.WorldEnvironment.SeaLevelMeters + 12_000d);
        var surface = new TerrainSurface(EnvironmentGenerator, bounds);
        var volume = new TerrainVolume(surface);
        return TerrainConstraintEvaluator.Evaluate(surface, volume, footprint, kind);
    }

    public IReadOnlyList<GeographicFeature> GetGeographicFeatures(WorldVolume volume, int maximumFeatures = 128)
    {
        var generated = EnvironmentGenerator.DetectGeographicFeatures(volume, maximumFeatures);
        foreach (var feature in generated)
        {
            if (_derivedNaturalToponyms.ContainsKey(feature.Id)) continue;
            _derivedNaturalToponyms.Add(feature.Id, EnvironmentGenerator.CreateToponym(feature));
        }
        return generated;
    }

    public bool TryGetNaturalToponym(GeographicFeatureId featureId, out NaturalToponym? toponym)
    {
        toponym = _naturalToponyms.Values.FirstOrDefault(item => item.FeatureId == featureId);
        if (toponym is not null) return true;
        return _derivedNaturalToponyms.TryGetValue(featureId, out toponym);
    }

    public WorldEnvironmentSnapshot CreateWorldEnvironmentSnapshot(WorldVolume volume, int sampleColumns = 8, int sampleRows = 8, int maximumFeatures = 128)
    {
        if (sampleColumns is <= 0 or > 32) throw new ArgumentOutOfRangeException(nameof(sampleColumns));
        if (sampleRows is <= 0 or > 32) throw new ArgumentOutOfRangeException(nameof(sampleRows));
        var samples = new RegionalEnvironmentSample[sampleColumns * sampleRows];
        var index = 0;
        for (var row = 0; row < sampleRows; row++)
        {
            var y = volume.Depth == 0d ? volume.MinY : volume.MinY + ((row + 0.5d) / sampleRows * volume.Depth);
            for (var column = 0; column < sampleColumns; column++)
            {
                var x = volume.Width == 0d ? volume.MinX : volume.MinX + ((column + 0.5d) / sampleColumns * volume.Width);
                samples[index++] = QueryEnvironment(new WorldPoint(x, y, 0d));
            }
        }
        var features = GetGeographicFeatures(volume, maximumFeatures);
        var toponyms = features.Select(EnvironmentGenerator.CreateToponym).OrderBy(static item => item.Id.Value).ToArray();
        return new WorldEnvironmentSnapshot(Config.WorldEnvironment, volume, samples, features, toponyms, Time.TickCount);
    }

    private WorldEnvironmentGenerator EnvironmentGenerator => _worldEnvironmentGenerator ??= new WorldEnvironmentGenerator(Config.WorldEnvironment);

    private WorldEnvironmentCheckpoint CreateWorldEnvironmentCheckpoint() => new(
        Config.WorldEnvironment,
        _geographicFeatures.Values.OrderBy(static item => item.Id.Value).ToArray(),
        _naturalToponyms.Values.OrderBy(static item => item.Id.Value).ToArray());

    private void RestoreWorldEnvironment(WorldEnvironmentCheckpoint? checkpoint)
    {
        if (checkpoint is null) return;
        if (checkpoint.Config != Config.WorldEnvironment) throw new ArgumentException("World environment checkpoint config does not match the simulation config.", nameof(checkpoint));
        _geographicFeatures.Clear();
        _naturalToponyms.Clear();
        _derivedNaturalToponyms.Clear();
        foreach (var feature in checkpoint.Features.OrderBy(static item => item.Id.Value)) _geographicFeatures.Add(feature.Id, feature);
        foreach (var toponym in checkpoint.Toponyms.OrderBy(static item => item.Id.Value)) _naturalToponyms.Add(toponym.Id, toponym);
        _terrainPartitions.Clear();
        _worldEnvironmentGenerator = new WorldEnvironmentGenerator(Config.WorldEnvironment);
    }

    private static void ValidateWorldEnvironmentCheckpoint(SimulationCheckpoint checkpoint)
    {
        var worldEnvironment = checkpoint.Economy?.WorldEnvironment;
        if (worldEnvironment is null) return;
        ArgumentNullException.ThrowIfNull(worldEnvironment.Config);
        ArgumentNullException.ThrowIfNull(worldEnvironment.Features);
        ArgumentNullException.ThrowIfNull(worldEnvironment.Toponyms);
        var featureIds = new HashSet<GeographicFeatureId>();
        foreach (var feature in worldEnvironment.Features)
        {
            ArgumentNullException.ThrowIfNull(feature);
            if (feature.Id.Value == 0 || !featureIds.Add(feature.Id)) throw new ArgumentException("Geographic feature IDs must be unique and greater than zero.", nameof(checkpoint));
            if (!Enum.IsDefined(feature.Type)) throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ArgumentNullException.ThrowIfNull(feature.Geometry);
            if (feature.Geometry.Count == 0) throw new ArgumentException("Geographic features must contain geometry.", nameof(checkpoint));
            if (!double.IsFinite(feature.AreaSquareMeters) || feature.AreaSquareMeters <= 0d) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Geographic feature area must be finite and positive.");
            if (!double.IsFinite(feature.MinimumElevationMeters) || !double.IsFinite(feature.MaximumElevationMeters) || feature.MaximumElevationMeters < feature.MinimumElevationMeters) throw new ArgumentOutOfRangeException(nameof(checkpoint));
            foreach (var point in feature.Geometry) ValidatePoint(point);
            if (feature.ParentId is { } parentId && parentId == feature.Id) throw new ArgumentException("Geographic features cannot parent themselves.", nameof(checkpoint));
        }
        foreach (var feature in worldEnvironment.Features)
        {
            if (feature.ParentId is { } parentId && !featureIds.Contains(parentId)) throw new ArgumentException("Geographic feature references a missing parent.", nameof(checkpoint));
        }
        ValidateAcyclicParentGraph(worldEnvironment.Features.Select(static item => (item.Id, item.ParentId)), "Geographic feature", nameof(checkpoint));
        var toponymIds = new HashSet<ToponymId>();
        foreach (var toponym in worldEnvironment.Toponyms)
        {
            ArgumentNullException.ThrowIfNull(toponym);
            if (toponym.Id.Value == 0 || !toponymIds.Add(toponym.Id)) throw new ArgumentException("Toponym IDs must be unique and greater than zero.", nameof(checkpoint));
            if (!featureIds.Contains(toponym.FeatureId)) throw new ArgumentException("Toponym references a missing geographic feature.", nameof(checkpoint));
            if (string.IsNullOrWhiteSpace(toponym.Name) || toponym.Name.Length > 128) throw new ArgumentException("Toponym names must be non-empty and bounded.", nameof(checkpoint));
            ArgumentNullException.ThrowIfNull(toponym.Provenance);
            if (!featureIds.Contains(toponym.Provenance.SourceFeatureId)) throw new ArgumentException("Toponym provenance references a missing geographic feature.", nameof(checkpoint));
            if (string.IsNullOrWhiteSpace(toponym.Provenance.GeneratorKey) || toponym.Provenance.GeneratorKey.Length > 128) throw new ArgumentException("Toponym provenance generator key must be non-empty and bounded.", nameof(checkpoint));
        }
        foreach (var toponym in worldEnvironment.Toponyms)
        {
            if (toponym.Provenance.ParentToponymId is { } parentId && !toponymIds.Contains(parentId))
                throw new ArgumentException("Toponym provenance references a missing parent toponym.", nameof(checkpoint));
        }
        ValidateAcyclicParentGraph(worldEnvironment.Toponyms.Select(static item => (item.Id, item.Provenance.ParentToponymId)), "Natural toponym", nameof(checkpoint));
    }

    private static void ValidateAcyclicParentGraph<T>(IEnumerable<(T Id, T? ParentId)> nodes, string entityName, string parameterName)
        where T : struct, IEquatable<T>
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        foreach (var start in parents.Keys)
        {
            var seen = new HashSet<T>();
            var current = start;
            while (parents.TryGetValue(current, out var parent) && parent is { } parentId)
            {
                if (!seen.Add(current)) throw new ArgumentException($"{entityName} parent graph contains a cycle.", parameterName);
                current = parentId;
            }
        }
    }
}
