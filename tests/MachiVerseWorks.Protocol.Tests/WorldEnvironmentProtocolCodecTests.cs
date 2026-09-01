using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class WorldEnvironmentProtocolCodecTests
{
    [TestMethod]
    public void RoundTripPreservesEnvironmentTerrainFeatureAndToponym()
    {
        var message = CreateMessage();

        var frame = WorldEnvironmentProtocolCodec.Serialize(message, ProtocolVersion.Current);
        var success = WorldEnvironmentProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(ProtocolVersion.Current, envelope.Version);
        Assert.AreEqual(message, envelope.Message);
    }

    [TestMethod]
    public void ProtocolBefore217RejectsEnvironmentSnapshot()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            WorldEnvironmentProtocolCodec.Serialize(CreateMessage(), new ProtocolVersion(2, 16)));
    }

    [TestMethod]
    public void DecoderRejectsTruncatedFrame()
    {
        var frame = WorldEnvironmentProtocolCodec.Serialize(CreateMessage(), ProtocolVersion.Current);
        var truncated = frame[..^1];

        Assert.IsFalse(WorldEnvironmentProtocolCodec.TryDeserialize(truncated, out _, out var error));
        Assert.AreEqual(ProtocolDecodeError.FrameLengthMismatch, error);
    }

    private static WorldEnvironmentSnapshotMessage CreateMessage() => new(
        29UL,
        new ProtocolEnvironmentConfig(29UL, 0d, 1d, 45d, 0, 0d, 0.55d, 0.45d, 11d, 20d, 900d, 0d, false, 250_000d, 512d),
        -1_000d, -1_000d, -500d, 1_000d, 1_000d, 2_000d,
        new[]
        {
            new ProtocolEnvironmentSample(10d, 20d, 120d, 1, 8_000d, 45.1d, 10d, 18d, 950d, 0.4d, 0.55d, 3, 0.7d, 0.6d, 0.2d, 1d, 0d, 0.25d, 0.8d, 0.75d),
        },
        new[]
        {
            new ProtocolTerrainSurfaceSample(10d, 20d, 125d, 0d, 0d, 1d, 4d, 0.1d, 2, 0),
        },
        new[]
        {
            new ProtocolGeographicFeature(100UL, 0, 0d, 0d, 0d, 100d, 100d, 500d, 10_000d, 0UL, 0d, 500d,
                new[] { new ProtocolWorldPoint(0d, 0d, 100d), new ProtocolWorldPoint(100d, 0d, 200d), new ProtocolWorldPoint(50d, 100d, 500d) }),
        },
        new[]
        {
            new ProtocolNaturalToponym(200UL, 100UL, "Aru Peak", 0, 100UL, 0UL, "phase29-natural-v1"),
        });
}
