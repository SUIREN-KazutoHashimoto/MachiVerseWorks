using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RadioProtocolTests
{
    [TestMethod]
    public void RadioSnapshotRoundTripsAllEntityKinds()
    {
        var message = new RadioSnapshotMessage(
            new ProtocolRadioStatistics(2, 1, 1, 1, 1, 0, 1, 0, 0, 0.5d, 28),
            [
                new ProtocolRadioSite(1, ProtocolRadioSiteKind.PointToPoint, 0d, 0d, 0d, 0d, 20d, true),
                new ProtocolRadioSite(2, ProtocolRadioSiteKind.PointToPoint, 500d, 0d, 0d, 0d, 20d, true),
            ],
            [
                new ProtocolRadioAntenna(1, 1, 0d, 0d, 20d, 1d, 0d, 0d, 12d, ProtocolRadioAntennaPatternKind.Directional, 90d, 20d, true),
                new ProtocolRadioAntenna(2, 2, 0d, 0d, 20d, -1d, 0d, 0d, 8d, ProtocolRadioAntennaPatternKind.Directional, 90d, 20d, true),
            ],
            [new ProtocolRadioTransmitter(1, 1, 1, 40d, true, true)],
            [new ProtocolRadioReceiver(1, 2, 2, 2_400d, 2_500d, -105d, true, true)],
            [new ProtocolRadioEmission(1, 1, 1, 2_450d, 20d, 36d, 0.5d, true, true)],
            [new ProtocolRadioLink(1, 1, 2, 1, 500d, 95d, -55d, -120d, 30d, 0.5d, ProtocolRadioLinkState.Healthy, true)],
            [new ProtocolRadioServiceArea(1, 1, 500d, 3d)]);

        var frame = RadioProtocolCodec.Serialize(message, ProtocolVersion.Current);
        Assert.IsTrue(RadioProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        var decoded = envelope.Message as RadioSnapshotMessage;
        Assert.IsNotNull(decoded);
        Assert.AreEqual(message.Statistics, decoded.Statistics);
        CollectionAssert.AreEqual(message.Sites.ToArray(), decoded.Sites.ToArray());
        CollectionAssert.AreEqual(message.Antennas.ToArray(), decoded.Antennas.ToArray());
        CollectionAssert.AreEqual(message.Transmitters.ToArray(), decoded.Transmitters.ToArray());
        CollectionAssert.AreEqual(message.Receivers.ToArray(), decoded.Receivers.ToArray());
        CollectionAssert.AreEqual(message.Emissions.ToArray(), decoded.Emissions.ToArray());
        CollectionAssert.AreEqual(message.Links.ToArray(), decoded.Links.ToArray());
    }

    [TestMethod]
    public void SpectrumSnapshotRoundTripsVariableStrings()
    {
        var message = new SpectrumSnapshotMessage(
            28,
            [new ProtocolSpectrumBand(1, "generic-5g", 5_000d, 5_500d)],
            [new ProtocolFrequencyBlock(1, 1, 5_200d, 20d)],
            [new ProtocolSpectrumConflict(1, 1, 1, 2, "overlappingEmissionWithinConflictDistance")]);

        var frame = RadioProtocolCodec.Serialize(message, ProtocolVersion.Current);
        Assert.IsTrue(RadioProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        var decoded = envelope!.Message as SpectrumSnapshotMessage;
        Assert.IsNotNull(decoded);
        CollectionAssert.AreEqual(message.Bands.ToArray(), decoded.Bands.ToArray());
        CollectionAssert.AreEqual(message.FrequencyBlocks.ToArray(), decoded.FrequencyBlocks.ToArray());
        CollectionAssert.AreEqual(message.Conflicts.ToArray(), decoded.Conflicts.ToArray());
    }

    [TestMethod]
    public void RadioAndSpectrumRejectDuplicateStableIdsAndDanglingReferences()
    {
        var radio = new RadioSnapshotMessage(
            new ProtocolRadioStatistics(1, 0, 0, 0, 0, 0, 0, 0, 0, 0d, 1),
            [new ProtocolRadioSite(1, ProtocolRadioSiteKind.Macro, 0, 0, 0, 0, 1, true)],
            [new ProtocolRadioAntenna(1, 1, 0, 0, 0, 1, 0, 0, 1, ProtocolRadioAntennaPatternKind.Omnidirectional, 360, 0, true)],
            [],
            [new ProtocolRadioReceiver(1, 1, 1, 100, 200, -100, true, true), new ProtocolRadioReceiver(1, 1, 1, 100, 200, -100, true, true)],
            [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RadioProtocolCodec.Serialize(radio, ProtocolVersion.Current));

        var spectrum = new SpectrumSnapshotMessage(1, [new ProtocolSpectrumBand(1, "band", 100, 200)], [new ProtocolFrequencyBlock(1, 999, 150, 10)], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RadioProtocolCodec.Serialize(spectrum, ProtocolVersion.Current));
    }

    [TestMethod]
    public void Protocol215RejectsRadioSnapshots()
    {
        var message = new RadioSnapshotMessage(new ProtocolRadioStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0d, 0), [], [], [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RadioProtocolCodec.Serialize(message, new ProtocolVersion(2, 15)));
    }
}
