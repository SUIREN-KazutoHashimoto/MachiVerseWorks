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
        var actual = envelope.Message as WorldEnvironmentSnapshotMessage;
        Assert.IsNotNull(actual);
        Assert.AreEqual(message.TickCount, actual.TickCount);
        Assert.AreEqual(message.Config, actual.Config);
        Assert.AreEqual(message.MinX, actual.MinX);
        Assert.AreEqual(message.MinY, actual.MinY);
        Assert.AreEqual(message.MinZ, actual.MinZ);
        Assert.AreEqual(message.MaxX, actual.MaxX);
        Assert.AreEqual(message.MaxY, actual.MaxY);
        Assert.AreEqual(message.MaxZ, actual.MaxZ);
        CollectionAssert.AreEqual(message.Samples.ToArray(), actual.Samples.ToArray());
        CollectionAssert.AreEqual(message.TerrainSamples.ToArray(), actual.TerrainSamples.ToArray());
        Assert.AreEqual(message.Features.Count, actual.Features.Count);
        for (var index = 0; index < message.Features.Count; index++)
        {
            var expectedFeature = message.Features[index];
            var actualFeature = actual.Features[index];
            Assert.AreEqual(expectedFeature.FeatureId, actualFeature.FeatureId);
            Assert.AreEqual(expectedFeature.FeatureType, actualFeature.FeatureType);
            Assert.AreEqual(expectedFeature.MinX, actualFeature.MinX);
            Assert.AreEqual(expectedFeature.MinY, actualFeature.MinY);
            Assert.AreEqual(expectedFeature.MinZ, actualFeature.MinZ);
            Assert.AreEqual(expectedFeature.MaxX, actualFeature.MaxX);
            Assert.AreEqual(expectedFeature.MaxY, actualFeature.MaxY);
            Assert.AreEqual(expectedFeature.MaxZ, actualFeature.MaxZ);
            Assert.AreEqual(expectedFeature.AreaSquareMeters, actualFeature.AreaSquareMeters);
            Assert.AreEqual(expectedFeature.ParentFeatureId, actualFeature.ParentFeatureId);
            Assert.AreEqual(expectedFeature.MinimumElevationMeters, actualFeature.MinimumElevationMeters);
            Assert.AreEqual(expectedFeature.MaximumElevationMeters, actualFeature.MaximumElevationMeters);
            CollectionAssert.AreEqual(expectedFeature.Geometry.ToArray(), actualFeature.Geometry.ToArray());
        }
        Assert.AreEqual(message.Toponyms.Count, actual.Toponyms.Count);
        for (var index = 0; index < message.Toponyms.Count; index++)
        {
            var expectedToponym = message.Toponyms[index];
            var actualToponym = actual.Toponyms[index];
            Assert.AreEqual(expectedToponym.ToponymId, actualToponym.ToponymId);
            Assert.AreEqual(expectedToponym.FeatureId, actualToponym.FeatureId);
            Assert.AreEqual(expectedToponym.Name, actualToponym.Name);
            Assert.AreEqual(expectedToponym.ProvenanceKind, actualToponym.ProvenanceKind);
            Assert.AreEqual(expectedToponym.SourceFeatureId, actualToponym.SourceFeatureId);
            Assert.AreEqual(expectedToponym.ParentToponymId, actualToponym.ParentToponymId);
            Assert.AreEqual(expectedToponym.GeneratorKey, actualToponym.GeneratorKey);
        }
    }

    [TestMethod]
    public void ProtocolBefore217RejectsEnvironmentSnapshot()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WorldEnvironmentProtocolCodec.Serialize(CreateMessage(), new ProtocolVersion(2, 16)));
    }

    [TestMethod]
    public void InvalidEnvironmentDiscriminantsAreRejected()
    {
        var message = CreateMessage();
        var invalidLandform = message with
        {
            Samples = new[] { message.Samples[0] with { Landform = byte.MaxValue } },
        };
        var invalidWater = message with
        {
            TerrainSamples = new[] { message.TerrainSamples[0] with { SurfaceWater = byte.MaxValue } },
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WorldEnvironmentProtocolCodec.Serialize(invalidLandform, ProtocolVersion.Current));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WorldEnvironmentProtocolCodec.Serialize(invalidWater, ProtocolVersion.Current));
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
