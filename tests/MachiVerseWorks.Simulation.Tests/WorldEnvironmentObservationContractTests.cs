using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class WorldEnvironmentObservationContractTests
{
    [TestMethod]
    public void GeneratedFeatureToponymCanBeProjectedWithoutMutatingWorldState()
    {
        var config = new WorldEnvironmentConfig(
            29010,
            new WorldVector(0d, 1d, 0d),
            latitudeDegrees: 44d,
            continentality: 0.58d,
            maritimeInfluence: 0.42d,
            meanAnnualTemperatureCelsius: 10d,
            seasonalityCelsius: 21d,
            annualPrecipitationMillimeters: 950d);
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: config));
        var volume = new WorldVolume(-500_000d, -500_000d, -10_000d, 500_000d, 500_000d, 10_000d);
        var feature = world.GetGeographicFeatures(volume, 64)[0];
        var before = world.CreateCheckpoint();

        var first = world.CreateNaturalToponym(feature);
        var second = world.CreateNaturalToponym(feature);
        var after = world.CreateCheckpoint();

        Assert.AreEqual(first, second);
        Assert.AreEqual(feature.Id, first.FeatureId);
        Assert.AreEqual(feature.Id, first.Provenance.SourceFeatureId);
        Assert.AreEqual("phase29-natural-v1", first.Provenance.GeneratorKey);
        Assert.IsFalse(world.TryGetNaturalToponym(feature.Id, out _));
        CollectionAssert.AreEqual(before.Economy!.WorldEnvironment!.Features.ToArray(), after.Economy!.WorldEnvironment!.Features.ToArray());
        CollectionAssert.AreEqual(before.Economy.WorldEnvironment.Toponyms.ToArray(), after.Economy.WorldEnvironment.Toponyms.ToArray());
    }
}
