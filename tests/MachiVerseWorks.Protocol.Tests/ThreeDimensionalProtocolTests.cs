using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class ThreeDimensionalProtocolTests
{
    [TestMethod]
    public void ProtocolMajorVersionIsTwoForNativeThreeDimensionalWireContract()
    {
        Assert.AreEqual((ushort)2, ProtocolVersion.Current.Major);
    }

    [TestMethod]
    public void SubscribeVolumeRoundTripsAllSixBounds()
    {
        var expected = new SubscribeVolumeMessage(-10d, -20d, -30d, 40d, 50d, 60d);
        var frame = ProtocolCodec.Serialize(expected);

        Assert.AreEqual(ProtocolFrameHeader.Size + 48, frame.Length);
        Assert.IsTrue(ProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(expected, envelope.Message);
    }

    [TestMethod]
    public void AgentStateWireLayoutIncludesAltitudeAndVerticalVelocity()
    {
        var expected = new AgentUpdateMessage(42UL, 1.25d, 2.5d, 3.75d, -4d, -5d, -6d, 77UL);
        var frame = ProtocolCodec.Serialize(expected);
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);

        Assert.AreEqual(64, payload.Length);
        Assert.AreEqual(42UL, BinaryPrimitives.ReadUInt64LittleEndian(payload));
        Assert.AreEqual(3.75d, ReadDouble(payload[24..]));
        Assert.AreEqual(-6d, ReadDouble(payload[48..]));
        Assert.AreEqual(77UL, BinaryPrimitives.ReadUInt64LittleEndian(payload[56..]));
    }

    [TestMethod]
    public void TwoDimensionalSubscriptionMessageTypeDoesNotExist()
    {
        Assert.IsNull(typeof(ProtocolCodec).Assembly.GetType("MachiVerseWorks.Protocol.SubscribeAreaMessage"));
        CollectionAssert.DoesNotContain(Enum.GetNames<MessageType>(), "SubscribeArea");
    }

    private static double ReadDouble(ReadOnlySpan<byte> source)
    {
        return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(source));
    }
}
