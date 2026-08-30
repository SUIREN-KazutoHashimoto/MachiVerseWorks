using System.Text;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class RailwayOperationsSaveTests
{
    [TestMethod]
    public void SaveV9RoundTripPreservesRailwayOperationsAndDeterministicContinuation()
    {
        var original = new SimulationWorld(new SimulationConfig(seed: 0x1809UL));
        RailwayOperationsFixtures.SeedDeterministic(original);
        for (var tick = 0; tick < 180; tick++) original.Step();

        var json = WorldSaveSerializer.Serialize(original);
        StringAssert.Contains(Encoding.UTF8.GetString(json), "\"formatVersion\": 9");
        StringAssert.Contains(Encoding.UTF8.GetString(json), "\"railwayOperations\"");
        var restored = WorldSaveSerializer.Deserialize(json);

        for (var tick = 0; tick < 240; tick++) { original.Step(); restored.Step(); }
        var expected = original.CreateRailwayOperationsSnapshot();
        var actual = restored.CreateRailwayOperationsSnapshot();
        Assert.AreEqual(expected.Services.Length, actual.Services.Length);
        Assert.AreEqual(expected.Trains.Length, actual.Trains.Length);
        for (var index = 0; index < expected.Services.Length; index++) Assert.AreEqual(expected.Services[index], actual.Services[index]);
        for (var index = 0; index < expected.Trains.Length; index++) Assert.AreEqual(expected.Trains[index], actual.Trains[index]);
    }

    [TestMethod]
    public void RailwayInfrastructureV8MigratesWithEmptyOperations()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "phase17-railway.save.json");
        using var stream = File.OpenRead(Path.GetFullPath(path));
        var world = WorldSaveSerializer.Load(stream);
        var operations = world.CreateRailwayOperationsSnapshot();
        Assert.AreEqual(0, operations.Formations.Length);
        Assert.AreEqual(0, operations.Services.Length);
        Assert.AreEqual(0, operations.Trains.Length);
    }
}
