using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class SnapshotTests
{
    [TestMethod]
    public void SnapshotIsDetachedFromMutableSimulationState()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1));
        var id = world.CreateAgent(new WorldPoint(1d, 2d), new WorldVector(3d, 4d));
        var area = new WorldRect(-10d, -10d, 10d, 10d);

        var beforeStep = world.CreateSnapshot(area);
        Assert.AreEqual(1, beforeStep.Length);

        world.Step();

        Assert.AreEqual(new WorldPoint(1d, 2d), beforeStep[0].Position);
        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var current));
        Assert.AreEqual(new WorldPoint(4d, 6d), current.Position);
    }

    [TestMethod]
    public void ReplacingSnapshotArrayElementDoesNotChangeSimulation()
    {
        var world = new SimulationWorld();
        var id = world.CreateAgent(new WorldPoint(5d, 6d), default);
        var snapshot = world.CreateSnapshot(new WorldRect(0d, 0d, 10d, 10d));

        snapshot[0] = default;

        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var current));
        Assert.AreEqual(new WorldPoint(5d, 6d), current.Position);
    }
}
