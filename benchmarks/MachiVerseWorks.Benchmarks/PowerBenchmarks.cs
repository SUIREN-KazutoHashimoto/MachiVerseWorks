using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class PowerBenchmarks
{
    private SimulationWorld _world = null!;

    [Params(1_000, 5_000)]
    public int LoadCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 10, seed: 23));
        var generatorNode = _world.CreatePowerNode(new WorldPoint(0d, 0d, 0d), PowerNodeKind.GeneratorBus);
        var previous = generatorNode;
        const int distributionNodeCount = 100;
        for (var index = 0; index < distributionNodeCount; index++)
        {
            var node = _world.CreatePowerNode(new WorldPoint((index + 1) * 10d, 0d, 0d), PowerNodeKind.Distribution);
            _world.CreatePowerLine(previous, node, 100_000d);
            previous = node;
        }
        _world.CreateGenerator(generatorNode, 100_000d);

        for (var index = 0; index < LoadCount; index++)
        {
            var x = 1_020d + (index % 100) * 2d;
            var y = (index / 100) * 2d;
            var building = _world.CreateBuilding(new WorldVolume(x, y, 0d, x + 1d, y + 1d, 3d), BuildingKind.Commercial);
            var node = _world.CreatePowerNode(new WorldPoint(x, y, 0d), PowerNodeKind.Load);
            _world.CreatePowerLine(previous, node, 100d);
            _world.CreatePowerLoad(node, 1d, buildingId: building);
        }
        _world.Step();
    }

    [Benchmark]
    public PowerStatistics StepAndSnapshotStatistics()
    {
        _world.Step();
        return _world.CreatePowerStatistics();
    }
}
