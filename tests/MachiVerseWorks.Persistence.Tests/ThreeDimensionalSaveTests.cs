using System.Text.Json;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class ThreeDimensionalSaveTests
{
    [TestMethod]
    public void SaveRoundTripPreservesAltitudeAndVerticalVelocity()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 7, spatialCellSize: 16d));
        var id = world.CreateAgent(
            new WorldPoint(1d, 2d, 3d),
            new WorldVector(1d, 2d, 3d));
        world.Step();

        var data = WorldSaveSerializer.Serialize(world);
        using var json = JsonDocument.Parse(data);
        Assert.AreEqual(SaveFormatVersion.Current, json.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.AreEqual(2, SaveFormatVersion.Current);

        var restored = WorldSaveSerializer.Deserialize(data);
        Assert.IsTrue(restored.TryGetAgentSnapshot(id, out var snapshot));
        Assert.AreEqual(new WorldPoint(1.5d, 3d, 4.5d), snapshot.Position);
        Assert.AreEqual(new WorldVector(1d, 2d, 3d), snapshot.Velocity);
    }

    [TestMethod]
    public void SaveRestoreKeepsSameHorizontalAgentsSeparatedByAltitude()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 11, spatialCellSize: 16d));
        var lower = world.CreateAgent(new WorldPoint(25d, -40d, 5d), new WorldVector(0d, 0d, 0d));
        var upper = world.CreateAgent(new WorldPoint(25d, -40d, 85d), new WorldVector(0d, 0d, 0d));

        var restored = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));
        var lowerSnapshots = restored.CreateSnapshot(new WorldVolume(24d, -41d, 0d, 26d, -39d, 10d));
        var upperSnapshots = restored.CreateSnapshot(new WorldVolume(24d, -41d, 80d, 26d, -39d, 90d));

        Assert.AreEqual(1, lowerSnapshots.Length);
        Assert.AreEqual(lower, lowerSnapshots[0].Id);
        Assert.AreEqual(5d, lowerSnapshots[0].Position.Z);
        Assert.AreEqual(1, upperSnapshots.Length);
        Assert.AreEqual(upper, upperSnapshots[0].Id);
        Assert.AreEqual(85d, upperSnapshots[0].Position.Z);
    }
}
