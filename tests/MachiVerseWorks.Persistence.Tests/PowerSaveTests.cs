using System.Text;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class PowerSaveTests
{
    [TestMethod]
    public void SavePreservesPowerStateAndDeterministicContinuation()
    {
        var original = CreateWorld();
        original.Step();
        original.Step();

        var bytes = WorldSaveSerializer.Serialize(original);
        var json = Encoding.UTF8.GetString(bytes);
        StringAssert.Contains(json, "\"power\"");
        StringAssert.Contains(json, "\"loads\"");
        var restored = WorldSaveSerializer.Deserialize(bytes);

        Assert.AreEqual(original.CreatePowerStatistics(), restored.CreatePowerStatistics());
        CollectionAssert.AreEqual(original.CreatePowerSnapshot().Nodes.ToArray(), restored.CreatePowerSnapshot().Nodes.ToArray());
        CollectionAssert.AreEqual(original.CreatePowerSnapshot().Lines.ToArray(), restored.CreatePowerSnapshot().Lines.ToArray());
        CollectionAssert.AreEqual(original.CreatePowerSnapshot().Generators.ToArray(), restored.CreatePowerSnapshot().Generators.ToArray());
        CollectionAssert.AreEqual(original.CreatePowerSnapshot().Loads.ToArray(), restored.CreatePowerSnapshot().Loads.ToArray());

        for (var tick = 0; tick < 20; tick++)
        {
            original.Step();
            restored.Step();
        }

        Assert.AreEqual(original.CreatePowerStatistics(), restored.CreatePowerStatistics());
        CollectionAssert.AreEqual(original.CreatePowerSnapshot().Loads.ToArray(), restored.CreatePowerSnapshot().Loads.ToArray());
    }

    [TestMethod]
    public void ExistingFormatElevenSaveWithoutPowerRestoresEmptyPowerState()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2302));
        var bytes = WorldSaveSerializer.Serialize(world);
        var json = Encoding.UTF8.GetString(bytes);
        json = json.Replace("      \"power\": null,\n", string.Empty, StringComparison.Ordinal);

        var restored = WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.AreEqual(0, restored.PowerNodeCount);
        Assert.AreEqual(0, restored.PowerLineCount);
        Assert.AreEqual(0, restored.GeneratorCount);
        Assert.AreEqual(0, restored.PowerLoadCount);
    }

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 23));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var generatorNode = world.CreatePowerNode(new WorldPoint(-10, 5, 0), PowerNodeKind.GeneratorBus);
        var substation = world.CreatePowerNode(new WorldPoint(-2, 5, 0), PowerNodeKind.Substation);
        var loadNode = world.CreatePowerNode(new WorldPoint(5, 5, 0), PowerNodeKind.Load);
        world.CreatePowerLine(generatorNode, substation, 15d);
        world.CreatePowerLine(substation, loadNode, 8d);
        world.CreateGenerator(generatorNode, 20d);
        world.CreatePowerLoad(loadNode, 10d, buildingId: building);
        return world;
    }
}
