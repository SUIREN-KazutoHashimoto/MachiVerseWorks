namespace MachiVerseWorks.Simulation;

public readonly record struct RoadNodeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RoadSegmentId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct LaneId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct LaneConnectionId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RoadAccessPointId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum RoadNodeKind : byte
{
    Endpoint = 0,
    Intersection = 1,
}

public enum RoadKind : byte
{
    Local = 0,
    Collector = 1,
    Arterial = 2,
    Highway = 3,
    Service = 4,
}

public enum LaneDirection : byte
{
    Forward = 0,
    Reverse = 1,
}

public enum TurnMovement : byte
{
    Unspecified = 0,
    Straight = 1,
    Left = 2,
    Right = 3,
    UTurn = 4,
}

[Flags]
public enum RoadAccessMode : byte
{
    None = 0,
    Motor = 1,
    Foot = 2,
}

public readonly record struct RoadNodeSnapshot(
    RoadNodeId Id,
    RoadNodeKind Kind,
    WorldPoint Position);

public readonly record struct RoadSegmentSnapshot(
    RoadSegmentId Id,
    RoadKind Kind,
    RoadNodeId StartNodeId,
    RoadNodeId EndNodeId);

public readonly record struct LaneSnapshot(
    LaneId Id,
    RoadSegmentId SegmentId,
    LaneDirection Direction,
    ushort Order,
    double WidthMeters,
    double SpeedLimitMetersPerSecond);

public readonly record struct LaneConnectionSnapshot(
    LaneConnectionId Id,
    LaneId FromLaneId,
    LaneId ToLaneId,
    RoadNodeId ViaNodeId,
    TurnMovement Movement);

public readonly record struct RoadAccessPointSnapshot(
    RoadAccessPointId Id,
    RoadSegmentId SegmentId,
    double SegmentOffset,
    BuildingId? BuildingId,
    PoiId? PoiId,
    RoadAccessMode Mode);

public sealed record RoadNetworkSnapshot(
    IReadOnlyList<RoadNodeSnapshot> Nodes,
    IReadOnlyList<RoadSegmentSnapshot> Segments,
    IReadOnlyList<LaneSnapshot> Lanes,
    IReadOnlyList<LaneConnectionSnapshot> Connections,
    IReadOnlyList<RoadAccessPointSnapshot> AccessPoints);
