namespace MachiVerseWorks.Simulation.Internal;

internal readonly record struct IntersectionEntryIntent(
    VehicleId VehicleId,
    LaneConnectionId ConnectionId,
    bool DownstreamAvailable);

internal sealed class IntersectionControlStore
{
    private const double StopLineOffsetMeters = 0d;
    private readonly Dictionary<LaneConnectionId, MovementRuntime> movementsByConnection = [];
    private readonly List<ControllerRuntime> controllers = [];
    private readonly HashSet<EntryGrantKey> grants = [];
    private int tickRate = 30;
    private ulong preparedTick = ulong.MaxValue;

    public void Rebuild(RoadNetworkSnapshot snapshot, RoadTrafficTopology topology, int configuredTickRate)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuredTickRate);

        movementsByConnection.Clear();
        controllers.Clear();
        grants.Clear();
        preparedTick = ulong.MaxValue;
        tickRate = configuredTickRate;

        var nodes = new Dictionary<RoadNodeId, RoadNodeSnapshot>(snapshot.Nodes.Count);
        foreach (var node in snapshot.Nodes) nodes.Add(node.Id, node);
        var segments = new Dictionary<RoadSegmentId, RoadSegmentSnapshot>(snapshot.Segments.Count);
        foreach (var segment in snapshot.Segments) segments.Add(segment.Id, segment);
        var lanes = new Dictionary<LaneId, LaneSnapshot>(snapshot.Lanes.Count);
        foreach (var lane in snapshot.Lanes) lanes.Add(lane.Id, lane);

        var movementsByNode = new Dictionary<RoadNodeId, List<MovementRuntime>>();
        foreach (var connection in snapshot.Connections.OrderBy(static item => item.Id.Value))
        {
            if (!nodes.TryGetValue(connection.ViaNodeId, out var node) || node.Kind != RoadNodeKind.Intersection)
                throw new InvalidOperationException($"Lane connection {connection.Id.Value} does not reference an Intersection node.");
            if (!lanes.TryGetValue(connection.FromLaneId, out var fromLane) || !lanes.TryGetValue(connection.ToLaneId, out var toLane))
                throw new InvalidOperationException($"Lane connection {connection.Id.Value} references an unknown Lane.");
            if (!segments.TryGetValue(fromLane.SegmentId, out var fromSegment) || !segments.ContainsKey(toLane.SegmentId))
                throw new InvalidOperationException($"Lane connection {connection.Id.Value} references an unknown RoadSegment.");

            var fromGeometry = topology.GetLane(connection.FromLaneId);
            var fromExitOffset = fromLane.Direction == LaneDirection.Forward ? 1d : 0d;
            var toEntryOffset = toLane.Direction == LaneDirection.Forward ? 0d : 1d;
            var stopProgress = Math.Max(0d, fromGeometry.LengthMeters - StopLineOffsetMeters);
            var stopOffset = fromLane.Direction == LaneDirection.Forward
                ? stopProgress / fromGeometry.LengthMeters
                : 1d - stopProgress / fromGeometry.LengthMeters;

            var movement = new MovementRuntime(
                new IntersectionMovementId(connection.Id.Value),
                connection,
                topology.GetPosition(connection.FromLaneId, Math.Clamp(stopOffset, 0d, 1d)),
                topology.GetPosition(connection.FromLaneId, fromExitOffset),
                topology.GetPosition(connection.ToLaneId, toEntryOffset),
                GetRoadPriority(fromSegment.Kind),
                GetTurnPriority(connection.Movement),
                fromLane.SegmentId);
            movementsByConnection.Add(connection.Id, movement);
            if (!movementsByNode.TryGetValue(connection.ViaNodeId, out var list))
            {
                list = [];
                movementsByNode.Add(connection.ViaNodeId, list);
            }
            list.Add(movement);
        }

        foreach (var pair in movementsByNode.OrderBy(static item => item.Key.Value))
        {
            var movements = pair.Value;
            movements.Sort(MovementRuntime.ComparePriority);
            BuildConflicts(movements);
            var phases = BuildPhases(movements);
            var incomingSegments = new HashSet<RoadSegmentId>();
            foreach (var movement in movements) incomingSegments.Add(movement.IncomingSegmentId);
            var mode = incomingSegments.Count >= 4 && phases.Count > 1
                ? IntersectionControlMode.FixedSignal
                : IntersectionControlMode.Unsignalized;
            var controller = new ControllerRuntime(pair.Key, mode, movements, phases);
            foreach (var movement in movements) movement.Controller = controller;
            controllers.Add(controller);
        }
    }

    public void PrepareTick(ulong tickCount, IReadOnlyList<IntersectionEntryIntent> intents)
    {
        ArgumentNullException.ThrowIfNull(intents);
        preparedTick = tickCount;
        grants.Clear();
        foreach (var movement in movementsByConnection.Values)
        {
            movement.QueueLength = 0;
            movement.EntryGranted = false;
        }
        foreach (var controller in controllers) controller.SelectedMovements.Clear();

        var candidates = new List<Candidate>(intents.Count);
        foreach (var intent in intents)
        {
            if (!movementsByConnection.TryGetValue(intent.ConnectionId, out var movement))
                throw new InvalidOperationException($"Intersection movement for LaneConnection {intent.ConnectionId.Value} is missing.");
            movement.QueueLength++;
            if (!intent.DownstreamAvailable) continue;
            if (GetIndication(movement, tickCount) != SignalIndication.Green) continue;
            candidates.Add(new Candidate(intent.VehicleId, movement));
        }

        candidates.Sort(static (left, right) =>
        {
            var node = left.Movement.Connection.ViaNodeId.Value.CompareTo(right.Movement.Connection.ViaNodeId.Value);
            if (node != 0) return node;
            var priority = MovementRuntime.ComparePriority(left.Movement, right.Movement);
            if (priority != 0) return priority;
            return left.VehicleId.Value.CompareTo(right.VehicleId.Value);
        });

        foreach (var candidate in candidates)
        {
            var movement = candidate.Movement;
            var controller = movement.Controller ?? throw new InvalidOperationException("Intersection movement is missing its controller.");
            if (movement.EntryGranted) continue;
            var conflicts = false;
            foreach (var selected in controller.SelectedMovements)
            {
                if (movement.Conflicts.Contains(selected.Id))
                {
                    conflicts = true;
                    break;
                }
            }
            if (conflicts) continue;
            movement.EntryGranted = true;
            controller.SelectedMovements.Add(movement);
            grants.Add(new EntryGrantKey(candidate.VehicleId, movement.Connection.Id));
        }
    }

    public bool IsEntryGranted(VehicleId vehicleId, LaneConnectionId connectionId) =>
        grants.Contains(new EntryGrantKey(vehicleId, connectionId));

    public IntersectionControlSnapshot CreateSnapshot(ulong tickCount)
    {
        var result = new IntersectionControllerSnapshot[controllers.Count];
        for (var controllerIndex = 0; controllerIndex < controllers.Count; controllerIndex++)
        {
            var controller = controllers[controllerIndex];
            GetPhaseState(controller, tickCount, out var phaseIndex, out var phaseTick);
            var movementSnapshots = new IntersectionMovementSnapshot[controller.Movements.Count];
            var movementStates = new IntersectionMovementStateSnapshot[controller.Movements.Count];
            for (var movementIndex = 0; movementIndex < controller.Movements.Count; movementIndex++)
            {
                var movement = controller.Movements[movementIndex];
                var conflicts = movement.Conflicts.OrderBy(static id => id.Value).ToArray();
                movementSnapshots[movementIndex] = new IntersectionMovementSnapshot(
                    movement.Id,
                    movement.Connection.Id,
                    movement.Connection.ViaNodeId,
                    movement.Connection.FromLaneId,
                    movement.Connection.ToLaneId,
                    movement.Connection.Movement,
                    movement.StopLinePosition,
                    conflicts);
                movementStates[movementIndex] = new IntersectionMovementStateSnapshot(
                    movement.Id,
                    GetIndication(movement, tickCount),
                    preparedTick == tickCount ? movement.QueueLength : 0,
                    preparedTick == tickCount && movement.EntryGranted);
            }
            result[controllerIndex] = new IntersectionControllerSnapshot(
                controller.NodeId,
                controller.Mode,
                phaseIndex,
                phaseTick,
                movementSnapshots,
                movementStates,
                tickCount);
        }
        return new IntersectionControlSnapshot(result, tickCount);
    }

    private SignalIndication GetIndication(MovementRuntime movement, ulong tickCount)
    {
        var controller = movement.Controller ?? throw new InvalidOperationException("Intersection movement is missing its controller.");
        if (controller.Mode == IntersectionControlMode.Unsignalized) return SignalIndication.Green;
        GetPhaseState(controller, tickCount, out var phaseIndex, out var phaseTick);
        if (phaseIndex != movement.SignalPhaseIndex) return SignalIndication.Red;
        var greenTicks = checked((ulong)tickRate * 20UL);
        var yellowTicks = checked((ulong)tickRate * 3UL);
        if (phaseTick < greenTicks) return SignalIndication.Green;
        if (phaseTick < greenTicks + yellowTicks) return SignalIndication.Yellow;
        return SignalIndication.Red;
    }

    private void GetPhaseState(ControllerRuntime controller, ulong tickCount, out int phaseIndex, out ulong phaseTick)
    {
        if (controller.Mode == IntersectionControlMode.Unsignalized || controller.Phases.Count == 0)
        {
            phaseIndex = 0;
            phaseTick = 0;
            return;
        }
        var phaseDuration = checked((ulong)tickRate * 24UL);
        var cycleDuration = checked(phaseDuration * (ulong)controller.Phases.Count);
        var cycleTick = tickCount % cycleDuration;
        phaseIndex = (int)(cycleTick / phaseDuration);
        phaseTick = cycleTick % phaseDuration;
    }

    private static void BuildConflicts(List<MovementRuntime> movements)
    {
        for (var leftIndex = 0; leftIndex < movements.Count; leftIndex++)
        {
            var left = movements[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < movements.Count; rightIndex++)
            {
                var right = movements[rightIndex];
                if (!Conflicts(left, right)) continue;
                left.Conflicts.Add(right.Id);
                right.Conflicts.Add(left.Id);
            }
        }
    }

    private static bool Conflicts(MovementRuntime left, MovementRuntime right)
    {
        if (left.Connection.FromLaneId == right.Connection.FromLaneId
            || left.Connection.ToLaneId == right.Connection.ToLaneId)
            return true;
        return SegmentsIntersect(left.PathStart, left.PathEnd, right.PathStart, right.PathEnd);
    }

    private static bool SegmentsIntersect(WorldPoint a, WorldPoint b, WorldPoint c, WorldPoint d)
    {
        const double epsilon = 1e-9;
        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        if (((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon))
            && ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon)))
            return true;
        return Math.Abs(abC) <= epsilon && OnSegment(a, b, c)
            || Math.Abs(abD) <= epsilon && OnSegment(a, b, d)
            || Math.Abs(cdA) <= epsilon && OnSegment(c, d, a)
            || Math.Abs(cdB) <= epsilon && OnSegment(c, d, b);
    }

    private static double Cross(WorldPoint a, WorldPoint b, WorldPoint c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool OnSegment(WorldPoint a, WorldPoint b, WorldPoint point) =>
        point.X >= Math.Min(a.X, b.X) - 1e-9 && point.X <= Math.Max(a.X, b.X) + 1e-9
        && point.Y >= Math.Min(a.Y, b.Y) - 1e-9 && point.Y <= Math.Max(a.Y, b.Y) + 1e-9;

    private static List<List<MovementRuntime>> BuildPhases(List<MovementRuntime> movements)
    {
        var phases = new List<List<MovementRuntime>>();
        foreach (var movement in movements)
        {
            var assigned = false;
            for (var phaseIndex = 0; phaseIndex < phases.Count; phaseIndex++)
            {
                var phase = phases[phaseIndex];
                var conflicts = false;
                foreach (var existing in phase)
                {
                    if (!movement.Conflicts.Contains(existing.Id)) continue;
                    conflicts = true;
                    break;
                }
                if (conflicts) continue;
                phase.Add(movement);
                movement.SignalPhaseIndex = phaseIndex;
                assigned = true;
                break;
            }
            if (assigned) continue;
            movement.SignalPhaseIndex = phases.Count;
            phases.Add([movement]);
        }
        return phases;
    }

    private static int GetRoadPriority(RoadKind kind) => kind switch
    {
        RoadKind.Highway => 0,
        RoadKind.Arterial => 1,
        RoadKind.Collector => 2,
        RoadKind.Local => 3,
        RoadKind.Service => 4,
        _ => 5,
    };

    private static int GetTurnPriority(TurnMovement movement) => movement switch
    {
        TurnMovement.Straight => 0,
        TurnMovement.Right => 1,
        TurnMovement.Left => 2,
        TurnMovement.UTurn => 3,
        _ => 2,
    };

    private readonly record struct Candidate(VehicleId VehicleId, MovementRuntime Movement);
    private readonly record struct EntryGrantKey(VehicleId VehicleId, LaneConnectionId ConnectionId);

    private sealed class ControllerRuntime(
        RoadNodeId nodeId,
        IntersectionControlMode mode,
        List<MovementRuntime> movements,
        List<List<MovementRuntime>> phases)
    {
        public RoadNodeId NodeId { get; } = nodeId;
        public IntersectionControlMode Mode { get; } = mode;
        public List<MovementRuntime> Movements { get; } = movements;
        public List<List<MovementRuntime>> Phases { get; } = phases;
        public List<MovementRuntime> SelectedMovements { get; } = [];
    }

    private sealed class MovementRuntime(
        IntersectionMovementId id,
        LaneConnectionSnapshot connection,
        WorldPoint stopLinePosition,
        WorldPoint pathStart,
        WorldPoint pathEnd,
        int roadPriority,
        int turnPriority,
        RoadSegmentId incomingSegmentId)
    {
        public IntersectionMovementId Id { get; } = id;
        public LaneConnectionSnapshot Connection { get; } = connection;
        public WorldPoint StopLinePosition { get; } = stopLinePosition;
        public WorldPoint PathStart { get; } = pathStart;
        public WorldPoint PathEnd { get; } = pathEnd;
        public int RoadPriority { get; } = roadPriority;
        public int TurnPriority { get; } = turnPriority;
        public RoadSegmentId IncomingSegmentId { get; } = incomingSegmentId;
        public HashSet<IntersectionMovementId> Conflicts { get; } = [];
        public ControllerRuntime? Controller { get; set; }
        public int SignalPhaseIndex { get; set; }
        public int QueueLength { get; set; }
        public bool EntryGranted { get; set; }

        public static int ComparePriority(MovementRuntime left, MovementRuntime right)
        {
            var road = left.RoadPriority.CompareTo(right.RoadPriority);
            if (road != 0) return road;
            var turn = left.TurnPriority.CompareTo(right.TurnPriority);
            if (turn != 0) return turn;
            return left.Id.Value.CompareTo(right.Id.Value);
        }
    }
}
