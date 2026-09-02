using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

public static class RegionalGenerationMessageMapper
{
    public static RegionalGenerationSnapshotMessage ToProtocol(RegionalGenerationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new RegionalGenerationSnapshotMessage(
            snapshot.TickCount,
            snapshot.WorldSeed,
            (byte)snapshot.Preset,
            snapshot.Iterations,
            snapshot.Volume.MinX,
            snapshot.Volume.MinY,
            snapshot.Volume.MinZ,
            snapshot.Volume.MaxX,
            snapshot.Volume.MaxY,
            snapshot.Volume.MaxZ,
            snapshot.Settlements.Select(ToProtocol).ToArray(),
            snapshot.GrowthEvents.Select(ToProtocol).ToArray(),
            snapshot.Corridors.Select(ToProtocol).ToArray(),
            snapshot.Districts.Select(ToProtocol).ToArray(),
            snapshot.Parcels.Select(ToProtocol).ToArray(),
            snapshot.Buildings.Select(ToProtocol).ToArray(),
            snapshot.Pois.Select(ToProtocol).ToArray(),
            snapshot.Toponyms.Select(ToProtocol).ToArray(),
            snapshot.RoadSigns.Select(ToProtocol).ToArray(),
            new ProtocolRegionalQualityReport(
                snapshot.Quality.TerrainAdaptation,
                snapshot.Quality.RoadConnectivity,
                snapshot.Quality.AverageSlopeCost,
                snapshot.Quality.Accessibility,
                snapshot.Quality.CongestionRisk,
                snapshot.Quality.LandUseConsistency,
                snapshot.Quality.FloodExposure,
                snapshot.Quality.UrbanCompactness,
                snapshot.Quality.PolycentricBalance,
                snapshot.Quality.OverallScore));
    }

    private static ProtocolSettlement ToProtocol(Settlement item) => new(
        item.Id.Value,
        item.Center.X,
        item.Center.Y,
        item.Center.Z,
        (byte)item.Environment,
        (byte)item.Origin,
        (byte)item.Role,
        (byte)item.InitialEconomy,
        new ProtocolSettlementSuitability(
            item.Suitability.Flatness,
            item.Suitability.WaterAccess,
            item.Suitability.TransportPotential,
            item.Suitability.Buildability,
            item.Suitability.ResourceAccess,
            item.Suitability.FloodRisk,
            item.Suitability.SteepSlopeRisk,
            item.Suitability.Isolation,
            item.Suitability.ConstructionCost,
            item.Suitability.TotalScore),
        item.Population,
        item.Jobs,
        item.InfluenceRadiusMeters,
        item.NameId.Value);

    private static ProtocolHistoricalGrowthEvent ToProtocol(HistoricalGrowthEvent item) => new(
        item.Id.Value,
        item.SettlementId.Value,
        (byte)item.Stage,
        item.Sequence,
        item.Center.X,
        item.Center.Y,
        item.Center.Z,
        item.PopulationDelta,
        item.JobDelta,
        item.Reason);

    private static ProtocolRegionalCorridor ToProtocol(RegionalCorridor item) => new(
        item.Id.Value,
        (byte)item.Kind,
        item.FromSettlementId.Value,
        item.ToSettlementId.Value,
        item.Geometry.Select(static point => new ProtocolWorldPoint(point.X, point.Y, point.Z)).ToArray(),
        item.TerrainAdaptation,
        item.ConstructionCost,
        item.NameId?.Value ?? 0UL);

    private static ProtocolDistrict ToProtocol(District item) => new(
        item.Id.Value,
        item.SettlementId.Value,
        (byte)item.Kind,
        item.Bounds.MinX,
        item.Bounds.MinY,
        item.Bounds.MinZ,
        item.Bounds.MaxX,
        item.Bounds.MaxY,
        item.Bounds.MaxZ,
        item.NameId.Value,
        item.Accessibility);

    private static ProtocolParcel ToProtocol(Parcel item) => new(
        item.Id.Value,
        item.SettlementId.Value,
        item.DistrictId.Value,
        item.Bounds.MinX,
        item.Bounds.MinY,
        item.Bounds.MinZ,
        item.Bounds.MaxX,
        item.Bounds.MaxY,
        item.Bounds.MaxZ,
        (byte)item.Zone,
        (byte)item.DevelopmentState,
        item.DevelopmentSuitability,
        item.LandValue,
        item.BuildingId?.Value ?? 0UL);

    private static ProtocolGeneratedBuilding ToProtocol(GeneratedBuilding item) => new(
        item.Id.Value,
        item.ParcelId.Value,
        (byte)item.Use,
        item.Bounds.MinX,
        item.Bounds.MinY,
        item.Bounds.MinZ,
        item.Bounds.MaxX,
        item.Bounds.MaxY,
        item.Bounds.MaxZ,
        item.Floors,
        item.Capacity,
        item.HistoricalStage);

    private static ProtocolGeneratedPoi ToProtocol(GeneratedPoi item) => new(
        item.Id.Value,
        item.SettlementId.Value,
        (byte)item.Kind,
        item.Position.X,
        item.Position.Y,
        item.Position.Z,
        item.BuildingId?.Value ?? 0UL,
        item.NameId?.Value ?? 0UL);

    private static ProtocolHumanToponym ToProtocol(HumanToponym item)
    {
        var natural = item.Provenance.SourceNaturalToponym;
        return new ProtocolHumanToponym(
            item.Id.Value,
            (byte)item.Kind,
            item.Name,
            natural?.Id.Value ?? 0UL,
            natural?.Name ?? string.Empty,
            item.Provenance.SourceFeatureId?.Value ?? 0UL,
            item.Provenance.ParentHumanToponymId?.Value ?? 0UL,
            item.Provenance.GeneratorKey);
    }

    private static ProtocolRoadSign ToProtocol(RoadSign item) => new(
        item.Id.Value,
        (byte)item.Kind,
        item.Position.X,
        item.Position.Y,
        item.Position.Z,
        item.CorridorId.Value,
        item.DestinationSettlementId?.Value ?? 0UL,
        item.FeatureId?.Value ?? 0UL,
        item.Text);
}
