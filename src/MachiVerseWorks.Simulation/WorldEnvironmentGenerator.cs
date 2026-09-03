namespace MachiVerseWorks.Simulation;

public sealed class WorldEnvironmentGenerator
{
    private const double MetersPerLatitudeDegree = 111_320d;
    public const int MaximumSettlementCandidateCount = 1_024;
    public const int MaximumGeographicFeatureCount = 1_024;
    public const int MaximumSamplingGridCells = 16_384;
    private readonly WorldEnvironmentConfig _config;

    public WorldEnvironmentGenerator(WorldEnvironmentConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public WorldEnvironmentConfig Config => _config;

    public RegionalEnvironmentSample Sample(WorldPoint position)
    {
        var elevation = SampleElevation(position.X, position.Y);
        var landform = DetermineLandform(position.X, position.Y, elevation);
        var latitude = CalculateLatitude(position.X, position.Y);
        var coastlineDistance = _config.ConfiguredCoastlineDistanceMeters ?? EstimateCoastlineDistance(position.X, position.Y, elevation);
        var climate = SampleClimate(position.X, position.Y, elevation, latitude, coastlineDistance);
        var hydrology = SampleHydrology(position.X, position.Y, elevation, climate.AnnualPrecipitationMillimeters, landform);
        var ruggedness = SampleRuggedness(position.X, position.Y, elevation);
        var buildability = CalculateBuildability(elevation, ruggedness, hydrology, landform);
        var settlement = CalculateSettlementScore(buildability, climate, hydrology, coastlineDistance, landform);
        return new RegionalEnvironmentSample(
            new WorldPoint(position.X, position.Y, elevation),
            landform,
            elevation,
            coastlineDistance,
            climate,
            hydrology,
            ruggedness,
            buildability,
            settlement);
    }

    public double SampleElevation(double x, double y)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        var scale = _config.GlobalScaleMeters;
        var continental = FractalNoise(x, y, scale * 3.4d, 4, 0x1101UL);
        var tectonic = RidgedNoise(x, y, scale * 0.95d, 4, 0x2202UL);
        var basins = FractalNoise(x, y, scale * 0.55d, 3, 0x3303UL);
        var islandMask = FractalNoise(x, y, scale * 0.34d, 3, 0x4404UL);
        var landSignal = (continental * 0.78d) + (islandMask * 0.22d) + ((_config.Continentality - 0.5d) * 0.12d);
        var baseElevation = (landSignal - 0.49d) * 5_600d;
        var mountainElevation = Math.Pow(tectonic, 2.1d) * Math.Max(0d, landSignal - 0.44d) * 7_200d;
        var basinOffset = (basins - 0.5d) * 900d;
        return Math.Clamp(_config.SeaLevelMeters + baseElevation + mountainElevation + basinOffset, _config.SeaLevelMeters - 8_500d, _config.SeaLevelMeters + 7_800d);
    }

    public IReadOnlyList<SettlementCandidateRegion> SelectSettlementCandidates(WorldVolume volume, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > MaximumSettlementCandidateCount)
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Settlement candidate count cannot exceed {MaximumSettlementCandidateCount}.");
        if (count == 0) return Array.Empty<SettlementCandidateRegion>();
        var targetCount = Math.Max(checked(count * 8), 64);
        var candidates = CreateSettlementCandidatePool(volume, targetCount);
        var selected = new List<SettlementCandidateRegion>(Math.Min(count, candidates.Count));

        foreach (var group in candidates.GroupBy(static item => item.Environment).OrderBy(static group => group.Key))
        {
            if (selected.Count >= count) break;
            selected.Add(group.OrderByDescending(static item => item.TotalScore).ThenBy(static item => item.Center.X).ThenBy(static item => item.Center.Y).First());
        }

        foreach (var candidate in candidates
                     .OrderByDescending(item => item.TotalScore + (DeterministicUnit(candidateX: item.Center.X, candidateY: item.Center.Y, 0x9911UL) * 0.015d))
                     .ThenBy(static item => item.Center.X)
                     .ThenBy(static item => item.Center.Y))
        {
            if (selected.Count >= count) break;
            if (!selected.Contains(candidate)) selected.Add(candidate);
        }

