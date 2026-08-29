using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class WorldSaveSerializerLimitTests
{
    [TestMethod]
    public void DeserializeRejectsInputLargerThanConfiguredByteLimit()
    {
        var limits = new WorldSaveLimits(maximumBytes: 8, maximumAgentCount: 10);
        var input = new byte[9];

        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(input, limits));
    }

    [TestMethod]
    public void LoadRejectsSeekableStreamLargerThanConfiguredByteLimitBeforeReading()
    {
        var limits = new WorldSaveLimits(maximumBytes: 32, maximumAgentCount: 10);
        using var source = new MemoryStream(new byte[33]);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Load(source, limits));
        Assert.AreEqual(0L, source.Position);
    }

    [TestMethod]
    public void AgentCountLargerThanConfiguredLimitIsRejected()
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
                  { "id": 2, "x": 1, "y": 1, "velocityX": 0, "velocityY": 0, "isActive": true }
                ]
              }
            }
            """u8.ToArray();
        var limits = new WorldSaveLimits(maximumBytes: json.Length, maximumAgentCount: 1);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(json, limits));
    }

    [TestMethod]
    public void AgentCountLimitIsAppliedBeforeAgentDtoMaterialization()
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
                "agents": [{}, {}]
              }
            }
            """u8.ToArray();
        var limits = new WorldSaveLimits(maximumBytes: json.Length, maximumAgentCount: 1);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(json, limits));

        StringAssert.Contains(exception.Message, "before deserialization");
    }

    [TestMethod]
    public void InputExactlyAtConfiguredByteLimitIsAccepted()
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
                "nextAgentId": 1,
                "agents": []
              }
            }
            """u8.ToArray();
        var limits = new WorldSaveLimits(maximumBytes: json.Length, maximumAgentCount: 1);

        var world = WorldSaveSerializer.Deserialize(json, limits);

        Assert.AreEqual(0UL, world.Time.TickCount);
        Assert.AreEqual(0, world.ActiveAgentCount);
    }
}
