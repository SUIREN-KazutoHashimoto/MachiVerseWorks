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
        _index = CreateIndex(AgentCount);
    }

    [Benchmark]
    public List<AgentId> Query()
    {
        var extent = QueryHalfExtent;
        var altitudeExtent = Math.Max(64d, extent / 4d);
        return _index.Query(new WorldVolume(
            -extent,
            -extent,
            -altitudeExtent,
            extent,
            extent,
            altitudeExtent));
    }

    internal static SpatialIndex CreateIndex(int agentCount)
    {
        var index = new SpatialIndex(64d);
        var side = (int)Math.Ceiling(Math.Cbrt(agentCount));
        var horizontalSpacing = 10_000d / side;
        var verticalSpacing = 1_000d / side;

        for (var agentIndex = 0; agentIndex < agentCount; agentIndex++)
        {
            var xIndex = agentIndex % side;
            var yIndex = (agentIndex / side) % side;
            var zIndex = agentIndex / (side * side);
            var position = new WorldPoint(
                -5_000d + ((xIndex + 0.5d) * horizontalSpacing),
                -5_000d + ((yIndex + 0.5d) * horizontalSpacing),
                -500d + ((zIndex + 0.5d) * verticalSpacing));
            index.Register(new AgentId((ulong)agentIndex + 1), position);
        }

        return index;
    }
}

public class SparseSpatialQueryBenchmarks
{
    private SpatialIndex _index = null!;

    [Params(10_000, 100_000)]
    public int AgentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _index = SpatialQueryBenchmarks.CreateIndex(AgentCount);
    }

    [Benchmark]
    public List<AgentId> QueryLargeSparseVolume()
    {
        return _index.Query(new WorldVolume(
            -1_000_000d,
            -1_000_000d,
            -1_000_000d,
            1_000_000d,
            1_000_000d,
            1_000_000d));
    }
}
