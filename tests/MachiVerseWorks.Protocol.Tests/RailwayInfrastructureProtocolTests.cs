using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RailwayInfrastructureProtocolTests
{
    [TestMethod]
    public void SnapshotRoundTripPreservesAllRailwayEntityKinds()
    {
        var expected = new RailwayInfrastructureSnapshotMessage(
            42,
            true,
            [new ProtocolTrackNode(1, 2, 1d, 2d, 3d), new ProtocolTrackNode(2, 0, 4d, 5d, 6d)],
            [new ProtocolTrackSegment(10, 1, 2, ProtocolTrackDirection.StartToEnd, 1.067d, 22d, ProtocolTrackElectrification.Overhead, ProtocolTrackUsage.Mainline)],
            [new ProtocolTrackConnection(20, 10, 11, 1)],
            [new ProtocolBlockSection(30, [10])],
            [new ProtocolStation(40, -1d, -2d, -3d, 10d, 20d, 30d)],
            [new ProtocolPlatform(50, 40, 10, 0.1d, 0.8d, 0d, 1d, 2d, 3d, 4d, 5d)],
            [new ProtocolPlatformAccessPoint(60, 50, 70)],
            [new ProtocolDepot(80, -5d, -5d, -1d, 5d, 5d, 2d, [10])]);

        var frame = RailwayInfrastructureProtocolCodec.Serialize(expected, ProtocolVersion.Current);
        var success = RailwayInfrastructureProtocolCodec.TryDeserialize(frame, out var actual, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.AreEqual(expected.Revision, actual.Revision);
        Assert.AreEqual(expected.IsFullSnapshot, actual.IsFullSnapshot);
        Assert.AreEqual(expected.Nodes[0], actual.Nodes[0]);
        Assert.AreEqual(expected.Segments[0], actual.Segments[0]);
        Assert.AreEqual(expected.Stations[0], actual.Stations[0]);
        Assert.AreEqual(expected.Platforms[0], actual.Platforms[0]);
        Assert.AreEqual(expected.PlatformAccessPoints[0], actual.PlatformAccessPoints[0]);
        CollectionAssert.AreEqual(expected.Blocks[0].SegmentIds.ToArray(), actual.Blocks[0].SegmentIds.ToArray());
        CollectionAssert.AreEqual(expected.Depots[0].TrackSegmentIds.ToArray(), actual.Depots[0].TrackSegmentIds.ToArray());
    }

    [TestMethod]
    public void OversizedSnapshotSplitsIntoSerializableChunks()
    {
        var nodes = Enumerable.Range(1, 40_000)
            .Select(static id => new ProtocolTrackNode((ulong)id, 0, id, 0d, 0d))
            .ToArray();
        var message = new RailwayInfrastructureSnapshotMessage(77, true, nodes, [], [], [], [], [], [], []);

        var chunks = RailwayInfrastructureProtocolChunker.Split(message);

        Assert.IsTrue(chunks.Count > 1);
        Assert.IsTrue(chunks[0].IsFullSnapshot);
        Assert.IsTrue(chunks.Skip(1).All(static chunk => !chunk.IsFullSnapshot));
        Assert.AreEqual(nodes.Length, chunks.Sum(static chunk => chunk.Nodes.Count));
        foreach (var chunk in chunks)
        {
            var frame = RailwayInfrastructureProtocolCodec.Serialize(chunk, ProtocolVersion.Current);
            Assert.IsTrue(frame.LongLength <= ProtocolFrameHeader.Size + (long)ProtocolFrameHeader.MaxPayloadLength);
        }
    }

    [TestMethod]
    public void ProtocolTwentyFiveRejectsRailwaySnapshots()
    {
        var message = new RailwayInfrastructureSnapshotMessage(1, true, [], [], [], [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RailwayInfrastructureProtocolCodec.Serialize(message, new ProtocolVersion(2, 5)));
    }

    [TestMethod]
    public void InvalidPayloadIsRejectedWithoutMaterializingMessage()
    {
        var message = new RailwayInfrastructureSnapshotMessage(1, true, [new ProtocolTrackNode(1, 0, 0d, 0d, 0d)], [], [], [], [], [], [], []);
        var frame = RailwayInfrastructureProtocolCodec.Serialize(message, ProtocolVersion.Current);
        frame[ProtocolFrameHeader.Size + sizeof(ulong)] = 2;

        var success = RailwayInfrastructureProtocolCodec.TryDeserialize(frame, out _, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }

    [TestMethod]
    public void RailwayInfrastructureRejectsDuplicateStableIdsWithinAFrame()
    {
        var message = new RailwayInfrastructureSnapshotMessage(
            1, true,
            [new ProtocolTrackNode(1, 0, 0, 0, 0), new ProtocolTrackNode(1, 0, 1, 0, 0)],
            [], [], [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RailwayInfrastructureProtocolCodec.Serialize(message, ProtocolVersion.Current));
    }

    [TestMethod]
    public void RailwayInfrastructureChunkerRejectsDanglingAggregateTopology()
    {
        var message = new RailwayInfrastructureSnapshotMessage(
            1, true,
            [new ProtocolTrackNode(1, 0, 0, 0, 0)],
            [new ProtocolTrackSegment(10, 1, 999, ProtocolTrackDirection.Bidirectional, 1.067, 20, ProtocolTrackElectrification.None, ProtocolTrackUsage.Mainline)],
            [], [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RailwayInfrastructureProtocolChunker.Split(message));
    }

}
