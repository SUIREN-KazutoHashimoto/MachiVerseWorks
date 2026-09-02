using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RegionalMaterializationTests
{
    [TestMethod]
    public void RegionalPlanMaterializesIntoExistingSimulationStores()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 30101, worldEnvironment: CreateConfig(30101)));
        var snapshot = world.InitializeRegionalWorld(
            CreateVolume(),
            new RegionalGenerationOptions(
                RegionalGenerationQualityPreset.Draft,
                settlementCount: 2,
                iterationBudget: 1),
            out var materialized);

        Assert.AreEqual(snapshot.Settlements.Sum(static item => item.Population), world.PersonCount);
        Assert.AreEqual(snapshot.Settlements.Count, world.CompanyCount);
        Assert.AreEqual(snapshot.Settlements.Count, world.EstablishmentCount);
        Assert.AreEqual(snapshot.Settlements.Count, world.JobCount);
        Assert.AreEqual(snapshot.Settlements.Sum(static item => Math.Min(item.Population, item.Jobs)), world.EmploymentCount);
        Assert.IsTrue(world.RoadNodeCount > 0);
        Assert.IsTrue(world.RoadSegmentCount > 0);
        Assert.IsTrue(world.LaneCount >= world.RoadSegmentCount * 2);
        Assert.IsTrue(world.LaneConnectionCount > 0);
        Assert.IsTrue(world.BuildingCount > 0);
        Assert.IsTrue(world.PoiCount > 0);
        Assert.IsTrue(world.RoadAccessPointCount > 0);
        Assert.AreEqual(world.PersonCount, materialized.PersonCount);
        Assert.AreEqual(world.RoadSegmentCount, materialized.RoadSegmentCount);
    }

    [TestMethod]
    public void MaterializedRegionalWorldRoundTripsThroughCheckpoint()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 30102, worldEnvironment: CreateConfig(30102)));
        var expectedRegional = world.InitializeRegionalWorld(
            CreateVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2, iterationBudget: 1),
            out _);
        var expectedRoads = world.CreateRoadNetworkSnapshot();
        var expectedPopulation = world.CreatePopulationStatistics();
        var expectedEconomy = world.CreateEconomyStatistics();

        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());
        var actualRegional = restored.CreateRegionalGenerationSnapshot();
        var actualRoads = restored.CreateRoadNetworkSnapshot();
        var actualPopulation = restored.CreatePopulationStatistics();
        var actualEconomy = restored.CreateEconomyStatistics();

        CollectionAssert.AreEqual(expectedRegional.Settlements.ToArray(), actualRegional.Settlements.ToArray());
        CollectionAssert.AreEqual(expectedRoads.Nodes.ToArray(), actualRoads.Nodes.ToArray());
        CollectionAssert.AreEqual(expectedRoads.Segments.ToArray(), actualRoads.Segments.ToArray());
        CollectionAssert.AreEqual(expectedRoads.Lanes.ToArray(), actualRoads.Lanes.ToArray());
        Assert.AreEqual(expectedPopulation.HouseholdCount, actualPopulation.HouseholdCount);
        Assert.AreEqual(expectedPopulation.PersonCount, actualPopulation.PersonCount);
        Assert.AreEqual(expectedEconomy.CompanyCount, actualEconomy.CompanyCount);
        Assert.AreEqual(expectedEconomy.EmployedPersonCount, actualEconomy.EmployedPersonCount);
    }

    [TestMethod]
    public void InfrastructureConstraintUsesTerrainAndRegionalContext()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(30103)));
        var regional = world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2));
        var center = regional.Settlements[0].Center;
        var footprint = new WorldVolume(
            center.X - 10d,
            center.Y - 10d,
            center.Z - 2d,
            center.X + 10d,
            center.Y + 10d,
            center.Z + 2d);

        var railway = world.EvaluateRegionalInfrastructureConstraint(footprint, RegionalInfrastructureKind.Railway);
        var gas = world.EvaluateRegionalInfrastructureConstraint(footprint, RegionalInfrastructureKind.Gas);

        Assert.IsNotNull(railway.NearestSettlementId);
        Assert.IsTrue(double.IsFinite(railway.SettlementDistanceMeters));
        Assert.IsTrue(double.IsFinite(railway.Terrain.MaximumSlopeDegrees));
        Assert.IsFalse(string.IsNullOrWhiteSpace(railway.Reason));
        Assert.AreEqual(RegionalInfrastructureKind.Gas, gas.Kind);
    }

    [TestMethod]
    public void RegionalMaterializationRejectsNonEmptyUrbanWorld()
    {
        var world = new SimulationWorld(new SimulationConfig(worldEnvironment: CreateConfig(30104)));
        world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2));
        world.CreateBuilding(new WorldVolume(0d, 0d, 0d, 10d, 10d, 10d));

        Assert.ThrowsException<InvalidOperationException>(() => world.MaterializeRegionalGeneration());
    }

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
        new(-700_000d, -700_000d, -12_000d, 700_000d, 700_000d, 12_000d);
}
