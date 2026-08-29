using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

public class RoadNetworkBenchmarks
{
    private SimulationWorld _world = null!;
    private RoadSegmentId _lastSegmentId;
    private WorldVolume _queryVolume;

    [Params(10_000, 100_000)]
    public int RoadSegmentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(spatialCellSize: 64d));
        const int columns = 1_000;
        for (var index = 0; index < RoadSegmentCount; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var altitude = (index % 4) * 5d;
            var start = _world.CreateRoadNode(new WorldPoint(column * 20d, row * 20d, altitude));
            var end = _world.CreateRoadNode(new WorldPoint((column * 20d) + 10d, row * 20d, altitude));
            _lastSegmentId = _world.CreateRoadSegment(start, end, RoadKind.Local);
        }

        _queryVolume = new WorldVolume(2_000d, 0d, -1d, 4_000d, 1_000d, 16d);
    }

    [Benchmark]
    public RoadNetworkSnapshot QuerySpatialVolume() => _world.CreateRoadNetworkSnapshot(_queryVolume);

    [Benchmark]
    public RoadNetworkSnapshot CreateFullTopologySnapshot() => _world.CreateRoadNetworkSnapshot();

    [Benchmark]
    public bool LookupStableSegment() => _world.TryGetRoadSegmentSnapshot(_lastSegmentId, out _);
}
