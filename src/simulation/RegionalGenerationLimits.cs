namespace MachiVerseWorks.Simulation;

/// <summary>
/// Authoritative collection limits for a materializable Regional Generation snapshot.
/// Persistence uses these limits before JSON collection materialization so hostile or malformed saves
/// cannot allocate substantially more data than the runtime/protocol can consume.
/// </summary>
public static class RegionalGenerationLimits
{
    public const int MaximumSettlements = 64;
    public const int MaximumGrowthEvents = 1_024;
    public const int MaximumCorridors = 512;
    public const int MaximumCorridorGeometryPoints = 256;
    public const int MaximumDistricts = 512;
    public const int MaximumParcels = 4_096;
    public const int MaximumBuildings = 4_096;
    public const int MaximumPois = 1_024;
    public const int MaximumToponyms = 4_096;
    public const int MaximumRoadSigns = 4_096;
}
