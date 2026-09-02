using System.Text;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class WorldEnvironmentSaveLimitTests
{
    [TestMethod]
    public void WorldEnvironmentCollectionsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        AssertPreScanLimit(
            "\"features\":[{},{}],\"toponyms\":[]",
            new WorldSaveLimits(maximumBytes: 100_000, maximumGeographicFeatureCount: 1),
            "simulation.economy.worldEnvironment.features");
        AssertPreScanLimit(
            "\"features\":[],\"toponyms\":[{},{}]",
            new WorldSaveLimits(maximumBytes: 100_000, maximumNaturalToponymCount: 1),
            "simulation.economy.worldEnvironment.toponyms");
        AssertPreScanLimit(
            "\"features\":[{\"geometry\":[{},{}]}],\"toponyms\":[]",
            new WorldSaveLimits(maximumBytes: 100_000, maximumGeographicFeatureGeometryPointCount: 1),
            "simulation.economy.worldEnvironment.features[].geometry");
    }

    [TestMethod]
    public void ReadOnlyEnvironmentObservationDoesNotChangeSaveBytes()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 29030UL));
        var before = WorldSaveSerializer.Serialize(world);
        var volume = new WorldVolume(-300_000d, -300_000d, -12_000d, 300_000d, 300_000d, 12_000d);

        _ = world.CreateDetailedWorldEnvironmentSnapshot(volume, 4, 4, 48);
        _ = world.QueryTerrainSurface(12_345d, -54_321d);
        _ = world.QueryTerrainSurfaces(12_345d, -54_321d, -12_000d, 12_000d);

        var after = WorldSaveSerializer.Serialize(world);
        CollectionAssert.AreEqual(before, after);
    }

    private static void AssertPreScanLimit(string worldEnvironmentMembers, WorldSaveLimits limits, string path)
    {
        var json = $"{{\"simulation\":{{\"economy\":{{\"worldEnvironment\":{{{worldEnvironmentMembers}}}}}}}}}}";
        var exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(json), limits));
        StringAssert.Contains(exception.Message, path);
        StringAssert.Contains(exception.Message, "before deserialization");
    }
}
