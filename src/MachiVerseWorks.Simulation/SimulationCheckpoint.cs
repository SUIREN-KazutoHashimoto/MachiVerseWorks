namespace MachiVerseWorks.Simulation;

public sealed record SimulationCheckpoint(
    int TickRate,
    ulong Seed,
    double SpatialCellSize,
    ulong TickCount,
    long ElapsedTicks,
    ulong RandomState,
    ulong NextAgentId,
    IReadOnlyList<SimulationAgentCheckpoint> Agents,
    ulong NextBuildingId,
    IReadOnlyList<SimulationBuildingCheckpoint> Buildings,
    ulong NextPoiId,
    IReadOnlyList<SimulationPoiCheckpoint> Pois);

public readonly record struct SimulationAgentCheckpoint(
    AgentId Id,
    WorldPoint Position,
    WorldVector Velocity,
    bool IsActive);

public readonly record struct SimulationBuildingCheckpoint(
    BuildingId Id,
    BuildingKind Kind,
    WorldVolume Bounds);

public readonly record struct SimulationPoiCheckpoint(
    PoiId Id,
    PoiKind Kind,
    WorldPoint Position,
    BuildingId? BuildingId);
