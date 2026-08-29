using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class SimulationWorldTests
{
    [TestMethod]
    public void StepAdvancesTickAndMovesAgent()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10, seed: 7));
        var id = world.CreateAgent(new WorldPoint(5d, 10d), new WorldVector(2d, -4d));

        world.Step();

        Assert.AreEqual(1UL, world.Time.TickCount);
        Assert.AreEqual(TimeSpan.FromSeconds(0.1d), world.Time.Elapsed);
        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var snapshot));
        Assert.AreEqual(5.2d, snapshot.Position.X, 1e-12);
        Assert.AreEqual(9.6d, snapshot.Position.Y, 1e-12);
        Assert.AreEqual(1UL, snapshot.TickCount);
    }

    [TestMethod]
    public void SameSeedAndInputsProduceSameState()
    {
        var configA = new SimulationConfig(tickRate: 30, seed: 987654321);
        var configB = new SimulationConfig(tickRate: 30, seed: 987654321);
        var first = new SimulationWorld(configA);
        var second = new SimulationWorld(configB);
        var area = new WorldRect(-100d, -100d, 100d, 100d);

        first.CreateAgents(256, area);
        second.CreateAgents(256, area);

        for (var tick = 0; tick < 120; tick++)
        {
            first.Step();
            second.Step();
        }

        var firstSnapshot = first.CreateSnapshot(new WorldRect(-1000d, -1000d, 1000d, 1000d));
        var secondSnapshot = second.CreateSnapshot(new WorldRect(-1000d, -1000d, 1000d, 1000d));

        CollectionAssert.AreEqual(firstSnapshot, secondSnapshot);
    }

    [TestMethod]
    public void AgentIdsAreMonotonicAndNeverRepacked()
    {
        var world = new SimulationWorld();
        var first = world.CreateAgent(new WorldPoint(0d, 0d), default);
        var second = world.CreateAgent(new WorldPoint(1d, 0d), default);

        Assert.IsTrue(world.RemoveAgent(first));

        var third = world.CreateAgent(new WorldPoint(2d, 0d), default);

        Assert.AreEqual(1UL, first.Value);
        Assert.AreEqual(2UL, second.Value);
        Assert.AreEqual(3UL, third.Value);
        Assert.IsFalse(world.TryGetAgentSnapshot(first, out _));
        Assert.IsTrue(world.TryGetAgentSnapshot(second, out _));
    }

    [TestMethod]
    public void BulkCreationCreatesRequestedNumberOfAgents()
    {
        var world = new SimulationWorld(new SimulationConfig(seed: 42));
        var ids = world.CreateAgents(10_000, new WorldRect(0d, 0d, 1000d, 1000d));

        Assert.AreEqual(10_000, ids.Length);
        Assert.AreEqual(10_000, world.ActiveAgentCount);
        Assert.AreEqual(10_000, world.TotalCreatedAgentCount);
        Assert.AreEqual(1UL, ids[0].Value);
        Assert.AreEqual(10_000UL, ids[^1].Value);
    }
}
