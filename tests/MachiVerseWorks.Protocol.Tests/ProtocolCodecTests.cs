using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class ProtocolCodecTests
{
    [TestMethod]
    public void CurrentVersionAcceptsSameMajorAndOlderMinor()
    {
        var current = new ProtocolVersion(2, 3);

        Assert.IsTrue(current.CanAccept(new ProtocolVersion(2, 3)));
        Assert.IsTrue(current.CanAccept(new ProtocolVersion(2, 1)));
        Assert.IsFalse(current.CanAccept(new ProtocolVersion(1, 9)));
        Assert.IsFalse(current.CanAccept(new ProtocolVersion(2, 4)));
    }

    [TestMethod]
    public void MessageTypeIdsRemainStable()
    {
        ushort[] expected = [1, 2, 3, 100, 101, 102, 900];
        ushort[] actual =
        [
            (ushort)MessageType.Hello,
            (ushort)MessageType.HelloAck,
            (ushort)MessageType.SubscribeArea,
            (ushort)MessageType.AgentSpawn,
            (ushort)MessageType.AgentUpdate,
            (ushort)MessageType.AgentRemove,
            (ushort)MessageType.Error,
        ];

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void HeaderUsesDocumentedLittleEndianLayout()
    {
        var frame = ProtocolCodec.Serialize(new HelloMessage());

        Assert.AreEqual(ProtocolFrameHeader.Size, frame.Length);
        Assert.AreEqual((byte)'M', frame[0]);
        Assert.AreEqual((byte)'V', frame[1]);
        Assert.AreEqual((byte)'W', frame[2]);
        Assert.AreEqual((byte)'P', frame[3]);
        Assert.AreEqual(ProtocolVersion.Current.Major, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(4, 2)));
        Assert.AreEqual(ProtocolVersion.Current.Minor, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(6, 2)));
        Assert.AreEqual((ushort)MessageType.Hello, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(8, 2)));
        Assert.AreEqual((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(10, 2)));
        Assert.AreEqual((uint)0, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12, 4)));
    }

    [TestMethod]
    public void HelloRoundTrips()
    {
        var envelope = RoundTrip(new HelloMessage());

        Assert.IsInstanceOfType<HelloMessage>(envelope.Message);
    }

    [TestMethod]
    public void HelloAckRoundTrips()
    {
        var expected = new HelloAckMessage(new ProtocolVersion(1, 0), 30);

        var actual = AssertMessage<HelloAckMessage>(RoundTrip(expected));

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SubscribeAreaRoundTrips()
    {
        var expected = new SubscribeAreaMessage(-125.5, -64.25, 1024.75, 2048.5);

        var actual = AssertMessage<SubscribeAreaMessage>(RoundTrip(expected));

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void AgentSpawnRoundTrips()
    {
        var expected = new AgentSpawnMessage(
            42,
            10.5,
            -20.25,
            1.25,
            -2.5,
            9001);

        var actual = AssertMessage<AgentSpawnMessage>(RoundTrip(expected));

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void AgentUpdateRoundTrips()
    {
        var expected = new AgentUpdateMessage(
            ulong.MaxValue - 1,
            -5000.5,
            4999.25,
            -0.125,
            0.25,
            123456789);

        var actual = AssertMessage<AgentUpdateMessage>(RoundTrip(expected));

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void AgentRemoveRoundTrips()
    {
        var expected = new AgentRemoveMessage(123, 456);

        var actual = AssertMessage<AgentRemoveMessage>(RoundTrip(expected));

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ErrorMessageRoundTripsStableCodeAndParameters()
    {
        var expected = new ProtocolErrorMessage(
            ProtocolErrorCode.UnsupportedProtocolVersion,
            new[]
            {
                new ProtocolErrorParameter(
                    ProtocolErrorParameterKeys.RequestedVersion,
                    "2.0"),
                new ProtocolErrorParameter(
                    ProtocolErrorParameterKeys.SupportedVersion,
                    "1.0"),
            });

        var actual = AssertMessage<ProtocolErrorMessage>(RoundTrip(expected));

        Assert.AreEqual(expected.Code, actual.Code);
        Assert.AreEqual(expected.Parameters.Count, actual.Parameters.Count);
        for (var index = 0; index < expected.Parameters.Count; index++)
        {
            Assert.AreEqual(expected.Parameters[index], actual.Parameters[index]);
        }
    }

    [TestMethod]
    public void FrameLengthMismatchIsRejected()
    {
        var validFrame = ProtocolCodec.Serialize(new HelloMessage());
        var invalidFrame = new byte[validFrame.Length + 1];
        validFrame.CopyTo(invalidFrame, 0);

        var success = ProtocolCodec.TryDeserialize(invalidFrame, out var envelope, out var error);

        Assert.IsFalse(success);
        Assert.IsNull(envelope);
        Assert.AreEqual(ProtocolDecodeError.FrameLengthMismatch, error);
    }

    [TestMethod]
    public void UnknownMessageTypeIsRejected()
    {
        var frame = ProtocolCodec.Serialize(new HelloMessage());
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8, 2), 65000);

        var success = ProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsFalse(success);
        Assert.IsNull(envelope);
        Assert.AreEqual(ProtocolDecodeError.UnknownMessageType, error);
    }

    [TestMethod]
    public void InvalidSubscribeAreaPayloadIsRejected()
    {
        var frame = ProtocolCodec.Serialize(new SubscribeAreaMessage(0, 0, 10, 10));
        BinaryPrimitives.WriteInt64LittleEndian(
            frame.AsSpan(ProtocolFrameHeader.Size, 8),
            BitConverter.DoubleToInt64Bits(double.NaN));

        var success = ProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsFalse(success);
        Assert.IsNull(envelope);
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }

    private static ProtocolEnvelope RoundTrip(IProtocolMessage message)
    {
        var frame = ProtocolCodec.Serialize(message);

        var success = ProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(ProtocolVersion.Current, envelope.Version);
        return envelope;
    }

    private static TMessage AssertMessage<TMessage>(ProtocolEnvelope envelope)
        where TMessage : class, IProtocolMessage
    {
        Assert.IsInstanceOfType<TMessage>(envelope.Message);
        return (TMessage)envelope.Message;
    }
}
