using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RegionalRoadSignPlacementTests
{
    [TestMethod]
    public void MaterializedSignsReferenceExistingRoadLaneFeatureAndNamedDestination()
    {
        var world = new SimulationWorld(new SimulationConfig(
            tickRate: 2,
            seed: 31_101,
            worldEnvironment: CreateConfig(31_101)));
        var regional = world.InitializeRegionalWorld(
            CreateVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2, iterationBudget: 1),
            out _);

        var placements = world.CreateRegionalRoadSignPlacements();
        var names = regional.Toponyms.ToDictionary(static item => item.Id);

        Assert.AreEqual(regional.RoadSigns.Count, placements.Count);
        Assert.IsTrue(placements.Count > 0);
        foreach (var placement in placements)
        {
            Assert.IsTrue(world.TryGetRoadSegmentSnapshot(placement.RoadSegmentId, out _));
            if (placement.LaneId is { } laneId)
            {
                Assert.IsTrue(world.TryGetLaneSnapshot(laneId, out var lane));
                Assert.AreEqual(placement.RoadSegmentId, lane.SegmentId);
            }
            if (placement.DestinationSettlementId is { } destinationId)
            {
                var settlement = regional.Settlements.First(item => item.Id == destinationId);
                Assert.AreEqual(settlement.NameId, placement.DestinationNameId);
                Assert.IsTrue(names.ContainsKey(settlement.NameId));
            }
        }
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
        new(-600_000d, -600_000d, -12_000d, 600_000d, 600_000d, 12_000d);
}
