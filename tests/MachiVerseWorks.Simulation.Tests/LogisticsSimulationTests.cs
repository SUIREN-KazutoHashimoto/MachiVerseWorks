using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class LogisticsSimulationTests
{
    [TestMethod]
    public void ProductionReplenishmentCreatesShipmentAndRestocksDestination()
    {
        var world = CreateWorld(out var commodity, out _, out var destination);

        for (var tick = 0; tick < 700; tick++) world.Step();

        Assert.IsTrue(world.TryGetInventorySnapshot(destination, commodity, out var inventory));
        Assert.AreEqual(10d, inventory.Quantity, 1e-9);
        var statistics = world.CreateLogisticsStatistics();
        Assert.AreEqual(1UL, statistics.DeliveredShipmentCount);
        Assert.AreEqual(0, statistics.OpenOrderCount);
        Assert.AreEqual(1, statistics.ShipmentCount);
        var shipment = world.CreateLogisticsSnapshot().Shipments.Single();
        Assert.AreEqual(ShipmentState.Delivered, shipment.State);
        Assert.IsNotNull(shipment.VehicleId);
    }

    [TestMethod]
    public void FreightVehicleSharesRoadTrafficAndCheckpointContinuation()
    {
        var original = CreateWorld(out _, out _, out _);
        for (var tick = 0; tick < 606; tick++) original.Step();
        var shipment = original.CreateLogisticsSnapshot().Shipments.Single();
        Assert.AreEqual(ShipmentState.InTransit, shipment.State);
        Assert.IsNotNull(shipment.VehicleId);
        Assert.IsTrue(original.TryGetVehicleSnapshot(shipment.VehicleId.Value, out _));

        var restored = SimulationWorld.RestoreCheckpoint(original.CreateCheckpoint());
        for (var tick = 0; tick < 100; tick++)
        {
            original.Step();
            restored.Step();
        }

        Assert.AreEqual(original.CreateLogisticsStatistics(), restored.CreateLogisticsStatistics());
        CollectionAssert.AreEqual(
            original.CreateLogisticsSnapshot().Inventories.ToArray(),
            restored.CreateLogisticsSnapshot().Inventories.ToArray());
        CollectionAssert.AreEqual(
            original.CreateLogisticsSnapshot().Shipments.ToArray(),
            restored.CreateLogisticsSnapshot().Shipments.ToArray());
    }

    [TestMethod]
    public void CongestionDelayIsReportedFromFreightVehicleProgress()
    {
        var world = CreateWorld(out _, out _, out _);
        for (var tick = 0; tick < 603; tick++) world.Step();
        var shipment = world.CreateLogisticsSnapshot().Shipments.Single();
        Assert.AreEqual(ShipmentState.InTransit, shipment.State);

        var checkpoint = world.CreateCheckpoint();
        var logistics = checkpoint.Economy!.Logistics!;
        var delayedShipment = logistics.Shipments.Single() with { PlannedDeliveryTick = checkpoint.TickCount };
        world = SimulationWorld.RestoreCheckpoint(checkpoint with
        {
            Economy = checkpoint.Economy with { Logistics = logistics with { Shipments = [delayedShipment] } },
        });
        world.Step();

        Assert.IsTrue(world.CreateLogisticsStatistics().DelayedShipmentCount > 0);
        Assert.IsTrue(world.CreateLogisticsSnapshot().Shipments.Single().DelayTicks > 0);
    }

    private static SimulationWorld CreateWorld(out CommodityId commodity, out EstablishmentId supplier, out EstablishmentId destination)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 22));
        var supplierBuilding = world.CreateBuilding(new WorldVolume(-1, -2, 0, 3, 2, 4), BuildingKind.Industrial);
        var destinationBuilding = world.CreateBuilding(new WorldVolume(17, -2, 0, 21, 2, 4), BuildingKind.Commercial);
        var home = world.CreateBuilding(new WorldVolume(-8, -2, 0, -4, 2, 4), BuildingKind.Residential);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var segment = world.CreateRoadSegment(start, end, RoadKind.Local);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 12d);
        var supplierAccess = world.CreateRoadAccessPoint(segment, 0.05, supplierBuilding, mode: RoadAccessMode.Motor);
        var destinationAccess = world.CreateRoadAccessPoint(segment, 0.95, destinationBuilding, mode: RoadAccessMode.Motor);

        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(household, new PersonDemographics(30, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        var supplierCompany = world.CreateCompany(IndustrySector.Manufacturing, 100_000, 20d);
        supplier = world.CreateEstablishment(supplierCompany, buildingId: supplierBuilding);
        var job = world.CreateJob(supplier, 1, 0);
        world.AssignEmployment(person, job);
        var destinationCompany = world.CreateCompany(IndustrySector.Retail, 100_000, 0d);
        destination = world.CreateEstablishment(destinationCompany, buildingId: destinationBuilding);

        commodity = world.CreateCommodity();
        world.ConfigureInventory(supplier, commodity, supplierAccess, InventoryRole.Supplier, capacity: 100d, initialQuantity: 20d);
        world.ConfigureInventory(destination, commodity, destinationAccess, InventoryRole.Consumer, capacity: 30d, initialQuantity: 0d, reorderPoint: 5d, targetQuantity: 10d, dailyConsumptionUnits: 5d);
        return world;
    }
}
