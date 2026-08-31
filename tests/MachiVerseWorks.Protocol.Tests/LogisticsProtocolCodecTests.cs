using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class LogisticsProtocolCodecTests
{
    [TestMethod]
    public void LogisticsSnapshotRoundTripsWithProtocol211()
    {
        var message = new LogisticsSnapshotMessage(
            new ProtocolLogisticsStatistics(1, 2, 1, 1, 1, 1, 18d, 10d, 4, 7, 900),
            [new ProtocolInventory(10, 1, 8d, 20d), new ProtocolInventory(11, 1, 10d, 30d)],
            [new ProtocolShipment(5, 4, 10, 11, 1, 10d, ProtocolShipmentState.InTransit, 3, 12)]);

        var frame = LogisticsProtocolCodec.Serialize(message, ProtocolVersion.Current);

        Assert.IsTrue(LogisticsProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        var decoded = (LogisticsSnapshotMessage)envelope.Message;
        Assert.AreEqual(message.Statistics, decoded.Statistics);
        CollectionAssert.AreEqual(message.Inventories.ToArray(), decoded.Inventories.ToArray());
        CollectionAssert.AreEqual(message.Shipments.ToArray(), decoded.Shipments.ToArray());
    }

    [TestMethod]
    public void LogisticsSnapshotRequiresProtocol211()
    {
        var message = new LogisticsSnapshotMessage(
            new ProtocolLogisticsStatistics(0, 0, 0, 0, 0, 0, 0d, 0d, 0, 0, 0),
            Array.Empty<ProtocolInventory>(),
            Array.Empty<ProtocolShipment>());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LogisticsProtocolCodec.Serialize(message, new ProtocolVersion(2, 10)));
    }

    [TestMethod]
    public void LogisticsSnapshotRejectsInvalidPayloadLength()
    {
        var message = new LogisticsSnapshotMessage(
            new ProtocolLogisticsStatistics(1, 1, 0, 0, 0, 0, 1d, 0d, 0, 1, 1),
            [new ProtocolInventory(1, 1, 1d, 10d)],
            Array.Empty<ProtocolShipment>());
        var frame = LogisticsProtocolCodec.Serialize(message, ProtocolVersion.Current);
        Array.Resize(ref frame, frame.Length - 1);

        Assert.IsFalse(LogisticsProtocolCodec.TryDeserialize(frame, out _, out var error));
        Assert.AreNotEqual(ProtocolDecodeError.None, error);
    }
}
