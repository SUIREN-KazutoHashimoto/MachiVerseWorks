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
            (ushort)MessageType.SubscribeVolume,
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
        Assert.AreEqual((uint)0, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12, 4)));
    }

    [TestMethod]
    public void HelloAndHelloAckRoundTrip()
    {
        Assert.IsInstanceOfType<HelloMessage>(RoundTrip(new HelloMessage()).Message);
        var helloAck = new HelloAckMessage(new ProtocolVersion(2, 0), 30);
        Assert.AreEqual(helloAck, AssertMessage<HelloAckMessage>(RoundTrip(helloAck)));
    }

    [TestMethod]
    public void SubscribeVolumeRoundTripsAllAxes()
    {
        var expected = new SubscribeVolumeMessage(-125.5, -64.25, -32.5, 1024.75, 2048.5, 512.25);
        Assert.AreEqual(expected, AssertMessage<SubscribeVolumeMessage>(RoundTrip(expected)));
    }

    [TestMethod]
    public void AgentSpawnRoundTripsAllPositionAndVelocityAxes()
    {
        var expected = new AgentSpawnMessage(42, 10.5, -20.25, 30.75, 1.25, -2.5, 3.75, 9001);
        Assert.AreEqual(expected, AssertMessage<AgentSpawnMessage>(RoundTrip(expected)));
    }

    [TestMethod]
    public void AgentUpdateRoundTripsAllPositionAndVelocityAxes()
    {
        var expected = new AgentUpdateMessage(ulong.MaxValue - 1, -5000.5, 4999.25, 120.5, -0.125, 0.25, -0.5, 123456789);
        Assert.AreEqual(expected, AssertMessage<AgentUpdateMessage>(RoundTrip(expected)));
    }

    [TestMethod]
    public void AgentRemoveRoundTrips()
    {
        var expected = new AgentRemoveMessage(123, 456);
        Assert.AreEqual(expected, AssertMessage<AgentRemoveMessage>(RoundTrip(expected)));
    }

    [TestMethod]
    public void ErrorMessageRoundTripsStableCodeAndParameters()
    {
        var expected = new ProtocolErrorMessage(
            ProtocolErrorCode.UnsupportedProtocolVersion,
            [
                new ProtocolErrorParameter(ProtocolErrorParameterKeys.RequestedVersion, "3.0"),
                new ProtocolErrorParameter(ProtocolErrorParameterKeys.SupportedVersion, "2.0"),
            ]);
        var actual = AssertMessage<ProtocolErrorMessage>(RoundTrip(expected));
        Assert.AreEqual(expected.Code, actual.Code);
        CollectionAssert.AreEqual(expected.Parameters.ToArray(), actual.Parameters.ToArray());
    }

    [TestMethod]
    public void FrameLengthMismatchAndUnknownMessageTypeAreRejected()
    {
        var validFrame = ProtocolCodec.Serialize(new HelloMessage());
        var invalidLength = new byte[validFrame.Length + 1];
        validFrame.CopyTo(invalidLength, 0);
        Assert.IsFalse(ProtocolCodec.TryDeserialize(invalidLength, out _, out var lengthError));
        Assert.AreEqual(ProtocolDecodeError.FrameLengthMismatch, lengthError);

        BinaryPrimitives.WriteUInt16LittleEndian(validFrame.AsSpan(8, 2), 65000);
        Assert.IsFalse(ProtocolCodec.TryDeserialize(validFrame, out _, out var typeError));
        Assert.AreEqual(ProtocolDecodeError.UnknownMessageType, typeError);
    }

    [TestMethod]
    public void InvalidSubscribeVolumePayloadIsRejected()
    {
        var frame = ProtocolCodec.Serialize(new SubscribeVolumeMessage(0, 0, 0, 10, 10, 10));
        BinaryPrimitives.WriteInt64LittleEndian(frame.AsSpan(ProtocolFrameHeader.Size, 8), BitConverter.DoubleToInt64Bits(double.NaN));

        Assert.IsFalse(ProtocolCodec.TryDeserialize(frame, out _, out var error));
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }

    private static ProtocolEnvelope RoundTrip(IProtocolMessage message)
    {
        var frame = ProtocolCodec.Serialize(message);
        Assert.IsTrue(ProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
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
