using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class OpticalProtocolCodecTests
{
    [TestMethod]
    public void SnapshotRoundTripsOnProtocol215()
    {
        var message = CreateMessage();
        var frame = OpticalProtocolCodec.Serialize(message, ProtocolVersion.Current);
        Assert.IsTrue(OpticalProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        var restored = envelope.Message as OpticalSnapshotMessage;
        Assert.IsNotNull(restored);
        Assert.AreEqual(message.Statistics, restored.Statistics);
        CollectionAssert.AreEqual(message.Nodes.ToArray(), restored.Nodes.ToArray());
        CollectionAssert.AreEqual(message.FiberCables.ToArray(), restored.FiberCables.ToArray());
        CollectionAssert.AreEqual(message.Equipment.ToArray(), restored.Equipment.ToArray());
        CollectionAssert.AreEqual(message.Backhauls.ToArray(), restored.Backhauls.ToArray());
        CollectionAssert.AreEqual(message.Demands.ToArray(), restored.Demands.ToArray());
        Assert.IsTrue(restored.Demands[0].EstimatedLatencyMilliseconds > 0d);
    }

    [TestMethod]
    public void Protocol214RejectsOpticalSnapshot()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => OpticalProtocolCodec.Serialize(CreateMessage(), new ProtocolVersion(2, 14)));
    }

    private static OpticalSnapshotMessage CreateMessage() => new(
        new ProtocolOpticalStatistics(2, 1, 2, 1, 1, 1, 0, 0, 0, 10d, 4d, 4d, 0.4d, 26),
        new[] { new ProtocolOpticalNode(1, ProtocolOpticalNodeKind.BackboneGateway, 0, 0, 0), new ProtocolOpticalNode(2, ProtocolOpticalNodeKind.Endpoint, 10, 0, 1) },
        new[] { new ProtocolFiberCable(1, 1, 2, 10d, 4d, 0.4d, true, false) },
        new[] { new ProtocolOpticalEquipment(1, 1, ProtocolOpticalEquipmentKind.Router, 0, 0, 10d, false, true, true, true), new ProtocolOpticalEquipment(2, 2, ProtocolOpticalEquipmentKind.Onu, 42, 0, 10d, true, true, true, true) },
        new[] { new ProtocolOpticalBackhaul(1, 1, 10d, 4d, 0.4d, true, true) },
        new[] { new ProtocolOpticalDemand(1, 2, ProtocolOpticalDemandKind.Building, 42, 0, 4d, 4d, 4d, ProtocolOpticalQualityState.Healthy, 1, 1.5d) });
}
