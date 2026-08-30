using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class RoutingBenchmarks
{
    private SimulationWorld _world = null!;
    private RouteRequest _cachedRequest = null!;
    private double _destinationX;
    private long _missSequence;

    [Params(100, 10_000, 100_000)]
    public int LaneCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(spatialCellSize: 64d));
        var previousNode = _world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        LaneId? previousLane = null;
        for (var index = 0; index < LaneCount; index++)
        {
            var isLast = index == LaneCount - 1;
            var nextNode = _world.CreateRoadNode(
                new WorldPoint((index + 1) * 10d, 0d, (index % 8) * 0.25d),
                isLast ? RoadNodeKind.Endpoint : RoadNodeKind.Intersection);
            var segment = _world.CreateRoadSegment(previousNode, nextNode, RoadKind.Local);
            var lane = _world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 13.8888888889d);
            if (previousLane is { } from)
                _world.CreateLaneConnection(from, lane, previousNode, TurnMovement.Straight);
            previousLane = lane;
            previousNode = nextNode;
        }

        _destinationX = LaneCount * 10d - 0.01d;
        _cachedRequest = new RouteRequest(new WorldPoint(0.01d, 0d, 0d), new WorldPoint(_destinationX, 0d, ((LaneCount - 1) % 8) * 0.25d));
        _ = _world.FindRoadRoute(_cachedRequest);
    }

    [Benchmark(Baseline = true)]
    public RouteResult CachedRoute() => _world.FindRoadRoute(_cachedRequest);

    [Benchmark]
    public RouteResult SearchCacheMiss()
    {
        var delta = (++_missSequence % 1_000_000L) * 1e-10d;
        var request = new RouteRequest(
            new WorldPoint(0.01d + delta, 0d, 0d),
            new WorldPoint(_destinationX, 0d, ((LaneCount - 1) % 8) * 0.25d));
        return _world.FindRoadRoute(request);
    }
}
