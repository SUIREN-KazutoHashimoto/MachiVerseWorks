namespace MachiVerseWorks.Protocol;

public readonly record struct ProtocolEnvironmentConfig(
    ulong WorldSeed,
    double GeographicNorthX,
    double GeographicNorthY,
    double LatitudeDegrees,
    byte Hemisphere,
    double SeaLevelMeters,
    double Continentality,
    double MaritimeInfluence,
    double MeanAnnualTemperatureCelsius,
    double SeasonalityCelsius,
    double AnnualPrecipitationMillimeters,
    double ConfiguredCoastlineDistanceMeters,
    bool HasConfiguredCoastlineDistance,
    double GlobalScaleMeters,
    double TerrainDetailScaleMeters);

public readonly record struct ProtocolWorldPoint(double X, double Y, double Z);

public readonly record struct ProtocolEnvironmentSample(
    double X,
    double Y,
    double ElevationMeters,
    byte Landform,
    double CoastlineDistanceMeters,
    double LatitudeDegrees,
    double MeanAnnualTemperatureCelsius,
    double SeasonalAmplitudeCelsius,
    double AnnualPrecipitationMillimeters,
    double MaritimeInfluence,
    double Continentality,
    byte SurfaceWater,
    double Drainage,
    double RiverStrength,
    double FloodRisk,
    double FlowDirectionX,
    double FlowDirectionY,
    double TerrainRuggedness,
    double Buildability,
    double SettlementScore);

public readonly record struct ProtocolTerrainSurfaceSample(
    double X,
    double Y,
    double Z,
    double NormalX,
    double NormalY,
    double NormalZ,
    double SlopeDegrees,
    double Roughness,
    byte Material,
    byte SurfaceWater);

public sealed record ProtocolGeographicFeature(
    ulong FeatureId,
    byte FeatureType,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    double AreaSquareMeters,
    ulong ParentFeatureId,
    double MinimumElevationMeters,
    double MaximumElevationMeters,
    IReadOnlyList<ProtocolWorldPoint> Geometry);

public sealed record ProtocolNaturalToponym(
    ulong ToponymId,
    ulong FeatureId,
    string Name,
    byte ProvenanceKind,
    ulong SourceFeatureId,
    ulong ParentToponymId,
    string GeneratorKey);

public sealed record WorldEnvironmentSnapshotMessage(
    ulong TickCount,
    ProtocolEnvironmentConfig Config,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    IReadOnlyList<ProtocolEnvironmentSample> Samples,
    IReadOnlyList<ProtocolTerrainSurfaceSample> TerrainSamples,
    IReadOnlyList<ProtocolGeographicFeature> Features,
    IReadOnlyList<ProtocolNaturalToponym> Toponyms) : IProtocolMessage
{
    public MessageType Type => MessageType.WorldEnvironmentSnapshot;
}
