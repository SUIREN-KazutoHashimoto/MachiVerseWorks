using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class PersistentRegionalEvolutionTests
{
    [TestMethod]
    public void SettlementScaleIsDerivedFromFunctionalState()
    {
        Assert.AreEqual(SettlementScale.Hamlet, PersistentRegionalEvolutionEngine.Classify(100, 20, 0.15, 0.10, 0.15));
        Assert.AreEqual(SettlementScale.Village, PersistentRegionalEvolutionEngine.Classify(2_000, 500, 0.40, 0.35, 0.45));
        Assert.AreEqual(SettlementScale.Town, PersistentRegionalEvolutionEngine.Classify(12_000, 4_000, 0.60, 0.55, 0.65));
        Assert.AreEqual(SettlementScale.City, PersistentRegionalEvolutionEngine.Classify(100_000, 50_000, 0.80, 0.75, 0.85));
        Assert.AreEqual(SettlementScale.Metropolis, PersistentRegionalEvolutionEngine.Classify(500_000, 250_000, 0.90, 0.90, 0.95));
    }

    [TestMethod]
    public void OneHundredTwentyYearsRemainDeterministic()
    {
        var first = CreateWorld(31001);
        var second = CreateWorld(31001);
        first.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        second.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        first.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Standard, settlementCount: 6, iterationBudget: 2));
        second.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Standard, settlementCount: 6, iterationBudget: 2));

        for (var year = 0; year < 120; year++)
        {
            first.Step();
            second.Step();
        }

        var firstSnapshot = first.CreatePersistentRegionalEvolutionSnapshot();
        var secondSnapshot = second.CreatePersistentRegionalEvolutionSnapshot();
        Assert.AreEqual(120, firstSnapshot.CurrentYear);
        Assert.AreEqual(JsonSerializer.Serialize(firstSnapshot), JsonSerializer.Serialize(secondSnapshot));
        Assert.IsTrue(firstSnapshot.Events.Count > 0);
        Assert.IsTrue(firstSnapshot.ServiceCatchments.Count > 0);
        Assert.IsTrue(firstSnapshot.InfrastructureDemands.Count > 0);
    }

    [TestMethod]
    public void OneHundredTwentyYearCycleRecordsDeclineThenRegrowthAcrossSettlements()
    {
        var world = CreateWorld(31005);
        var generated = world.GenerateRegionalGeneration(
            CreateVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 4, iterationBudget: 1));
        var initial = PersistentRegionalEvolutionEngine.Initialize(generated);
        var declined = PersistentRegionalEvolutionEngine.AdvanceYears(
            initial,
            generated,
            60,
            static _ => new RegionalEvolutionDrivers(0d, 0d, 0d, 0d, 0d, 0d));
        var recovered = PersistentRegionalEvolutionEngine.AdvanceYears(
            declined,
            generated,
            60,
            static _ => new RegionalEvolutionDrivers(1d, 1d, 1d, 1d, 1d, 1d));

        Assert.AreEqual(120, recovered.CurrentYear);
        Assert.IsTrue(recovered.Settlements.Count >= 4);
        Assert.IsTrue(recovered.Events.Any(static item => item.Kind == RegionalEvolutionEventKind.Decline && item.Year <= 60));
        Assert.IsTrue(recovered.Events.Any(static item => item.Kind == RegionalEvolutionEventKind.Growth && item.Year > 60));
        Assert.IsTrue(recovered.Settlements.Any(item =>
            item.Population > declined.Settlements.First(before => before.SettlementId == item.SettlementId).Population));
    }

    [TestMethod]
    public void MaterializedWorldEvolutionRemainsDeterministicAndCheckpointSafe()
    {
        var first = CreateWorld(31004);
        var second = CreateWorld(31004);
        first.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        second.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        first.InitializeRegionalWorld(
            CreateVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2, iterationBudget: 1),
            out _);
        second.InitializeRegionalWorld(
            CreateVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2, iterationBudget: 1),
            out _);
        var initialBuildingCount = first.BuildingCount;
        var initialPersonCount = first.PersonCount;

        first.AdvancePersistentRegionalEvolutionYears(12);
        second.AdvancePersistentRegionalEvolutionYears(12);

        var expected = first.CreatePersistentRegionalEvolutionSnapshot();
        var duplicate = second.CreatePersistentRegionalEvolutionSnapshot();
        Assert.AreEqual(12, expected.CurrentYear);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(duplicate));
        Assert.IsTrue(first.RoadNodeCount > 0);
        Assert.IsTrue(first.BuildingCount >= initialBuildingCount);
        Assert.IsTrue(first.PersonCount >= initialPersonCount);
        Assert.IsTrue(expected.Settlements.All(static item => item.Accessibility is >= 0d and <= 1d));

        var restored = SimulationWorld.RestoreCheckpoint(first.CreateCheckpoint());
        var restoredEvolution = restored.CreatePersistentRegionalEvolutionSnapshot();
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(restoredEvolution));
        Assert.AreEqual(first.BuildingCount, restored.BuildingCount);
        Assert.AreEqual(first.PersonCount, restored.PersonCount);
        Assert.AreEqual(first.CompanyCount, restored.CompanyCount);
    }

    [TestMethod]
    public void CheckpointRoundTripPreservesAuthoritativeEvolution()
    {
        var world = CreateWorld(31002);
        world.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Standard, settlementCount: 5, iterationBudget: 2));
        for (var year = 0; year < 25; year++) world.Step();

        var expected = world.CreatePersistentRegionalEvolutionSnapshot();
        var checkpoint = world.CreateCheckpoint();
        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);
        var actual = restored.CreatePersistentRegionalEvolutionSnapshot();

        Assert.IsNotNull(checkpoint.Economy?.RegionalEvolution);
        Assert.IsTrue(restored.HasPersistentRegionalEvolution);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [TestMethod]
    public void DeclineAndDormancyRemainInHistory()
    {
        var world = CreateWorld(31003);
        var generated = world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 4, iterationBudget: 1));
        var initial = PersistentRegionalEvolutionEngine.Initialize(generated);
        var declined = PersistentRegionalEvolutionEngine.AdvanceYears(
            initial,
            generated,
            120,
            static _ => new RegionalEvolutionDrivers(0d, 0d, 0d, 0d, 0d, 0d));

        Assert.IsTrue(declined.Settlements.Any(item => item.Population < initial.Settlements.First(before => before.SettlementId == item.SettlementId).Population));
        Assert.IsTrue(declined.Events.Any(item => item.Kind == RegionalEvolutionEventKind.Decline));
        Assert.IsTrue(declined.Events.Select(item => item.Id.Value).SequenceEqual(declined.Events.Select(item => item.Id.Value).OrderBy(static id => id)));
    }

    [TestMethod]
    public void ClassificationAndEvolutionRulesDoNotDependOnFixedCityType()
    {
        var scaleA = PersistentRegionalEvolutionEngine.Classify(8_000, 3_000, 0.55, 0.45, 0.60);
        var scaleB = PersistentRegionalEvolutionEngine.Classify(8_000, 3_000, 0.55, 0.45, 0.60);
        Assert.AreEqual(scaleA, scaleB);
        Assert.IsTrue(PersistentRegionalEvolutionEngine.ShouldEmerge(500, 120, 0.55, 0.25));
        Assert.IsFalse(PersistentRegionalEvolutionEngine.ShouldEmerge(500, 120, 0.20, 0.25));
    }

    private static SimulationWorld CreateWorld(ulong seed) =>
        new(new SimulationConfig(tickRate: 2, seed: seed, worldEnvironment: CreateConfig(seed + 10_000)));

    private static WorldEnvironmentConfig CreateConfig(ulong worldSeed) => new(
        worldSeed,
        new WorldVector(0.2d, 1d, 0d),
        latitudeDegrees: 43d,
        continentality: 0.54d,
        maritimeInfluence: 0.46d,
        meanAnnualTemperatureCelsius: 10.5d,
        seasonalityCelsius: 20d,
        annualPrecipitationMillimeters: 980d);

    private static WorldVolume CreateVolume() =>
        new(-1_000_000d, -1_000_000d, -12_000d, 1_000_000d, 1_000_000d, 12_000d);
}
