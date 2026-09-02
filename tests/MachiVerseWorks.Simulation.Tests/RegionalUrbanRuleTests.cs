using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RegionalUrbanRuleTests
{
    [TestMethod]
    public void DevelopmentRuleSupportsBuildVacancyAndRedevelopment()
    {
        var parcel = CreateParcel(ParcelDevelopmentState.Vacant, suitability: 0.8d, landValue: 0.7d);
        var build = RegionalDevelopmentRule.Evaluate(parcel, normalizedDemand: 0.8d, buildingAgeYears: 0);
        var vacate = RegionalDevelopmentRule.Evaluate(parcel with { DevelopmentState = ParcelDevelopmentState.Occupied }, normalizedDemand: 0.05d, buildingAgeYears: 10);
        var redevelop = RegionalDevelopmentRule.Evaluate(parcel with { DevelopmentState = ParcelDevelopmentState.Occupied }, normalizedDemand: 0.95d, buildingAgeYears: 60);

        Assert.IsTrue(build.Build);
        Assert.AreEqual(ParcelDevelopmentState.Developing, build.NextState);
        Assert.IsTrue(vacate.Vacate);
        Assert.AreEqual(ParcelDevelopmentState.Vacant, vacate.NextState);
        Assert.IsTrue(redevelop.Redevelop);
        Assert.AreEqual(ParcelDevelopmentState.Redeveloping, redevelop.NextState);
    }

    [TestMethod]
    public void GrowthHistoryRuleUsesAccessibilityCongestionAndLandPressure()
    {
        var highPressure = RegionalGrowthHistoryRule.Evaluate(0.88d, 0.91d, 0.84d, existingCenterCount: 1);
        var landOnly = RegionalGrowthHistoryRule.Evaluate(0.35d, 0.30d, 0.95d, existingCenterCount: 1);
        var saturatedCenters = RegionalGrowthHistoryRule.Evaluate(0.92d, 0.94d, 0.90d, existingCenterCount: 4);

        Assert.IsTrue(highPressure.Redevelop);
        Assert.IsTrue(highPressure.FormNewCenter);
        Assert.IsFalse(landOnly.FormNewCenter);
        Assert.IsTrue(saturatedCenters.Redevelop);
        Assert.IsFalse(saturatedCenters.FormNewCenter);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RegionalGrowthHistoryRule.Evaluate(1.1d, 0.5d, 0.5d, 1));
    }

    [TestMethod]
    public void RockSlopeGetsDedicatedWarningSign()
    {
        var context = new RoadContextAnalysis(
            new RegionalCorridorId(99),
            MaximumGrade: 0.02d,
            MaximumTurnAngleDegrees: 10d,
            FloodRisk: 0.1d,
            CrossesWater: false,
            IsRockSlope: true,
            IsMountainPass: false,
            RequiresTunnel: false,
            IsCoastalLowland: false,
            FeatureId: null,
            DestinationSettlementId: new SettlementId(2));

        var signs = RegionalRoadSignRule.DetermineRequiredSigns(context);

        Assert.IsTrue(signs.Contains(RoadSignKind.Direction));
        Assert.IsTrue(signs.Contains(RoadSignKind.RockSlope));
        Assert.IsFalse(signs.Contains(RoadSignKind.SteepGrade));
    }

    [TestMethod]
    public void RoadContextProducesDeterministicRequiredSignSet()
    {
        var environment = new WorldEnvironmentGenerator(CreateEnvironment(31001));
        var corridor = new RegionalCorridor(
            new RegionalCorridorId(1),
            RegionalCorridorKind.IntercityRoad,
            new SettlementId(1),
            new SettlementId(2),
            new[]
            {
                environment.Sample(new WorldPoint(-50_000d, 0d, 0d)).Position,
                environment.Sample(new WorldPoint(0d, 20_000d, 0d)).Position,
                environment.Sample(new WorldPoint(50_000d, 0d, 0d)).Position,
            },
            0.8d,
            1_000d,
            null);
        var features = environment.DetectGeographicFeatures(new WorldVolume(-100_000d, -100_000d, -12_000d, 100_000d, 100_000d, 12_000d), 32);
        var analyzer = new RegionalRoadContextAnalyzer(environment);

        var first = analyzer.Analyze(corridor, features);
        var second = analyzer.Analyze(corridor, features);
        var firstSigns = RegionalRoadSignRule.DetermineRequiredSigns(first);
        var secondSigns = RegionalRoadSignRule.DetermineRequiredSigns(second);

        Assert.AreEqual(first, second);
        CollectionAssert.AreEqual(firstSigns.ToArray(), secondSigns.ToArray());
        Assert.IsTrue(firstSigns.Contains(RoadSignKind.Direction));
        Assert.IsTrue(first.MaximumTurnAngleDegrees >= 0d);
        Assert.IsTrue(first.FloodRisk is >= 0d and <= 1d);
    }

    [TestMethod]
    public void BridgeAndTunnelNamesAreStableAndRetainProvenance()
    {
        var natural = new NaturalToponym(
            new ToponymId(10),
            new GeographicFeatureId(20),
            "Aru River",
            new ToponymProvenance(ToponymProvenanceKind.GeneratedNaturalFeature, new GeographicFeatureId(20), null, "phase29-natural-v1"));
        var roadName = new HumanToponym(
            new HumanToponymId(30),
            HumanToponymKind.Road,
            "Aru Road",
            new HumanToponymProvenance(natural, natural.FeatureId, null, "phase30-regional-v1"));
        var corridor = new RegionalCorridor(
            new RegionalCorridorId(40),
            RegionalCorridorKind.IntercityRoad,
            new SettlementId(1),
            new SettlementId(2),
            new[] { new WorldPoint(0d, 0d, 0d), new WorldPoint(1d, 0d, 0d) },
            1d,
            1d,
            roadName.Id);
        var context = new RoadContextAnalysis(corridor.Id, 0.2d, 15d, 0.2d, true, true, false, true, false, natural.FeatureId, corridor.ToSettlementId);

        var bridge = RegionalStructureNaming.CreateBridgeName(corridor, context, new[] { roadName });
        var tunnel = RegionalStructureNaming.CreateTunnelName(corridor, context, new[] { roadName });

        Assert.AreEqual("Aru Bridge", bridge.Name);
        Assert.AreEqual("Aru Tunnel", tunnel.Name);
        Assert.AreEqual(natural.FeatureId, bridge.Provenance.SourceFeatureId);
        Assert.AreEqual(natural.FeatureId, tunnel.Provenance.SourceFeatureId);
        Assert.AreEqual("phase30-structure-v1", bridge.Provenance.GeneratorKey);
    }

    [TestMethod]
    public void NamedRegionalFixturesAreDeterministic()
    {
        foreach (var kind in Enum.GetValues<RegionalGenerationFixtureKind>())
        {
            var first = RegionalGenerationFixture.Create(kind);
            var second = RegionalGenerationFixture.Create(kind);
            Assert.AreEqual(first, second);
            var firstWorld = new SimulationWorld(new SimulationConfig(worldEnvironment: first.Environment));
            var secondWorld = new SimulationWorld(new SimulationConfig(worldEnvironment: second.Environment));
            var firstSnapshot = firstWorld.GenerateRegionalGeneration(first.Volume, first.Options);
            var secondSnapshot = secondWorld.GenerateRegionalGeneration(second.Volume, second.Options);
            CollectionAssert.AreEqual(firstSnapshot.Settlements.Select(static item => item.Id).ToArray(), secondSnapshot.Settlements.Select(static item => item.Id).ToArray());
        }
    }

    private static Parcel CreateParcel(ParcelDevelopmentState state, double suitability, double landValue) => new(
        new ParcelId(1),
        new SettlementId(1),
        new DistrictId(1),
        new WorldVolume(0d, 0d, 0d, 10d, 10d, 10d),
        ZoneKind.Residential,
        state,
        suitability,
        landValue,
        null);

    private static WorldEnvironmentConfig CreateEnvironment(ulong seed) => new(
        seed,
        new WorldVector(0d, 1d, 0d),
        latitudeDegrees: 43d,
        continentality: 0.55d,
        maritimeInfluence: 0.45d,
        meanAnnualTemperatureCelsius: 10d,
        seasonalityCelsius: 20d,
        annualPrecipitationMillimeters: 900d);
}
