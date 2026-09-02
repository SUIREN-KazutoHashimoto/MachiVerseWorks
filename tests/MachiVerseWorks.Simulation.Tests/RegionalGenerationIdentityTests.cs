using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RegionalGenerationIdentityTests
{
    [TestMethod]
    public void DenseRegionalGenerationKeepsSettlementAndOriginIdsUnique()
    {
        var environment = new WorldEnvironmentConfig(
            worldSeed: 30_036UL,
            plateDrift: new WorldVector(0.2d, 1d, 0d),
            latitudeDegrees: 43d,
            continentality: 0.54d,
            maritimeInfluence: 0.46d,
            meanAnnualTemperatureCelsius: 10.5d,
            seasonalityCelsius: 20d,
            annualPrecipitationMillimeters: 980d);
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: environment));
        var volume = new WorldVolume(
            -1_000_000d,
            -1_000_000d,
            -12_000d,
            1_000_000d,
            1_000_000d,
            12_000d);

        var snapshot = world.GenerateRegionalGeneration(
            volume,
            new RegionalGenerationOptions(
                RegionalGenerationQualityPreset.HighQuality,
                settlementCount: 16,
                iterationBudget: 3));

        Assert.AreEqual(
            snapshot.Settlements.Count,
            snapshot.Settlements.Select(static settlement => settlement.Id).Distinct().Count());
        Assert.AreEqual(
            snapshot.Settlements.Count,
            snapshot.Settlements.Select(static settlement => settlement.OriginId).Distinct().Count());
    }
}
