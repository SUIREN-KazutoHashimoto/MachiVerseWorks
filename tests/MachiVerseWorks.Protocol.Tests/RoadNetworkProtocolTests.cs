using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RoadNetworkProtocolTests
{
    [TestMethod]
    public void RoadNetworkSnapshotRoundTripsOnProtocolTwoOne()
    {
        var expected = CreateValidMessage();
        var bytes = ProtocolCodec.Serialize(expected, new ProtocolVersion(2, 1));
        Assert.IsTrue(ProtocolCodec.TryDeserialize(bytes, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        var actual = envelope.Message as RoadNetworkSnapshotMessage;
        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.TickCount, actual.TickCount);
        CollectionAssert.AreEqual(expected.Nodes.ToArray(), actual.Nodes.ToArray());
        CollectionAssert.AreEqual(expected.Segments.ToArray(), actual.Segments.ToArray());
        CollectionAssert.AreEqual(expected.Lanes.ToArray(), actual.Lanes.ToArray());
        CollectionAssert.AreEqual(expected.Connections.ToArray(), actual.Connections.ToArray());
        CollectionAssert.AreEqual(expected.AccessPoints.ToArray(), actual.AccessPoints.ToArray());
    }

    [TestMethod]
    public void ProtocolTwoZeroCannotCarryRoadNetworkSnapshot()
    {
        var message = new RoadNetworkSnapshotMessage(0, [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(message, new ProtocolVersion(2, 0)));
    }

    [TestMethod]
    public void SerializerRejectsDuplicateRoadEntityIds()
    {
        var duplicateNode = CreateValidMessage() with
        {
            Nodes =
            [
                new ProtocolRoadNode(1, ProtocolRoadNodeKind.Intersection, 0, 0, 0),
                new ProtocolRoadNode(1, ProtocolRoadNodeKind.Endpoint, 10, 0, 0),
                new ProtocolRoadNode(3, ProtocolRoadNodeKind.Endpoint, 20, 0, 0),
            ],
        };

        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(duplicateNode, new ProtocolVersion(2, 1)));
    }

    [TestMethod]
    public void SerializerRejectsDanglingRoadTopologyReferences()
    {
        var valid = CreateValidMessage();
        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(valid with
        {
            Segments = [new ProtocolRoadSegment(1, ProtocolRoadKind.Local, 99, 2), new ProtocolRoadSegment(2, ProtocolRoadKind.Local, 1, 3)],
        }, new ProtocolVersion(2, 1)));
        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(valid with
        {
            Lanes = [new ProtocolLane(1, 99, ProtocolLaneDirection.Forward, 0, 3, 10), new ProtocolLane(2, 2, ProtocolLaneDirection.Forward, 0, 3, 10)],
        }, new ProtocolVersion(2, 1)));
        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(valid with
        {
            Connections = [new ProtocolLaneConnection(1, 1, 99, 1, ProtocolTurnMovement.Straight)],
        }, new ProtocolVersion(2, 1)));
        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(valid with
        {
            Connections = [new ProtocolLaneConnection(1, 1, 2, 99, ProtocolTurnMovement.Straight)],
        }, new ProtocolVersion(2, 1)));
        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(valid with
        {
            AccessPoints = [new ProtocolRoadAccessPoint(1, 99, 0.5, 7, 0, ProtocolRoadAccessMode.Motor)],
        }, new ProtocolVersion(2, 1)));
    }

    [TestMethod]
    public void DecoderRejectsDuplicateNodeIdInWireFrame()
    {
        var frame = ProtocolCodec.Serialize(CreateValidMessage(), new ProtocolVersion(2, 1));
        const int firstNodeOffset = ProtocolFrameHeader.Size + 28;
        const int secondNodeOffset = firstNodeOffset + 33;
        BinaryPrimitives.WriteUInt64LittleEndian(frame.AsSpan(secondNodeOffset, sizeof(ulong)), 1UL);

        Assert.IsFalse(ProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.IsNull(envelope);
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }

    [TestMethod]
    public void DecoderRejectsDanglingReferencesInWireFrame()
    {
        var segmentFrame = ProtocolCodec.Serialize(CreateValidMessage(), new ProtocolVersion(2, 1));
        const int recordsOffset = ProtocolFrameHeader.Size + 28;
        const int segmentOffset = recordsOffset + 3 * 33;
        BinaryPrimitives.WriteUInt64LittleEndian(segmentFrame.AsSpan(segmentOffset + 9, sizeof(ulong)), 99UL);
        AssertInvalidPayload(segmentFrame);

        var laneFrame = ProtocolCodec.Serialize(CreateValidMessage(), new ProtocolVersion(2, 1));
        const int laneOffset = segmentOffset + 2 * 25;
        BinaryPrimitives.WriteUInt64LittleEndian(laneFrame.AsSpan(laneOffset + 8, sizeof(ulong)), 99UL);
        AssertInvalidPayload(laneFrame);

        var connectionFrame = ProtocolCodec.Serialize(CreateValidMessage(), new ProtocolVersion(2, 1));
        const int connectionOffset = laneOffset + 2 * 35;
        BinaryPrimitives.WriteUInt64LittleEndian(connectionFrame.AsSpan(connectionOffset + 16, sizeof(ulong)), 99UL);
        AssertInvalidPayload(connectionFrame);

        var viaNodeFrame = ProtocolCodec.Serialize(CreateValidMessage(), new ProtocolVersion(2, 1));
        BinaryPrimitives.WriteUInt64LittleEndian(viaNodeFrame.AsSpan(connectionOffset + 24, sizeof(ulong)), 99UL);
        AssertInvalidPayload(viaNodeFrame);

        var accessFrame = ProtocolCodec.Serialize(CreateValidMessage(), new ProtocolVersion(2, 1));
        const int accessOffset = connectionOffset + 33;
        BinaryPrimitives.WriteUInt64LittleEndian(accessFrame.AsSpan(accessOffset + 8, sizeof(ulong)), 99UL);
        AssertInvalidPayload(accessFrame);
    }

    [TestMethod]
    public void SingleFrameBudgetReportsRoadPayloadBoundary()
    {
        var below = new RoadNetworkSnapshotMessage(
            1,
            Enumerable.Range(1, 31_774).Select(index => new ProtocolRoadNode((ulong)index, ProtocolRoadNodeKind.Endpoint, index, 0, 0)).ToArray(),
            [], [], [], []);
        var above = below with
        {
            Nodes = Enumerable.Range(1, 31_775).Select(index => new ProtocolRoadNode((ulong)index, ProtocolRoadNodeKind.Endpoint, index, 0, 0)).ToArray(),
        };

        Assert.IsTrue(ProtocolCodec.FitsSingleFrame(below));
        Assert.IsFalse(ProtocolCodec.FitsSingleFrame(above));
        Assert.IsTrue(ProtocolCodec.GetPayloadLength(below) <= ProtocolFrameHeader.MaxPayloadLength);
        Assert.IsTrue(ProtocolCodec.GetPayloadLength(above) > ProtocolFrameHeader.MaxPayloadLength);
    }

    private static RoadNetworkSnapshotMessage CreateValidMessage() => new(
        42,
        [
            new ProtocolRoadNode(1, ProtocolRoadNodeKind.Intersection, 0, 0, 0),
            new ProtocolRoadNode(2, ProtocolRoadNodeKind.Endpoint, 10, 0, 0),
            new ProtocolRoadNode(3, ProtocolRoadNodeKind.Endpoint, 0, 10, 0),
        ],
        [
            new ProtocolRoadSegment(1, ProtocolRoadKind.Local, 2, 1),
            new ProtocolRoadSegment(2, ProtocolRoadKind.Local, 1, 3),
        ],
        [
            new ProtocolLane(1, 1, ProtocolLaneDirection.Forward, 0, 3, 10),
            new ProtocolLane(2, 2, ProtocolLaneDirection.Forward, 0, 3, 10),
        ],
        [new ProtocolLaneConnection(1, 1, 2, 1, ProtocolTurnMovement.Straight)],
        [new ProtocolRoadAccessPoint(1, 1, 0.5, 7, 0, ProtocolRoadAccessMode.Motor)]);

    private static void AssertInvalidPayload(byte[] frame)
    {
        Assert.IsFalse(ProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.IsNull(envelope);
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }
}
