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

    public SaveAgentData?[]? Agents { get; init; }

    public ulong? NextBuildingId { get; init; }

    public SaveBuildingData?[]? Buildings { get; init; }

    public ulong? NextPoiId { get; init; }

    public SavePoiData?[]? Pois { get; init; }
}

internal sealed class SaveAgentData
{
    public ulong? Id { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public double? Z { get; init; }

    public double? VelocityX { get; init; }

    public double? VelocityY { get; init; }

    public double? VelocityZ { get; init; }

    public bool? IsActive { get; init; }
}

internal sealed class SaveBuildingData
{
    public ulong? Id { get; init; }

    public byte? Kind { get; init; }

    public double? MinX { get; init; }

    public double? MinY { get; init; }

    public double? MinZ { get; init; }

    public double? MaxX { get; init; }

    public double? MaxY { get; init; }

    public double? MaxZ { get; init; }
}

internal sealed class SavePoiData
{
    public ulong? Id { get; init; }

    public byte? Kind { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public double? Z { get; init; }

    public ulong? BuildingId { get; init; }
}
