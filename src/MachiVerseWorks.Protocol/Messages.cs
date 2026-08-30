namespace MachiVerseWorks.Protocol;

public interface IProtocolMessage
{
    MessageType Type { get; }
}

public sealed record HelloMessage : IProtocolMessage
{
    public MessageType Type => MessageType.Hello;
}

public sealed record HelloAckMessage(ProtocolVersion ProtocolVersion, ushort TickRate) : IProtocolMessage
{
    public MessageType Type => MessageType.HelloAck;
}

public sealed record SubscribeVolumeMessage(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ) : IProtocolMessage
{
    public MessageType Type => MessageType.SubscribeVolume;
}

public sealed record AgentSpawnMessage(ulong AgentId, double X, double Y, double Z, double VelocityX, double VelocityY, double VelocityZ, ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.AgentSpawn;
}

public sealed record AgentUpdateMessage(ulong AgentId, double X, double Y, double Z, double VelocityX, double VelocityY, double VelocityZ, ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.AgentUpdate;
}

public sealed record AgentRemoveMessage(ulong AgentId, ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.AgentRemove;
}

public enum ProtocolRoadNodeKind : byte { Endpoint = 0, Intersection = 1 }
public enum ProtocolRoadKind : byte { Local = 0, Collector = 1, Arterial = 2, Highway = 3, Service = 4 }
public enum ProtocolLaneDirection : byte { Forward = 0, Reverse = 1 }
public enum ProtocolTurnMovement : byte { Unspecified = 0, Straight = 1, Left = 2, Right = 3, UTurn = 4 }
[Flags] public enum ProtocolRoadAccessMode : byte { None = 0, Motor = 1, Foot = 2 }
public enum ProtocolPedestrianMovementState : byte { Walking = 0, WaitingForCrossing = 1, WaitingForOccupancy = 2, Arrived = 3 }

public readonly record struct ProtocolRoadNode(ulong Id, ProtocolRoadNodeKind Kind, double X, double Y, double Z);
public readonly record struct ProtocolRoadSegment(ulong Id, ProtocolRoadKind Kind, ulong StartNodeId, ulong EndNodeId);
public readonly record struct ProtocolLane(ulong Id, ulong SegmentId, ProtocolLaneDirection Direction, ushort Order, double WidthMeters, double SpeedLimitMetersPerSecond);
public readonly record struct ProtocolLaneConnection(ulong Id, ulong FromLaneId, ulong ToLaneId, ulong ViaNodeId, ProtocolTurnMovement Movement);
public readonly record struct ProtocolRoadAccessPoint(ulong Id, ulong SegmentId, double SegmentOffset, ulong BuildingId, ulong PoiId, ProtocolRoadAccessMode Mode);

public sealed record RoadNetworkSnapshotMessage(
    ulong TickCount,
    IReadOnlyList<ProtocolRoadNode> Nodes,
    IReadOnlyList<ProtocolRoadSegment> Segments,
    IReadOnlyList<ProtocolLane> Lanes,
    IReadOnlyList<ProtocolLaneConnection> Connections,
    IReadOnlyList<ProtocolRoadAccessPoint> AccessPoints) : IProtocolMessage
{
    public MessageType Type => MessageType.RoadNetworkSnapshot;
}

public sealed record PedestrianSpawnMessage(
    ulong PedestrianId,
    ulong TripRequestId,
    double X,
    double Y,
    double Z,
    double VelocityX,
    double VelocityY,
    double VelocityZ,
    double WalkingSpeedMetersPerSecond,
    ProtocolPedestrianMovementState State,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.PedestrianSpawn;
}

public sealed record PedestrianUpdateMessage(
    ulong PedestrianId,
    ulong TripRequestId,
    double X,
    double Y,
    double Z,
    double VelocityX,
    double VelocityY,
    double VelocityZ,
    double WalkingSpeedMetersPerSecond,
    ProtocolPedestrianMovementState State,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.PedestrianUpdate;
}

public sealed record PedestrianRemoveMessage(ulong PedestrianId, ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.PedestrianRemove;
}

public sealed record ProtocolEnvelope(ProtocolVersion Version, IProtocolMessage Message);
