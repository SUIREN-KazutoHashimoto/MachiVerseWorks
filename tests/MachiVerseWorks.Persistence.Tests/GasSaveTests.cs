using System.Text;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class GasSaveTests
{
    [TestMethod]
    public void SavePreservesGasStateAndDeterministicContinuation()
    {
        var original = CreateWorld(); original.Step(); original.Step();
        var bytes = WorldSaveSerializer.Serialize(original);
        var json = Encoding.UTF8.GetString(bytes);
        StringAssert.Contains(json, "\"gas\""); StringAssert.Contains(json, "\"servicePoints\"");
        var restored = WorldSaveSerializer.Deserialize(bytes);
        Assert.AreEqual(original.CreateGasStatistics(), restored.CreateGasStatistics());
        CollectionAssert.AreEqual(original.CreateGasSnapshot().Nodes.ToArray(), restored.CreateGasSnapshot().Nodes.ToArray());
        CollectionAssert.AreEqual(original.CreateGasSnapshot().Pipelines.ToArray(), restored.CreateGasSnapshot().Pipelines.ToArray());
        CollectionAssert.AreEqual(original.CreateGasSnapshot().Sources.ToArray(), restored.CreateGasSnapshot().Sources.ToArray());
        CollectionAssert.AreEqual(original.CreateGasSnapshot().Storages.ToArray(), restored.CreateGasSnapshot().Storages.ToArray());
        CollectionAssert.AreEqual(original.CreateGasSnapshot().ServicePoints.ToArray(), restored.CreateGasSnapshot().ServicePoints.ToArray());
        for (var tick = 0; tick < 20; tick++) { original.Step(); restored.Step(); }
        Assert.AreEqual(original.CreateGasStatistics(), restored.CreateGasStatistics());
        CollectionAssert.AreEqual(original.CreateGasSnapshot().ServicePoints.ToArray(), restored.CreateGasSnapshot().ServicePoints.ToArray());
    }

    [TestMethod]
    public void ExistingFormatElevenSaveWithoutGasRestoresEmptyGasState()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2502));
        var json = Encoding.UTF8.GetString(WorldSaveSerializer.Serialize(world));
        json = json.Replace(",\n      \"gas\": null", string.Empty, StringComparison.Ordinal);
        var restored = WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(json));
        Assert.AreEqual(0, restored.GasNodeCount); Assert.AreEqual(0, restored.GasPipelineCount); Assert.AreEqual(0, restored.GasServicePointCount);
    }

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 25));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var sourceNode = world.CreateGasNode(new WorldPoint(-10, 5, 0), GasNodeKind.Source);
        var serviceNode = world.CreateGasNode(new WorldPoint(5, 5, 0), GasNodeKind.Service);
        world.CreateGasPipeline(sourceNode, serviceNode, 20d); world.CreateGasSource(sourceNode, 20d); world.CreateGasStorage(sourceNode, 100d, 30d, 5d); world.CreatePipedGasServicePoint(serviceNode, 10d, buildingId: building);
        return world;
    }
}
