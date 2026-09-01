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

[MemoryDiagnoser]
public class DeliveredGasBenchmarks
{
    private SimulationWorld _world = null!;

    [Params(100, 1_000)]
    public int InventoryCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2501));
        var supplierBuilding = _world.CreateBuilding(new WorldVolume(0, -5, 0, 10, 5, 5), BuildingKind.Industrial);
        var destinationBuilding = _world.CreateBuilding(new WorldVolume(390, -5, 0, 400, 5, 5), BuildingKind.Commercial);
        var start = _world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = _world.CreateRoadNode(new WorldPoint(400, 0, 0));
        var segment = _world.CreateRoadSegment(start, end, RoadKind.Arterial);
        _world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 20d);
        var supplierAccess = _world.CreateRoadAccessPoint(segment, 0.02d, supplierBuilding, mode: RoadAccessMode.Motor);
        var destinationAccess = _world.CreateRoadAccessPoint(segment, 0.98d, destinationBuilding, mode: RoadAccessMode.Motor);
        var supplierCompany = _world.CreateCompany(IndustrySector.Transport, 100_000_000d, 0d);
        var supplier = _world.CreateEstablishment(supplierCompany, buildingId: supplierBuilding);
        var gas = _world.CreateCommodity(CommodityKind.Gas);
        _world.ConfigureInventory(supplier, gas, supplierAccess, InventoryRole.Supplier, capacity: InventoryCount * 20d, initialQuantity: InventoryCount * 10d);

        for (var index = 0; index < InventoryCount; index++)
        {
            var company = _world.CreateCompany(IndustrySector.Services, 100_000d, 0d);
            var establishment = _world.CreateEstablishment(company, buildingId: destinationBuilding);
            _world.ConfigureInventory(establishment, gas, destinationAccess, InventoryRole.Consumer, capacity: 20d, initialQuantity: 0d, reorderPoint: 5d, targetQuantity: 10d, dailyConsumptionUnits: 1d);
            _world.CreateDeliveredGasServicePoint(establishment, gas, 1d, destinationBuilding);
        }

        for (ulong tick = 0; tick < EconomyDefaults.TicksPerEconomicDay; tick++) _world.Step();
        var logistics = _world.CreateLogisticsSnapshot();
        if (logistics.Shipments.Count != InventoryCount)
            throw new InvalidOperationException($"Delivered Gas benchmark expected {InventoryCount} Shipments, got {logistics.Shipments.Count}.");
    }

    [Benchmark]
    public void Tick() => _world.Step();

    [Benchmark]
    public (GasSnapshot Gas, LogisticsSnapshot Logistics) Snapshots() =>
        (_world.CreateGasSnapshot(), _world.CreateLogisticsSnapshot());
}
