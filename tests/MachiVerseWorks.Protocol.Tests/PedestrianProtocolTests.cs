using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class PedestrianProtocolTests
{
    [TestMethod]
    public void CurrentProtocolSupportsPedestrianMessages()
    {
        Assert.AreEqual(new ProtocolVersion(2, 2), ProtocolVersion.Current);
        Assert.IsTrue(ProtocolVersion.Current.SupportsPedestrians);
        Assert.IsFalse(new ProtocolVersion(2, 1).SupportsPedestrians);
    }

    [TestMethod]
    public void PedestrianSpawnAndUpdateRoundTripAllThreeAxesAndState()
    {
        var spawn = new PedestrianSpawnMessage(7, 11, 1.25, -2.5, 3.75, 0.5, -0.25, 0.125, 1.4, ProtocolPedestrianMovementState.Walking, 100);
        var update = new PedestrianUpdateMessage(7, 11, 10.25, -20.5, 30.75, 0, 0, 0, 1.4, ProtocolPedestrianMovementState.WaitingForCrossing, 101);

        Assert.AreEqual(spawn, RoundTrip<PedestrianSpawnMessage>(spawn));
        Assert.AreEqual(update, RoundTrip<PedestrianUpdateMessage>(update));
    }

    [TestMethod]
    public void PedestrianRemoveRoundTrips()
    {
        var expected = new PedestrianRemoveMessage(42, 500);
        Assert.AreEqual(expected, RoundTrip<PedestrianRemoveMessage>(expected));
    }

    [TestMethod]
    public void ProtocolTwoOneRejectsPedestrianSerialization()
    {
        var message = new PedestrianRemoveMessage(1, 1);
        Assert.ThrowsExactly<ArgumentException>(() => ProtocolCodec.Serialize(message, new ProtocolVersion(2, 1)));
    }

    [TestMethod]
    public void InvalidPedestrianStatePayloadIsRejected()
    {
        var message = new PedestrianSpawnMessage(1, 2, 0, 0, 0, 1, 0, 0, 1.4, ProtocolPedestrianMovementState.Walking, 10);
        var frame = ProtocolCodec.Serialize(message);
        frame[ProtocolFrameHeader.Size + 72] = 255;

        Assert.IsFalse(ProtocolCodec.TryDeserialize(frame, out _, out var error));
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }

    private static T RoundTrip<T>(IProtocolMessage message) where T : class, IProtocolMessage
    {
        var frame = ProtocolCodec.Serialize(message);
        Assert.IsTrue(ProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(ProtocolVersion.Current, envelope.Version);
        Assert.IsInstanceOfType<T>(envelope.Message);
        return (T)envelope.Message;
    }
}
