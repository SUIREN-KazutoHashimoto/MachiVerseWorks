namespace MachiVerseWorks.Simulation;

public readonly record struct AgentSnapshot(
    AgentId Id,
    WorldPoint Position,
    WorldVector Velocity,
    ulong TickCount);
