namespace MachiVerseWorks.Simulation;

public sealed record SimulationCheckpoint(
    int TickRate,
    ulong Seed,
    double SpatialCellSize,
    ulong TickCount,
    long ElapsedTicks,
    ulong RandomState,
    ulong NextAgentId,
    IReadOnlyList<SimulationAgentCheckpoint> Agents);

public readonly record struct SimulationAgentCheckpoint(
    AgentId Id,
    WorldPoint Position,
    WorldVector Velocity,
    bool IsActive);
