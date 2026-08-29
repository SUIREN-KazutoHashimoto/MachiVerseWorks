using MachiVerseWorks.Simulation;
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
        var data = System.Text.Encoding.UTF8.GetBytes(CreateSaveJson("[{},{}]", "[]", "[]"));
        var limits = new WorldSaveLimits(maximumBytes: data.Length, maximumAgentCount: 1);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(data, limits));
    }

    [TestMethod]
    public void AgentCountLimitIsAppliedBeforeAgentDtoMaterialization()
    {
        var data = System.Text.Encoding.UTF8.GetBytes(CreateSaveJson("[{},{}]", "[]", "[]"));
        var limits = new WorldSaveLimits(maximumBytes: data.Length, maximumAgentCount: 1);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(data, limits));

        StringAssert.Contains(exception.Message, "before deserialization");
    }

    [TestMethod]
    public void BuildingCountLimitIsAppliedBeforeBuildingDtoMaterialization()
    {
        var data = System.Text.Encoding.UTF8.GetBytes(CreateSaveJson("[]", "[{},{}]", "[]"));
        var limits = new WorldSaveLimits(
            maximumBytes: data.Length,
            maximumAgentCount: 10,
            maximumBuildingCount: 1,
            maximumPoiCount: 10);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(data, limits));

        StringAssert.Contains(exception.Message, "Building count");
        StringAssert.Contains(exception.Message, "before deserialization");
    }

    [TestMethod]
    public void PoiCountLimitIsAppliedBeforePoiDtoMaterialization()
    {
        var data = System.Text.Encoding.UTF8.GetBytes(CreateSaveJson("[]", "[]", "[{},{}]"));
        var limits = new WorldSaveLimits(
            maximumBytes: data.Length,
            maximumAgentCount: 10,
            maximumBuildingCount: 10,
            maximumPoiCount: 1);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Deserialize(data, limits));

        StringAssert.Contains(exception.Message, "POI count");
        StringAssert.Contains(exception.Message, "before deserialization");
    }

    [TestMethod]
    public void InputExactlyAtConfiguredByteLimitIsAccepted()
    {
        var json = """
            {
              "formatVersion": 3,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 0,
                "randomState": 1,
                "nextAgentId": 1,
                "agents": [],
                "nextBuildingId": 1,
                "buildings": [],
                "nextPoiId": 1,
                "pois": []
              }
            }
            """u8.ToArray();
        var limits = new WorldSaveLimits(maximumBytes: json.Length, maximumAgentCount: 1);

        var world = WorldSaveSerializer.Deserialize(json, limits);

        Assert.AreEqual(0UL, world.Time.TickCount);
        Assert.AreEqual(0, world.ActiveAgentCount);
        Assert.AreEqual(0, world.BuildingCount);
        Assert.AreEqual(0, world.PoiCount);
    }

    [TestMethod]
    public void SerializeRejectsWorldAboveConfiguredAgentLimitBeforeProducingOutput()
    {
        var world = new SimulationWorld();
        world.CreateAgents(2, new WorldVolume(0, 0, 0, 1, 1, 1));
        var limits = new WorldSaveLimits(maximumBytes: 1_000_000, maximumAgentCount: 1);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(world, limits));
    }

    [TestMethod]
    public void SerializeRejectsWorldAboveConfiguredBuildingOrPoiLimit()
    {
        var world = new SimulationWorld();
        var firstBuilding = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10));
        world.CreateBuilding(new WorldVolume(20, 20, 0, 30, 30, 10));
        world.CreatePoi(new WorldPoint(1, 1, 1), buildingId: firstBuilding);
        world.CreatePoi(new WorldPoint(100, 100, 0));

        var buildingLimits = new WorldSaveLimits(
            maximumBytes: 1_000_000,
            maximumAgentCount: 10,
            maximumBuildingCount: 1,
            maximumPoiCount: 10);
        var poiLimits = new WorldSaveLimits(
            maximumBytes: 1_000_000,
            maximumAgentCount: 10,
            maximumBuildingCount: 10,
            maximumPoiCount: 1);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(world, buildingLimits));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(world, poiLimits));
    }

    [TestMethod]
    public void SerializeRejectsOutputAboveConfiguredByteLimit()
    {
        var world = new SimulationWorld();
        var baseline = WorldSaveSerializer.Serialize(
            world,
            new WorldSaveLimits(maximumBytes: 1_000_000, maximumAgentCount: 10));
        var limits = new WorldSaveLimits(
            maximumBytes: baseline.Length - 1,
            maximumAgentCount: 10);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Serialize(world, limits));
    }

    [TestMethod]
    public void SaveDoesNotWritePartialDataWhenConfiguredOutputLimitIsExceeded()
    {
        var world = new SimulationWorld();
        var baseline = WorldSaveSerializer.Serialize(
            world,
            new WorldSaveLimits(maximumBytes: 1_000_000, maximumAgentCount: 10));
        var limits = new WorldSaveLimits(
            maximumBytes: baseline.Length - 1,
            maximumAgentCount: 10);
        using var destination = new MemoryStream();

        Assert.ThrowsExactly<InvalidDataException>(() =>
            WorldSaveSerializer.Save(destination, world, limits));

        Assert.AreEqual(0L, destination.Length);
        Assert.AreEqual(0L, destination.Position);
    }

    [TestMethod]
    public void SaveProducedWithConfiguredLimitsCanLoadWithTheSameLimits()
    {
        var world = new SimulationWorld(new SimulationConfig(seed: 42));
        world.CreateAgents(2, new WorldVolume(-1, -1, -1, 1, 1, 1));
        var building = world.CreateBuilding(new WorldVolume(-10, -10, -10, 10, 10, 10));
        world.CreatePoi(new WorldPoint(0, 0, 0), buildingId: building);
        var baseline = WorldSaveSerializer.Serialize(
            world,
            new WorldSaveLimits(
                maximumBytes: 1_000_000,
                maximumAgentCount: 2,
                maximumBuildingCount: 1,
                maximumPoiCount: 1));
        var limits = new WorldSaveLimits(
            maximumBytes: baseline.Length,
            maximumAgentCount: 2,
            maximumBuildingCount: 1,
            maximumPoiCount: 1);

        var data = WorldSaveSerializer.Serialize(world, limits);
        var restored = WorldSaveSerializer.Deserialize(data, limits);

        Assert.AreEqual(world.ActiveAgentCount, restored.ActiveAgentCount);
        Assert.AreEqual(world.TotalCreatedAgentCount, restored.TotalCreatedAgentCount);
        Assert.AreEqual(world.BuildingCount, restored.BuildingCount);
        Assert.AreEqual(world.PoiCount, restored.PoiCount);
        Assert.AreEqual(world.CreateCheckpoint().RandomState, restored.CreateCheckpoint().RandomState);
    }

    private static string CreateSaveJson(string agents, string buildings, string pois)
    {
        return $$"""
            {
              "formatVersion": 3,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 0,
                "randomState": 1,
                "nextAgentId": 3,
                "agents": {{agents}},
                "nextBuildingId": 3,
                "buildings": {{buildings}},
                "nextPoiId": 3,
                "pois": {{pois}}
              }
            }
            """;
    }
}
