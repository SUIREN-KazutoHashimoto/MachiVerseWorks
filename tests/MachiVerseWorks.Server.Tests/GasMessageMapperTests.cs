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

        var message = GasMessageMapper.Create(world.CreateGasSnapshot());

        Assert.AreEqual((uint)2, message.Statistics.NodeCount);
        Assert.AreEqual((uint)1, message.Statistics.PipelineCount);
        Assert.HasCount(2, message.Nodes);
        Assert.HasCount(1, message.Pipelines);
        Assert.HasCount(2, message.Facilities);
        Assert.HasCount(1, message.ServicePoints);
        Assert.AreEqual(ProtocolGasDeliveryMode.Piped, message.ServicePoints[0].DeliveryMode);
        Assert.AreEqual(ProtocolGasServiceState.Supplied, message.ServicePoints[0].ServiceState);
    }
}
