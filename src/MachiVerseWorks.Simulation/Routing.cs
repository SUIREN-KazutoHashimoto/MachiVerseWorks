namespace MachiVerseWorks.Simulation;

public enum RoutingCostMetric : byte
{
    Distance = 0,
    EstimatedTravelTime = 1,
}

public sealed class RouteConstraints
{
    private readonly HashSet<LaneId> closedLaneIds;
    private readonly HashSet<LaneConnectionId> closedConnectionIds;

    public RouteConstraints(
        IEnumerable<LaneId>? closedLaneIds = null,
        IEnumerable<LaneConnectionId>? closedConnectionIds = null)
    {
        var lanes = closedLaneIds?.Distinct().OrderBy(static id => id.Value).ToArray() ?? [];
        var connections = closedConnectionIds?.Distinct().OrderBy(static id => id.Value).ToArray() ?? [];
        this.closedLaneIds = lanes.ToHashSet();
        this.closedConnectionIds = connections.ToHashSet();
        ClosedLaneIds = Array.AsReadOnly(lanes);
        ClosedConnectionIds = Array.AsReadOnly(connections);
    }

    public IReadOnlyList<LaneId> ClosedLaneIds { get; }
    public IReadOnlyList<LaneConnectionId> ClosedConnectionIds { get; }

    internal bool HasClosures => closedLaneIds.Count != 0 || closedConnectionIds.Count != 0;
    internal bool IsLaneClosed(LaneId id) => closedLaneIds.Contains(id);
    internal bool IsConnectionClosed(LaneConnectionId id) => closedConnectionIds.Contains(id);
}

public sealed record RouteRequest(
    WorldPoint Origin,
    WorldPoint Destination,
    RoutingCostMetric CostMetric = RoutingCostMetric.Distance,
    RouteConstraints? Constraints = null);

public readonly record struct RouteLaneStep(
    LaneId LaneId,
    RoadSegmentId SegmentId,
    double StartSegmentOffset,
    double EndSegmentOffset,
    double DistanceMeters,
    double EstimatedTravelTimeSeconds,
    LaneConnectionId? ExitConnectionId);

public sealed class RouteResult
{
    internal RouteResult(
        RoutingCostMetric costMetric,
        double cost,
        double totalDistanceMeters,
        double estimatedTravelTimeSeconds,
        IReadOnlyList<RouteLaneStep> steps)
    {
        CostMetric = costMetric;
        Cost = cost;
        TotalDistanceMeters = totalDistanceMeters;
        EstimatedTravelTimeSeconds = estimatedTravelTimeSeconds;
        Steps = Array.AsReadOnly(steps.ToArray());
    }

    public RoutingCostMetric CostMetric { get; }
    public double Cost { get; }
    public double TotalDistanceMeters { get; }
    public double EstimatedTravelTimeSeconds { get; }
    public IReadOnlyList<RouteLaneStep> Steps { get; }

    public LaneId OriginLaneId => Steps.Count == 0 ? default : Steps[0].LaneId;
    public LaneId DestinationLaneId => Steps.Count == 0 ? default : Steps[^1].LaneId;
}