        return selected;
    }

    public IReadOnlyList<GeographicFeature> DetectGeographicFeatures(WorldVolume volume, int maximumFeatures = 128)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFeatures);
        if (maximumFeatures > MaximumGeographicFeatureCount)
            throw new ArgumentOutOfRangeException(nameof(maximumFeatures), maximumFeatures, $"Geographic feature count cannot exceed {MaximumGeographicFeatureCount}.");
        var width = Math.Max(volume.Width, _config.TerrainDetailScaleMeters);
        var depth = Math.Max(volume.Depth, _config.TerrainDetailScaleMeters);
        var targetCells = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(checked(maximumFeatures * 2d))));
        if (checked(targetCells * targetCells) > MaximumSamplingGridCells)
            throw new ArgumentOutOfRangeException(nameof(maximumFeatures), maximumFeatures, $"Geographic feature query cannot exceed {MaximumSamplingGridCells} sampling cells.");
        var stepX = width / targetCells;
        var stepY = depth / targetCells;
        var features = new Dictionary<GeographicFeatureId, GeographicFeature>();

        for (var iy = 0; iy < targetCells && features.Count < maximumFeatures; iy++)
        {
            for (var ix = 0; ix < targetCells && features.Count < maximumFeatures; ix++)
            {
                var x = volume.MinX + Math.Min(volume.Width, (ix + 0.5d) * stepX);
                var y = volume.MinY + Math.Min(volume.Depth, (iy + 0.5d) * stepY);
                var sample = Sample(new WorldPoint(x, y, 0d));
                var type = DetermineFeatureType(sample, DeterministicUnit(x, y, 0xA551UL));
                if (type is null) continue;
                var feature = CreateFeature(type.Value, sample, stepX, stepY);
                features.TryAdd(feature.Id, feature);
            }
        }

        return features.Values.OrderBy(static item => item.Id.Value).ToArray();
    }

    public NaturalToponym CreateToponym(GeographicFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        if (feature.Id.Value == 0) throw new ArgumentOutOfRangeException(nameof(feature), "Geographic feature IDs must be stable and greater than zero.");
        var hash = Hash64(feature.Id.Value ^ _config.WorldSeed ^ 0x544F504FUL);
        var first = NameSyllables[(int)(hash % (ulong)NameSyllables.Length)];
        var second = NameSyllables[(int)((hash >> 11) % (ulong)NameSyllables.Length)];
        var third = NameSyllables[(int)((hash >> 23) % (ulong)NameSyllables.Length)];
        var stem = first + second + ((hash & 1UL) == 0 ? string.Empty : third);
        var name = stem + GetFeatureSuffix(feature.Type);
        var id = new ToponymId(EnsureNonZero(Hash64(feature.Id.Value ^ 0xBADC0FFEE0DDF00DUL)));
        return new NaturalToponym(
            id,
            feature.Id,
            name,
            new ToponymProvenance(ToponymProvenanceKind.GeneratedNaturalFeature, feature.Id, null, "phase29-natural-v1"));
    }

    private List<SettlementCandidateRegion> CreateSettlementCandidatePool(WorldVolume volume, int targetCount)
    {
        var columns = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(targetCount)));
        var rows = columns;
        if (checked(columns * rows) > MaximumSamplingGridCells)
            throw new ArgumentOutOfRangeException(nameof(targetCount), targetCount, $"Settlement candidate query cannot exceed {MaximumSamplingGridCells} sampling cells.");
        var width = Math.Max(volume.Width, _config.GlobalScaleMeters * 0.1d);
        var depth = Math.Max(volume.Depth, _config.GlobalScaleMeters * 0.1d);
        var candidates = new List<SettlementCandidateRegion>(columns * rows);
        for (var yIndex = 0; yIndex < rows; yIndex++)
        {
            for (var xIndex = 0; xIndex < columns; xIndex++)
            {
                var x = volume.Width == 0d ? volume.MinX : volume.MinX + ((xIndex + 0.5d) / columns * width);
                var y = volume.Depth == 0d ? volume.MinY : volume.MinY + ((yIndex + 0.5d) / rows * depth);
                x = Math.Min(volume.MaxX, x);
                y = Math.Min(volume.MaxY, y);
                var sample = Sample(new WorldPoint(x, y, 0d));
                if (sample.Landform == GlobalLandformKind.Ocean) continue;
                var environment = ClassifySettlementEnvironment(sample);
                var natural = sample.Buildability * Math.Clamp(1d - Math.Abs(sample.Climate.MeanAnnualTemperatureCelsius - 14d) / 45d, 0d, 1d);
                var water = Math.Clamp((sample.Hydrology.RiverStrength * 0.6d) + (1d - Math.Min(sample.CoastlineDistanceMeters, 150_000d) / 150_000d) * 0.4d, 0d, 1d);
                var transport = Math.Clamp((1d - sample.TerrainRuggedness) * 0.72d + sample.Buildability * 0.28d, 0d, 1d);
                var total = Math.Clamp((natural * 0.42d) + (transport * 0.33d) + (water * 0.25d), 0d, 1d);
                candidates.Add(new SettlementCandidateRegion(sample.Position, environment, natural, transport, water, total));
            }
        }
        return candidates;
    }

    private GlobalLandformKind DetermineLandform(double x, double y, double elevation)
    {
        if (elevation <= _config.SeaLevelMeters) return GlobalLandformKind.Ocean;
        var continental = FractalNoise(x, y, _config.GlobalScaleMeters * 3.4d, 3, 0x1101UL);
        var local = FractalNoise(x, y, _config.GlobalScaleMeters * 0.31d, 3, 0x4404UL);
        return continental < 0.51d && local > 0.57d ? GlobalLandformKind.Island : GlobalLandformKind.Continent;
    }

    private double CalculateLatitude(double x, double y)
    {
        var northing = (_config.GeographicNorth.X * x) + (_config.GeographicNorth.Y * y);
        return Math.Clamp(_config.LatitudeDegrees + (northing / MetersPerLatitudeDegree), -90d, 90d);
    }

    private double EstimateCoastlineDistance(double x, double y, double elevation)
    {
        var sampleSpacing = Math.Max(5_000d, _config.GlobalScaleMeters * 0.08d);
        var nearestOpposite = double.PositiveInfinity;
        var isLand = elevation > _config.SeaLevelMeters;
        for (var ring = 1; ring <= 12; ring++)
        {
            var distance = ring * sampleSpacing;
            for (var index = 0; index < 16; index++)
            {
                var angle = index * (Math.PI * 2d / 16d);
                var other = SampleElevation(x + (Math.Cos(angle) * distance), y + (Math.Sin(angle) * distance));
                if ((other > _config.SeaLevelMeters) != isLand) nearestOpposite = Math.Min(nearestOpposite, distance);
            }
            if (double.IsFinite(nearestOpposite)) break;
        }
        return double.IsFinite(nearestOpposite) ? nearestOpposite : 12d * sampleSpacing;
    }

    private ClimateSample SampleClimate(double x, double y, double elevation, double latitude, double coastlineDistance)
    {
        var latitudeDelta = Math.Abs(latitude) - Math.Abs(_config.LatitudeDegrees);
        var lapse = Math.Max(0d, elevation - _config.SeaLevelMeters) * 0.0065d;
        var maritime = Math.Clamp(_config.MaritimeInfluence * Math.Exp(-coastlineDistance / 300_000d), 0d, 1d);
        var localTemperature = (FractalNoise(x, y, _config.GlobalScaleMeters * 0.7d, 3, 0x7707UL) - 0.5d) * 6d;
        var temperature = _config.MeanAnnualTemperatureCelsius - (latitudeDelta * 0.42d) - lapse + localTemperature;
        var seasonality = _config.SeasonalityCelsius * (0.62d + (_config.Continentality * 0.58d)) * (1d - (maritime * 0.35d));
        var precipitationNoise = FractalNoise(x, y, _config.GlobalScaleMeters * 0.43d, 4, 0x8808UL);
        var orographic = Math.Clamp((SampleRuggedness(x, y, elevation) - 0.25d) * 0.7d, -0.1d, 0.45d);
        var precipitation = Math.Max(0d, _config.AnnualPrecipitationMillimeters * (0.48d + (precipitationNoise * 0.7d) + (maritime * 0.35d) + orographic));
        return new ClimateSample(latitude, temperature, seasonality, precipitation, maritime, _config.Continentality);
    }

    private HydrologySample SampleHydrology(double x, double y, double elevation, double precipitation, GlobalLandformKind landform)
    {
        if (landform == GlobalLandformKind.Ocean)
            return new HydrologySample(SurfaceWaterKind.Ocean, 1d, 0d, 0d, new WorldVector(0d, 0d, 0d));
        var spacing = Math.Max(1_000d, _config.GlobalScaleMeters * 0.012d);
        var dx = SampleElevation(x + spacing, y) - SampleElevation(x - spacing, y);
        var dy = SampleElevation(x, y + spacing) - SampleElevation(x, y - spacing);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var flow = length <= 1e-9 ? new WorldVector(0d, 0d, 0d) : new WorldVector(-dx / length, -dy / length, 0d);
        var drainageNoise = FractalNoise(x, y, _config.GlobalScaleMeters * 0.16d, 4, 0x6606UL);
        var drainage = Math.Clamp((precipitation / 2_000d) * 0.55d + drainageNoise * 0.45d, 0d, 1d);
        var riverStrength = Math.Clamp((drainage - 0.55d) * 2.4d, 0d, 1d);
        var basin = 1d - Math.Clamp(length / 3_000d, 0d, 1d);
        var lake = drainage > 0.72d && basin > 0.75d && DeterministicUnit(x, y, 0x5515UL) > 0.72d;
        var surfaceWater = lake ? SurfaceWaterKind.Lake : riverStrength > 0.67d ? SurfaceWaterKind.River : riverStrength > 0.38d ? SurfaceWaterKind.Tributary : SurfaceWaterKind.None;
        var floodRisk = Math.Clamp((riverStrength * 0.64d) + (basin * 0.24d) + (drainage * 0.12d), 0d, 1d);
        return new HydrologySample(surfaceWater, drainage, riverStrength, floodRisk, flow);
    }

    private double SampleRuggedness(double x, double y, double elevation)
    {
        var spacing = Math.Max(2_000d, _config.GlobalScaleMeters * 0.018d);
        var e1 = SampleElevation(x + spacing, y);
        var e2 = SampleElevation(x - spacing, y);
        var e3 = SampleElevation(x, y + spacing);
        var e4 = SampleElevation(x, y - spacing);
        var spread = Math.Max(Math.Max(Math.Abs(e1 - elevation), Math.Abs(e2 - elevation)), Math.Max(Math.Abs(e3 - elevation), Math.Abs(e4 - elevation)));
        return Math.Clamp(spread / 1_800d, 0d, 1d);
    }

    private static double CalculateBuildability(double elevation, double ruggedness, HydrologySample hydrology, GlobalLandformKind landform)
    {
        if (landform == GlobalLandformKind.Ocean) return 0d;
        var elevationPenalty = Math.Clamp(Math.Max(0d, elevation - 2_500d) / 3_500d, 0d, 0.65d);
        return Math.Clamp(1d - (ruggedness * 0.72d) - (hydrology.FloodRisk * 0.35d) - elevationPenalty, 0d, 1d);
    }

    private static double CalculateSettlementScore(double buildability, ClimateSample climate, HydrologySample hydrology, double coastlineDistance, GlobalLandformKind landform)
    {
        if (landform == GlobalLandformKind.Ocean) return 0d;
        var climateComfort = Math.Clamp(1d - Math.Abs(climate.MeanAnnualTemperatureCelsius - 14d) / 42d, 0d, 1d);
        var waterAccess = Math.Max(hydrology.RiverStrength, 1d - Math.Clamp(coastlineDistance / 180_000d, 0d, 1d));
        return Math.Clamp((buildability * 0.5d) + (climateComfort * 0.27d) + (waterAccess * 0.23d), 0d, 1d);
    }

    private static SettlementEnvironmentKind ClassifySettlementEnvironment(RegionalEnvironmentSample sample)
    {
        if (sample.Landform == GlobalLandformKind.Island) return SettlementEnvironmentKind.Island;
        if (sample.CoastlineDistanceMeters < 40_000d) return SettlementEnvironmentKind.Coastal;
        if (sample.Hydrology.RiverStrength > 0.45d) return SettlementEnvironmentKind.River;
        if (sample.Climate.MeanAnnualTemperatureCelsius < 2d) return SettlementEnvironmentKind.Cold;
        if (sample.Climate.AnnualPrecipitationMillimeters < 350d) return SettlementEnvironmentKind.Dry;
        if (sample.ElevationMeters > 1_500d || sample.TerrainRuggedness > 0.62d) return SettlementEnvironmentKind.Mountain;
        if (sample.Hydrology.FloodRisk > 0.55d) return SettlementEnvironmentKind.Basin;
        return SettlementEnvironmentKind.InlandPlain;
    }

    private GeographicFeatureType? DetermineFeatureType(RegionalEnvironmentSample sample, double selector)
    {
        if (sample.Landform == GlobalLandformKind.Ocean) return null;
        if (sample.Landform == GlobalLandformKind.Island && selector < 0.24d) return GeographicFeatureType.Island;
        if (sample.Hydrology.SurfaceWater == SurfaceWaterKind.Lake) return GeographicFeatureType.Lake;
        if (sample.Hydrology.SurfaceWater == SurfaceWaterKind.River) return selector < 0.7d ? GeographicFeatureType.River : GeographicFeatureType.Valley;
        if (sample.Hydrology.SurfaceWater == SurfaceWaterKind.Tributary) return GeographicFeatureType.Tributary;
        if (sample.CoastlineDistanceMeters < 20_000d)
        {
            if (selector < 0.25d) return GeographicFeatureType.Coast;
            if (selector < 0.5d) return GeographicFeatureType.Cape;
            if (selector < 0.75d) return GeographicFeatureType.Bay;
            return GeographicFeatureType.Peninsula;
        }
        if (sample.ElevationMeters > _config.SeaLevelMeters + 2_400d) return selector < 0.55d ? GeographicFeatureType.Mountain : GeographicFeatureType.MountainRange;
        if (sample.TerrainRuggedness > 0.72d) return selector < 0.5d ? GeographicFeatureType.Pass : GeographicFeatureType.MountainRange;
        if (sample.ElevationMeters > _config.SeaLevelMeters + 900d && sample.TerrainRuggedness < 0.35d) return GeographicFeatureType.Plateau;
        if (sample.Hydrology.FloodRisk > 0.64d) return GeographicFeatureType.Basin;
        if (sample.TerrainRuggedness < 0.2d) return GeographicFeatureType.Plain;
        if (selector > 0.91d) return GeographicFeatureType.Cave;
        if (selector > 0.72d) return GeographicFeatureType.Valley;
        return null;
    }

    private GeographicFeature CreateFeature(GeographicFeatureType type, RegionalEnvironmentSample sample, double stepX, double stepY)
    {
        var halfX = Math.Max(_config.TerrainDetailScaleMeters * 2d, stepX * 0.38d);
        var halfY = Math.Max(_config.TerrainDetailScaleMeters * 2d, stepY * 0.38d);
        var minZ = Math.Min(_config.SeaLevelMeters, sample.ElevationMeters) - 100d;
        var maxZ = Math.Max(_config.SeaLevelMeters, sample.ElevationMeters) + Math.Max(100d, sample.TerrainRuggedness * 800d);
        var bounds = new WorldVolume(sample.Position.X - halfX, sample.Position.Y - halfY, minZ, sample.Position.X + halfX, sample.Position.Y + halfY, maxZ);
        var geometry = new[]
        {
            new WorldPoint(sample.Position.X - halfX, sample.Position.Y, sample.ElevationMeters),
            new WorldPoint(sample.Position.X, sample.Position.Y + halfY, sample.ElevationMeters),
            new WorldPoint(sample.Position.X + halfX, sample.Position.Y, sample.ElevationMeters),
            new WorldPoint(sample.Position.X, sample.Position.Y - halfY, sample.ElevationMeters),
        };
        var gridX = checked((long)Math.Floor(sample.Position.X / Math.Max(1d, _config.TerrainDetailScaleMeters)));
        var gridY = checked((long)Math.Floor(sample.Position.Y / Math.Max(1d, _config.TerrainDetailScaleMeters)));
        var raw = Hash64(unchecked((ulong)gridX) ^ RotateLeft(unchecked((ulong)gridY), 21) ^ ((ulong)type << 48) ^ _config.WorldSeed);
        return new GeographicFeature(new GeographicFeatureId(EnsureNonZero(raw)), type, bounds, geometry, null, minZ, maxZ);
    }

    private double FractalNoise(double x, double y, double baseScale, int octaves, ulong salt)
    {
        var total = 0d;
        var amplitude = 1d;
        var amplitudeTotal = 0d;
        var scale = baseScale;
        for (var octave = 0; octave < octaves; octave++)
        {
            total += ValueNoise(x, y, scale, salt + (ulong)octave * 0x9E3779B97F4A7C15UL) * amplitude;
            amplitudeTotal += amplitude;
            amplitude *= 0.5d;
            scale *= 0.5d;
        }
        return total / amplitudeTotal;
    }

    private double RidgedNoise(double x, double y, double baseScale, int octaves, ulong salt)
    {
        var noise = FractalNoise(x, y, baseScale, octaves, salt);
        return 1d - Math.Abs((noise * 2d) - 1d);
    }

    private double ValueNoise(double x, double y, double scale, ulong salt)
    {
        var gx = x / scale;
        var gy = y / scale;
        var x0 = checked((long)Math.Floor(gx));
        var y0 = checked((long)Math.Floor(gy));
        var tx = Smooth(gx - x0);
        var ty = Smooth(gy - y0);
        var n00 = Lattice(x0, y0, salt);
        var n10 = Lattice(x0 + 1, y0, salt);
        var n01 = Lattice(x0, y0 + 1, salt);
        var n11 = Lattice(x0 + 1, y0 + 1, salt);
        return Lerp(Lerp(n00, n10, tx), Lerp(n01, n11, tx), ty);
    }

    private double Lattice(long x, long y, ulong salt)
    {
        var value = unchecked((ulong)x) ^ RotateLeft(unchecked((ulong)y), 32) ^ _config.WorldSeed ^ salt;
        return ToUnit(Hash64(value));
    }

    private double DeterministicUnit(double candidateX, double candidateY, ulong salt)
    {
        var x = checked((long)Math.Floor(candidateX / Math.Max(1d, _config.TerrainDetailScaleMeters)));
        var y = checked((long)Math.Floor(candidateY / Math.Max(1d, _config.TerrainDetailScaleMeters)));
        return ToUnit(Hash64(unchecked((ulong)x) ^ RotateLeft(unchecked((ulong)y), 27) ^ salt ^ _config.WorldSeed));
    }

    private static ulong Hash64(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
    private static ulong EnsureNonZero(ulong value) => value == 0 ? 1UL : value;
    private static double ToUnit(ulong value) => (value >> 11) * (1d / 9_007_199_254_740_992d);
    private static double Smooth(double value) => value * value * (3d - (2d * value));
    private static double Lerp(double left, double right, double amount) => left + ((right - left) * amount);

    private static void ValidateCoordinate(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < -9e14d || value > 9e14d)
            throw new ArgumentOutOfRangeException(parameterName, value, "World environment coordinates must be finite and within the supported deterministic range.");
    }

    private static readonly string[] NameSyllables =
    [
        "Aru", "Bel", "Cala", "Doro", "Eli", "Fara", "Glen", "Hali",
        "Iria", "Jora", "Kela", "Luma", "Mira", "Noro", "Orin", "Pela",
        "Quen", "Rava", "Sera", "Tala", "Ulen", "Vara", "Wren", "Yora", "Zela",
    ];

    private static string GetFeatureSuffix(GeographicFeatureType type) => type switch
    {
        GeographicFeatureType.Mountain => " Peak",
        GeographicFeatureType.MountainRange => " Range",
        GeographicFeatureType.River => " River",
        GeographicFeatureType.Tributary => " Brook",
        GeographicFeatureType.Lake => " Lake",
        GeographicFeatureType.Valley => " Valley",
        GeographicFeatureType.Basin => " Basin",
        GeographicFeatureType.Plain => " Plain",
        GeographicFeatureType.Plateau => " Plateau",
        GeographicFeatureType.Pass => " Pass",
        GeographicFeatureType.Cape => " Cape",
        GeographicFeatureType.Bay => " Bay",
        GeographicFeatureType.Coast => " Coast",
        GeographicFeatureType.Island => " Isle",
        GeographicFeatureType.Peninsula => " Peninsula",
        GeographicFeatureType.Cave => " Cavern",
        _ => string.Empty,
    };
}
