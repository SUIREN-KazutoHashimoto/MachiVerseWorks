namespace MachiVerseWorks.Simulation;

public readonly record struct TrackNodeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct TrackSegmentId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct TrackConnectionId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct BlockSectionId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct StationId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PlatformId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PlatformAccessPointId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct DepotId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum TrackNodeKind : byte
{
    Endpoint = 0,
    Junction = 1,
    Switch = 2,
}

public enum TrackDirection : byte
{
    Bidirectional = 0,
    StartToEnd = 1,
    EndToStart = 2,
}

public enum TrackElectrification : byte
{
    None = 0,
    Overhead = 1,
    ThirdRail = 2,
}

public enum TrackUsage : byte
{
    Mainline = 0,
    Siding = 1,
    Depot = 2,
}

public readonly record struct TrackNodeSnapshot(
    TrackNodeId Id,
    TrackNodeKind Kind,
    WorldPoint Position);

public readonly record struct TrackSegmentSnapshot(
    TrackSegmentId Id,
    TrackNodeId StartNodeId,
    TrackNodeId EndNodeId,
    TrackDirection Direction,
    double GaugeMeters,
    double SpeedLimitMetersPerSecond,
    TrackElectrification Electrification,
    TrackUsage Usage);

public readonly record struct TrackConnectionSnapshot(
    TrackConnectionId Id,
    TrackSegmentId FromSegmentId,
    TrackSegmentId ToSegmentId,
    TrackNodeId ViaNodeId);

public sealed record BlockSectionSnapshot(
    BlockSectionId Id,
    IReadOnlyList<TrackSegmentId> SegmentIds);

public readonly record struct StationSnapshot(
    StationId Id,
    WorldVolume Bounds);

public readonly record struct PlatformSnapshot(
    PlatformId Id,
    StationId StationId,
    TrackSegmentId TrackSegmentId,
    double StartSegmentOffset,
    double EndSegmentOffset,
    WorldVolume Bounds);

public readonly record struct PlatformAccessPointSnapshot(
    PlatformAccessPointId Id,
    PlatformId PlatformId,
    RoadAccessPointId RoadAccessPointId);

public sealed record DepotSnapshot(
    DepotId Id,
    WorldVolume Bounds,
    IReadOnlyList<TrackSegmentId> TrackSegmentIds);

public sealed record RailwayInfrastructureSnapshot(
    IReadOnlyList<TrackNodeSnapshot> Nodes,
    IReadOnlyList<TrackSegmentSnapshot> Segments,
    IReadOnlyList<TrackConnectionSnapshot> Connections,
    IReadOnlyList<BlockSectionSnapshot> Blocks,
    IReadOnlyList<StationSnapshot> Stations,
    IReadOnlyList<PlatformSnapshot> Platforms,
    IReadOnlyList<PlatformAccessPointSnapshot> PlatformAccessPoints,
    IReadOnlyList<DepotSnapshot> Depots);

public readonly record struct RailwayInfrastructureValidationResult(
    int TrackComponentCount,
    int TraversableConnectionCount);
