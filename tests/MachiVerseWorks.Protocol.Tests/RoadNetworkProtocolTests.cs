using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RoadNetworkProtocolTests
{
    [TestMethod]
    public void RoadNetworkSnapshotRoundTripsOnProtocolTwoOne()
    {
        var expected = new RoadNetworkSnapshotMessage(42,
            [new ProtocolRoadNode(1, ProtocolRoadNodeKind.Intersection, 10, 20, -5), new ProtocolRoadNode(2, ProtocolRoadNodeKind.Endpoint, 30, 40, 15)],
            [new ProtocolRoadSegment(1, ProtocolRoadKind.Arterial, 1, 2)],
            [new ProtocolLane(1, 1, ProtocolLaneDirection.Forward, 0, 3.5, 16.7)],
            [], [new ProtocolRoadAccessPoint(1, 1, 0.25, 7, 0, ProtocolRoadAccessMode.Motor)]);
        var bytes = ProtocolCodec.Serialize(expected, new ProtocolVersion(2, 1));
        Assert.IsTrue(ProtocolCodec.TryDeserialize(bytes, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error); Assert.IsNotNull(envelope);
        var actual = envelope.Message as RoadNetworkSnapshotMessage; Assert.IsNotNull(actual);
        Assert.AreEqual(expected.TickCount, actual.TickCount);
        CollectionAssert.AreEqual(expected.Nodes.ToArray(), actual.Nodes.ToArray()); CollectionAssert.AreEqual(expected.Segments.ToArray(), actual.Segments.ToArray());
        CollectionAssert.AreEqual(expected.Lanes.ToArray(), actual.Lanes.ToArray()); CollectionAssert.AreEqual(expected.AccessPoints.ToArray(), actual.AccessPoints.ToArray());
    }

    [TestMethod]
    public void ProtocolTwoZeroCannotCarryRoadNetworkSnapshot()
    {
        var message = new RoadNetworkSnapshotMessage(0, [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(message, new ProtocolVersion(2, 0)));
    }
}
