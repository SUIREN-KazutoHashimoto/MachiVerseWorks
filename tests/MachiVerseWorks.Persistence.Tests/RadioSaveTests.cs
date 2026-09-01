using System.Text;
using System.Text.Json.Nodes;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class RadioSaveTests
{
    [TestMethod]
    public void SavePreservesRadioEntitiesAndDeterministicContinuation()
    {
        var original = CreateWorld();
        original.Step(); original.Step();
        var bytes = WorldSaveSerializer.Serialize(original);
        var json = Encoding.UTF8.GetString(bytes);
        StringAssert.Contains(json, "\"radio\"");
        StringAssert.Contains(json, "\"antennas\"");
        StringAssert.Contains(json, "\"emissions\"");

        var restored = WorldSaveSerializer.Deserialize(bytes);
        var expected = original.CreateRadioSnapshot();
        var actual = restored.CreateRadioSnapshot();
        Assert.AreEqual(expected.Statistics, actual.Statistics);
        CollectionAssert.AreEqual(expected.Antennas!.ToArray(), actual.Antennas!.ToArray());
        CollectionAssert.AreEqual(expected.Transmitters!.ToArray(), actual.Transmitters!.ToArray());
        CollectionAssert.AreEqual(expected.Receivers!.ToArray(), actual.Receivers!.ToArray());
        CollectionAssert.AreEqual(expected.Emissions!.ToArray(), actual.Emissions!.ToArray());
        CollectionAssert.AreEqual(expected.Links.ToArray(), actual.Links.ToArray());

        for (var tick = 0; tick < 20; tick++) { original.Step(); restored.Step(); }
        CollectionAssert.AreEqual(original.CreateRadioSnapshot().Links.ToArray(), restored.CreateRadioSnapshot().Links.ToArray());
    }

    [TestMethod]
    public void ExistingEconomySaveWithoutRadioRestoresEmptyRadioState()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2802));
        var root = JsonNode.Parse(WorldSaveSerializer.Serialize(world))!.AsObject();
        var simulation = root["simulation"]!.AsObject();
        simulation["economy"]!.AsObject().Remove("radio");
        var restored = WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(root.ToJsonString()));
        Assert.AreEqual(0, restored.RadioSiteCount);
        Assert.AreEqual(0, restored.RadioAntennaCount);
        Assert.AreEqual(0, restored.RadioEmissionCount);
        Assert.AreEqual(0, restored.RadioLinkCount);
    }

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 28));
        world.CreateBuilding(new WorldVolume(200d, -25d, 0d, 250d, 25d, 50d), BuildingKind.Commercial);
        var band = world.CreateSpectrumBand("save-radio", 2_400d, 2_500d);
        var channel = world.CreateRadioChannel(band, 2_450d, 20d);
        var source = world.CreateRadioSite(new WorldPoint(0d, 0d, 0d), RadioSiteKind.PointToPoint);
        var target = world.CreateRadioSite(new WorldPoint(500d, 0d, 0d), RadioSiteKind.PointToPoint);
        var sourceAntenna = world.CreateRadioAntenna(source, new WorldVector(0d, 0d, 20d), new WorldVector(1d, 0d, 0d), 12d, RadioAntennaPatternKind.Directional, 90d, 20d);
        var targetAntenna = world.CreateRadioAntenna(target, new WorldVector(0d, 0d, 20d), new WorldVector(-1d, 0d, 0d), 8d, RadioAntennaPatternKind.Directional, 90d, 20d);
        var transmitter = world.CreateRadioTransmitter(source, sourceAntenna, 40d);
        var receiver = world.CreateRadioReceiver(target, targetAntenna, 2_400d, 2_500d, -105d);
        var emission = world.CreateRadioEmission(transmitter, channel, 36d, 0.45d);
        world.CreateRadioLink(emission, receiver);
        return world;
    }
}
