using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class GasMessageMapperTests
{
    [TestMethod]
    public void MapperPreservesTopologyFacilitiesAndServiceState()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 25));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var source = world.CreateGasNode(new WorldPoint(-10, 0, 0), GasNodeKind.Source);
        var service = world.CreateGasNode(new WorldPoint(0, 0, 0), GasNodeKind.Service);
        world.CreateGasPipeline(source, service, 100d);
        world.CreateGasSource(source, 100d);
        world.CreateGasStorage(source, 100d, 50d, 20d);
        world.CreatePipedGasServicePoint(service, 10d, buildingId: building);
        world.Step();

        var message = GasMessageMapper.Create(world.CreateGasSnapshot(), world.CreateLogisticsSnapshot());

        Assert.AreEqual((uint)2, message.Statistics.NodeCount);
        Assert.AreEqual((uint)1, message.Statistics.PipelineCount);
        Assert.HasCount(2, message.Nodes);
        Assert.HasCount(1, message.Pipelines);
        Assert.HasCount(2, message.Facilities);
        Assert.HasCount(1, message.ServicePoints);
        Assert.AreEqual(ProtocolGasDeliveryMode.Piped, message.ServicePoints[0].DeliveryMode);
        Assert.AreEqual(ProtocolGasServiceState.Supplied, message.ServicePoints[0].ServiceState);
        Assert.AreEqual(0d, message.ServicePoints[0].DeliveredInventoryCubicMeters);
        Assert.AreEqual((uint)0, message.ServicePoints[0].ActiveShipmentCount);
    }

    [TestMethod]
    public void MapperJoinsDeliveredGasInventoryFromLogistics()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2502));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0, 20, 0));
        var end = world.CreateRoadNode(new WorldPoint(20, 20, 0));
        var segment = world.CreateRoadSegment(start, end, RoadKind.Local);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 12d);
        var access = world.CreateRoadAccessPoint(segment, 0.5d, building, mode: RoadAccessMode.Motor);
        var company = world.CreateCompany(IndustrySector.Services, 100_000, 0d);
        var establishment = world.CreateEstablishment(company, buildingId: building);
        var gas = world.CreateCommodity(CommodityKind.Gas);
        world.ConfigureInventory(establishment, gas, access, InventoryRole.Consumer, capacity: 40d, initialQuantity: 12d, reorderPoint: 5d, targetQuantity: 20d, dailyConsumptionUnits: 0d);
        world.CreateDeliveredGasServicePoint(establishment, gas, 8d, building);
        world.Step();

        var message = GasMessageMapper.Create(world.CreateGasSnapshot(), world.CreateLogisticsSnapshot());
        var delivered = message.ServicePoints.Single();

        Assert.AreEqual(ProtocolGasDeliveryMode.Delivered, delivered.DeliveryMode);
        Assert.AreEqual(12d, delivered.DeliveredInventoryCubicMeters, 1e-9);
        Assert.AreEqual(40d, delivered.DeliveredInventoryCapacityCubicMeters, 1e-9);
        Assert.AreEqual(0d, delivered.ActiveShipmentCubicMeters, 1e-9);
        Assert.AreEqual((uint)0, delivered.ActiveShipmentCount);
    }
}
