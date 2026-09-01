using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class GasProtocolCodecTests
{
    [TestMethod]
    public void SnapshotRoundTripsOnProtocol214()
    {
        var message = CreateMessage();

        var frame = GasProtocolCodec.Serialize(message, ProtocolVersion.Current);
        var decoded = GasProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsTrue(decoded);
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(ProtocolVersion.Current, envelope.Version);
        var restored = envelope.Message as GasSnapshotMessage;
        Assert.IsNotNull(restored);
        Assert.AreEqual(message.Statistics, restored.Statistics);
        CollectionAssert.AreEqual(message.Nodes.ToArray(), restored.Nodes.ToArray());
        CollectionAssert.AreEqual(message.Pipelines.ToArray(), restored.Pipelines.ToArray());
        CollectionAssert.AreEqual(message.Facilities.ToArray(), restored.Facilities.ToArray());
        CollectionAssert.AreEqual(message.ServicePoints.ToArray(), restored.ServicePoints.ToArray());
    }

    [TestMethod]
    public void Protocol213RejectsGasSnapshot()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GasProtocolCodec.Serialize(CreateMessage(), new ProtocolVersion(2, 13)));
    }

    [TestMethod]
    public void DecoderRejectsInvalidServiceState()
    {
        var frame = GasProtocolCodec.Serialize(CreateMessage(), ProtocolVersion.Current);
        frame[^1] = byte.MaxValue;

        Assert.IsFalse(GasProtocolCodec.TryDeserialize(frame, out _, out var error));
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }

    private static GasSnapshotMessage CreateMessage() => new(
        new ProtocolGasStatistics(
            2, 1, 1, 0, 1, 2, 1, 1, 0,
            110d, 18d, 18d, 0d, 40d, 24),
        new[]
        {
            new ProtocolGasNode(1, ProtocolGasNodeKind.Source, 0d, 0d, 0d),
            new ProtocolGasNode(2, ProtocolGasNodeKind.Service, 10d, 0d, 0d),
        },
        new[]
        {
            new ProtocolGasPipeline(1, 1, 2, 100d, true),
        },
        new[]
        {
            new ProtocolGasFacility(ProtocolGasFacilityKind.Source, 1, 1, 100d, 10d, 0d, ProtocolGasOperatingState.Online),
            new ProtocolGasFacility(ProtocolGasFacilityKind.Storage, 1, 1, 10d, 0d, 40d, ProtocolGasOperatingState.Online),
        },
        new[]
        {
            new ProtocolGasServicePoint(1, 2, 42, 0, ProtocolGasDeliveryMode.Piped, 0, 10d, 10d, 10d, 0d, ProtocolGasServiceState.Supplied),
            new ProtocolGasServicePoint(2, 0, 43, 7, ProtocolGasDeliveryMode.Delivered, 3, 8d, 8d, 8d, 0d, ProtocolGasServiceState.Supplied),
        });
}
