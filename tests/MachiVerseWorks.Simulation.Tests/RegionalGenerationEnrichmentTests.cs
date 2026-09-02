using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RegionalGenerationEnrichmentTests
{
    [TestMethod]
    public void RoleDistrictsHaveGeneratedParcelsAndStagedDevelopment()
    {
        var fixture = RegionalGenerationFixture.Create(RegionalGenerationFixtureKind.Port);
        var world = new SimulationWorld(new SimulationConfig(
            tickRate: 2,
            seed: fixture.Environment.WorldSeed,
            worldEnvironment: fixture.Environment));
        var snapshot = world.GenerateRegionalGeneration(fixture.Volume, fixture.Options);

        foreach (var settlement in snapshot.Settlements)
        {
            if (settlement.Role is RegionalRole.TransportHub or RegionalRole.Port)
            {
                var stationDistrict = snapshot.Districts.FirstOrDefault(item => item.SettlementId == settlement.Id && item.Kind == DistrictKind.StationDistrict);
                Assert.IsNotNull(stationDistrict);
                Assert.AreEqual(4, snapshot.Parcels.Count(item => item.DistrictId == stationDistrict.Id));
            }
            if (settlement.Role is RegionalRole.Industrial or RegionalRole.Resource)
            {
                var industrialDistrict = snapshot.Districts.FirstOrDefault(item => item.SettlementId == settlement.Id && item.Kind == DistrictKind.IndustrialArea);
                Assert.IsNotNull(industrialDistrict);
                Assert.AreEqual(4, snapshot.Parcels.Count(item => item.DistrictId == industrialDistrict.Id));
            }
        }

        Assert.IsTrue(snapshot.Parcels.All(static item => item.DevelopmentSuitability is >= 0d and <= 1d));
        Assert.IsTrue(snapshot.Parcels.All(static item => item.LandValue is >= 0d and <= 1d));
        Assert.IsTrue(snapshot.Parcels.Any(static item => item.DevelopmentState == ParcelDevelopmentState.Vacant));
        Assert.IsTrue(snapshot.Parcels.Any(static item => item.DevelopmentState is ParcelDevelopmentState.Occupied or ParcelDevelopmentState.Developing or ParcelDevelopmentState.Redeveloping));
    }

    [TestMethod]
    public void RoadContextArtifactsIncludePlaceNamesAndRequiredWarnings()
    {
        var fixture = RegionalGenerationFixture.Create(RegionalGenerationFixtureKind.Mountain);
        var environment = new WorldEnvironmentGenerator(fixture.Environment);
        var world = new SimulationWorld(new SimulationConfig(
            tickRate: 2,
            seed: fixture.Environment.WorldSeed,
            worldEnvironment: fixture.Environment));
        var snapshot = world.GenerateRegionalGeneration(fixture.Volume, fixture.Options);
        var features = environment.DetectGeographicFeatures(fixture.Volume, 128);
        var analyzer = new RegionalRoadContextAnalyzer(environment);

        foreach (var corridor in snapshot.Corridors.Where(static item => item.Kind != RegionalCorridorKind.Railway))
        {
            Assert.IsTrue(snapshot.RoadSigns.Any(item => item.CorridorId == corridor.Id && item.Kind == RoadSignKind.Direction));
            Assert.IsTrue(snapshot.RoadSigns.Any(item => item.CorridorId == corridor.Id && item.Kind == RoadSignKind.PlaceName));
            var context = analyzer.Analyze(corridor, features);
            foreach (var kind in RegionalRoadSignRule.DetermineRequiredSigns(context))
                Assert.IsTrue(snapshot.RoadSigns.Any(item => item.CorridorId == corridor.Id && item.Kind == kind), $"Missing {kind} for corridor {corridor.Id.Value}.");
            if (context.CrossesWater)
                Assert.IsTrue(snapshot.Toponyms.Any(item => item.Kind == HumanToponymKind.Bridge && item.Provenance.ParentHumanToponymId == corridor.NameId));
            if (context.RequiresTunnel)
                Assert.IsTrue(snapshot.Toponyms.Any(item => item.Kind == HumanToponymKind.Tunnel && item.Provenance.ParentHumanToponymId == corridor.NameId));
        }
    }

    [TestMethod]
    public void ParcelSuitabilityRewardsRoadAccessAndSafeTerrainFactors()
    {
        var config = new WorldEnvironmentConfig(
            31_303UL,
            new WorldVector(0d, 1d, 0d),
            latitudeDegrees: 43d,
            continentality: 0.55d,
            maritimeInfluence: 0.45d,
            meanAnnualTemperatureCelsius: 10d,
            seasonalityCelsius: 20d,
            annualPrecipitationMillimeters: 950d);
        var environment = new WorldEnvironmentGenerator(config);
        var evaluator = new RegionalParcelSuitabilityEvaluator(environment);
        var settlement = new Settlement(
            new SettlementId(1),
            environment.Sample(new WorldPoint(0d, 0d, 0d)).Position,
            SettlementEnvironmentKind.Inland,
            SettlementOriginKind.InlandPlain,
            RegionalRole.Market,
            InitialEconomyKind.Trade,
            new SettlementSuitability(0.8d, 0.7d, 0.8d, 0.8d, 0.5d, 0.1d, 0.2d, 0.1d, 0.2d, 0.8d),
            2_000,
            900,
            4_000d,
            new HumanToponymId(1));
        var district = new District(
            new DistrictId(1),
            settlement.Id,
            DistrictKind.CentralBusiness,
            new WorldVolume(-500d, -500d, -20d, 500d, 500d, 100d),
            new HumanToponymId(2),
            0.9d);
        var road = new RegionalCorridor(
            new RegionalCorridorId(1),
            RegionalCorridorKind.RegionalRoad,
            settlement.Id,
            new SettlementId(2),
            [new WorldPoint(-1_000d, 0d, 0d), new WorldPoint(1_000d, 0d, 0d)],
            0.9d,
            1d,
            null);

        var near = evaluator.Evaluate(new WorldVolume(-120d, 20d, -5d, 120d, 220d, 80d), district, settlement, ZoneKind.Commercial, [road]);
        var far = evaluator.Evaluate(new WorldVolume(-120d, 10_000d, -5d, 120d, 10_200d, 80d), district, settlement, ZoneKind.Commercial, [road]);

        Assert.IsTrue(near.RoadAccess > far.RoadAccess);
        Assert.IsTrue(near.TotalScore > far.TotalScore);
    }
}
