namespace MachiVerseWorks.Protocol;

public enum ProtocolTrackDirection : byte
{
    Bidirectional = 0,
    StartToEnd = 1,
    EndToStart = 2,
}

public enum ProtocolTrackElectrification : byte
{
    None = 0,
    Overhead = 1,
    ThirdRail = 2,
}

public enum ProtocolTrackUsage : byte
{
    Mainline = 0,
    Siding = 1,
    Depot = 2,
}

public readonly record struct ProtocolTrackNode(ulong Id, byte Kind, double X, double Y, double Z);

public readonly record struct ProtocolTrackSegment(
    ulong Id,
    ulong StartNodeId,
    ulong EndNodeId,
    ProtocolTrackDirection Direction,
    double GaugeMeters,
    double SpeedLimitMetersPerSecond,
    ProtocolTrackElectrification Electrification,
    ProtocolTrackUsage Usage);

public readonly record struct ProtocolTrackConnection(ulong Id, ulong FromSegmentId, ulong ToSegmentId, ulong ViaNodeId);

public sealed record ProtocolBlockSection(ulong Id, IReadOnlyList<ulong> SegmentIds);

public readonly record struct ProtocolStation(
    ulong Id,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ);

public readonly record struct ProtocolPlatform(
    ulong Id,
    ulong StationId,
    ulong TrackSegmentId,
    double StartSegmentOffset,
    double EndSegmentOffset,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ);

public readonly record struct ProtocolPlatformAccessPoint(ulong Id, ulong PlatformId, ulong RoadAccessPointId);

public sealed record ProtocolDepot(
    ulong Id,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    IReadOnlyList<ulong> TrackSegmentIds);

public sealed record RailwayInfrastructureSnapshotMessage(
    ulong Revision,
    bool IsFullSnapshot,
    IReadOnlyList<ProtocolTrackNode> Nodes,
    IReadOnlyList<ProtocolTrackSegment> Segments,
    IReadOnlyList<ProtocolTrackConnection> Connections,
    IReadOnlyList<ProtocolBlockSection> Blocks,
    IReadOnlyList<ProtocolStation> Stations,
    IReadOnlyList<ProtocolPlatform> Platforms,
    IReadOnlyList<ProtocolPlatformAccessPoint> PlatformAccessPoints,
    IReadOnlyList<ProtocolDepot> Depots) : IProtocolMessage
{
    public MessageType Type => MessageType.RailwayInfrastructureSnapshot;
}
