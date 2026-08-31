using System.Text;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class LogisticsSaveTests
{
    [TestMethod]
    public void SavePreservesLogisticsAndDeterministicContinuation()
    {
        var original = CreateWorld();
        for (var tick = 0; tick < 603; tick++) original.Step();

        var bytes = WorldSaveSerializer.Serialize(original);
        StringAssert.Contains(Encoding.UTF8.GetString(bytes), "\"logistics\"");
        var restored = WorldSaveSerializer.Deserialize(bytes);

        Assert.AreEqual(original.CreateLogisticsStatistics(), restored.CreateLogisticsStatistics());
        CollectionAssert.AreEqual(
            original.CreateLogisticsSnapshot().Inventories.ToArray(),
            restored.CreateLogisticsSnapshot().Inventories.ToArray());
        CollectionAssert.AreEqual(
            original.CreateLogisticsSnapshot().Shipments.ToArray(),
            restored.CreateLogisticsSnapshot().Shipments.ToArray());

        for (var tick = 0; tick < 100; tick++)
        {
            original.Step();
            restored.Step();
        }

        Assert.AreEqual(original.CreateLogisticsStatistics(), restored.CreateLogisticsStatistics());
        CollectionAssert.AreEqual(
            original.CreateLogisticsSnapshot().Shipments.ToArray(),
            restored.CreateLogisticsSnapshot().Shipments.ToArray());
    }

    private static SimulationWorld CreateWorld()
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
        var supplier = world.CreateEstablishment(supplierCompany, buildingId: supplierBuilding);
        var job = world.CreateJob(supplier, 1, 0);
        world.AssignEmployment(person, job);
        var destinationCompany = world.CreateCompany(IndustrySector.Retail, 100_000, 0d);
        var destination = world.CreateEstablishment(destinationCompany, buildingId: destinationBuilding);
        var commodity = world.CreateCommodity();
        world.ConfigureInventory(supplier, commodity, supplierAccess, InventoryRole.Supplier, capacity: 100d, initialQuantity: 20d);
        world.ConfigureInventory(destination, commodity, destinationAccess, InventoryRole.Consumer, capacity: 30d, initialQuantity: 0d, reorderPoint: 5d, targetQuantity: 10d, dailyConsumptionUnits: 5d);
        return world;
    }
}
