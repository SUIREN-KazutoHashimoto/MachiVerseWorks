namespace MachiVerseWorks.Simulation.Internal;

internal sealed class RoadTrafficTopology
{
    private readonly Dictionary<LaneId, TrafficLaneGeometry> lanes = [];
    private readonly Dictionary<LaneConnectionId, LaneConnectionSnapshot> connections = [];
    private bool dirty = true;
    private double totalLaneLengthMeters;

    public bool NeedsTopology => dirty;
    public double TotalLaneLengthMeters => totalLaneLengthMeters;

    public void Invalidate() => dirty = true;

    public void Rebuild(RoadNetworkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lanes.Clear();
        connections.Clear();
        totalLaneLengthMeters = 0d;

        var nodes = snapshot.Nodes.ToDictionary(static item => item.Id);
        var segments = snapshot.Segments.ToDictionary(static item => item.Id);
        var offsets = BuildLaneCenterOffsets(snapshot.Lanes);
        foreach (var lane in snapshot.Lanes.OrderBy(static item => item.Id.Value))
        {
            if (!segments.TryGetValue(lane.SegmentId, out var segment)
                || !nodes.TryGetValue(segment.StartNodeId, out var start)
                || !nodes.TryGetValue(segment.EndNodeId, out var end))
            {
                throw new InvalidOperationException($"Lane {lane.Id.Value} references incomplete Road geometry.");
            }

            var dx = end.Position.X - start.Position.X;
            var dy = end.Position.Y - start.Position.Y;
            var dz = end.Position.Z - start.Position.Z;
            var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (!double.IsFinite(length) || length <= 0d)
                throw new InvalidOperationException($"Lane {lane.Id.Value} requires a positive 3D segment length.");

            var horizontalLength = Math.Sqrt(dx * dx + dy * dy);
            var centerOffset = offsets.TryGetValue(lane.Id, out var value) ? value : 0d;
            var offsetX = horizontalLength > 0d ? -dy / horizontalLength * centerOffset : 0d;
            var offsetY = horizontalLength > 0d ? dx / horizontalLength * centerOffset : 0d;
            var centerStart = new WorldPoint(start.Position.X + offsetX, start.Position.Y + offsetY, start.Position.Z);
            var centerEnd = new WorldPoint(end.Position.X + offsetX, end.Position.Y + offsetY, end.Position.Z);
            var forwardScale = lane.Direction == LaneDirection.Forward ? 1d / length : -1d / length;
            var forward = new WorldVector(dx * forwardScale, dy * forwardScale, dz * forwardScale);
            lanes.Add(lane.Id, new TrafficLaneGeometry(lane, centerStart, centerEnd, length, forward));
            totalLaneLengthMeters += length;
        }

        foreach (var connection in snapshot.Connections)
        {
            if (!lanes.ContainsKey(connection.FromLaneId) || !lanes.ContainsKey(connection.ToLaneId))
                throw new InvalidOperationException($"Lane connection {connection.Id.Value} references an unknown Lane.");
            connections.Add(connection.Id, connection);
        }
        dirty = false;
    }

    public TrafficLaneGeometry GetLane(LaneId id)
    {
        if (dirty) throw new InvalidOperationException("Road Traffic topology must be rebuilt before use.");
        if (!lanes.TryGetValue(id, out var lane)) throw new InvalidOperationException($"Lane {id.Value} does not exist in Road Traffic topology.");
        return lane;
    }

    public WorldPoint GetPosition(LaneId laneId, double segmentOffset)
    {
        var lane = GetLane(laneId);
        if (!double.IsFinite(segmentOffset) || segmentOffset < 0d || segmentOffset > 1d) throw new ArgumentOutOfRangeException(nameof(segmentOffset));
        return new WorldPoint(
            lane.CenterStart.X + (lane.CenterEnd.X - lane.CenterStart.X) * segmentOffset,
            lane.CenterStart.Y + (lane.CenterEnd.Y - lane.CenterStart.Y) * segmentOffset,
            lane.CenterStart.Z + (lane.CenterEnd.Z - lane.CenterStart.Z) * segmentOffset);
    }

    public double GetLaneTravelProgress(LaneId laneId, double segmentOffset)
    {
        var lane = GetLane(laneId);
        if (!double.IsFinite(segmentOffset) || segmentOffset < 0d || segmentOffset > 1d) throw new ArgumentOutOfRangeException(nameof(segmentOffset));
        return lane.Snapshot.Direction == LaneDirection.Forward
            ? segmentOffset * lane.LengthMeters
            : (1d - segmentOffset) * lane.LengthMeters;
    }

    public double GetSegmentOffset(RouteLaneStep step, double progressMeters)
    {
        _ = GetLane(step.LaneId);
        if (!double.IsFinite(progressMeters) || progressMeters < 0d || progressMeters > step.DistanceMeters + 1e-9)
            throw new ArgumentOutOfRangeException(nameof(progressMeters));
        if (step.DistanceMeters <= 1e-12) return step.EndSegmentOffset;
        var ratio = Math.Clamp(progressMeters / step.DistanceMeters, 0d, 1d);
        return step.StartSegmentOffset + (step.EndSegmentOffset - step.StartSegmentOffset) * ratio;
    }

