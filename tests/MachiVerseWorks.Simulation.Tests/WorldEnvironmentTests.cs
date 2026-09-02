using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class WorldEnvironmentTests
{
    [TestMethod]
    public void GlobalEnvironmentIsDeterministicForSameSeedAndConfig()
    {
        var config = CreateConfig(worldSeed: 29001);
        var first = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 17, worldEnvironment: config));
        var second = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 17, worldEnvironment: config));
        var point = new WorldPoint(128_500d, -42_250d, 0d);

        var firstSample = first.QueryEnvironment(point);
        var secondSample = second.QueryEnvironment(point);

        Assert.AreEqual(firstSample, secondSample);
        Assert.IsTrue(double.IsFinite(firstSample.ElevationMeters));
        Assert.IsTrue(firstSample.Buildability is >= 0d and <= 1d);
        Assert.IsTrue(firstSample.SettlementScore is >= 0d and <= 1d);
    }

    [TestMethod]
    public void ConfiguredCoastlineDistanceHasPriorityOverGeneratedEstimate()
    {
        var config = new WorldEnvironmentConfig(
            29002,
            new WorldVector(1d, 1d, 0d),
            latitudeDegrees: -32d,
            continentality: 0.4d,
            maritimeInfluence: 0.7d,
            meanAnnualTemperatureCelsius: 16d,
            seasonalityCelsius: 12d,
            annualPrecipitationMillimeters: 1_100d,
            configuredCoastlineDistanceMeters: 12_345d);
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: config));

        var sample = world.QueryEnvironment(new WorldPoint(100_000d, 200_000d, 0d));

        Assert.AreEqual(12_345d, sample.CoastlineDistanceMeters, 0d);
        Assert.AreEqual(WorldHemisphere.Southern, world.WorldEnvironment.Hemisphere);
    }

    [TestMethod]
    public void SettlementCandidateSelectionIsDeterministicAndBounded()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(29003)));
        var volume = new WorldVolume(-1_000_000d, -1_000_000d, -10_000d, 1_000_000d, 1_000_000d, 10_000d);

        var first = world.SelectSettlementCandidates(volume, 12).ToArray();
        var second = world.SelectSettlementCandidates(volume, 12).ToArray();

        CollectionAssert.AreEqual(first, second);
        Assert.IsTrue(first.Length <= 12);
        Assert.IsTrue(first.Length > 0);
        Assert.IsTrue(first.All(static item => item.TotalScore is >= 0d and <= 1d));
    }

    [TestMethod]
    public void DetailedTerrainSupportsSurfaceVolumeAndGroundSnap()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(29004)));
        var surface = world.QueryTerrainSurface(16_384d, 0d);
        var snapped = world.SnapToGround(new WorldPoint(16_384d, 0d, surface.Position.Z + 500d));
        var below = world.QueryTerrainVolume(new WorldPoint(surface.Position.X, surface.Position.Y, surface.Position.Z - 1d));
        var surfaces = world.QueryTerrainSurfaces(surface.Position.X, surface.Position.Y, surface.Position.Z - 500d, surface.Position.Z + 500d);

        Assert.AreEqual(surface.Position.Z, snapped.Z, 1e-9);
        Assert.IsTrue(double.IsFinite(surface.SlopeDegrees));
        Assert.IsTrue(surface.Normal.Z > 0d);
        Assert.IsTrue(below.Matter is TerrainMatterKind.Soil or TerrainMatterKind.Rock or TerrainMatterKind.Void);
        Assert.IsTrue(surfaces.Any(static item => item.IsPrimaryGroundSurface));
    }

    [TestMethod]
    public void TerrainConstraintsWorkAcrossPartitionBoundary()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(29005)));
        var footprint = new WorldVolume(16_380d, -8d, -100d, 16_400d, 8d, 100d);

        var result = world.EvaluateTerrainConstraint(footprint, TerrainConstraintKind.Road);

        Assert.IsTrue(double.IsFinite(result.MaximumSlopeDegrees));
        Assert.IsTrue(result.ElevationRangeMeters >= 0d);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Reason));
    }

    [TestMethod]
    public void CavitySurfacesRemainBelowPrimaryGround()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(29008)));
        double? cavityGroundZ = null;
        IReadOnlyList<TerrainSurfaceIntersection>? cavitySurfaces = null;

        for (var y = -100_000d; y <= 100_000d && cavityGroundZ is null; y += 2_048d)
        {
            for (var x = -100_000d; x <= 100_000d; x += 2_048d)
            {
                var surfaces = world.QueryTerrainSurfaces(x, y, -12_000d, 12_000d);
                if (!surfaces.Any(static item => item.IsCavityBoundary)) continue;
                cavityGroundZ = surfaces.Single(static item => item.IsPrimaryGroundSurface).Z;
                cavitySurfaces = surfaces;
                break;
            }
        }

        Assert.IsNotNull(cavityGroundZ);
        Assert.IsNotNull(cavitySurfaces);
        Assert.IsTrue(cavitySurfaces.Where(static item => item.IsCavityBoundary).All(item => item.Z < cavityGroundZ.Value));
    }

    [TestMethod]
    public void ElevatedFootprintDoesNotIntersectSurfaceWater()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(29009)));
        TerrainSurfaceSample? wetSurface = null;

        for (var y = -500_000d; y <= 500_000d && wetSurface is null; y += 50_000d)
        {
            for (var x = -500_000d; x <= 500_000d; x += 50_000d)
            {
                var sample = world.QueryTerrainSurface(x, y);
                if (sample.SurfaceWater == SurfaceWaterKind.None) continue;
                wetSurface = sample;
                break;
            }
        }

        Assert.IsNotNull(wetSurface);
        var wet = wetSurface.Value;
        var footprint = new WorldVolume(
            wet.Position.X - 1d,
            wet.Position.Y - 1d,
            wet.Position.Z + 1_000d,
            wet.Position.X + 1d,
            wet.Position.Y + 1d,
            wet.Position.Z + 1_010d);
        var result = world.EvaluateTerrainConstraint(footprint, TerrainConstraintKind.Road);

        Assert.IsFalse(result.IntersectsWater);
    }

    [TestMethod]
    public void GeographicFeaturesAndToponymsHaveStableIdentityAndProvenance()
    {
        var config = CreateConfig(29006);
        var volume = new WorldVolume(-500_000d, -500_000d, -10_000d, 500_000d, 500_000d, 10_000d);
        var first = new SimulationWorld(new SimulationConfig(worldEnvironment: config));
        var second = new SimulationWorld(new SimulationConfig(worldEnvironment: config));

        var firstFeatures = first.GetGeographicFeatures(volume, 64).ToArray();
        var secondFeatures = second.GetGeographicFeatures(volume, 64).ToArray();

        CollectionAssert.AreEqual(firstFeatures, secondFeatures);
        Assert.IsTrue(firstFeatures.Length > 0);
        var feature = firstFeatures[0];
        Assert.IsTrue(feature.Id.Value > 0);
        Assert.IsTrue(feature.AreaSquareMeters > 0d);
        Assert.IsTrue(first.TryGetNaturalToponym(feature.Id, out var firstName));
        Assert.IsTrue(second.TryGetNaturalToponym(feature.Id, out var secondName));
        Assert.AreEqual(firstName, secondName);
        Assert.AreEqual(feature.Id, firstName!.Provenance.SourceFeatureId);
        Assert.AreEqual("phase29-natural-v1", firstName.Provenance.GeneratorKey);
    }

    [TestMethod]
    public void CheckpointPreservesConfigAndRegeneratesDerivedFeaturesAndTerrain()
    {
        var config = CreateConfig(29007);
        var world = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 73, worldEnvironment: config));
        var volume = new WorldVolume(-300_000d, -300_000d, -10_000d, 300_000d, 300_000d, 10_000d);
        var expectedSnapshot = world.CreateDetailedWorldEnvironmentSnapshot(volume, 4, 4, 48);
        var expectedTerrain = world.QueryTerrainSurface(12_345d, 67_890d);
        world.Step();
        var checkpoint = world.CreateCheckpoint();

        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);
        var actualSnapshot = restored.CreateDetailedWorldEnvironmentSnapshot(volume, 4, 4, 48);
        var actualTerrain = restored.QueryTerrainSurface(12_345d, 67_890d);
        var restoredCheckpoint = restored.CreateCheckpoint();

        Assert.AreEqual(config, restored.WorldEnvironment);
        Assert.AreEqual(expectedTerrain, actualTerrain);
        CollectionAssert.AreEqual(expectedSnapshot.Features.ToArray(), actualSnapshot.Features.ToArray());
        CollectionAssert.AreEqual(expectedSnapshot.Toponyms.ToArray(), actualSnapshot.Toponyms.ToArray());
        Assert.IsNotNull(restoredCheckpoint.Economy?.WorldEnvironment);
        Assert.AreEqual(config, restoredCheckpoint.Economy!.WorldEnvironment!.Config);
        Assert.AreEqual(0, checkpoint.Economy!.WorldEnvironment!.Features.Count);
        Assert.AreEqual(0, checkpoint.Economy.WorldEnvironment.Toponyms.Count);
        Assert.AreEqual(0, restoredCheckpoint.Economy.WorldEnvironment.Features.Count);
        Assert.AreEqual(0, restoredCheckpoint.Economy.WorldEnvironment.Toponyms.Count);
    }

    [TestMethod]
    public void SimulationSeedZeroRemainsSupported()
    {
        var world = new SimulationWorld(new SimulationConfig(seed: 0));
        Assert.AreEqual(0UL, world.Config.Seed);
        Assert.AreEqual(1UL, world.WorldEnvironment.WorldSeed);
    }

    private static WorldEnvironmentConfig CreateConfig(ulong worldSeed) => new(
        worldSeed,
        new WorldVector(0d, 1d, 0d),
        latitudeDegrees: 44d,
        continentality: 0.58d,
        maritimeInfluence: 0.42d,
        meanAnnualTemperatureCelsius: 10d,
        seasonalityCelsius: 21d,
        annualPrecipitationMillimeters: 950d);
}
