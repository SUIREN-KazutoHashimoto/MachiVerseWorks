using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;
using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Benchmarks;

public class SpatialQueryBenchmarks
{
    private SpatialIndex _index = null!;

    [Params(10_000, 100_000)]
    public int AgentCount { get; set; }

    [Params(256d, 1_024d)]
    public double QueryHalfExtent { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _index = new SpatialIndex(64d);
        var side = (int)Math.Ceiling(Math.Sqrt(AgentCount));
        var spacing = 10_000d / side;

        for (var index = 0; index < AgentCount; index++)
        {
            var xIndex = index % side;
            var yIndex = index / side;
            var position = new WorldPoint(
                -5_000d + ((xIndex + 0.5d) * spacing),
                -5_000d + ((yIndex + 0.5d) * spacing));
            _index.Register(new AgentId((ulong)index + 1), position);
        }
    }

    [Benchmark]
    public List<AgentId> Query()
    {
        var extent = QueryHalfExtent;
        return _index.Query(new WorldRect(-extent, -extent, extent, extent));
    }
}
