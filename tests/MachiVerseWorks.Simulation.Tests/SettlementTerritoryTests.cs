using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class SettlementTerritoryTests
{
    [TestMethod]
    public void TerritoryIsDerivedDeterministicallyFromCurrentSettlementState()
    {
        var first = CreateWorld(31_301);
        var second = CreateWorld(31_301);
        first.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        second.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        first.InitializeRegionalWorld(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, 4, iterationBudget: 1), out _);
        second.InitializeRegionalWorld(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, 4, iterationBudget: 1), out _);

        first.AdvancePersistentRegionalEvolutionYears(2);
        second.AdvancePersistentRegionalEvolutionYears(2);

        var firstTerritories = first.CreateSettlementTerritorySnapshot();
        var secondTerritories = second.CreateSettlementTerritorySnapshot();
        var evolution = first.CreatePersistentRegionalEvolutionSnapshot();

        Assert.AreEqual(JsonSerializer.Serialize(firstTerritories), JsonSerializer.Serialize(secondTerritories));
        Assert.IsTrue(firstTerritories.Length > 1);
        Assert.IsTrue(firstTerritories.All(static item =>
            item.TerritoryRadiusMeters is >= 250d && item.TerritoryRadiusMeters <= item.InfluenceRadiusMeters));
        foreach (var territory in firstTerritories)
        {
            var settlement = evolution.Settlements.Single(item => item.SettlementId == territory.SettlementId);
            Assert.AreEqual(settlement.Center, territory.Center);
            foreach (var neighborId in territory.NeighborSettlementIds)
            {
                var neighbor = firstTerritories.Single(item => item.SettlementId == neighborId);
                Assert.IsTrue(neighbor.NeighborSettlementIds.Contains(territory.SettlementId));
            }
        }
    }

    private static SimulationWorld CreateWorld(ulong seed) =>
        new(new SimulationConfig(
            tickRate: 2,
            seed: seed,
            worldEnvironment: new WorldEnvironmentConfig(
                seed + 10_000,
                new WorldVector(0.2d, 1d, 0d),
                latitudeDegrees: 43d,
                continentality: 0.54d,
                maritimeInfluence: 0.46d,
                meanAnnualTemperatureCelsius: 10.5d,
                seasonalityCelsius: 20d,
                annualPrecipitationMillimeters: 980d)));

    private static WorldVolume CreateVolume() =>
        new(-1_000_000d, -1_000_000d, -12_000d, 1_000_000d, 1_000_000d, 12_000d);
}
