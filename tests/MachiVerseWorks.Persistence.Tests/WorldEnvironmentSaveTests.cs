using System.Text;
using System.Text.Json.Nodes;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class WorldEnvironmentSaveTests
{
    [TestMethod]
    public void SavePreservesWorldEnvironmentFeaturesToponymsAndDeterministicTerrain()
    {
        var config = new WorldEnvironmentConfig(
            29_024UL,
            new WorldVector(0.2d, 1d, 0d),
            latitudeDegrees: 42d,
            continentality: 0.62d,
            maritimeInfluence: 0.38d,
            meanAnnualTemperatureCelsius: 9d,
            seasonalityCelsius: 23d,
            annualPrecipitationMillimeters: 870d,
            seaLevelMeters: 12d,
            configuredCoastlineDistanceMeters: 18_500d);
        var original = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 29024UL, worldEnvironment: config));
        var volume = new WorldVolume(-350_000d, -350_000d, -12_000d, 350_000d, 350_000d, 12_000d);
        var expectedSnapshot = original.CreateDetailedWorldEnvironmentSnapshot(volume, 4, 4, 48);
        var expectedTerrain = original.QueryTerrainSurface(12_345d, -67_890d);

        var bytes = WorldSaveSerializer.Serialize(original);
        var json = Encoding.UTF8.GetString(bytes);
        StringAssert.Contains(json, "\"worldEnvironment\"");
        StringAssert.Contains(json, "\"features\"");
        StringAssert.Contains(json, "\"toponyms\"");

        var restored = WorldSaveSerializer.Deserialize(bytes);
        var actualSnapshot = restored.CreateDetailedWorldEnvironmentSnapshot(volume, 4, 4, 48);
        var actualTerrain = restored.QueryTerrainSurface(12_345d, -67_890d);

        Assert.AreEqual(config, restored.WorldEnvironment);
        CollectionAssert.AreEqual(expectedSnapshot.Samples.ToArray(), actualSnapshot.Samples.ToArray());
        CollectionAssert.AreEqual(expectedSnapshot.TerrainSamples.ToArray(), actualSnapshot.TerrainSamples.ToArray());
        CollectionAssert.AreEqual(expectedSnapshot.Features.ToArray(), actualSnapshot.Features.ToArray());
        CollectionAssert.AreEqual(expectedSnapshot.Toponyms.ToArray(), actualSnapshot.Toponyms.ToArray());
        Assert.AreEqual(expectedTerrain, actualTerrain);
    }

    [TestMethod]
    public void ExistingEconomySaveWithoutWorldEnvironmentRestoresDefaultEnvironment()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 29025UL));
        var root = JsonNode.Parse(WorldSaveSerializer.Serialize(world))!.AsObject();
        var simulation = root["simulation"]!.AsObject();
        simulation["economy"]!.AsObject().Remove("worldEnvironment");

        var restored = WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(root.ToJsonString()));

        Assert.AreEqual(WorldEnvironmentConfig.CreateDefault(29025UL), restored.WorldEnvironment);
        Assert.IsNotNull(restored.QueryEnvironment(new WorldPoint(0d, 0d, 0d)));
    }
}
