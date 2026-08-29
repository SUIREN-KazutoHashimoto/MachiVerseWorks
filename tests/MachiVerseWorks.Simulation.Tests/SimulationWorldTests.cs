using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class SimulationWorldTests
{
    [TestMethod]
    public void StepAdvancesTickAndMovesAgentAcrossAllAxes()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10, seed: 7));
        var id = world.CreateAgent(
            new WorldPoint(5d, 10d, 15d),
            new WorldVector(2d, -4d, 6d));

        world.Step();

        Assert.AreEqual(1UL, world.Time.TickCount);
        Assert.AreEqual(TimeSpan.FromSeconds(0.1d), world.Time.Elapsed);
        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var snapshot));
        Assert.AreEqual(new WorldPoint(5.2d, 9.6d, 15.6d), snapshot.Position);
        Assert.AreEqual(new WorldVector(2d, -4d, 6d), snapshot.Velocity);
        Assert.AreEqual(1UL, snapshot.TickCount);
    }

    [TestMethod]
    public void SameSeedAndThreeDimensionalInputsProduceSameState()
    {
        var first = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 987654321));
        var second = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 987654321));
        var spawnVolume = new WorldVolume(-100d, -100d, -100d, 100d, 100d, 100d);

        first.CreateAgents(256, spawnVolume);
        second.CreateAgents(256, spawnVolume);

        for (var tick = 0; tick < 120; tick++)
        {
            first.Step();
            second.Step();
        }

        var snapshotVolume = new WorldVolume(-1000d, -1000d, -1000d, 1000d, 1000d, 1000d);
        CollectionAssert.AreEqual(
            first.CreateSnapshot(snapshotVolume),
            second.CreateSnapshot(snapshotVolume));
    }

    [TestMethod]
    public void AgentIdsAreMonotonicAndNeverRepacked()
    {
        var world = new SimulationWorld();
        var zeroVelocity = new WorldVector(0d, 0d, 0d);
        var first = world.CreateAgent(new WorldPoint(0d, 0d, 0d), zeroVelocity);
        var second = world.CreateAgent(new WorldPoint(1d, 0d, 1d), zeroVelocity);

        Assert.IsTrue(world.RemoveAgent(first));
        var third = world.CreateAgent(new WorldPoint(2d, 0d, 2d), zeroVelocity);

        Assert.AreEqual(1UL, first.Value);
        Assert.AreEqual(2UL, second.Value);
        Assert.AreEqual(3UL, third.Value);
        Assert.IsFalse(world.TryGetAgentSnapshot(first, out _));
        Assert.IsTrue(world.TryGetAgentSnapshot(second, out _));
    }

    [TestMethod]
    public void BulkCreationUsesThreeDimensionalSpawnVolume()
    {
        var world = new SimulationWorld(new SimulationConfig(seed: 42));
        var volume = new WorldVolume(0d, 0d, 100d, 1000d, 1000d, 200d);
        var ids = world.CreateAgents(10_000, volume);
        var snapshots = world.CreateSnapshot(volume);

        Assert.AreEqual(10_000, ids.Length);
        Assert.AreEqual(10_000, snapshots.Length);
        Assert.AreEqual(10_000, world.ActiveAgentCount);
        Assert.IsTrue(snapshots.All(static agent => agent.Position.Z >= 100d && agent.Position.Z <= 200d));
    }

    [TestMethod]
    public void InvalidSpatialCreateDoesNotMutateWorldOrRandomState()
    {
        var config = new SimulationConfig(seed: 1234);
        var world = new SimulationWorld(config);
        var before = world.CreateCheckpoint();
        var outsideX = ((double)int.MaxValue + 1d) * config.SpatialCellSize;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            world.CreateAgent(new WorldPoint(outsideX, 0d, 0d)));

        var after = world.CreateCheckpoint();
        Assert.AreEqual(before.TickCount, after.TickCount);
        Assert.AreEqual(before.ElapsedTicks, after.ElapsedTicks);
        Assert.AreEqual(before.RandomState, after.RandomState);
        Assert.AreEqual(before.NextAgentId, after.NextAgentId);
        Assert.AreEqual(0, world.ActiveAgentCount);
        Assert.AreEqual(0, world.TotalCreatedAgentCount);
    }

    [TestMethod]
    public void SpatialFailureDuringStepLeavesTimeAndAgentPositionUnchanged()
    {
        var config = new SimulationConfig(tickRate: 1, spatialCellSize: 64d);
        var world = new SimulationWorld(config);
        var insideX = ((double)int.MaxValue + 0.5d) * config.SpatialCellSize;
        var id = world.CreateAgent(
            new WorldPoint(insideX, 0d, 10d),
            new WorldVector(config.SpatialCellSize, 0d, 2d));
        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var before));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => world.Step());

        Assert.AreEqual(0UL, world.Time.TickCount);
        Assert.AreEqual(TimeSpan.Zero, world.Time.Elapsed);
        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var after));
        Assert.AreEqual(before, after);
    }

    [TestMethod]
    public void LaterSpatialFailureRollsBackEarlierThreeDimensionalMovement()
    {
        var config = new SimulationConfig(tickRate: 1, spatialCellSize: 64d);
        var world = new SimulationWorld(config);
        var firstId = world.CreateAgent(
            new WorldPoint(10d, 20d, 30d),
            new WorldVector(3d, 4d, 5d));
        var insideX = ((double)int.MaxValue + 0.5d) * config.SpatialCellSize;
        var secondId = world.CreateAgent(
            new WorldPoint(insideX, 0d, 40d),
            new WorldVector(config.SpatialCellSize, 0d, 6d));
        Assert.IsTrue(world.TryGetAgentSnapshot(firstId, out var firstBefore));
        Assert.IsTrue(world.TryGetAgentSnapshot(secondId, out var secondBefore));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => world.Step());

        Assert.AreEqual(0UL, world.Time.TickCount);
        Assert.IsTrue(world.TryGetAgentSnapshot(firstId, out var firstAfter));
        Assert.IsTrue(world.TryGetAgentSnapshot(secondId, out var secondAfter));
        Assert.AreEqual(firstBefore, firstAfter);
        Assert.AreEqual(secondBefore, secondAfter);
    }
}
