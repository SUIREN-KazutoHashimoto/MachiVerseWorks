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

public sealed record InspectPersonMessage(ulong PersonId) : IProtocolMessage
{
    public MessageType Type => MessageType.InspectPerson;
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
public enum ProtocolVehicleMovementState : byte { Driving = 0, WaitingForTraffic = 1, ChangingLane = 2, Arrived = 3 }
public enum ProtocolIntersectionControlMode : byte { Unsignalized = 0, FixedSignal = 1 }
public enum ProtocolSignalIndication : byte { Red = 0, Yellow = 1, Green = 2 }
public enum ProtocolActivityKind : byte { Home = 0, Work = 1, Education = 2, Shopping = 3, Healthcare = 4, Recreation = 5, Errand = 6 }
public enum ProtocolPersonTravelState : byte { AtActivity = 0, Walking = 1, Driving = 2 }
public enum ProtocolTravelMode : byte { Any = 0, Foot = 1, Motor = 2 }

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

public sealed record VehicleSpawnMessage(
    ulong VehicleId,
    ulong LaneId,
    double X,
    double Y,
    double Z,
    double ForwardX,
    double ForwardY,
    double ForwardZ,
    double SpeedMetersPerSecond,
    double LengthMeters,
    double WidthMeters,
    double HeightMeters,
    ProtocolVehicleMovementState State,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.VehicleSpawn;
}

public sealed record VehicleUpdateMessage(
    ulong VehicleId,
    ulong LaneId,
    double X,
    double Y,
    double Z,
    double ForwardX,
    double ForwardY,
    double ForwardZ,
    double SpeedMetersPerSecond,
    double LengthMeters,
    double WidthMeters,
    double HeightMeters,
    ProtocolVehicleMovementState State,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.VehicleUpdate;
}

public sealed record VehicleRemoveMessage(ulong VehicleId, ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.VehicleRemove;
}

public readonly record struct ProtocolIntersectionMovementState(
    ulong MovementId,
    ulong ConnectionId,
    ulong FromLaneId,
    ulong ToLaneId,
    ProtocolTurnMovement TurnMovement,
    double StopLineX,
    double StopLineY,
    double StopLineZ,
    ProtocolSignalIndication Indication,
    uint QueueLength,
    bool EntryGrantedThisTick);

public sealed record IntersectionControlSnapshotMessage(
    ulong TickCount,
    ulong IntersectionNodeId,
    ProtocolIntersectionControlMode Mode,
    ushort PhaseIndex,
    ulong PhaseTick,
    IReadOnlyList<ProtocolIntersectionMovementState> Movements) : IProtocolMessage
{
    public MessageType Type => MessageType.IntersectionControlSnapshot;
}

public sealed record PopulationStatisticsMessage(
    uint HouseholdCount,
    uint PersonCount,
    uint AtActivityCount,
    uint WalkingCount,
    uint DrivingCount,
    uint HomeCount,
    uint WorkCount,
    uint EducationCount,
    uint ShoppingCount,
    uint HealthcareCount,
    uint RecreationCount,
    uint ErrandCount,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.PopulationStatistics;
}

public sealed record PersonDebugMessage(
    ulong PersonId,
    ulong HouseholdId,
    ulong ResidenceBuildingId,
    ulong ResidencePoiId,
    ulong CurrentBuildingId,
    ulong CurrentPoiId,
    ProtocolActivityKind CurrentActivity,
    ProtocolPersonTravelState TravelState,
    ulong DestinationBuildingId,
    ulong DestinationPoiId,
    ProtocolActivityKind? DestinationActivity,
    ulong ActiveTripRequestId,
    ProtocolTravelMode? ActiveTravelMode,
    ulong PedestrianId,
    ulong VehicleId,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.PersonDebug;
}
