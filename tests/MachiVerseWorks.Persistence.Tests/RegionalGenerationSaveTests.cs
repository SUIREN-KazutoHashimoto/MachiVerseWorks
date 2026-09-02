using System.Text;
using System.Text.Json;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class RegionalGenerationSaveTests
{
    [TestMethod]
    public void SaveV11PreservesRegionalGenerationSnapshot()
    {
        var world = new SimulationWorld(new SimulationConfig(
            tickRate: 2,
            seed: 30_701,
            worldEnvironment: CreateConfig(30_701)));
        var expected = world.GenerateRegionalGeneration(
            CreateVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2, iterationBudget: 1));

        var bytes = WorldSaveSerializer.Serialize(world);
        var json = Encoding.UTF8.GetString(bytes);
        StringAssert.Contains(json, "\"regionalGeneration\"");

        var restored = WorldSaveSerializer.Deserialize(bytes);
        var actual = restored.CreateRegionalGenerationSnapshot();

        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [TestMethod]
    public void RegionalCollectionsAreRejectedBeforeDtoMaterializationAboveLimits()
    {
        AssertBoundary(
            "settlements",
            new WorldSaveLimits(maximumBytes: 100_000, maximumBuildingCount: 1),
            "simulation.economy.regionalGeneration.snapshot.settlements");
        AssertBoundary(
            "growthEvents",
            new WorldSaveLimits(maximumBytes: 100_000, maximumPersonCount: 1),
            "simulation.economy.regionalGeneration.snapshot.growthEvents");
        AssertBoundary(
            "corridors",
            new WorldSaveLimits(maximumBytes: 100_000, maximumRoadSegmentCount: 1),
            "simulation.economy.regionalGeneration.snapshot.corridors");
        AssertBoundary(
            "roadSigns",
            new WorldSaveLimits(maximumBytes: 100_000, maximumRoadAccessPointCount: 1),
            "simulation.economy.regionalGeneration.snapshot.roadSigns");
    }

    [TestMethod]
    public void RegionalCorridorGeometryIsRejectedBeforeDtoMaterializationAboveLimits()
    {
        var json = """
            {
              "formatVersion": 11,
              "simulation": {
                "economy": {
                  "regionalGeneration": {
                    "snapshot": {
                      "corridors": [
                        {
                          "geometry": [{},{},{}]
                        }
                      ]
                    }
                  }
                }
              }
            }
            """;
        var limits = new WorldSaveLimits(
            maximumBytes: 100_000,
            maximumGeographicFeatureGeometryPointCount: 2);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(json), limits));

        StringAssert.Contains(exception.Message, "simulation.economy.regionalGeneration.snapshot.corridors[].geometry");
        StringAssert.Contains(exception.Message, "before deserialization");
    }

    private static void AssertBoundary(string property, WorldSaveLimits limits, string path)
    {
        var atLimit = CreateJson(property, "{}");
        var aboveLimit = CreateJson(property, "{},{}");

        var atLimitException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(atLimit), limits));
        Assert.IsFalse(
            atLimitException.Message.Contains(path, StringComparison.Ordinal),
            $"The configured boundary itself must not be rejected by the nested scanner: {atLimitException.Message}");

        var aboveLimitException = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(aboveLimit), limits));
        StringAssert.Contains(aboveLimitException.Message, path);
        StringAssert.Contains(aboveLimitException.Message, "before deserialization");
    }

    private static string CreateJson(string property, string values) => $$"""
        {
          "formatVersion": 11,
          "simulation": {
            "economy": {
              "regionalGeneration": {
                "snapshot": {
                  "{{property}}": [{{values}}]
                }
              }
            }
          }
        }
        """;

    private static WorldEnvironmentConfig CreateConfig(ulong worldSeed) => new(
        worldSeed,
        new WorldVector(0d, 1d, 0d),
        latitudeDegrees: 43d,
        continentality: 0.55d,
        maritimeInfluence: 0.45d,
        meanAnnualTemperatureCelsius: 10d,
        seasonalityCelsius: 20d,
        annualPrecipitationMillimeters: 950d);

    private static WorldVolume CreateVolume() =>
        new(-600_000d, -600_000d, -12_000d, 600_000d, 600_000d, 12_000d);
}
