using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class WaterSewerBenchmarks
{
    private SimulationWorld _world = null!;

    [Params(1_000, 5_000)]
    public int LoadCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 10, seed: 24));
        var source = _world.CreateWaterNode(new WorldPoint(0d, 0d, 0d), WaterNodeKind.Source);
        var distribution = _world.CreateWaterNode(new WorldPoint(10d, 0d, 0d), WaterNodeKind.Distribution);
        _world.CreateWaterPipe(source, distribution, 1_000_000d);
        _world.CreateWaterSource(source, 1_000_000d);

        var collection = _world.CreateSewerNode(new WorldPoint(10d, 100d, -3d), SewerNodeKind.Collection);
        var treatment = _world.CreateSewerNode(new WorldPoint(0d, 100d, -3d), SewerNodeKind.Treatment);
        _world.CreateSewerPipe(collection, treatment, 1_000_000d);
        _world.CreateSewageTreatmentPlant(treatment, 1_000_000d);

        for (var index = 0; index < LoadCount; index++)
        {
            var x = 20d + (index % 100) * 2d;
            var y = (index / 100) * 2d;
            var building = _world.CreateBuilding(new WorldVolume(x, y, 0d, x + 1d, y + 1d, 3d), BuildingKind.Commercial);
            var waterService = _world.CreateWaterNode(new WorldPoint(x, y, 0d), WaterNodeKind.Service);
            var sewerService = _world.CreateSewerNode(new WorldPoint(x, y, -3d), SewerNodeKind.Service);
            _world.CreateWaterPipe(distribution, waterService, 100d);
            _world.CreateSewerPipe(sewerService, collection, 100d);
            _world.CreateWaterSewerServicePoint(waterService, sewerService, 1d, buildingId: building);
        }
        _world.Step();
    }

    [Benchmark]
    public WaterSewerStatistics StepAndSnapshotStatistics()
    {
        _world.Step();
        return _world.CreateWaterSewerStatistics();
    }
}
