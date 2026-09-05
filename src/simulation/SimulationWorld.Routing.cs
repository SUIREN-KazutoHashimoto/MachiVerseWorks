using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly RoadRouter _roadRouter = new();

    public RouteResult FindRoadRoute(RouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePoint(request.Origin);
        ValidatePoint(request.Destination);
        ValidateEnum(request.CostMetric, nameof(request));
        if (_roadRouter.NeedsTopology) _roadRouter.Rebuild(_roads.CreateSnapshot());
        return _roadRouter.FindRoute(request);
    }

    private void InvalidateRouting()
    {
        _roadRouter.Invalidate();
        _roadTrafficTopology.Invalidate();
    }

    internal RoutingCacheStatistics GetRoutingCacheStatistics() => _roadRouter.GetCacheStatistics();
}
