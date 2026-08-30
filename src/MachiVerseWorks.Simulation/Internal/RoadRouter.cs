namespace MachiVerseWorks.Simulation.Internal;

internal sealed class RoadRouter
{
    internal const int CacheCapacity = 1024;
    internal const int CacheStepCapacity = 100_000;

    private readonly Dictionary<LaneId, RoutingLane> lanes = [];
    private readonly Dictionary<LaneId, LaneConnectionSnapshot[]> outgoing = [];
    private readonly Dictionary<LaneConnectionId, LaneConnectionSnapshot> connections = [];
    private readonly Dictionary<RouteCacheKey, LinkedListNode<RouteCacheEntry>> cache = [];
    private readonly LinkedList<RouteCacheEntry> lru = new();
    private RoutingLane[] orderedLanes = [];
    private bool topologyDirty = true;
    private int cachedSteps;
    private long cacheHits;
    private long cacheMisses;
    private long invalidations;

    public bool NeedsTopology => topologyDirty;

    public void Invalidate()
    {
        topologyDirty = true;
        lanes.Clear();
        orderedLanes = [];
        outgoing.Clear();
        connections.Clear();
        cache.Clear();
        lru.Clear();
        cachedSteps = 0;
        invalidations++;
    }

    public void Rebuild(RoadNetworkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lanes.Clear();
        outgoing.Clear();
        connections.Clear();
        cache.Clear();
        lru.Clear();
        cachedSteps = 0;

        var nodes = snapshot.Nodes.ToDictionary(static item => item.Id);
        var segments = snapshot.Segments.ToDictionary(static item => item.Id);
        foreach (var lane in snapshot.Lanes.OrderBy(static item => item.Id.Value))
        {
            if (!segments.TryGetValue(lane.SegmentId, out var segment))
                throw new InvalidOperationException($"Lane {lane.Id.Value} references missing Road segment {lane.SegmentId.Value}.");
            if (!nodes.TryGetValue(segment.StartNodeId, out var start) || !nodes.TryGetValue(segment.EndNodeId, out var end))
                throw new InvalidOperationException($"Road segment {segment.Id.Value} references missing Road nodes.");
            var length = Distance(start.Position, end.Position);
            lanes.Add(lane.Id, new RoutingLane(lane, start.Position, end.Position, length));
            outgoing.Add(lane.Id, []);
        }
        orderedLanes = lanes.Values.OrderBy(static item => item.Snapshot.Id.Value).ToArray();

        var grouped = new Dictionary<LaneId, List<LaneConnectionSnapshot>>();
        foreach (var connection in snapshot.Connections.OrderBy(static item => item.Id.Value))
        {
            if (!lanes.ContainsKey(connection.FromLaneId) || !lanes.ContainsKey(connection.ToLaneId))
                throw new InvalidOperationException($"Lane connection {connection.Id.Value} references missing lanes.");
            connections.Add(connection.Id, connection);
            if (!grouped.TryGetValue(connection.FromLaneId, out var list))
            {
                list = [];
                grouped.Add(connection.FromLaneId, list);
            }
            list.Add(connection);
        }
        foreach (var entry in grouped)
        {
            entry.Value.Sort(static (left, right) =>
            {
                var connection = left.Id.Value.CompareTo(right.Id.Value);
                return connection != 0 ? connection : left.ToLaneId.Value.CompareTo(right.ToLaneId.Value);
            });
            outgoing[entry.Key] = entry.Value.ToArray();
        }

        topologyDirty = false;
    }

    public RouteResult FindRoute(RouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (topologyDirty) throw new InvalidOperationException("Routing topology must be rebuilt before searching for a route.");

        var constraints = request.Constraints ?? EmptyConstraints.Instance;
        ValidateConstraints(constraints);
        if (!constraints.HasClosures)
        {
            var key = RouteCacheKey.Create(request);
            if (TryGetCached(key, out var cached))
            {
                cacheHits++;
                return cached;
            }
            cacheMisses++;
            var result = FindRouteCore(request, constraints);
            AddCached(key, result);
            return result;
        }

        return FindRouteCore(request, constraints);
    }

    public RoutingCacheStatistics GetCacheStatistics() => new(cache.Count, cacheHits, cacheMisses, invalidations);

