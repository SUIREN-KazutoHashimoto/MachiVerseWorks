namespace MachiVerseWorks.Simulation;

public enum WorldHemisphere : byte { Northern = 0, Southern = 1 }
public enum GlobalLandformKind : byte { Ocean = 0, Continent = 1, Island = 2 }
public enum SurfaceWaterKind : byte { None = 0, Ocean = 1, Lake = 2, River = 3, Tributary = 4, Floodplain = 5 }
public enum TerrainMaterialKind : byte { Water = 0, Sand = 1, Soil = 2, Rock = 3, Snow = 4, Gravel = 5 }
public enum TerrainMatterKind : byte { Air = 0, Water = 1, Soil = 2, Rock = 3, Void = 4 }
public enum GeographicFeatureType : byte { Mountain = 0, MountainRange = 1, River = 2, Tributary = 3, Lake = 4, Valley = 5, Basin = 6, Plain = 7, Plateau = 8, Pass = 9, Cape = 10, Bay = 11, Coast = 12, Island = 13, Peninsula = 14, Cave = 15 }
public enum ToponymProvenanceKind : byte { GeneratedNaturalFeature = 0, InheritedNaturalFeature = 1 }
public enum SettlementEnvironmentKind : byte { Coastal = 0, River = 1, Basin = 2, Mountain = 3, Cold = 4, Dry = 5, Island = 6, InlandPlain = 7 }

public readonly record struct GeographicFeatureId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct ToponymId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record WorldEnvironmentConfig
{
    public const double DefaultGlobalScaleMeters = 250_000d;
    public const double DefaultTerrainDetailScaleMeters = 512d;

    [System.Text.Json.Serialization.JsonConstructor]
    public WorldEnvironmentConfig(
        ulong worldSeed,
        WorldVector geographicNorth,
        double latitudeDegrees,
        double continentality,
        double maritimeInfluence,
        double meanAnnualTemperatureCelsius,
        double seasonalityCelsius,
        double annualPrecipitationMillimeters,
        double seaLevelMeters = 0d,
        double? configuredCoastlineDistanceMeters = null,
        double globalScaleMeters = DefaultGlobalScaleMeters,
        double terrainDetailScaleMeters = DefaultTerrainDetailScaleMeters)
    {
        if (worldSeed == 0) throw new ArgumentOutOfRangeException(nameof(worldSeed), worldSeed, "World seed must be greater than zero.");
        ValidateFinite(latitudeDegrees, nameof(latitudeDegrees));
        if (latitudeDegrees is < -90d or > 90d) throw new ArgumentOutOfRangeException(nameof(latitudeDegrees), latitudeDegrees, "Latitude must be between -90 and 90 degrees.");
        ValidateUnit(continentality, nameof(continentality));
        ValidateUnit(maritimeInfluence, nameof(maritimeInfluence));
        ValidateFinite(meanAnnualTemperatureCelsius, nameof(meanAnnualTemperatureCelsius));
        ValidateNonNegative(seasonalityCelsius, nameof(seasonalityCelsius));
        ValidateNonNegative(annualPrecipitationMillimeters, nameof(annualPrecipitationMillimeters));
        ValidateFinite(seaLevelMeters, nameof(seaLevelMeters));
        if (configuredCoastlineDistanceMeters is { } coastlineDistance) ValidateNonNegative(coastlineDistance, nameof(configuredCoastlineDistanceMeters));
        ValidatePositive(globalScaleMeters, nameof(globalScaleMeters));
        ValidatePositive(terrainDetailScaleMeters, nameof(terrainDetailScaleMeters));

        var northLength = Math.Sqrt((geographicNorth.X * geographicNorth.X) + (geographicNorth.Y * geographicNorth.Y));
        if (!double.IsFinite(northLength) || northLength <= 1e-12 || Math.Abs(geographicNorth.Z) > 1e-9)
            throw new ArgumentOutOfRangeException(nameof(geographicNorth), "Geographic north must be a finite non-zero horizontal vector.");

        WorldSeed = worldSeed;
        GeographicNorth = new WorldVector(geographicNorth.X / northLength, geographicNorth.Y / northLength, 0d);
        LatitudeDegrees = latitudeDegrees;
        Continentality = continentality;
        MaritimeInfluence = maritimeInfluence;
        MeanAnnualTemperatureCelsius = meanAnnualTemperatureCelsius;
        SeasonalityCelsius = seasonalityCelsius;
        AnnualPrecipitationMillimeters = annualPrecipitationMillimeters;
        SeaLevelMeters = seaLevelMeters;
        ConfiguredCoastlineDistanceMeters = configuredCoastlineDistanceMeters;
        GlobalScaleMeters = globalScaleMeters;
        TerrainDetailScaleMeters = terrainDetailScaleMeters;
    }

    public ulong WorldSeed { get; }
    public WorldVector GeographicNorth { get; }
    public double LatitudeDegrees { get; }
    public WorldHemisphere Hemisphere => LatitudeDegrees < 0d ? WorldHemisphere.Southern : WorldHemisphere.Northern;
    public double Continentality { get; }
    public double MaritimeInfluence { get; }
    public double MeanAnnualTemperatureCelsius { get; }
    public double SeasonalityCelsius { get; }
    public double AnnualPrecipitationMillimeters { get; }
    public double SeaLevelMeters { get; }
    public double? ConfiguredCoastlineDistanceMeters { get; }
    public double GlobalScaleMeters { get; }
    public double TerrainDetailScaleMeters { get; }

    public static WorldEnvironmentConfig CreateDefault(ulong worldSeed) => new(
        worldSeed == 0 ? 1UL : worldSeed,
        new WorldVector(0d, 1d, 0d),
        latitudeDegrees: 45d,
        continentality: 0.55d,
        maritimeInfluence: 0.45d,
        meanAnnualTemperatureCelsius: 11d,
        seasonalityCelsius: 20d,
        annualPrecipitationMillimeters: 900d);

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite.");
    }

    private static void ValidatePositive(double value, string parameterName)
    {
        ValidateFinite(value, parameterName);
        if (value <= 0d) throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than zero.");
    }

    private static void ValidateNonNegative(double value, string parameterName)
    {
        ValidateFinite(value, parameterName);
        if (value < 0d) throw new ArgumentOutOfRangeException(parameterName, value, "Value cannot be negative.");
    }

    private static void ValidateUnit(double value, string parameterName)
    {
        ValidateFinite(value, parameterName);
        if (value is < 0d or > 1d) throw new ArgumentOutOfRangeException(parameterName, value, "Value must be between zero and one.");
    }
}

