using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class SpatialGridTests
{
    [TestMethod]
    [DataRow(0d, 0d, 0, 0)]
    [DataRow(63.999d, 63.999d, 0, 0)]
    [DataRow(64d, 64d, 1, 1)]
    [DataRow(-0.001d, -0.001d, -1, -1)]
    [DataRow(-64d, -64d, -1, -1)]
    [DataRow(-64.001d, -64.001d, -2, -2)]
    public void WorldCoordinatesMapToExpectedCell(double x, double y, int expectedX, int expectedY)
    {
        var cell = SpatialGrid.ToCell(new WorldPoint(x, y), 64d);

        Assert.AreEqual(new SpatialCell(expectedX, expectedY), cell);
    }

    [TestMethod]
    public void AreaQueryIncludesBoundaryAndExcludesOutsideAgents()
    {
        var world = new SimulationWorld(new SimulationConfig(spatialCellSize: 10d));
        var minBoundary = world.CreateAgent(new WorldPoint(0d, 0d), default);
        var maxBoundary = world.CreateAgent(new WorldPoint(10d, 10d), default);
        world.CreateAgent(new WorldPoint(-0.001d, 5d), default);
        world.CreateAgent(new WorldPoint(10.001d, 5d), default);

        var snapshot = world.CreateSnapshot(new WorldRect(0d, 0d, 10d, 10d));
        var ids = snapshot.Select(static agent => agent.Id).OrderBy(static id => id.Value).ToArray();

        CollectionAssert.AreEqual(new[] { minBoundary, maxBoundary }, ids);
    }

    [TestMethod]
    public void MovingAgentChangesSpatialCellMembership()
    {
        var world = new SimulationWorld(
            new SimulationConfig(tickRate: 1, seed: 1, spatialCellSize: 10d));
        var id = world.CreateAgent(new WorldPoint(9d, 5d), new WorldVector(2d, 0d));

        var before = world.CreateSnapshot(new WorldRect(0d, 0d, 9.999d, 9.999d));
        Assert.AreEqual(1, before.Length);

        world.Step();

        var oldArea = world.CreateSnapshot(new WorldRect(0d, 0d, 9.999d, 9.999d));
        var newArea = world.CreateSnapshot(new WorldRect(10d, 0d, 20d, 10d));

        Assert.AreEqual(0, oldArea.Length);
        Assert.AreEqual(1, newArea.Length);
        Assert.AreEqual(id, newArea[0].Id);
    }
}