    private RouteResult FindRouteCore(RouteRequest request, RouteConstraints constraints)
    {
        if (lanes.Count == 0) throw new InvalidOperationException("The Road Network does not contain any lanes.");
        var origin = ResolveNearestLane(request.Origin, constraints);
        var destination = ResolveNearestLane(request.Destination, constraints);
        var originLane = lanes[origin.LaneId];
        var destinationLane = lanes[destination.LaneId];

        if (origin.LaneId == destination.LaneId && IsForwardReachable(originLane, origin.SegmentOffset, destination.SegmentOffset))
            return BuildDirectResult(request.CostMetric, originLane, origin.SegmentOffset, destination.SegmentOffset);

        var distances = new Dictionary<LaneId, double>();
        var previous = new Dictionary<LaneId, RoutePredecessor>();
        var settled = new HashSet<LaneId>();
        var queue = new PriorityQueue<LaneId, (double Cost, ulong LaneId)>();
        var originExitDistance = DistanceToExit(originLane, origin.SegmentOffset);
        var originExitCost = ToCost(request.CostMetric, originExitDistance, originLane.Snapshot.SpeedLimitMetersPerSecond);
        distances.Add(origin.LaneId, originExitCost);
        queue.Enqueue(origin.LaneId, (originExitCost, origin.LaneId.Value));

        var bestGoalCost = double.PositiveInfinity;
        RouteGoalPredecessor? bestGoal = null;
        while (queue.TryDequeue(out var current, out var priority))
        {
            if (!distances.TryGetValue(current, out var known) || priority.Cost > known || settled.Contains(current)) continue;
            if (known > bestGoalCost) break;
            settled.Add(current);
            if (!outgoing.TryGetValue(current, out var nextConnections)) continue;

            foreach (var connection in nextConnections)
            {
                if (constraints.IsConnectionClosed(connection.Id) || constraints.IsLaneClosed(connection.ToLaneId)) continue;
                var nextLane = lanes[connection.ToLaneId];
                if (connection.ToLaneId == destination.LaneId)
                {
                    var destinationDistance = DistanceFromEntry(nextLane, destination.SegmentOffset);
                    var candidateGoal = known + ToCost(request.CostMetric, destinationDistance, nextLane.Snapshot.SpeedLimitMetersPerSecond);
                    if (candidateGoal < bestGoalCost || (candidateGoal == bestGoalCost && IsPreferredGoal(current, connection.Id, bestGoal)))
                    {
                        bestGoalCost = candidateGoal;
                        bestGoal = new RouteGoalPredecessor(current, connection.Id);
                    }
                    continue;
                }

                var candidate = known + ToCost(request.CostMetric, nextLane.LengthMeters, nextLane.Snapshot.SpeedLimitMetersPerSecond);
                if (settled.Contains(connection.ToLaneId))
                {
                    if (distances.TryGetValue(connection.ToLaneId, out var settledCost)
                        && candidate == settledCost
                        && IsPreferredPredecessor(current, connection.Id, previous, connection.ToLaneId)
                        && !WouldCreatePredecessorCycle(current, connection.ToLaneId, previous))
                    {
                        previous[connection.ToLaneId] = new RoutePredecessor(current, connection.Id);
                    }
                    continue;
                }

                if (!distances.TryGetValue(connection.ToLaneId, out var old)
                    || candidate < old
                    || (candidate == old && IsPreferredPredecessor(current, connection.Id, previous, connection.ToLaneId)))
                {
                    distances[connection.ToLaneId] = candidate;
                    previous[connection.ToLaneId] = new RoutePredecessor(current, connection.Id);
                    queue.Enqueue(connection.ToLaneId, (candidate, connection.ToLaneId.Value));
                }
            }
        }

        if (bestGoal is not { } goal)
            throw new InvalidOperationException("No drivable road route exists between the resolved origin and destination lanes.");

        var laneSequence = ReconstructLaneSequence(origin.LaneId, destination.LaneId, goal, previous, out var transitionSequence);
        return BuildResult(request.CostMetric, laneSequence, transitionSequence, origin.SegmentOffset, destination.SegmentOffset);
    }

    private ResolvedLane ResolveNearestLane(WorldPoint point, RouteConstraints constraints)
    {
        var found = false;
        LaneId selected = default;
        var selectedOffset = 0d;
        var selectedDistanceSquared = double.PositiveInfinity;
        foreach (var lane in orderedLanes)
        {
            if (constraints.IsLaneClosed(lane.Snapshot.Id)) continue;
            var offset = ProjectSegmentOffset(point, lane.SegmentStart, lane.SegmentEnd);
            var projected = Interpolate(lane.SegmentStart, lane.SegmentEnd, offset);
            var distanceSquared = DistanceSquared(point, projected);
            if (!found || distanceSquared < selectedDistanceSquared || (distanceSquared == selectedDistanceSquared && lane.Snapshot.Id.Value < selected.Value))
            {
                found = true;
                selected = lane.Snapshot.Id;
                selectedOffset = offset;
                selectedDistanceSquared = distanceSquared;
            }
        }
        if (!found) throw new InvalidOperationException("No open Road lane is available for endpoint resolution.");
        return new ResolvedLane(selected, selectedOffset);
    }

