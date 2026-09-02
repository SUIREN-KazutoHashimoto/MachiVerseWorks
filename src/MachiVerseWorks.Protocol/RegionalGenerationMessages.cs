namespace MachiVerseWorks.Protocol;

public readonly record struct ProtocolSettlementSuitability(
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

public sealed record ProtocolSettlement(
    ulong SettlementId,
    double X,
    double Y,
    double Z,
    byte Environment,
    byte Origin,
    byte Role,
    byte InitialEconomy,
    ProtocolSettlementSuitability Suitability,
    int Population,
    int Jobs,
    double InfluenceRadiusMeters,
    ulong NameId);

public sealed record ProtocolHistoricalGrowthEvent(
    ulong EventId,
    ulong SettlementId,
    byte Stage,
    int Sequence,
    double X,
    double Y,
    double Z,
    int PopulationDelta,
    int JobDelta,
    string Reason);

public sealed record ProtocolRegionalCorridor(
    ulong CorridorId,
    byte Kind,
    ulong FromSettlementId,
    ulong ToSettlementId,
    IReadOnlyList<ProtocolWorldPoint> Geometry,
    double TerrainAdaptation,
    double ConstructionCost,
    ulong NameId);

public sealed record ProtocolDistrict(
    ulong DistrictId,
    ulong SettlementId,
    byte Kind,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    ulong NameId,
    double Accessibility);

public sealed record ProtocolParcel(
    ulong ParcelId,
    ulong SettlementId,
    ulong DistrictId,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    byte Zone,
    byte DevelopmentState,
    double DevelopmentSuitability,
    double LandValue,
    ulong BuildingId);

public sealed record ProtocolGeneratedBuilding(
    ulong BuildingId,
    ulong ParcelId,
    byte Use,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    int Floors,
    int Capacity,
    int HistoricalStage);

public sealed record ProtocolGeneratedPoi(
    ulong PoiId,
    ulong SettlementId,
    byte Kind,
    double X,
    double Y,
    double Z,
    ulong BuildingId,
    ulong NameId);

public sealed record ProtocolHumanToponym(
    ulong ToponymId,
    byte Kind,
    string Name,
    ulong SourceNaturalToponymId,
    string SourceNaturalName,
    ulong SourceFeatureId,
    ulong ParentHumanToponymId,
    string GeneratorKey);

public sealed record ProtocolRoadSign(
    ulong RoadSignId,
    byte Kind,
    double X,
    double Y,
    double Z,
    ulong CorridorId,
    ulong DestinationSettlementId,
    ulong FeatureId,
    string Text);

public readonly record struct ProtocolRegionalQualityReport(
    double TerrainAdaptation,
    double RoadConnectivity,
    double AverageSlopeCost,
    double Accessibility,
    double CongestionRisk,
    double LandUseConsistency,
    double FloodExposure,
    double UrbanCompactness,
    double PolycentricBalance,
    double OverallScore);

public sealed record RegionalGenerationSnapshotMessage(
    ulong TickCount,
    ulong WorldSeed,
    byte Preset,
    int Iterations,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    IReadOnlyList<ProtocolSettlement> Settlements,
    IReadOnlyList<ProtocolHistoricalGrowthEvent> GrowthEvents,
    IReadOnlyList<ProtocolRegionalCorridor> Corridors,
    IReadOnlyList<ProtocolDistrict> Districts,
    IReadOnlyList<ProtocolParcel> Parcels,
    IReadOnlyList<ProtocolGeneratedBuilding> Buildings,
    IReadOnlyList<ProtocolGeneratedPoi> Pois,
    IReadOnlyList<ProtocolHumanToponym> Toponyms,
    IReadOnlyList<ProtocolRoadSign> RoadSigns,
    ProtocolRegionalQualityReport Quality) : IProtocolMessage
{
    public MessageType Type => MessageType.RegionalGenerationSnapshot;
}
