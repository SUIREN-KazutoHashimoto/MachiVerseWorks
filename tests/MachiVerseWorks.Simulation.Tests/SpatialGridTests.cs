using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class SpatialGridTests
{
    [TestMethod]
    [DataRow(0d, 0d, 0d, 0, 0, 0)]
    [DataRow(63.999d, 63.999d, 63.999d, 0, 0, 0)]
    [DataRow(64d, 64d, 64d, 1, 1, 1)]
    [DataRow(-0.001d, -0.001d, -0.001d, -1, -1, -1)]
    [DataRow(-64d, -64d, -64d, -1, -1, -1)]
    [DataRow(-64.001d, -64.001d, -64.001d, -2, -2, -2)]
    public void WorldCoordinatesMapToExpectedThreeDimensionalCell(
        double x,
        double y,
        double z,
        int expectedX,
        int expectedY,
        int expectedZ)
    {
        var cell = SpatialGrid.ToCell(new WorldPoint(x, y, z), 64d);
        Assert.AreEqual(new SpatialCell(expectedX, expectedY, expectedZ), cell);
    }

    [TestMethod]
    public void VolumeQueryIncludesBoundaryAndExcludesOutsideAgentsOnEveryAxis()
    {
        var world = new SimulationWorld(new SimulationConfig(spatialCellSize: 10d));
        var zeroVelocity = new WorldVector(0d, 0d, 0d);
        var minBoundary = world.CreateAgent(new WorldPoint(0d, 0d, 0d), zeroVelocity);
        var maxBoundary = world.CreateAgent(new WorldPoint(10d, 10d, 10d), zeroVelocity);
        world.CreateAgent(new WorldPoint(-0.001d, 5d, 5d), zeroVelocity);
        world.CreateAgent(new WorldPoint(5d, 10.001d, 5d), zeroVelocity);
        world.CreateAgent(new WorldPoint(5d, 5d, 10.001d), zeroVelocity);

        var snapshot = world.CreateSnapshot(new WorldVolume(0d, 0d, 0d, 10d, 10d, 10d));
        var ids = snapshot.Select(static agent => agent.Id).OrderBy(static id => id.Value).ToArray();

        CollectionAssert.AreEqual(new[] { minBoundary, maxBoundary }, ids);
    }

    [TestMethod]
    public void MovingAgentChangesThreeDimensionalSpatialCellMembership()
    {
        var world = new SimulationWorld(
            new SimulationConfig(tickRate: 1, seed: 1, spatialCellSize: 10d));
        var id = world.CreateAgent(
            new WorldPoint(9d, 5d, 9d),
            new WorldVector(2d, 0d, 2d));

        var before = world.CreateSnapshot(new WorldVolume(0d, 0d, 0d, 9.999d, 9.999d, 9.999d));
        Assert.AreEqual(1, before.Length);

        world.Step();

        var oldVolume = world.CreateSnapshot(new WorldVolume(0d, 0d, 0d, 9.999d, 9.999d, 9.999d));
        var newVolume = world.CreateSnapshot(new WorldVolume(10d, 0d, 10d, 20d, 10d, 20d));

        Assert.AreEqual(0, oldVolume.Length);
        Assert.AreEqual(1, newVolume.Length);
        Assert.AreEqual(id, newVolume[0].Id);
    }
}
