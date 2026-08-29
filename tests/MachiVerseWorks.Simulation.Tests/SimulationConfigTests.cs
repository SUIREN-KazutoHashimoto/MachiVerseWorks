using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class SimulationConfigTests
{
    [TestMethod]
    public void DefaultsUseThirtyTicksPerSecond()
    {
        var config = new SimulationConfig();

        Assert.AreEqual(30, config.TickRate);
        Assert.AreEqual(1UL, config.Seed);
        Assert.AreEqual(1d / 30d, config.TickDurationSeconds, 1e-12);
    }

    [TestMethod]
    public void ConstructorKeepsSeedAndTickRate()
    {
        var config = new SimulationConfig(tickRate: 60, seed: 1234, spatialCellSize: 32d);

        Assert.AreEqual(60, config.TickRate);
        Assert.AreEqual(1234UL, config.Seed);
        Assert.AreEqual(32d, config.SpatialCellSize);
    }

    [TestMethod]
    public void InvalidTickRateIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SimulationConfig(tickRate: 0));
    }
}