    public void ValidateRoute(IReadOnlyList<RouteLaneStep> route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.Count == 0) throw new ArgumentException("Vehicle Route must contain at least one Lane step.", nameof(route));
        for (var index = 0; index < route.Count; index++)
        {
            var step = route[index];
            var lane = GetLane(step.LaneId);
            if (lane.Snapshot.SegmentId != step.SegmentId) throw new ArgumentException($"Vehicle Route step {index} has a Lane/segment mismatch.", nameof(route));
            if (!double.IsFinite(step.StartSegmentOffset) || !double.IsFinite(step.EndSegmentOffset)
                || step.StartSegmentOffset < 0d || step.StartSegmentOffset > 1d
                || step.EndSegmentOffset < 0d || step.EndSegmentOffset > 1d)
                throw new ArgumentException($"Vehicle Route step {index} contains an invalid segment offset.", nameof(route));
            var delta = lane.Snapshot.Direction == LaneDirection.Forward
                ? step.EndSegmentOffset - step.StartSegmentOffset
                : step.StartSegmentOffset - step.EndSegmentOffset;
            if (delta < -1e-12) throw new ArgumentException($"Vehicle Route step {index} attempts to travel against Lane direction.", nameof(route));
            var expectedDistance = Math.Max(0d, delta) * lane.LengthMeters;
            if (!double.IsFinite(step.DistanceMeters) || step.DistanceMeters < 0d || Math.Abs(step.DistanceMeters - expectedDistance) > Math.Max(1e-6, expectedDistance * 1e-8))
                throw new ArgumentException($"Vehicle Route step {index} distance does not match Lane geometry.", nameof(route));

            if (index == route.Count - 1)
            {
                if (step.ExitConnectionId is not null) throw new ArgumentException("The final Vehicle Route step cannot declare an exit connection.", nameof(route));
                continue;
            }

            var next = route[index + 1];
            var nextLane = GetLane(next.LaneId);
            var sameSegmentLaneChange = step.SegmentId == next.SegmentId && step.LaneId != next.LaneId;
            if (sameSegmentLaneChange)
            {
                if (step.ExitConnectionId is not null) throw new ArgumentException("A same-segment Lane change must not use an intersection LaneConnection.", nameof(route));
                if (lane.Snapshot.Direction != nextLane.Snapshot.Direction)
                    throw new ArgumentException("A same-segment Lane change cannot reverse direction.", nameof(route));
                var orderDelta = Math.Abs((int)lane.Snapshot.Order - nextLane.Snapshot.Order);
                if (orderDelta != 1) throw new ArgumentException("A Vehicle Route may only change to an adjacent Lane on the same segment.", nameof(route));
                continue;
            }

            if (step.ExitConnectionId is not { } connectionId
                || !connections.TryGetValue(connectionId, out var connection)
                || connection.FromLaneId != step.LaneId
                || connection.ToLaneId != next.LaneId)
            {
                throw new ArgumentException($"Vehicle Route step {index} does not have a valid LaneConnection to the next Lane.", nameof(route));
            }
        }
    }

    private static Dictionary<LaneId, double> BuildLaneCenterOffsets(IReadOnlyList<LaneSnapshot> laneSnapshots)
    {
        var groups = new Dictionary<(RoadSegmentId SegmentId, LaneDirection Direction), List<LaneSnapshot>>();
        foreach (var lane in laneSnapshots)
        {
            var key = (lane.SegmentId, lane.Direction);
            if (!groups.TryGetValue(key, out var group)) { group = []; groups.Add(key, group); }
            group.Add(lane);
        }
        var result = new Dictionary<LaneId, double>(laneSnapshots.Count);
        foreach (var group in groups.Values)
        {
            group.Sort(static (left, right) =>
            {
                var order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : left.Id.Value.CompareTo(right.Id.Value);
            });
            var innerEdge = 0d;
            foreach (var lane in group)
            {
                var magnitude = innerEdge + lane.WidthMeters * 0.5d;
                var nextInnerEdge = innerEdge + lane.WidthMeters;
                if (!double.IsFinite(magnitude) || !double.IsFinite(nextInnerEdge))
                    throw new InvalidOperationException($"Lane {lane.Id.Value} produces a non-finite Road Traffic center offset.");
                result.Add(lane.Id, lane.Direction == LaneDirection.Forward ? magnitude : -magnitude);
                innerEdge = nextInnerEdge;
            }
        }
        return result;
    }
}

internal readonly record struct TrafficLaneGeometry(
    LaneSnapshot Snapshot,
    WorldPoint CenterStart,
    WorldPoint CenterEnd,
    double LengthMeters,
    WorldVector Forward);
