using System.Text.Json;
using MachiVerseWorks.Protocol;
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
        var id = world.CreateAgent(new WorldPoint(1d, 2d, 3d), new WorldVector(1d, 2d, 3d));
        world.Step();

        var data = WorldSaveSerializer.Serialize(world);
        using var json = JsonDocument.Parse(data);
        Assert.AreEqual(SaveFormatVersion.Current, json.RootElement.GetProperty("formatVersion").GetInt32());

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
        Assert.AreEqual(1, upperSnapshots.Length);
        Assert.AreEqual(upper, upperSnapshots[0].Id);
    }

    [TestMethod]
    public void SaveRestoreStateCanFlowThroughThreeDimensionalProtocolWithoutLosingAltitude()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 19, spatialCellSize: 16d));
        world.CreateAgent(new WorldPoint(25d, -40d, -320d), new WorldVector(1d, 2d, -3d));
        world.CreateAgent(new WorldPoint(25d, -40d, 850d), new WorldVector(4d, 5d, 6d));

        var restored = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));
        var snapshots = restored.CreateSnapshot(new WorldVolume(20d, -45d, -400d, 30d, -35d, 900d));
        Assert.AreEqual(2, snapshots.Length);

        var decodedMessages = snapshots
            .Select(static snapshot => new AgentSpawnMessage(
                snapshot.Id.Value,
                snapshot.Position.X,
                snapshot.Position.Y,
                snapshot.Position.Z,
                snapshot.Velocity.X,
                snapshot.Velocity.Y,
                snapshot.Velocity.Z,
                snapshot.TickCount))
            .Select(static message => ProtocolCodec.Serialize(message))
            .Select(static frame => DecodeAgentSpawn(frame))
            .OrderBy(static message => message.Z)
            .ToArray();

        Assert.AreEqual(2, decodedMessages.Length);
        Assert.AreEqual(25d, decodedMessages[0].X);
        Assert.AreEqual(25d, decodedMessages[1].X);
        Assert.AreEqual(-40d, decodedMessages[0].Y);
        Assert.AreEqual(-40d, decodedMessages[1].Y);
        Assert.AreEqual(-320d, decodedMessages[0].Z);
        Assert.AreEqual(850d, decodedMessages[1].Z);
        Assert.AreEqual(-3d, decodedMessages[0].VelocityZ);
        Assert.AreEqual(6d, decodedMessages[1].VelocityZ);
    }

    private static AgentSpawnMessage DecodeAgentSpawn(byte[] frame)
    {
        Assert.IsTrue(ProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.IsInstanceOfType<AgentSpawnMessage>(envelope.Message);
        return (AgentSpawnMessage)envelope.Message;
    }
}