    private static RouteResult BuildDirectResult(RoutingCostMetric metric, RoutingLane lane, double startOffset, double endOffset)
    {
        var distance = DistanceBetween(lane, startOffset, endOffset);
        var time = distance / lane.Snapshot.SpeedLimitMetersPerSecond;
        var step = new RouteLaneStep(lane.Snapshot.Id, lane.Snapshot.SegmentId, startOffset, endOffset, distance, time, null);
        return new RouteResult(metric, metric == RoutingCostMetric.Distance ? distance : time, distance, time, [step]);
    }

    private RouteResult BuildResult(
        RoutingCostMetric metric,
        LaneId[] laneSequence,
        LaneConnectionId[] transitionSequence,
        double originOffset,
        double destinationOffset)
    {
        var steps = new RouteLaneStep[laneSequence.Length];
        var totalDistance = 0d;
        var totalTime = 0d;
        for (var index = 0; index < laneSequence.Length; index++)
        {
            var lane = lanes[laneSequence[index]];
            var startOffset = index == 0 ? originOffset : EntryOffset(lane);
            var endOffset = index == laneSequence.Length - 1 ? destinationOffset : ExitOffset(lane);
            var distance = DistanceBetween(lane, startOffset, endOffset);
            var time = distance / lane.Snapshot.SpeedLimitMetersPerSecond;
            LaneConnectionId? exitConnection = index < transitionSequence.Length ? transitionSequence[index] : null;
            steps[index] = new RouteLaneStep(lane.Snapshot.Id, lane.Snapshot.SegmentId, startOffset, endOffset, distance, time, exitConnection);
            totalDistance += distance;
            totalTime += time;
        }
        var cost = metric == RoutingCostMetric.Distance ? totalDistance : totalTime;
        return new RouteResult(metric, cost, totalDistance, totalTime, steps);
    }

    private static LaneId[] ReconstructLaneSequence(
        LaneId origin,
        LaneId destination,
        RouteGoalPredecessor goal,
        Dictionary<LaneId, RoutePredecessor> previous,
        out LaneConnectionId[] transitions)
    {
        var reversedLanes = new List<LaneId> { goal.FromLaneId };
        var reversedTransitions = new List<LaneConnectionId>();
        var cursor = goal.FromLaneId;
        while (cursor != origin)
        {
            if (!previous.TryGetValue(cursor, out var step))
                throw new InvalidOperationException("Road route predecessor chain does not terminate at the resolved origin lane.");
            reversedTransitions.Add(step.ConnectionId);
            cursor = step.FromLaneId;
            reversedLanes.Add(cursor);
        }
        reversedLanes.Reverse();
        reversedTransitions.Reverse();
        reversedLanes.Add(destination);
        reversedTransitions.Add(goal.ConnectionId);
        transitions = reversedTransitions.ToArray();
        return reversedLanes.ToArray();
    }

    private void ValidateConstraints(RouteConstraints constraints)
    {
        foreach (var laneId in constraints.ClosedLaneIds)
        {
            if (!lanes.ContainsKey(laneId)) throw new ArgumentException($"Closed Lane {laneId.Value} does not exist in the Road Network.", nameof(constraints));
        }
        foreach (var connectionId in constraints.ClosedConnectionIds)
        {
            if (!connections.ContainsKey(connectionId)) throw new ArgumentException($"Closed Lane connection {connectionId.Value} does not exist in the Road Network.", nameof(constraints));
        }
    }

    private bool TryGetCached(RouteCacheKey key, out RouteResult result)
    {
        if (!cache.TryGetValue(key, out var node))
        {
            result = null!;
            return false;
        }
        lru.Remove(node);
        lru.AddFirst(node);
        result = node.Value.Result;
        return true;
    }

    private void AddCached(RouteCacheKey key, RouteResult result)
    {
        var stepCount = result.Steps.Count;
        if (stepCount > CacheStepCapacity) return;

        if (cache.TryGetValue(key, out var existing))
        {
            cachedSteps -= existing.Value.Result.Steps.Count;
            existing.Value = new RouteCacheEntry(key, result);
            cachedSteps += stepCount;
            lru.Remove(existing);
            lru.AddFirst(existing);
        }
        else
        {
            var node = new LinkedListNode<RouteCacheEntry>(new RouteCacheEntry(key, result));
            lru.AddFirst(node);
            cache.Add(key, node);
            cachedSteps += stepCount;
        }

        while (cache.Count > CacheCapacity || cachedSteps > CacheStepCapacity)
        {
            var last = lru.Last!;
            lru.RemoveLast();
            cache.Remove(last.Value.Key);
            cachedSteps -= last.Value.Result.Steps.Count;
        }
    }

    private static bool IsPreferredPredecessor(
        LaneId current,
        LaneConnectionId connection,
        Dictionary<LaneId, RoutePredecessor> previous,
        LaneId next)
    {
        if (!previous.TryGetValue(next, out var old)) return true;
        var connectionComparison = connection.Value.CompareTo(old.ConnectionId.Value);
        return connectionComparison < 0 || (connectionComparison == 0 && current.Value < old.FromLaneId.Value);
    }

