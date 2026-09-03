using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RegionalGenerationSnapshotChunkTests
{
    [TestMethod]
    public void LargeRegionalGenerationSnapshotRoundTripsAcrossMultipleProtocol222Frames()
    {
        var message = CreateLargeMessage();
        var version = new ProtocolVersion(2, 22);

        var chunks = RegionalGenerationSnapshotChunker.Split(message, snapshotId: 77);
        Assert.IsTrue(chunks.Count > 1, "The regression fixture must exceed one protocol frame.");

        var decoded = new List<RegionalGenerationSnapshotChunkMessage>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var frame = RegionalGenerationSnapshotChunkProtocolCodec.Serialize(chunk, version);
            Assert.IsTrue(frame.Length <= ProtocolFrameHeader.Size + ProtocolFrameHeader.MaxPayloadLength);
            Assert.IsTrue(RegionalGenerationSnapshotChunkProtocolCodec.TryDeserialize(frame, out var roundTrippedChunk, out var error));
            Assert.AreEqual(ProtocolDecodeError.None, error);
            decoded.Add(roundTrippedChunk);
        }

        decoded.Reverse();
        var assembled = RegionalGenerationSnapshotChunker.Assemble(decoded);

        Assert.AreEqual(message.WorldSeed, assembled.WorldSeed);
        Assert.AreEqual(message.TickCount, assembled.TickCount);
        Assert.AreEqual(message.Toponyms.Count, assembled.Toponyms.Count);
        Assert.AreEqual(message.Toponyms[0].Name, assembled.Toponyms[0].Name);
        Assert.AreEqual(message.Settlements[0], assembled.Settlements[0]);
        Assert.AreEqual(message.Quality, assembled.Quality);
    }

    [TestMethod]
    public void RegionalGenerationChunkCodecRequiresProtocol222()
    {
        var chunk = RegionalGenerationSnapshotChunker.Split(CreateLargeMessage(), snapshotId: 1)[0];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            RegionalGenerationSnapshotChunkProtocolCodec.Serialize(chunk, new ProtocolVersion(2, 21)));
    }

    [TestMethod]
    public void AssembleRejectsDuplicateOrIncompleteChunkSets()
    {
        var chunks = RegionalGenerationSnapshotChunker.Split(CreateLargeMessage(), snapshotId: 2);
        Assert.IsTrue(chunks.Count > 1);

        var incomplete = chunks.Take(chunks.Count - 1).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => RegionalGenerationSnapshotChunker.Assemble(incomplete));

        var duplicate = chunks.ToArray();
        duplicate[^1] = duplicate[0];
        Assert.ThrowsExactly<ArgumentException>(() => RegionalGenerationSnapshotChunker.Assemble(duplicate));
    }

    private static RegionalGenerationSnapshotMessage CreateLargeMessage()
    {
        const ulong firstToponymId = 1_000;
        var name = new string('N', 150);
        var generatorKey = new string('G', 120);
        var toponyms = Enumerable.Range(0, 4_096)
            .Select(index => new ProtocolHumanToponym(
                firstToponymId + checked((ulong)index),
                Kind: 0,
                Name: name,
                SourceNaturalToponymId: 0,
                SourceNaturalName: string.Empty,
                SourceFeatureId: 0,
                ParentHumanToponymId: 0,
                GeneratorKey: generatorKey))
            .ToArray();

        return new RegionalGenerationSnapshotMessage(
            TickCount: 42,
            WorldSeed: 123_456,
            Preset: 2,
            Iterations: 4,
            MinX: -10_000,
            MinY: -10_000,
            MinZ: -100,
            MaxX: 10_000,
            MaxY: 10_000,
            MaxZ: 1_000,
            Settlements:
            [
                new ProtocolSettlement(
                    SettlementId: 1,
                    X: 0,
                    Y: 0,
                    Z: 0,
                    Environment: 0,
                    Origin: 0,
                    Role: 0,
                    InitialEconomy: 0,
                    Suitability: new ProtocolSettlementSuitability(0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5),
                    Population: 1_000,
                    Jobs: 500,
                    InfluenceRadiusMeters: 5_000,
                    NameId: firstToponymId),
            ],
            GrowthEvents: [],
            Corridors: [],
            Districts: [],
            Parcels: [],
            Buildings: [],
            Pois: [],
            Toponyms: toponyms,
            RoadSigns: [],
            Quality: new ProtocolRegionalQualityReport(0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5));
    }
}
