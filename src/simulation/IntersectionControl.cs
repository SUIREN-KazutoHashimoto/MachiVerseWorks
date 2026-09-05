namespace MachiVerseWorks.Simulation;

public readonly record struct IntersectionMovementId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum IntersectionControlMode : byte
{
    Unsignalized = 0,
    FixedSignal = 1,
}

public enum SignalIndication : byte
{
    Red = 0,
    Yellow = 1,
    Green = 2,
}

public readonly record struct IntersectionMovementSnapshot(
    IntersectionMovementId Id,
    LaneConnectionId ConnectionId,
    RoadNodeId IntersectionNodeId,
    LaneId FromLaneId,
    LaneId ToLaneId,
    TurnMovement TurnMovement,
    WorldPoint StopLinePosition,
    IReadOnlyList<IntersectionMovementId> Conflicts);

public readonly record struct IntersectionMovementStateSnapshot(
    IntersectionMovementId MovementId,
    SignalIndication Indication,
    int QueueLength,
    bool EntryGrantedThisTick);

public sealed record IntersectionControllerSnapshot(
    RoadNodeId IntersectionNodeId,
    IntersectionControlMode Mode,
    int PhaseIndex,
    ulong PhaseTick,
    IReadOnlyList<IntersectionMovementSnapshot> Movements,
    IReadOnlyList<IntersectionMovementStateSnapshot> MovementStates,
    ulong TickCount);

public sealed record IntersectionControlSnapshot(
    IReadOnlyList<IntersectionControllerSnapshot> Controllers,
    ulong TickCount);
