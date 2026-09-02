namespace MachiVerseWorks.Simulation;

public readonly record struct SettlementId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RegionalCorridorId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GrowthEventId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct DistrictId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct ParcelId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GeneratedBuildingId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GeneratedPoiId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct HumanToponymId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RoadSignId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum SettlementOriginKind : byte
{
    InlandPlain = 0,
    RiverPlain = 1,
    Estuary = 2,
    Bay = 3,
    Basin = 4,
    Valley = 5,
    MountainPass = 6,
    ResourceAccess = 7,
    Coastal = 8,
    Island = 9,
}

public enum RegionalRole : byte
{
    LocalService = 0,
    Agricultural = 1,
    Market = 2,
    Administrative = 3,
    Industrial = 4,
    Port = 5,
    TransportHub = 6,
    Resource = 7,
}

public enum InitialEconomyKind : byte
{
    Subsistence = 0,
    Agriculture = 1,
    Trade = 2,
    Manufacturing = 3,
    PortTrade = 4,
    Transport = 5,
    ResourceExtraction = 6,
    Services = 7,
}

public enum HistoricalGrowthStage : byte
{
    Origin = 0,
    CenterFormation = 1,
    UrbanExpansion = 2,
    Suburbanization = 3,
    Redevelopment = 4,
    NewCenterFormation = 5,
}

public enum RegionalCorridorKind : byte
{
    PrimaryRoad = 0,
    RegionalRoad = 1,
    IntercityRoad = 2,
    Railway = 3,
}

public enum ZoneKind : byte
{
    Residential = 0,
    Commercial = 1,
    Industrial = 2,
    MixedUse = 3,
    Civic = 4,
    Agricultural = 5,
    OpenSpace = 6,
}

public enum ParcelDevelopmentState : byte
{
    Vacant = 0,
    Developing = 1,
    Occupied = 2,
    Redeveloping = 3,
}

public enum GeneratedBuildingUse : byte
{
    Residential = 0,
    Commercial = 1,
    Industrial = 2,
    MixedUse = 3,
    Civic = 4,
    Transport = 5,
    Utility = 6,
}

public enum GeneratedPoiKind : byte
{
    SettlementCenter = 0,
    Market = 1,
    Station = 2,
    CivicCenter = 3,
    IndustrialHub = 4,
    Port = 5,
}

public enum DistrictKind : byte
{
    OldTown = 0,
    CentralBusiness = 1,
    StationDistrict = 2,
    IndustrialArea = 3,
    Suburb = 4,
    ResidentialQuarter = 5,
}

public enum HumanToponymKind : byte
{
    Settlement = 0,
    District = 1,
    Road = 2,
    Bridge = 3,
    Tunnel = 4,
    Station = 5,
}

public enum RoadSignKind : byte
{
    Direction = 0,
    PlaceName = 1,
    SteepGrade = 2,
    SharpCurve = 3,
    FloodWarning = 4,
    RiverCrossing = 5,
    MountainPass = 6,
    Tunnel = 7,
    CoastalLowland = 8,
}

public enum RegionalGenerationQualityPreset : byte
{
    Draft = 0,
    Standard = 1,
    HighQuality = 2,
}

public readonly record struct SettlementSuitability(
    double Flatness,
    double WaterAccess,
    double TransportPotential,
    double Buildability,
    double ResourceAccess,
    double FloodRisk,
    double SteepSlopeRisk,
    double Isolation,
    double ConstructionCost,
    double TotalScore);

public sealed record Settlement(
    SettlementId Id,
    WorldPoint Center,
    SettlementEnvironmentKind Environment,
    SettlementOriginKind Origin,
    RegionalRole Role,
    InitialEconomyKind InitialEconomy,
    SettlementSuitability Suitability,
    int Population,
    int Jobs,
    double InfluenceRadiusMeters,
    HumanToponymId NameId);

public sealed record HistoricalGrowthEvent(
    GrowthEventId Id,
    SettlementId SettlementId,
    HistoricalGrowthStage Stage,
    int Sequence,
    WorldPoint Center,
    int PopulationDelta,
    int JobDelta,
    string Reason);

public sealed record RegionalCorridor(
    RegionalCorridorId Id,
    RegionalCorridorKind Kind,
    SettlementId FromSettlementId,
    SettlementId ToSettlementId,
    IReadOnlyList<WorldPoint> Geometry,
    double TerrainAdaptation,
    double ConstructionCost,
    HumanToponymId? NameId);

