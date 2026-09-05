namespace MachiVerseWorks.Simulation;

public sealed record PersistentRegionalMaterializationBinding(
    GeneratedBuildingId GeneratedBuildingId,
    BuildingId BuildingId,
    PoiId? PoiId,
    RoadAccessPointId? RoadAccessPointId,
    CompanyId? CompanyId,
    EstablishmentId? EstablishmentId,
    JobId? JobId);
