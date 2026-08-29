using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class SnapshotMessagePlannerTests
{
    [TestMethod]
    public void PlanCreatesSpawnUpdateAndRemoveMessagesWithThreeDimensionalState()
    {
        var snapshots = new[]
        {
            new AgentSnapshot(new AgentId(1), new WorldPoint(10d, 20d, 30d), new WorldVector(1d, 2d, 3d), 50),
            new AgentSnapshot(new AgentId(2), new WorldPoint(30d, 40d, 50d), new WorldVector(3d, 4d, 5d), 50),
        };
        IReadOnlySet<ulong> known = new HashSet<ulong> { 1, 3 };

        var plan = SnapshotMessagePlanner.Create(snapshots, known, 50);

        Assert.AreEqual(3, plan.Messages.Count);
        var update = Assert.IsInstanceOfType<AgentUpdateMessage>(plan.Messages[0]);
        var spawn = Assert.IsInstanceOfType<AgentSpawnMessage>(plan.Messages[1]);
        var remove = Assert.IsInstanceOfType<AgentRemoveMessage>(plan.Messages[2]);
        Assert.AreEqual(30d, update.Z);
        Assert.AreEqual(3d, update.VelocityZ);
        Assert.AreEqual(50d, spawn.Z);
        Assert.AreEqual(5d, spawn.VelocityZ);
        Assert.AreEqual(3UL, remove.AgentId);
        CollectionAssert.AreEquivalent(new ulong[] { 1, 2 }, plan.CurrentAgentIds.ToArray());
    }
}
