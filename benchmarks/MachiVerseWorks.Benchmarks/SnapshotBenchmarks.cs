using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[Config(typeof(PerformanceBenchmarkConfig))]
public sealed class SnapshotBenchmarks
{
    private static readonly WorldRect SpawnArea = new(-5_000d, -5_000d, 5_000d, 5_000d);
    private static readonly WorldRect SubscriptionArea = new(-512d, -512d, 512d, 512d);
    private SimulationWorld _world = null!;

    [Params(1_000, 10_000, 100_000)]
    public int AgentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 1234, spatialCellSize: 64d));
        _world.CreateAgents(AgentCount, SpawnArea);
        for (var index = 0; index < 30; index++)
        {
            _world.Step();
        }
    }

    [Benchmark]
    public AgentSnapshot[] CreateSnapshot()
    {
        return _world.CreateSnapshot(SubscriptionArea);
    }
}
