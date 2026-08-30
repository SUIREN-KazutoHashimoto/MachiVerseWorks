using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class IntersectionControlProtocolTests
{
    [TestMethod]
    public void IntersectionControlSnapshotRoundTripsInProtocol24()
    {
        var message = new IntersectionControlSnapshotMessage(
            123,
            5,
            ProtocolIntersectionControlMode.FixedSignal,
            1,
            42,
            [
                new ProtocolIntersectionMovementState(
                    11,
                    11,
                    21,
                    22,
                    ProtocolTurnMovement.Left,
                    1.5,
                    -2.25,
                    0,
                    ProtocolSignalIndication.Red,
                    3,
                    false),
            ]);

        var frame = IntersectionControlProtocolCodec.Serialize(message, ProtocolVersion.Current);

        Assert.IsTrue(IntersectionControlProtocolCodec.TryDeserialize(frame, out var decoded, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.AreEqual(message.TickCount, decoded.TickCount);
        Assert.AreEqual(message.IntersectionNodeId, decoded.IntersectionNodeId);
        Assert.AreEqual(message.Mode, decoded.Mode);
        Assert.AreEqual(message.PhaseIndex, decoded.PhaseIndex);
        Assert.AreEqual(message.PhaseTick, decoded.PhaseTick);
        Assert.AreEqual(1, decoded.Movements.Count);
        Assert.AreEqual(message.Movements[0], decoded.Movements[0]);
    }

    [TestMethod]
    public void IntersectionControlSnapshotRequiresProtocol24()
    {
        var message = new IntersectionControlSnapshotMessage(
            0,
            1,
            ProtocolIntersectionControlMode.Unsignalized,
            0,
            0,
            []);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            IntersectionControlProtocolCodec.Serialize(message, new ProtocolVersion(2, 3)));
    }
}
