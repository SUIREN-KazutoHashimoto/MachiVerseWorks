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
}
