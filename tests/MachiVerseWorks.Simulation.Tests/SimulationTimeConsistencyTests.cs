using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class SimulationTimeConsistencyTests
{
    [DataTestMethod]
    [DataRow(30)]
    [DataRow(60)]
    public void LongRunningElapsedTimeDoesNotAccumulateTimeSpanRoundingDrift(int tickRate)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: tickRate));
        var id = world.CreateAgent(new WorldPoint(0, 0, 0), new WorldVector(3, 0, 0));
        var ticks = tickRate * 1_000;

        for (var index = 0; index < ticks; index++) world.Step();

        Assert.AreEqual((ulong)ticks, world.Time.TickCount);
        Assert.AreEqual(TimeSpan.FromSeconds(1_000), world.Time.Elapsed);
        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var snapshot));
        Assert.AreEqual(3_000d, snapshot.Position.X, 1e-7);
    }

    [TestMethod]
    public void FractionalTimeSpanRemainderIsCarriedAcrossTicks()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 60));

        for (var index = 0; index < 60; index++) world.Step();

        Assert.AreEqual(TimeSpan.FromSeconds(1), world.Time.Elapsed);
        Assert.AreEqual(TimeSpan.TicksPerSecond, world.Time.Elapsed.Ticks);
    }

    [TestMethod]
    public void CheckpointRoundTripKeepsDerivedElapsedTimeModel()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 60));
        for (var index = 0; index < 137; index++) world.Step();

        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());
        restored.Step();

        Assert.AreEqual(138UL, restored.Time.TickCount);
        Assert.AreEqual((long)((UInt128)138 * (ulong)TimeSpan.TicksPerSecond / 60u), restored.Time.Elapsed.Ticks);
    }
}
