using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class GasBenchmarks
{
    private SimulationWorld _world = null!;

    [Params(1_000, 5_000)]
    public int LoadCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 10, seed: 25));
        var source = _world.CreateGasNode(new WorldPoint(0d, 0d, 0d), GasNodeKind.Source);
        var distribution = _world.CreateGasNode(new WorldPoint(10d, 0d, 0d), GasNodeKind.Distribution);
        _world.CreateGasPipeline(source, distribution, 1_000_000d);
        _world.CreateGasSource(source, 1_000_000d);
        _world.CreateGasStorage(source, 1_000_000d, 500_000d, 100_000d);

        for (var index = 0; index < LoadCount; index++)
        {
            var x = 20d + (index % 100) * 2d;
            var y = (index / 100) * 2d;
            var building = _world.CreateBuilding(new WorldVolume(x, y, 0d, x + 1d, y + 1d, 3d), BuildingKind.Commercial);
            var service = _world.CreateGasNode(new WorldPoint(x, y, 0d), GasNodeKind.Service);
            _world.CreateGasPipeline(distribution, service, 100d);
            _world.CreatePipedGasServicePoint(service, 1d, buildingId: building);
        }
        _world.Step();
    }

    [Benchmark]
    public GasStatistics StepAndSnapshotStatistics()
    {
        _world.Step();
        return _world.CreateGasStatistics();
    }
}
