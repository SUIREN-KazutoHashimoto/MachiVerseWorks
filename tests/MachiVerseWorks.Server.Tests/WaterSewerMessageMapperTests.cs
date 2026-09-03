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

    [TestMethod]
    public void MapperBudgetsServicePointNodePairsTogether()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2401));
        var waterNodes = Enumerable.Range(0, 512)
            .Select(index => world.CreateWaterNode(new WorldPoint(index, 0, 0), WaterNodeKind.Service))
            .ToArray();
        var sewerNodes = Enumerable.Range(0, 512)
            .Select(index => world.CreateSewerNode(new WorldPoint(index, 0, -2), SewerNodeKind.Service))
            .ToArray();
        var buildings = Enumerable.Range(0, 512)
            .Select(index => world.CreateBuilding(new WorldVolume(index * 2, 10, 0, index * 2 + 1, 11, 1), BuildingKind.Residential))
            .ToArray();
        for (var index = 0; index < 512; index++)
            world.CreateWaterSewerServicePoint(waterNodes[index], sewerNodes[511 - index], 1d, buildingId: buildings[index]);
        world.Step();

        var message = WaterSewerMessageMapper.Create(world.CreateWaterSewerSnapshot());
        var waterIds = message.Nodes.Where(static node => node.NetworkKind == ProtocolUtilityNetworkKind.Water).Select(static node => node.NodeId).ToHashSet();
        var sewerIds = message.Nodes.Where(static node => node.NetworkKind == ProtocolUtilityNetworkKind.Sewer).Select(static node => node.NodeId).ToHashSet();

        Assert.HasCount(512, message.Nodes);
        Assert.HasCount(256, message.ServicePoints);
        Assert.IsTrue(message.ServicePoints.All(item => waterIds.Contains(item.WaterNodeId) && sewerIds.Contains(item.SewerNodeId)));
    }
}
