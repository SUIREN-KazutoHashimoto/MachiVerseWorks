using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class WaterSewerProtocolCodecTests
{
    [TestMethod]
    public void SnapshotRoundTripsOnProtocol213()
    {
        var message = CreateMessage();

        var frame = WaterSewerProtocolCodec.Serialize(message, ProtocolVersion.Current);
        var decoded = WaterSewerProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsTrue(decoded);
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(ProtocolVersion.Current, envelope.Version);
        var restored = envelope.Message as WaterSewerSnapshotMessage;
        Assert.IsNotNull(restored);
        Assert.AreEqual(message.Statistics, restored.Statistics);
        CollectionAssert.AreEqual(message.Nodes.ToArray(), restored.Nodes.ToArray());
        CollectionAssert.AreEqual(message.Pipes.ToArray(), restored.Pipes.ToArray());
        CollectionAssert.AreEqual(message.Facilities.ToArray(), restored.Facilities.ToArray());
        CollectionAssert.AreEqual(message.ServicePoints.ToArray(), restored.ServicePoints.ToArray());
    }

    [TestMethod]
    public void Protocol212RejectsWaterSewerSnapshot()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WaterSewerProtocolCodec.Serialize(CreateMessage(), new ProtocolVersion(2, 12)));
    }

    [TestMethod]
    public void DecoderRejectsInvalidServiceState()
    {
        var frame = WaterSewerProtocolCodec.Serialize(CreateMessage(), ProtocolVersion.Current);
        frame[^1] = byte.MaxValue;

        Assert.IsFalse(WaterSewerProtocolCodec.TryDeserialize(frame, out _, out var error));
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }

    private static WaterSewerSnapshotMessage CreateMessage() => new(
        new ProtocolWaterSewerStatistics(
            2, 1, 2, 1, 1, 0, 1, 1, 1, 0, 0, 0,
            100d, 10d, 10d, 9d, 9d, 0d, 24),
        new[]
        {
            new ProtocolUtilityNode(ProtocolUtilityNetworkKind.Water, 1, ProtocolUtilityNodeKind.Source, 0d, 0d, 0d),
            new ProtocolUtilityNode(ProtocolUtilityNetworkKind.Water, 2, ProtocolUtilityNodeKind.Service, 10d, 0d, 0d),
            new ProtocolUtilityNode(ProtocolUtilityNetworkKind.Sewer, 1, ProtocolUtilityNodeKind.Service, 10d, 0d, -2d),
            new ProtocolUtilityNode(ProtocolUtilityNetworkKind.Sewer, 2, ProtocolUtilityNodeKind.Treatment, 20d, 0d, -2d),
        },
        new[]
        {
            new ProtocolUtilityPipe(ProtocolUtilityNetworkKind.Water, 1, 1, 2, 100d, true),
            new ProtocolUtilityPipe(ProtocolUtilityNetworkKind.Sewer, 1, 1, 2, 100d, true),
        },
        new[]
        {
            new ProtocolUtilityFacility(ProtocolUtilityFacilityKind.WaterSource, 1, 1, 0, 100d, 10d, ProtocolUtilityOperatingState.Online),
            new ProtocolUtilityFacility(ProtocolUtilityFacilityKind.SewageTreatmentPlant, 1, 2, 0, 100d, 9d, ProtocolUtilityOperatingState.Online),
        },
        new[]
        {
            new ProtocolWaterSewerServicePoint(
                1, 2, 1, 42, 0, 10d, 0.9d, 10d, 10d, 0d, ProtocolWaterServiceState.Supplied,
                9d, 9d, 0d, ProtocolSewerServiceState.Available),
        });
}
