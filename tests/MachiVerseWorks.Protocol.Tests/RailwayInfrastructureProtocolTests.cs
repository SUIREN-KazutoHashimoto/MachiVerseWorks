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
        frame[^1] = 0xff;

        var success = RailwayInfrastructureProtocolCodec.TryDeserialize(frame, out _, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }
}
