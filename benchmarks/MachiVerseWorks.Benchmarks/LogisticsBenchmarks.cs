using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class LogisticsBenchmarks
{
    private SimulationWorld _world = null!;
    private RouteRequest _routeRequest;

    [Params(100, 1_000)]
    public int InventoryCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 22));
        var supplierBuilding = _world.CreateBuilding(new WorldVolume(0, -5, 0, 10, 5, 5), BuildingKind.Industrial);
        var destinationBuilding = _world.CreateBuilding(new WorldVolume(90, -5, 0, 100, 5, 5), BuildingKind.Commercial);
        var start = _world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = _world.CreateRoadNode(new WorldPoint(100, 0, 0));
        var segment = _world.CreateRoadSegment(start, end, RoadKind.Arterial);
        _world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 20d);
        var supplierAccess = _world.CreateRoadAccessPoint(segment, 0.05d, supplierBuilding, mode: RoadAccessMode.Motor);
        var destinationAccess = _world.CreateRoadAccessPoint(segment, 0.95d, destinationBuilding, mode: RoadAccessMode.Motor);
        var supplierCompany = _world.CreateCompany(IndustrySector.Manufacturing, 100_000_000, InventoryCount * 20d);
        var supplier = _world.CreateEstablishment(supplierCompany, buildingId: supplierBuilding);
        var commodity = _world.CreateCommodity();
        _world.ConfigureInventory(supplier, commodity, supplierAccess, InventoryRole.Supplier, capacity: InventoryCount * 20d, initialQuantity: InventoryCount * 10d);

        for (var index = 0; index < InventoryCount; index++)
        {
            var company = _world.CreateCompany(IndustrySector.Retail, 100_000, 0d);
            var establishment = _world.CreateEstablishment(company, buildingId: destinationBuilding);
            _world.ConfigureInventory(establishment, commodity, destinationAccess, InventoryRole.Consumer, capacity: 20d, initialQuantity: 0d, reorderPoint: 5d, targetQuantity: 10d, dailyConsumptionUnits: 1d);
        }

        for (var tick = 0; tick < EconomyDefaults.TicksPerEconomicDay; tick++) _world.Step();
        _routeRequest = new RouteRequest(new WorldPoint(5, 0, 0), new WorldPoint(95, 0, 0), RoutingCostMetric.EstimatedTravelTime);
    }

    [Benchmark]
    public void Tick() => _world.Step();

    [Benchmark]
    public int RoutingBatch()
    {
        var steps = 0;
        for (var index = 0; index < InventoryCount; index++) steps += _world.FindRoadRoute(_routeRequest).Steps.Count;
        return steps;
    }

    [Benchmark]
    public LogisticsSnapshot Snapshot() => _world.CreateLogisticsSnapshot();
}