public sealed record District(
    DistrictId Id,
    SettlementId SettlementId,
    DistrictKind Kind,
    WorldVolume Bounds,
    HumanToponymId NameId,
    double Accessibility);

public sealed record Parcel(
    ParcelId Id,
    SettlementId SettlementId,
    DistrictId DistrictId,
    WorldVolume Bounds,
    ZoneKind Zone,
    ParcelDevelopmentState DevelopmentState,
    double DevelopmentSuitability,
    double LandValue,
    GeneratedBuildingId? BuildingId);

public sealed record GeneratedBuilding(
    GeneratedBuildingId Id,
    ParcelId ParcelId,
    GeneratedBuildingUse Use,
    WorldVolume Bounds,
    int Floors,
    int Capacity,
    int HistoricalStage);

public sealed record GeneratedPoi(
    GeneratedPoiId Id,
    SettlementId SettlementId,
    GeneratedPoiKind Kind,
    WorldPoint Position,
    GeneratedBuildingId? BuildingId,
    HumanToponymId? NameId);

public sealed record HumanToponymProvenance(
    NaturalToponym? SourceNaturalToponym,
    GeographicFeatureId? SourceFeatureId,
    HumanToponymId? ParentHumanToponymId,
    string GeneratorKey);

public sealed record HumanToponym(
    HumanToponymId Id,
    HumanToponymKind Kind,
    string Name,
    HumanToponymProvenance Provenance);

public sealed record RoadSign(
    RoadSignId Id,
    RoadSignKind Kind,
    WorldPoint Position,
    RegionalCorridorId CorridorId,
    SettlementId? DestinationSettlementId,
    GeographicFeatureId? FeatureId,
    string Text);

public sealed record RegionalQualityReport(
    double TerrainAdaptation,
    double RoadConnectivity,
    double AverageSlopeCost,
    double Accessibility,
    double CongestionRisk,
    double LandUseConsistency,
    double FloodExposure,
    double UrbanCompactness,
    double PolycentricBalance)
{
    public double OverallScore => Math.Clamp(
        (TerrainAdaptation + RoadConnectivity + (1d - AverageSlopeCost) + Accessibility + (1d - CongestionRisk)
        + LandUseConsistency + (1d - FloodExposure) + UrbanCompactness + PolycentricBalance) / 9d,
        0d,
        1d);
}

public sealed record RegionalGenerationOptions
{
    public RegionalGenerationOptions(
        RegionalGenerationQualityPreset preset = RegionalGenerationQualityPreset.Standard,
        int? settlementCount = null,
        int? iterationBudget = null)
    {
        if (settlementCount is <= 0 or > 64) throw new ArgumentOutOfRangeException(nameof(settlementCount));
        if (iterationBudget is <= 0 or > 32) throw new ArgumentOutOfRangeException(nameof(iterationBudget));
        Preset = preset;
        SettlementCount = settlementCount;
        IterationBudget = iterationBudget;
    }

    public RegionalGenerationQualityPreset Preset { get; }
    public int? SettlementCount { get; }
    public int? IterationBudget { get; }

    public int ResolveSettlementCount() => SettlementCount ?? Preset switch
    {
        RegionalGenerationQualityPreset.Draft => 4,
        RegionalGenerationQualityPreset.Standard => 8,
        RegionalGenerationQualityPreset.HighQuality => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(Preset)), Preset, "Unknown regional generation quality preset."),
    };

    public int ResolveIterationBudget() => IterationBudget ?? Preset switch
    {
        RegionalGenerationQualityPreset.Draft => 1,
        RegionalGenerationQualityPreset.Standard => 3,
        RegionalGenerationQualityPreset.HighQuality => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(Preset)), Preset, "Unknown regional generation quality preset."),
    };
}

public sealed record RegionalGenerationSnapshot(
    WorldVolume Volume,
    RegionalGenerationQualityPreset Preset,
    ulong WorldSeed,
    int Iterations,
    IReadOnlyList<Settlement> Settlements,
    IReadOnlyList<HistoricalGrowthEvent> GrowthEvents,
    IReadOnlyList<RegionalCorridor> Corridors,
    IReadOnlyList<District> Districts,
    IReadOnlyList<Parcel> Parcels,
    IReadOnlyList<GeneratedBuilding> Buildings,
    IReadOnlyList<GeneratedPoi> Pois,
    IReadOnlyList<HumanToponym> Toponyms,
    IReadOnlyList<RoadSign> RoadSigns,
    RegionalQualityReport Quality,
    ulong TickCount);

public sealed record RegionalGenerationCheckpoint(RegionalGenerationSnapshot Snapshot);
