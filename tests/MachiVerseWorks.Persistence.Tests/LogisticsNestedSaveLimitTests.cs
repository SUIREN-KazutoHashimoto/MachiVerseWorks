using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class LogisticsNestedSaveLimitTests
{
    [TestMethod]
    public void LogisticsCollectionsAreRejectedBeforeDtoMaterializationAboveLimits()
    {
        AssertBoundary(
            "commodities",
            new WorldSaveLimits(maximumBytes: 100_000, maximumBuildingCount: 1),
            "simulation.economy.logistics.commodities");
        AssertBoundary(
            "inventories",
            new WorldSaveLimits(maximumBytes: 100_000, maximumBuildingCount: 1),
            "simulation.economy.logistics.inventories");
        AssertBoundary(
            "orders",
            new WorldSaveLimits(maximumBytes: 100_000, maximumPersonCount: 1),
            "simulation.economy.logistics.orders");
        AssertBoundary(
            "shipments",
            new WorldSaveLimits(maximumBytes: 100_000, maximumVehicleCount: 1),
            "simulation.economy.logistics.shipments");
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
              "logistics": {
                "{{property}}": [{{values}}]
              }
            }
          }
        }
        """;
}
