using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class ThreeDimensionalSimulationTests
{
    [TestMethod]
    public void SpatialGridMapsAllThreeAxesUsingFloor()
    {
        var cell = SpatialGrid.ToCell(new WorldPoint(64.1d, -0.1d, -64.1d), 64d);

        Assert.AreEqual(new SpatialCell(1, -1, -2), cell);
    }

    [TestMethod]
    public void TickAdvancesPositionAcrossAllThreeAxes()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 1, spatialCellSize: 16d));
        var id = world.CreateAgent(
            new WorldPoint(1d, 2d, 3d),
            new WorldVector(4d, 6d, 8d));

        world.Step();

        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var snapshot));
        Assert.AreEqual(new WorldPoint(3d, 5d, 7d), snapshot.Position);
        Assert.AreEqual(new WorldVector(4d, 6d, 8d), snapshot.Velocity);
    }

    [TestMethod]
    public void AutomaticallyGeneratedAgentsHaveZeroVerticalVelocity()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 17, spatialCellSize: 16d));
        var ids = world.CreateAgents(8, new WorldVolume(-10d, -20d, -30d, 10d, 20d, 30d));

        foreach (var id in ids)
        {
            Assert.IsTrue(world.TryGetAgentSnapshot(id, out var snapshot));
            Assert.AreEqual(0d, snapshot.Velocity.Z);
        }
    }

    [TestMethod]
    public void VolumeQuerySeparatesAgentsWithSameHorizontalPositionByAltitude()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 1, spatialCellSize: 16d));
        var ground = world.CreateAgent(new WorldPoint(10d, 20d, 0d), new WorldVector(0d, 0d, 0d));
        var elevated = world.CreateAgent(new WorldPoint(10d, 20d, 100d), new WorldVector(0d, 0d, 0d));

        var groundSnapshots = world.CreateSnapshot(new WorldVolume(9d, 19d, -1d, 11d, 21d, 1d));
        var elevatedSnapshots = world.CreateSnapshot(new WorldVolume(9d, 19d, 99d, 11d, 21d, 101d));

        Assert.AreEqual(1, groundSnapshots.Length);
        Assert.AreEqual(ground, groundSnapshots[0].Id);
        Assert.AreEqual(1, elevatedSnapshots.Length);
        Assert.AreEqual(elevated, elevatedSnapshots[0].Id);
    }

    [TestMethod]
    public void HugeSparseVolumeSnapshotReturnsOccupiedAgentsWithoutEnumeratingEveryCell()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 1, spatialCellSize: 16d));
        var first = world.CreateAgent(new WorldPoint(-100d, 20d, -300d), new WorldVector(0d, 0d, 0d));
        var second = world.CreateAgent(new WorldPoint(500d, -600d, 700d), new WorldVector(0d, 0d, 0d));

        var snapshots = world.CreateSnapshot(new WorldVolume(
            -1_000_000d,
            -1_000_000d,
            -1_000_000d,
            1_000_000d,
            1_000_000d,
            1_000_000d));

        Assert.AreEqual(2, snapshots.Length);
        CollectionAssert.AreEquivalent(
            new[] { first.Value, second.Value },
            snapshots.Select(static snapshot => snapshot.Id.Value).ToArray());
    }

    [TestMethod]
    public void CheckpointRestorePreservesAltitudeAndVerticalVelocity()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 4, seed: 91, spatialCellSize: 32d));
        var id = world.CreateAgent(
            new WorldPoint(-5d, 7d, 30d),
            new WorldVector(2d, -4d, 6d));
        world.Step();

        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());

        Assert.IsTrue(restored.TryGetAgentSnapshot(id, out var restoredSnapshot));
        Assert.AreEqual(new WorldPoint(-4.5d, 6d, 31.5d), restoredSnapshot.Position);
        Assert.AreEqual(new WorldVector(2d, -4d, 6d), restoredSnapshot.Velocity);

        world.Step();
        restored.Step();
        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var expectedNext));
        Assert.IsTrue(restored.TryGetAgentSnapshot(id, out var actualNext));
        Assert.AreEqual(expectedNext, actualNext);
    }

    [TestMethod]
    public void FailedThreeDimensionalTickRollsBackEarlierAgentMovement()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 1, spatialCellSize: 1d));
        var first = world.CreateAgent(
            new WorldPoint(0d, 0d, 10d),
            new WorldVector(0d, 0d, 5d));
        var second = world.CreateAgent(
            new WorldPoint(0d, 0d, int.MaxValue - 0.25d),
            new WorldVector(0d, 0d, 2d));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => world.Step());

        Assert.AreEqual(0UL, world.Time.TickCount);
        Assert.IsTrue(world.TryGetAgentSnapshot(first, out var firstSnapshot));
        Assert.IsTrue(world.TryGetAgentSnapshot(second, out var secondSnapshot));
        Assert.AreEqual(10d, firstSnapshot.Position.Z);
        Assert.AreEqual(int.MaxValue - 0.25d, secondSnapshot.Position.Z);
    }

    [TestMethod]
    public void NonFiniteAltitudeAndVerticalVelocityAreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorldPoint(0d, 0d, double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorldVector(0d, 0d, double.PositiveInfinity));
    }
}
