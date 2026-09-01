using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class GasFixtureHostedService(SimulationRuntime simulation, IConfiguration configuration) : BackgroundService
{
    private GasPipelineId _pipelineId;
    private bool _enabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _enabled = bool.TryParse(configuration["Simulation:GasFixture"], out var enabled) && enabled;
        if (!_enabled) return;
        simulation.Mutate(world => { Seed(world); return true; });
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var tick = simulation.TickCount;
                simulation.Mutate(world => { world.SetGasPipelineInService(_pipelineId, tick % 100UL is < 60UL or >= 80UL); return true; });
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private void Seed(SimulationWorld world)
    {
        var supplierBuilding = world.CreateBuilding(new WorldVolume(0d, 20d, 0d, 10d, 30d, 8d), BuildingKind.Industrial);
        var consumerBuilding = world.CreateBuilding(new WorldVolume(395d, 20d, 0d, 405d, 30d, 8d), BuildingKind.Commercial);
        var sourceNode = world.CreateGasNode(new WorldPoint(5d, 15d, 0d), GasNodeKind.Source);
        var regulatorNode = world.CreateGasNode(new WorldPoint(200d, 20d, 0d), GasNodeKind.Regulator);
        var serviceNode = world.CreateGasNode(new WorldPoint(400d, 25d, 0d), GasNodeKind.Service);
        world.CreateGasPipeline(sourceNode, regulatorNode, 30d);
        _pipelineId = world.CreateGasPipeline(regulatorNode, serviceNode, 30d);
        world.CreateGasSource(sourceNode, 30d);
        world.CreateGasStorage(sourceNode, 100d, 40d, 10d);
        world.CreatePipedGasServicePoint(serviceNode, 12d, buildingId: consumerBuilding);

        var roadStart = world.CreateRoadNode(new WorldPoint(5d, 35d, 0d));
        var roadEnd = world.CreateRoadNode(new WorldPoint(400d, 35d, 0d));
        var segment = world.CreateRoadSegment(roadStart, roadEnd, RoadKind.Local);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 12d);
        var supplierAccess = world.CreateRoadAccessPoint(segment, 0.02d, supplierBuilding, mode: RoadAccessMode.Motor);
        var consumerAccess = world.CreateRoadAccessPoint(segment, 0.98d, consumerBuilding, mode: RoadAccessMode.Motor);
        var supplierCompany = world.CreateCompany(IndustrySector.Transport, 100_000, 0d);
        var consumerCompany = world.CreateCompany(IndustrySector.Services, 100_000, 0d);
        var supplier = world.CreateEstablishment(supplierCompany, buildingId: supplierBuilding);
        var consumer = world.CreateEstablishment(consumerCompany, buildingId: consumerBuilding);
        var gas = world.CreateCommodity(CommodityKind.Gas);
        world.ConfigureInventory(supplier, gas, supplierAccess, InventoryRole.Supplier, 200d, initialQuantity: 120d);
        world.ConfigureInventory(consumer, gas, consumerAccess, InventoryRole.Consumer, 40d, initialQuantity: 20d, reorderPoint: 10d, targetQuantity: 30d, dailyConsumptionUnits: 8d);
        world.CreateDeliveredGasServicePoint(consumer, gas, 8d, consumerBuilding);
    }
}
