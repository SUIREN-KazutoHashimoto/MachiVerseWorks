using System.Text.Json;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class WorldSaveSerializerTests
{
    [TestMethod]
    public void SaveLoadRestoresExactSimulationStateAndContinuation()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 20, seed: 987654321, spatialCellSize: 32d));
        var ids = world.CreateAgents(8, new WorldRect(-100d, -100d, 100d, 100d));
        Assert.IsTrue(world.RemoveAgent(ids[2]));
        Assert.IsTrue(world.RemoveAgent(ids[6]));
        for (var tick = 0; tick < 7; tick++)
        {
            world.Step();
        }

        var serialized = WorldSaveSerializer.Serialize(world);
        var restored = WorldSaveSerializer.Deserialize(serialized);

        AssertCheckpointEqual(world.CreateCheckpoint(), restored.CreateCheckpoint());

        var originalNextId = world.CreateAgent(new WorldPoint(500d, -200d));
        var restoredNextId = restored.CreateAgent(new WorldPoint(500d, -200d));
        Assert.AreEqual(originalNextId, restoredNextId);
        Assert.IsTrue(world.TryGetAgentSnapshot(originalNextId, out var originalNext));
        Assert.IsTrue(restored.TryGetAgentSnapshot(restoredNextId, out var restoredNext));
        Assert.AreEqual(originalNext, restoredNext);
    }

    [TestMethod]
    public void StreamSaveAndLoadRoundTripsEmptyWorld()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 60, seed: 0, spatialCellSize: 16d));
        using var stream = new MemoryStream();

        WorldSaveSerializer.Save(stream, world);
        stream.Position = 0;
        var restored = WorldSaveSerializer.Load(stream);

        AssertCheckpointEqual(world.CreateCheckpoint(), restored.CreateCheckpoint());
    }

    [TestMethod]
    public void SerializedSaveContainsNoLocalizedDisplayStrings()
    {
        var world = new SimulationWorld(new SimulationConfig(seed: 42));
        world.CreateAgent(new WorldPoint(1d, 2d), new WorldVector(3d, 4d));

        var serialized = WorldSaveSerializer.Serialize(world);
        using var document = JsonDocument.Parse(serialized);

        AssertNoStringValuesOrLocalizedPropertyNames(document.RootElement);
    }

    [TestMethod]
    public void UnsupportedFormatVersionIsRejected()
    {
        var json = """
            {
              "formatVersion": 999,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 0,
                "randomState": 1,
                "nextAgentId": 1,
                "agents": []
              }
            }
            """u8.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json));
    }

    [TestMethod]
    public void MalformedJsonIsRejected()
    {
        var json = "{ not-json"u8.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json));
    }

    [TestMethod]
    public void MissingRequiredFieldIsRejected()
    {
        var json = """
            {
              "formatVersion": 1,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 0,
                "randomState": 1,
                "agents": []
              }
            }
            """u8.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json));
    }

    [TestMethod]
    public void DuplicateAgentIdsAreRejected()
    {
        var json = """
            {
              "formatVersion": 1,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 0,
                "randomState": 1,
                "nextAgentId": 3,
                "agents": [
                  { "id": 1, "x": 0, "y": 0, "velocityX": 0, "velocityY": 0, "isActive": true },
                  { "id": 1, "x": 1, "y": 1, "velocityX": 0, "velocityY": 0, "isActive": false }
                ]
              }
            }
            """u8.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json));
    }

    [TestMethod]
    public void UnknownFieldsAreRejected()
    {
        var json = """
            {
              "formatVersion": 1,
              "unexpected": 123,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 0,
                "randomState": 1,
                "nextAgentId": 1,
                "agents": []
              }
            }
            """u8.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json));
    }

    [TestMethod]
    public void MaximumTickCountIsRejected()
    {
        var json = """
            {
              "formatVersion": 1,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 18446744073709551615,
                "elapsedTicks": 0,
                "randomState": 1,
                "nextAgentId": 1,
                "agents": []
              }
            }
            """u8.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json));
    }

    [TestMethod]
    public void ElapsedTimeThatCannotAdvanceIsRejected()
    {
        var json = """
            {
              "formatVersion": 1,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 9223372036854775807,
                "randomState": 1,
                "nextAgentId": 1,
                "agents": []
              }
            }
            """u8.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json));
    }

    private static void AssertCheckpointEqual(SimulationCheckpoint expected, SimulationCheckpoint actual)
    {
        Assert.AreEqual(expected.TickRate, actual.TickRate);
        Assert.AreEqual(expected.Seed, actual.Seed);
        Assert.AreEqual(expected.SpatialCellSize, actual.SpatialCellSize);
        Assert.AreEqual(expected.TickCount, actual.TickCount);
        Assert.AreEqual(expected.ElapsedTicks, actual.ElapsedTicks);
        Assert.AreEqual(expected.RandomState, actual.RandomState);
        Assert.AreEqual(expected.NextAgentId, actual.NextAgentId);
        CollectionAssert.AreEqual(expected.Agents.ToArray(), actual.Agents.ToArray());
    }

    private static void AssertNoStringValuesOrLocalizedPropertyNames(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Assert.IsFalse(property.Name.Contains("locale", StringComparison.OrdinalIgnoreCase));
                    Assert.IsFalse(property.Name.Contains("label", StringComparison.OrdinalIgnoreCase));
                    Assert.IsFalse(property.Name.Contains("display", StringComparison.OrdinalIgnoreCase));
                    AssertNoStringValuesOrLocalizedPropertyNames(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AssertNoStringValuesOrLocalizedPropertyNames(item);
                }
                break;
            case JsonValueKind.String:
                Assert.Fail("Save Data must not contain localized display string values.");
                break;
        }
    }
}
