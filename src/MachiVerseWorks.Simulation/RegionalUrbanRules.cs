namespace MachiVerseWorks.Simulation;

public sealed record RoadContextAnalysis(
    RegionalCorridorId CorridorId,
    double MaximumGrade,
    double MaximumTurnAngleDegrees,
    double FloodRisk,
    bool CrossesWater,
    bool IsRockSlope,
    bool IsMountainPass,
    bool RequiresTunnel,
    bool IsCoastalLowland,
    GeographicFeatureId? FeatureId,
    SettlementId DestinationSettlementId);

public sealed class RegionalRoadContextAnalyzer
{
    private readonly WorldEnvironmentGenerator _environment;

    public RegionalRoadContextAnalyzer(WorldEnvironmentGenerator environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public RoadContextAnalysis Analyze(
        RegionalCorridor corridor,
        IReadOnlyList<GeographicFeature> features)
    {
        ArgumentNullException.ThrowIfNull(corridor);
        ArgumentNullException.ThrowIfNull(features);
        if (corridor.Geometry.Count < 2) throw new ArgumentException("Corridor geometry requires at least two points.", nameof(corridor));

        var maximumGrade = 0d;
        var maximumTurn = 0d;
        var floodRisk = 0d;
        var crossesWater = false;
        var ruggedness = 0d;
        foreach (var point in corridor.Geometry)
        {
            var sample = _environment.Sample(point);
            floodRisk = Math.Max(floodRisk, sample.Hydrology.FloodRisk);
            ruggedness = Math.Max(ruggedness, sample.TerrainRuggedness);
            crossesWater |= sample.Hydrology.SurfaceWater is SurfaceWaterKind.River or SurfaceWaterKind.Tributary or SurfaceWaterKind.Lake;
        }
        for (var index = 1; index < corridor.Geometry.Count; index++)
        {
            var a = corridor.Geometry[index - 1];
            var b = corridor.Geometry[index];
            var horizontal = Distance2D(a, b);
            if (horizontal > 1e-6) maximumGrade = Math.Max(maximumGrade, Math.Abs(b.Z - a.Z) / horizontal);
        }
        for (var index = 1; index + 1 < corridor.Geometry.Count; index++)
            maximumTurn = Math.Max(maximumTurn, CalculateTurnAngle(corridor.Geometry[index - 1], corridor.Geometry[index], corridor.Geometry[index + 1]));

        var middle = corridor.Geometry[corridor.Geometry.Count / 2];
        var nearestFeature = features
            .OrderBy(feature => Distance2D(middle, Center(feature.Bounds)))
            .FirstOrDefault();
        var isMountainPass = nearestFeature?.Type == GeographicFeatureType.Pass;
        var requiresTunnel = ruggedness > 0.82d && maximumGrade > 0.09d;
        var coastal = _environment.Sample(middle).CoastlineDistanceMeters < 8_000d;
        return new RoadContextAnalysis(
            corridor.Id,
            Math.Clamp(maximumGrade, 0d, 1d),
            maximumTurn,
            floodRisk,
            crossesWater,
            ruggedness > 0.72d,
            isMountainPass,
            requiresTunnel,
            coastal,
            nearestFeature?.Id,
            corridor.ToSettlementId);
    }

    private static double CalculateTurnAngle(WorldPoint a, WorldPoint b, WorldPoint c)
    {
        var ax = a.X - b.X;
        var ay = a.Y - b.Y;
        var bx = c.X - b.X;
        var by = c.Y - b.Y;
        var al = Math.Sqrt(ax * ax + ay * ay);
        var bl = Math.Sqrt(bx * bx + by * by);
        if (al <= 1e-9 || bl <= 1e-9) return 0d;
        var cosine = Math.Clamp((ax * bx + ay * by) / (al * bl), -1d, 1d);
        return 180d - (Math.Acos(cosine) * 180d / Math.PI);
    }

    private static WorldPoint Center(WorldVolume volume) => new(
        (volume.MinX + volume.MaxX) * 0.5d,
        (volume.MinY + volume.MaxY) * 0.5d,
        (volume.MinZ + volume.MaxZ) * 0.5d);

    private static double Distance2D(WorldPoint a, WorldPoint b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }
}

public readonly record struct ParcelDevelopmentDecision(
    ParcelDevelopmentState NextState,
    bool Build,
    bool Vacate,
    bool Redevelop,
    double Pressure);

public static class RegionalDevelopmentRule
{
    public static ParcelDevelopmentDecision Evaluate(Parcel parcel, double normalizedDemand, int buildingAgeYears)
    {
        ArgumentNullException.ThrowIfNull(parcel);
        if (!double.IsFinite(normalizedDemand) || normalizedDemand is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(normalizedDemand));
        ArgumentOutOfRangeException.ThrowIfNegative(buildingAgeYears);

        var pressure = Math.Clamp(
            normalizedDemand * 0.55d
            + parcel.DevelopmentSuitability * 0.25d
            + parcel.LandValue * 0.20d,
            0d,
            1d);
        if (parcel.DevelopmentState == ParcelDevelopmentState.Vacant)
            return pressure >= 0.48d
                ? new(ParcelDevelopmentState.Developing, Build: true, Vacate: false, Redevelop: false, pressure)
                : new(ParcelDevelopmentState.Vacant, Build: false, Vacate: false, Redevelop: false, pressure);
        if (normalizedDemand < 0.16d)
            return new(ParcelDevelopmentState.Vacant, Build: false, Vacate: true, Redevelop: false, pressure);
        if (buildingAgeYears >= 35 && pressure >= 0.67d)
            return new(ParcelDevelopmentState.Redeveloping, Build: false, Vacate: false, Redevelop: true, pressure);
        return new(ParcelDevelopmentState.Occupied, Build: false, Vacate: false, Redevelop: false, pressure);
    }
}

public static class RegionalRoadSignRule
{
    public static IReadOnlyList<RoadSignKind> DetermineRequiredSigns(RoadContextAnalysis context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var result = new List<RoadSignKind> { RoadSignKind.Direction };
        if (context.MaximumGrade >= 0.08d || context.IsRockSlope) result.Add(RoadSignKind.SteepGrade);
        if (context.MaximumTurnAngleDegrees >= 45d) result.Add(RoadSignKind.SharpCurve);
        if (context.FloodRisk >= 0.62d) result.Add(RoadSignKind.FloodWarning);
        if (context.CrossesWater) result.Add(RoadSignKind.RiverCrossing);
        if (context.IsMountainPass) result.Add(RoadSignKind.MountainPass);
        if (context.RequiresTunnel) result.Add(RoadSignKind.Tunnel);
        if (context.IsCoastalLowland) result.Add(RoadSignKind.CoastalLowland);
        return result.Distinct().ToArray();
    }
}

public static class RegionalStructureNaming
{
    public static HumanToponym CreateBridgeName(RegionalCorridor corridor, RoadContextAnalysis context, IReadOnlyList<HumanToponym> existingNames)
        => CreateStructureName(HumanToponymKind.Bridge, "Bridge", corridor, context, existingNames, 0xB12D6EUL);

