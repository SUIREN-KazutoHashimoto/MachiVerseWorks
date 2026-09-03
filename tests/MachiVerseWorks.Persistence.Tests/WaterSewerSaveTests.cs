using System.Text;
using System.Text.Json.Nodes;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class WaterSewerSaveTests
{
    [TestMethod]
    public void SavePreservesWaterSewerStateAndDeterministicContinuation()
    {
        var original = CreateWorld();
        original.Step();
        original.Step();

        var bytes = WorldSaveSerializer.Serialize(original);
        var json = Encoding.UTF8.GetString(bytes);
        StringAssert.Contains(json, "\"waterSewer\"");
        StringAssert.Contains(json, "\"servicePoints\"");
        var restored = WorldSaveSerializer.Deserialize(bytes);

        Assert.AreEqual(original.CreateWaterSewerStatistics(), restored.CreateWaterSewerStatistics());
        CollectionAssert.AreEqual(original.CreateWaterSewerSnapshot().WaterNodes.ToArray(), restored.CreateWaterSewerSnapshot().WaterNodes.ToArray());
        CollectionAssert.AreEqual(original.CreateWaterSewerSnapshot().WaterPipes.ToArray(), restored.CreateWaterSewerSnapshot().WaterPipes.ToArray());
        CollectionAssert.AreEqual(original.CreateWaterSewerSnapshot().SewerNodes.ToArray(), restored.CreateWaterSewerSnapshot().SewerNodes.ToArray());
        CollectionAssert.AreEqual(original.CreateWaterSewerSnapshot().SewerPipes.ToArray(), restored.CreateWaterSewerSnapshot().SewerPipes.ToArray());
        CollectionAssert.AreEqual(original.CreateWaterSewerSnapshot().ServicePoints.ToArray(), restored.CreateWaterSewerSnapshot().ServicePoints.ToArray());

        for (var tick = 0; tick < 20; tick++)
        {
            original.Step();
            restored.Step();
        }

        Assert.AreEqual(original.CreateWaterSewerStatistics(), restored.CreateWaterSewerStatistics());
        CollectionAssert.AreEqual(original.CreateWaterSewerSnapshot().ServicePoints.ToArray(), restored.CreateWaterSewerSnapshot().ServicePoints.ToArray());
    }

    [TestMethod]
    public void ExistingFormatElevenSaveWithoutWaterSewerRestoresEmptyUtilityState()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2402));
        var root = JsonNode.Parse(WorldSaveSerializer.Serialize(world))!.AsObject();
        var economy = root["simulation"]!["economy"]!.AsObject();
        Assert.IsTrue(economy.Remove("waterSewer"));
        Assert.IsFalse(economy.ContainsKey("waterSewer"));

        var restored = WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(root.ToJsonString()));

        Assert.AreEqual(0, restored.WaterNodeCount);
        Assert.AreEqual(0, restored.WaterPipeCount);
        Assert.AreEqual(0, restored.SewerNodeCount);
        Assert.AreEqual(0, restored.SewerPipeCount);
        Assert.AreEqual(0, restored.WaterSewerServicePointCount);
    }

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 24));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var sourceNode = world.CreateWaterNode(new WorldPoint(-10, 5, 0), WaterNodeKind.Source);
        var serviceNode = world.CreateWaterNode(new WorldPoint(5, 5, 0), WaterNodeKind.Service);
        world.CreateWaterPipe(sourceNode, serviceNode, 20d);
        world.CreateWaterSource(sourceNode, 20d);
        var sewerNode = world.CreateSewerNode(new WorldPoint(5, 5, -2), SewerNodeKind.Service);
        var treatmentNode = world.CreateSewerNode(new WorldPoint(20, 5, -2), SewerNodeKind.Treatment);
        world.CreateSewerPipe(sewerNode, treatmentNode, 20d);
        world.CreateSewageTreatmentPlant(treatmentNode, 20d);
        world.CreateWaterSewerServicePoint(serviceNode, sewerNode, 10d, buildingId: building);
        return world;
    }
}
