using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

public static class WorldEnvironmentMessageMapper
{
    public static WorldEnvironmentSnapshotMessage ToProtocol(WorldEnvironmentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var config = snapshot.Config;
        return new WorldEnvironmentSnapshotMessage(
            snapshot.TickCount,
            new ProtocolEnvironmentConfig(
                config.WorldSeed,
                config.GeographicNorth.X,
                config.GeographicNorth.Y,
                config.LatitudeDegrees,
                (byte)config.Hemisphere,
                config.SeaLevelMeters,
                config.Continentality,
                config.MaritimeInfluence,
                config.MeanAnnualTemperatureCelsius,
                config.SeasonalityCelsius,
                config.AnnualPrecipitationMillimeters,
                config.ConfiguredCoastlineDistanceMeters ?? 0d,
                config.ConfiguredCoastlineDistanceMeters.HasValue,
                config.GlobalScaleMeters,
                config.TerrainDetailScaleMeters),
            snapshot.Volume.MinX,
            snapshot.Volume.MinY,
            snapshot.Volume.MinZ,
            snapshot.Volume.MaxX,
            snapshot.Volume.MaxY,
            snapshot.Volume.MaxZ,
            snapshot.Samples.Select(ToProtocol).ToArray(),
            snapshot.TerrainSamples.Select(ToProtocol).ToArray(),
            snapshot.Features.Select(ToProtocol).ToArray(),
            snapshot.Toponyms.Select(ToProtocol).ToArray());
    }

    private static ProtocolEnvironmentSample ToProtocol(RegionalEnvironmentSample sample) => new(
        sample.Position.X,
        sample.Position.Y,
        sample.ElevationMeters,
        (byte)sample.Landform,
        sample.CoastlineDistanceMeters,
        sample.Climate.LatitudeDegrees,
        sample.Climate.MeanAnnualTemperatureCelsius,
        sample.Climate.SeasonalAmplitudeCelsius,
        sample.Climate.AnnualPrecipitationMillimeters,
        sample.Climate.MaritimeInfluence,
        sample.Climate.Continentality,
        (byte)sample.Hydrology.SurfaceWater,
        sample.Hydrology.Drainage,
        sample.Hydrology.RiverStrength,
        sample.Hydrology.FloodRisk,
        sample.Hydrology.FlowDirection.X,
        sample.Hydrology.FlowDirection.Y,
        sample.TerrainRuggedness,
        sample.Buildability,
        sample.SettlementScore);

    private static ProtocolTerrainSurfaceSample ToProtocol(TerrainSurfaceSample sample) => new(
        sample.Position.X,
        sample.Position.Y,
        sample.Position.Z,
        sample.Normal.X,
        sample.Normal.Y,
        sample.Normal.Z,
        sample.SlopeDegrees,
        sample.Roughness,
        (byte)sample.Material,
        (byte)sample.SurfaceWater);

    private static ProtocolGeographicFeature ToProtocol(GeographicFeature feature) => new(
        feature.Id.Value,
        (byte)feature.Type,
        feature.Bounds.MinX,
        feature.Bounds.MinY,
        feature.Bounds.MinZ,
        feature.Bounds.MaxX,
        feature.Bounds.MaxY,
        feature.Bounds.MaxZ,
        feature.AreaSquareMeters,
        feature.ParentId?.Value ?? 0UL,
        feature.MinimumElevationMeters,
        feature.MaximumElevationMeters,
        feature.Geometry.Select(static point => new ProtocolWorldPoint(point.X, point.Y, point.Z)).ToArray());

    private static ProtocolNaturalToponym ToProtocol(NaturalToponym toponym) => new(
        toponym.Id.Value,
        toponym.FeatureId.Value,
        toponym.Name,
        (byte)toponym.Provenance.Kind,
        toponym.Provenance.SourceFeatureId.Value,
        toponym.Provenance.ParentToponymId?.Value ?? 0UL,
        toponym.Provenance.GeneratorKey);
}
