using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RegionalPolycentricInteractionTests
{
    [TestMethod]
    public void SimilarNearbyCentersAreCompetitive()
    {
        var first = CreateSettlement(1, 0d, 0d, 20_000, 8_000, 0.65d, 0.55d, SettlementScale.Town);
        var second = CreateSettlement(2, 2_000d, 0d, 21_000, 8_200, 0.63d, 0.56d, SettlementScale.Town);

        var profile = RegionalPolycentricInteractionRules.Evaluate(first, second);

        Assert.AreEqual(RegionalInteractionMode.Competition, profile.DominantMode);
        Assert.IsTrue(profile.Competition > profile.Specialization);
    }

    [TestMethod]
    public void ContrastingCentersExposeSpecializationAndComplementarity()
    {
        var residential = CreateSettlement(1, 0d, 0d, 35_000, 2_000, 0.35d, 0.70d, SettlementScale.City);
        var employmentCenter = CreateSettlement(2, 3_000d, 0d, 8_000, 14_000, 0.90d, 0.30d, SettlementScale.Town);

        var profile = RegionalPolycentricInteractionRules.Evaluate(residential, employmentCenter);

        Assert.AreEqual(new SettlementId(1), profile.FirstSettlementId);
        Assert.AreEqual(new SettlementId(2), profile.SecondSettlementId);
        Assert.IsTrue(profile.Specialization > 0.30d);
        Assert.IsTrue(profile.Complementarity > 0.20d);
        Assert.AreNotEqual(RegionalInteractionMode.Competition, profile.DominantMode);
    }

    private static SettlementEvolutionState CreateSettlement(
        ulong id,
        double x,
        double y,
        int population,
        int jobs,
        double services,
        double density,
        SettlementScale scale) =>
        new(
            new SettlementId(id),
            new WorldPoint(x, y, 0d),
            population,
            jobs,
            services,
            density,
            0.7d,
            8_000d,
            scale,
            SettlementTrend.Stable,
            true,
            0,
            null);
}
