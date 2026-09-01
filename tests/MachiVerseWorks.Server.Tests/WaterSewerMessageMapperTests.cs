using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class WaterSewerMessageMapperTests
{
    [TestMethod]
    public void MapperPreservesTopologyFacilitiesAndServiceState()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 24));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var source = world.CreateWaterNode(new WorldPoint(-10, 0, 0), WaterNodeKind.Source);
        var service = world.CreateWaterNode(new WorldPoint(0, 0, 0), WaterNodeKind.Service);
        world.CreateWaterPipe(source, service, 100d);
        world.CreateWaterSource(source, 100d);
        var sewerService = world.CreateSewerNode(new WorldPoint(0, 0, -2), SewerNodeKind.Service);
        var treatment = world.CreateSewerNode(new WorldPoint(10, 0, -2), SewerNodeKind.Treatment);
        world.CreateSewerPipe(sewerService, treatment, 100d);
        world.CreateSewageTreatmentPlant(treatment, 100d);
        world.CreateWaterSewerServicePoint(service, sewerService, 10d, buildingId: building);
        world.Step();

        var message = WaterSewerMessageMapper.Create(world.CreateWaterSewerSnapshot());

        Assert.AreEqual((uint)2, message.Statistics.WaterNodeCount);
        Assert.AreEqual((uint)2, message.Statistics.SewerNodeCount);
        Assert.HasCount(4, message.Nodes);
        Assert.HasCount(2, message.Pipes);
        Assert.HasCount(2, message.Facilities);
        Assert.HasCount(1, message.ServicePoints);
        Assert.AreEqual(ProtocolWaterServiceState.Supplied, message.ServicePoints[0].WaterState);
        Assert.AreEqual(ProtocolSewerServiceState.Available, message.ServicePoints[0].SewerState);
    }
}
