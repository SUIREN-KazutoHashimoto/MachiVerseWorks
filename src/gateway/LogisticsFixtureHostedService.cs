using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class LogisticsFixtureHostedService(
    SimulationRuntime simulation,
    IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!bool.TryParse(configuration["Simulation:LogisticsFixture"], out var enabled) || !enabled)
            return Task.CompletedTask;

        simulation.Mutate(static world =>
        {
            Seed(world);
            return true;
        }, roadTopologyChanged: true);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void Seed(SimulationWorld world)
    {
        var supplierBuilding = world.CreateBuilding(new WorldVolume(-1d, -3d, 0d, 3d, 3d, 4d), BuildingKind.Industrial);
        var destinationBuilding = world.CreateBuilding(new WorldVolume(17d, -3d, 0d, 21d, 3d, 4d), BuildingKind.Commercial);
        var home = world.CreateBuilding(new WorldVolume(-8d, -3d, 0d, -4d, 3d, 4d), BuildingKind.Residential);
        var start = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var end = world.CreateRoadNode(new WorldPoint(20d, 0d, 0d));
        var segment = world.CreateRoadSegment(start, end, RoadKind.Local);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 12d);
        var supplierAccess = world.CreateRoadAccessPoint(segment, 0.05d, supplierBuilding, mode: RoadAccessMode.Motor);
        var destinationAccess = world.CreateRoadAccessPoint(segment, 0.95d, destinationBuilding, mode: RoadAccessMode.Motor);

        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(household, new PersonDemographics(32, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        var supplierCompany = world.CreateCompany(IndustrySector.Manufacturing, 100_000, 20d);
        var supplier = world.CreateEstablishment(supplierCompany, buildingId: supplierBuilding);
        var job = world.CreateJob(supplier, 1, 0);
        world.AssignEmployment(person, job);
        var destinationCompany = world.CreateCompany(IndustrySector.Retail, 100_000, 0d);
        var destination = world.CreateEstablishment(destinationCompany, buildingId: destinationBuilding);
        var commodity = world.CreateCommodity(CommodityKind.GeneralGoods);
        world.ConfigureInventory(supplier, commodity, supplierAccess, InventoryRole.Supplier, capacity: 100d, initialQuantity: 20d);
        world.ConfigureInventory(destination, commodity, destinationAccess, InventoryRole.Consumer, capacity: 30d, initialQuantity: 0d, reorderPoint: 5d, targetQuantity: 10d, dailyConsumptionUnits: 5d);
    }
}
