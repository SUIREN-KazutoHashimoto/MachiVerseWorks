using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

public class SnapshotBenchmarks
{
    private static readonly WorldVolume SpawnVolume = new(
        -5_000d,
        -5_000d,
        -500d,
        5_000d,
        5_000d,
        500d);
    private static readonly WorldVolume SubscriptionVolume = new(
        -512d,
        -512d,
        -128d,
        512d,
        512d,
        128d);
    private SimulationWorld _world = null!;

    [Params(1_000, 10_000, 100_000)]
    public int AgentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 1234, spatialCellSize: 64d));
        _world.CreateAgents(AgentCount, SpawnVolume);
        for (var index = 0; index < 30; index++)
        {
            _world.Step();
        }
    }

    [Benchmark]
    public AgentSnapshot[] CreateSnapshot()
    {
        return _world.CreateSnapshot(SubscriptionVolume);
    }
}