    private static bool WouldCreatePredecessorCycle(
        LaneId current,
        LaneId next,
        Dictionary<LaneId, RoutePredecessor> previous)
    {
        var cursor = current;
        while (true)
        {
            if (cursor == next) return true;
            if (!previous.TryGetValue(cursor, out var step)) return false;
            cursor = step.FromLaneId;
        }
    }

    private static bool IsPreferredGoal(LaneId current, LaneConnectionId connection, RouteGoalPredecessor? old)
    {
        if (old is not { } previous) return true;
        var connectionComparison = connection.Value.CompareTo(previous.ConnectionId.Value);
        return connectionComparison < 0 || (connectionComparison == 0 && current.Value < previous.FromLaneId.Value);
    }

    private static double ToCost(RoutingCostMetric metric, double distanceMeters, double speedMetersPerSecond) =>
        metric == RoutingCostMetric.Distance ? distanceMeters : distanceMeters / speedMetersPerSecond;

    private static bool IsForwardReachable(RoutingLane lane, double fromOffset, double toOffset) =>
        lane.Snapshot.Direction == LaneDirection.Forward ? toOffset >= fromOffset : toOffset <= fromOffset;

    private static double EntryOffset(RoutingLane lane) => lane.Snapshot.Direction == LaneDirection.Forward ? 0d : 1d;
    private static double ExitOffset(RoutingLane lane) => lane.Snapshot.Direction == LaneDirection.Forward ? 1d : 0d;
    private static double DistanceToExit(RoutingLane lane, double fromOffset) => DistanceBetween(lane, fromOffset, ExitOffset(lane));
    private static double DistanceFromEntry(RoutingLane lane, double toOffset) => DistanceBetween(lane, EntryOffset(lane), toOffset);

    private static double DistanceBetween(RoutingLane lane, double fromOffset, double toOffset)
    {
        var delta = lane.Snapshot.Direction == LaneDirection.Forward ? toOffset - fromOffset : fromOffset - toOffset;
        if (delta < 0d) throw new InvalidOperationException("Route progress attempts to move against Lane direction.");
        return lane.LengthMeters * delta;
    }

    private static double ProjectSegmentOffset(WorldPoint point, WorldPoint start, WorldPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var dz = end.Z - start.Z;
        var lengthSquared = dx * dx + dy * dy + dz * dz;
        if (lengthSquared == 0d) return 0d;
        var projection = ((point.X - start.X) * dx + (point.Y - start.Y) * dy + (point.Z - start.Z) * dz) / lengthSquared;
        return Math.Clamp(projection, 0d, 1d);
    }

    private static WorldPoint Interpolate(WorldPoint start, WorldPoint end, double offset) => new(
        start.X + (end.X - start.X) * offset,
        start.Y + (end.Y - start.Y) * offset,
        start.Z + (end.Z - start.Z) * offset);

    private static double Distance(WorldPoint first, WorldPoint second) => Math.Sqrt(DistanceSquared(first, second));
    private static double DistanceSquared(WorldPoint first, WorldPoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        var dz = second.Z - first.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    private readonly record struct RoutingLane(
        LaneSnapshot Snapshot,
        WorldPoint SegmentStart,
        WorldPoint SegmentEnd,
        double LengthMeters);

    private readonly record struct ResolvedLane(LaneId LaneId, double SegmentOffset);
    private readonly record struct RoutePredecessor(LaneId FromLaneId, LaneConnectionId ConnectionId);
    private readonly record struct RouteGoalPredecessor(LaneId FromLaneId, LaneConnectionId ConnectionId);
    private readonly record struct RouteCacheEntry(RouteCacheKey Key, RouteResult Result);

    private readonly record struct RouteCacheKey(
        long OriginX,
        long OriginY,
        long OriginZ,
        long DestinationX,
        long DestinationY,
        long DestinationZ,
        RoutingCostMetric Metric)
    {
        public static RouteCacheKey Create(RouteRequest request) => new(
            BitConverter.DoubleToInt64Bits(request.Origin.X),
            BitConverter.DoubleToInt64Bits(request.Origin.Y),
            BitConverter.DoubleToInt64Bits(request.Origin.Z),
            BitConverter.DoubleToInt64Bits(request.Destination.X),
            BitConverter.DoubleToInt64Bits(request.Destination.Y),
            BitConverter.DoubleToInt64Bits(request.Destination.Z),
            request.CostMetric);
    }

    private static class EmptyConstraints
    {
        public static readonly RouteConstraints Instance = new();
    }
}

internal readonly record struct RoutingCacheStatistics(int Entries, long Hits, long Misses, long Invalidations);
