namespace MachiVerseWorks.Persistence;

internal sealed class SaveDataDocument
{
    public int? FormatVersion { get; init; }

    public SaveSimulationData? Simulation { get; init; }
}

internal sealed class SaveSimulationData
{
    public int? TickRate { get; init; }

    public ulong? Seed { get; init; }

    public double? SpatialCellSize { get; init; }

    public ulong? TickCount { get; init; }

    public long? ElapsedTicks { get; init; }

    public ulong? RandomState { get; init; }

    public ulong? NextAgentId { get; init; }

    public SaveAgentData[]? Agents { get; init; }
}

internal sealed class SaveAgentData
{
    public ulong? Id { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public double? VelocityX { get; init; }

    public double? VelocityY { get; init; }

    public bool? IsActive { get; init; }
}