public readonly record struct ClimateSample(double LatitudeDegrees, double MeanAnnualTemperatureCelsius, double SeasonalAmplitudeCelsius, double AnnualPrecipitationMillimeters, double MaritimeInfluence, double Continentality);
public readonly record struct HydrologySample(SurfaceWaterKind SurfaceWater, double Drainage, double RiverStrength, double FloodRisk, WorldVector FlowDirection);
public readonly record struct RegionalEnvironmentSample(WorldPoint Position, GlobalLandformKind Landform, double ElevationMeters, double CoastlineDistanceMeters, ClimateSample Climate, HydrologySample Hydrology, double TerrainRuggedness, double Buildability, double SettlementScore);
public readonly record struct SettlementCandidateRegion(WorldPoint Center, SettlementEnvironmentKind Environment, double NaturalScore, double TransportScore, double WaterScore, double TotalScore);
public readonly record struct TerrainSurfaceSample(WorldPoint Position, WorldVector Normal, double SlopeDegrees, double Roughness, TerrainMaterialKind Material, SurfaceWaterKind SurfaceWater);
public readonly record struct TerrainSurfaceIntersection(double Z, WorldVector Normal, TerrainMaterialKind Material, bool IsPrimaryGroundSurface, bool IsWaterSurface, bool IsCavityBoundary);
public readonly record struct TerrainVolumeSample(WorldPoint Position, TerrainMatterKind Matter, double SignedDistanceToGroundMeters);
public readonly record struct TerrainConstraintResult(bool IsAllowed, double MaximumSlopeDegrees, double ElevationRangeMeters, bool IntersectsWater, bool IntersectsVoid, string Reason);

public sealed record GeographicFeature(
    GeographicFeatureId Id,
    GeographicFeatureType Type,
    WorldVolume Bounds,
    IReadOnlyList<WorldPoint> Geometry,
    GeographicFeatureId? ParentId,
    double MinimumElevationMeters,
    double MaximumElevationMeters)
{
    public double AreaSquareMeters => Bounds.Width * Bounds.Depth;
}

public sealed record ToponymProvenance(ToponymProvenanceKind Kind, GeographicFeatureId SourceFeatureId, ToponymId? ParentToponymId, string GeneratorKey);
public sealed record NaturalToponym(ToponymId Id, GeographicFeatureId FeatureId, string Name, ToponymProvenance Provenance);
public sealed record WorldEnvironmentCheckpoint(WorldEnvironmentConfig Config, IReadOnlyList<GeographicFeature> Features, IReadOnlyList<NaturalToponym> Toponyms);
public sealed record WorldEnvironmentSnapshot(
    WorldEnvironmentConfig Config,
    WorldVolume Volume,
    IReadOnlyList<RegionalEnvironmentSample> Samples,
    IReadOnlyList<TerrainSurfaceSample> TerrainSamples,
    IReadOnlyList<GeographicFeature> Features,
    IReadOnlyList<NaturalToponym> Toponyms,
    ulong TickCount);
