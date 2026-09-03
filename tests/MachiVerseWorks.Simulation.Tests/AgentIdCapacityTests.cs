using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class AgentIdCapacityTests
{
    [TestMethod]
    public void ExhaustedAgentIdDoesNotAdvanceRandomStateOnCreateAgentFailure()
    {
        var checkpoint = new SimulationWorld(new SimulationConfig(seed: 123)).CreateCheckpoint() with
        {
            NextAgentId = ulong.MaxValue,
        };
        var world = SimulationWorld.RestoreCheckpoint(checkpoint);
        var before = world.CreateCheckpoint();

        Assert.ThrowsExactly<OverflowException>(() =>
            world.CreateAgent(new WorldPoint(1, 2, 3)));

        var after = world.CreateCheckpoint();
        Assert.AreEqual(before.RandomState, after.RandomState);
        Assert.AreEqual(before.NextAgentId, after.NextAgentId);
        Assert.AreEqual(0, world.ActiveAgentCount);
        Assert.AreEqual(0, world.TotalCreatedAgentCount);
    }

    [TestMethod]
    public void BulkCreationPreflightsAllRequiredAgentIdsBeforeMutation()
    {
        var checkpoint = new SimulationWorld(new SimulationConfig(seed: 456)).CreateCheckpoint() with
        {
            NextAgentId = ulong.MaxValue - 1,
        };
        var world = SimulationWorld.RestoreCheckpoint(checkpoint);
        var before = world.CreateCheckpoint();
        var volume = new WorldVolume(-10, -10, -10, 10, 10, 10);

        Assert.ThrowsExactly<OverflowException>(() => world.CreateAgents(2, volume));

        var after = world.CreateCheckpoint();
        Assert.AreEqual(before.RandomState, after.RandomState);
        Assert.AreEqual(before.NextAgentId, after.NextAgentId);
        Assert.AreEqual(0, world.ActiveAgentCount);
        Assert.AreEqual(0, world.TotalCreatedAgentCount);
        Assert.AreEqual(0, world.CreateSnapshot(volume).Length);
    }

    [TestMethod]
    public void SparseCheckpointDoesNotTreatNextIdAsHistoricalCreatedCount()
    {
        var checkpoint = new SimulationWorld(new SimulationConfig(seed: 789)).CreateCheckpoint() with
        {
            NextAgentId = 100,
            TotalCreatedAgentCount = 0,
        };
        var world = SimulationWorld.RestoreCheckpoint(checkpoint);

        Assert.AreEqual(0, world.TotalCreatedAgentCount);
        var id = world.CreateAgent(new WorldPoint(0, 0, 0), default);

        Assert.AreEqual(100UL, id.Value);
        Assert.AreEqual(1, world.TotalCreatedAgentCount);
    }
}