    public static HumanToponym CreateTunnelName(RegionalCorridor corridor, RoadContextAnalysis context, IReadOnlyList<HumanToponym> existingNames)
        => CreateStructureName(HumanToponymKind.Tunnel, "Tunnel", corridor, context, existingNames, 0x7A66E1UL);

    private static HumanToponym CreateStructureName(
        HumanToponymKind kind,
        string suffix,
        RegionalCorridor corridor,
        RoadContextAnalysis context,
        IReadOnlyList<HumanToponym> existingNames,
        ulong salt)
    {
        ArgumentNullException.ThrowIfNull(corridor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(existingNames);
        var corridorName = corridor.NameId is { } corridorNameId
            ? existingNames.FirstOrDefault(item => item.Id == corridorNameId)
            : null;
        var natural = existingNames
            .Select(static item => item.Provenance.SourceNaturalToponym)
            .FirstOrDefault(item => item is not null && item.FeatureId == context.FeatureId);
        var stem = natural?.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? corridorName?.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? "Regional";
        var id = new HumanToponymId(EnsureNonZero(Hash64(corridor.Id.Value ^ salt)));
        return new HumanToponym(
            id,
            kind,
            $"{stem} {suffix}",
            new HumanToponymProvenance(natural, context.FeatureId, corridor.NameId, "phase30-structure-v1"));
    }

    private static ulong Hash64(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static ulong EnsureNonZero(ulong value) => value == 0UL ? 1UL : value;
}

public enum RegionalGenerationFixtureKind : byte
{
    River = 0,
    Port = 1,
    Basin = 2,
    Valley = 3,
    Mountain = 4,
    Cold = 5,
    DryInland = 6,
    Island = 7,
}

public sealed record RegionalGenerationFixtureCase(
    RegionalGenerationFixtureKind Kind,
    WorldEnvironmentConfig Environment,
    WorldVolume Volume,
    RegionalGenerationOptions Options);

public static class RegionalGenerationFixture
{
    public static RegionalGenerationFixtureCase Create(RegionalGenerationFixtureKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var seed = 30_000UL + (ulong)kind * 997UL + 1UL;
        var temperature = kind == RegionalGenerationFixtureKind.Cold ? -6d : 11d;
        var precipitation = kind == RegionalGenerationFixtureKind.DryInland ? 220d : 980d;
        var maritime = kind is RegionalGenerationFixtureKind.Port or RegionalGenerationFixtureKind.Island ? 0.72d : 0.38d;
        var continentality = kind == RegionalGenerationFixtureKind.DryInland ? 0.82d : 0.55d;
        var environment = new WorldEnvironmentConfig(
            seed,
            new WorldVector(0.15d, 1d, 0d),
            latitudeDegrees: kind == RegionalGenerationFixtureKind.Cold ? 62d : 43d,
            continentality,
            maritime,
            temperature,
            seasonalityCelsius: kind == RegionalGenerationFixtureKind.Cold ? 30d : 20d,
            annualPrecipitationMillimeters: precipitation);
        var span = 1_600_000d;
        var offsetX = ((int)kind - 3) * 310_000d;
        var offsetY = (((int)kind * 5) % 7 - 3) * 270_000d;
        var volume = new WorldVolume(
            offsetX - span * 0.5d,
            offsetY - span * 0.5d,
            -12_000d,
            offsetX + span * 0.5d,
            offsetY + span * 0.5d,
            12_000d);
        return new RegionalGenerationFixtureCase(
            kind,
            environment,
            volume,
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 4, iterationBudget: 1));
    }
}
