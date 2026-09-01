using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class PowerProtocolCodecTests
{
    [TestMethod]
    public void PowerSnapshotRoundTripsAtProtocol212()
    {
        var message = new PowerSnapshotMessage(
            new ProtocolPowerStatistics(3, 2, 1, 1, 0, 20d, 8d, 8d, 8d, 0d, 42),
            [
                new ProtocolPowerNode(1, ProtocolPowerNodeKind.GeneratorBus, 0d, 0d, 0d),
                new ProtocolPowerNode(2, ProtocolPowerNodeKind.Substation, 10d, 0d, 0d),
                new ProtocolPowerNode(3, ProtocolPowerNodeKind.Load, 20d, 0d, 0d),
            ],
            [
                new ProtocolPowerLine(1, 1, 2, 20d, true),
                new ProtocolPowerLine(2, 2, 3, 10d, true),
            ],
            [new ProtocolGenerator(1, 1, 20d, 8d, ProtocolGeneratorOperatingState.Online)],
            [new ProtocolPowerLoad(1, 3, 7, 9, 10d, 8d, 8d, 0d, ProtocolPowerSupplyState.Supplied)]);

        var frame = PowerProtocolCodec.Serialize(message, new ProtocolVersion(2, 12));
        Assert.IsTrue(PowerProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(new ProtocolVersion(2, 12), envelope.Version);
        var decoded = (PowerSnapshotMessage)envelope.Message;
        Assert.AreEqual(message.Statistics, decoded.Statistics);
        CollectionAssert.AreEqual(message.Nodes.ToArray(), decoded.Nodes.ToArray());
        CollectionAssert.AreEqual(message.Lines.ToArray(), decoded.Lines.ToArray());
        CollectionAssert.AreEqual(message.Generators.ToArray(), decoded.Generators.ToArray());
        CollectionAssert.AreEqual(message.Loads.ToArray(), decoded.Loads.ToArray());
    }

    [TestMethod]
    public void PowerSnapshotRequiresProtocol212()
    {
        var message = new PowerSnapshotMessage(
            new ProtocolPowerStatistics(0, 0, 0, 0, 0, 0d, 0d, 0d, 0d, 0d, 0), [], [], [], []);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => PowerProtocolCodec.Serialize(message, new ProtocolVersion(2, 11)));
    }

    [TestMethod]
    public void DecoderRejectsInvalidPowerPayload()
    {
        var message = new PowerSnapshotMessage(
            new ProtocolPowerStatistics(1, 0, 0, 1, 1, 0d, 0d, 5d, 0d, 5d, 10),
            [new ProtocolPowerNode(1, ProtocolPowerNodeKind.Load, 0d, 0d, 0d)],
            [], [],
            [new ProtocolPowerLoad(1, 1, 1, 0, 5d, 5d, 0d, 5d, ProtocolPowerSupplyState.Outage)]);
        var frame = PowerProtocolCodec.Serialize(message, new ProtocolVersion(2, 12));
        frame[^1] = byte.MaxValue;

        Assert.IsFalse(PowerProtocolCodec.TryDeserialize(frame, out _, out var error));
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }
}
