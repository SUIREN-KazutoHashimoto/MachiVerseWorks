using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RegionalGenerationTests
{
    [TestMethod]
    public void SameSeedVolumeAndPresetProduceSameRegionalWorld()
    {
        var config = CreateConfig(30001);
        var firstWorld = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 101, worldEnvironment: config));
        var secondWorld = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 101, worldEnvironment: config));
        var volume = CreateVolume();

        var first = firstWorld.GenerateRegionalGeneration(volume, new RegionalGenerationOptions(RegionalGenerationQualityPreset.Standard));
        var second = secondWorld.GenerateRegionalGeneration(volume, new RegionalGenerationOptions(RegionalGenerationQualityPreset.Standard));

        Assert.AreEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [TestMethod]
    public void StandardGenerationCreatesPolycentricHistoricalUrbanFabric()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(30002)));

        var snapshot = world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Standard));

        Assert.IsTrue(snapshot.Settlements.Count >= 2);
        Assert.IsTrue(snapshot.Settlements.All(static item => item.Id.Value > 0UL));
        Assert.IsTrue(snapshot.Settlements.All(static item => item.Population > 0 && item.Jobs > 0));
        Assert.IsTrue(snapshot.Settlements.All(settlement => snapshot.GrowthEvents.Any(item => item.SettlementId == settlement.Id && item.Stage == HistoricalGrowthStage.Origin)));
        Assert.IsTrue(snapshot.Settlements.All(settlement => snapshot.GrowthEvents.Any(item => item.SettlementId == settlement.Id && item.Stage == HistoricalGrowthStage.UrbanExpansion)));
        Assert.IsTrue(snapshot.Corridors.Count >= snapshot.Settlements.Count - 1);
        Assert.IsTrue(snapshot.Districts.Count >= snapshot.Settlements.Count * 2);
        Assert.IsTrue(snapshot.Parcels.Count >= snapshot.Districts.Count * 4);
        Assert.IsTrue(snapshot.Buildings.Count > 0);
        Assert.IsTrue(snapshot.Pois.Count >= snapshot.Settlements.Count);
        Assert.IsTrue(snapshot.Toponyms.Count >= snapshot.Settlements.Count + snapshot.Districts.Count);
        Assert.IsTrue(snapshot.RoadSigns.Count > 0);
        Assert.AreEqual(1d, snapshot.Quality.RoadConnectivity, 1e-9);
        Assert.IsTrue(snapshot.Quality.PolycentricBalance is > 0d and <= 1d);
        Assert.IsTrue(snapshot.Quality.OverallScore is >= 0d and <= 1d);
    }

    [TestMethod]
    public void HumanSettlementNamesRetainNaturalNameProvenance()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(30003)));
        var snapshot = world.GenerateRegionalGeneration(CreateVolume());
        var names = snapshot.Toponyms.ToDictionary(static item => item.Id);

        foreach (var settlement in snapshot.Settlements)
        {
            Assert.IsTrue(names.TryGetValue(settlement.NameId, out var name));
            Assert.IsNotNull(name);
            Assert.AreEqual(HumanToponymKind.Settlement, name.Kind);
            Assert.AreEqual("phase30-regional-v1", name.Provenance.GeneratorKey);
            if (name.Provenance.SourceNaturalToponym is { } natural)
            {
                Assert.AreEqual(natural.FeatureId, name.Provenance.SourceFeatureId);
                Assert.AreEqual("phase29-natural-v1", natural.Provenance.GeneratorKey);
            }
        }
    }

    [TestMethod]
    public void CheckpointRoundTripPreservesRegionalGeneration()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 55, worldEnvironment: CreateConfig(30004)));
        world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.HighQuality, settlementCount: 6, iterationBudget: 2));
        world.Step();
        var expected = world.CreateRegionalGenerationSnapshot();
        var checkpoint = world.CreateCheckpoint();

        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);
        var actual = restored.CreateRegionalGenerationSnapshot();
        var restoredCheckpoint = restored.CreateCheckpoint();

        Assert.IsTrue(restored.HasRegionalGeneration);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.IsNotNull(checkpoint.Economy?.RegionalGeneration);
        Assert.IsNotNull(restoredCheckpoint.Economy?.RegionalGeneration);
    }

    [TestMethod]
    public void ObservationSnapshotIsDetachedFromAuthoritativeRegionalState()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(30005)));
        var generated = world.GenerateRegionalGeneration(CreateVolume());
        var observed = world.CreateRegionalGenerationSnapshot();
        var observedArray = observed.Settlements as Settlement[];

        Assert.IsNotNull(observedArray);
        var original = generated.Settlements[0];
        observedArray[0] = original with { Population = original.Population + 999_999 };

        var after = world.CreateRegionalGenerationSnapshot();
        Assert.AreEqual(original.Population, after.Settlements.First(item => item.Id == original.Id).Population);
    }

    [TestMethod]
    public void QualityPresetsResolveIncreasingGenerationBudgets()
    {
        var draft = new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft);
        var standard = new RegionalGenerationOptions(RegionalGenerationQualityPreset.Standard);
        var high = new RegionalGenerationOptions(RegionalGenerationQualityPreset.HighQuality);

        Assert.IsTrue(draft.ResolveSettlementCount() < standard.ResolveSettlementCount());
        Assert.IsTrue(standard.ResolveSettlementCount() < high.ResolveSettlementCount());
        Assert.IsTrue(draft.ResolveIterationBudget() < standard.ResolveIterationBudget());
        Assert.IsTrue(standard.ResolveIterationBudget() < high.ResolveIterationBudget());
    }

    [TestMethod]
    public void RegionalGenerationCanOnlyInitializeAuthoritativeWorldOnce()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(30006)));
        world.GenerateRegionalGeneration(CreateVolume());

        Assert.ThrowsException<InvalidOperationException>(() => world.GenerateRegionalGeneration(CreateVolume()));
    }

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
