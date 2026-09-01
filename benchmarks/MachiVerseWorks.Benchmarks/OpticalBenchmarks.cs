using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class OpticalBenchmarks
{
    private SimulationWorld _world = null!;

    [Params(1_000, 5_000)]
    public int LoadCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 10, seed: 26));
        var backbone = _world.CreateOpticalNode(new WorldPoint(0d, 0d, 0d), OpticalNodeKind.BackboneGateway);
        var aggregation = _world.CreateOpticalNode(new WorldPoint(10d, 0d, 0d), OpticalNodeKind.Distribution);
        _world.CreateFiberCable(backbone, aggregation, 1_000_000d);
        _world.CreateOpticalBackhaul(backbone, 1_000_000d);
        _world.CreateOpticalEquipment(backbone, OpticalEquipmentKind.Router, 1_000_000d, requiresPower: false);
        _world.CreateOpticalEquipment(aggregation, OpticalEquipmentKind.Switch, 1_000_000d, requiresPower: false);

        for (var index = 0; index < LoadCount; index++)
        {
            var x = 20d + (index % 100) * 2d;
            var y = (index / 100) * 2d;
            var building = _world.CreateBuilding(new WorldVolume(x, y, 0d, x + 1d, y + 1d, 3d), BuildingKind.Commercial);
            var endpoint = _world.CreateOpticalNode(new WorldPoint(x, y, 1d), OpticalNodeKind.Endpoint);
            _world.CreateFiberCable(aggregation, endpoint, 100d);
            _world.CreateOpticalEquipment(endpoint, OpticalEquipmentKind.Onu, 100d, building, requiresPower: false);
            _world.CreateBuildingOpticalDemand(endpoint, building, 1d);
        }
        _world.Step();
    }

    [Benchmark]
    public OpticalStatistics StepAndSnapshotStatistics()
    {
        _world.Step();
        return _world.CreateOpticalStatistics();
    }

    [Benchmark]
    public OpticalSnapshot TopologySnapshot() => _world.CreateOpticalSnapshot();
}
