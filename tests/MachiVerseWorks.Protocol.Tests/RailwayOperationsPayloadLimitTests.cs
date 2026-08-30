using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RailwayOperationsPayloadLimitTests
{
    [TestMethod]
    public void PayloadPreflightDetectsOneMiBBoundaryBeforeSerialization()
    {
        var fitting = CreateMessage(26_213);
        var oversized = CreateMessage(26_214);

        Assert.IsTrue(RailwayOperationsProtocolCodec.FitsSingleFrame(fitting));
        Assert.IsFalse(RailwayOperationsProtocolCodec.FitsSingleFrame(oversized));
        Assert.IsTrue(RailwayOperationsProtocolCodec.GetPayloadLength(fitting) <= ProtocolFrameHeader.MaxPayloadLength);
        Assert.IsTrue(RailwayOperationsProtocolCodec.GetPayloadLength(oversized) > ProtocolFrameHeader.MaxPayloadLength);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RailwayOperationsProtocolCodec.Serialize(oversized, ProtocolVersion.Current));
    }

    private static RailwayOperationsSnapshotMessage CreateMessage(int stopCount)
    {
        var stops = Enumerable.Range(0, stopCount)
            .Select(static index => new ProtocolTimetableStop(1, (ulong)index, (ulong)index, 0, 0))
            .ToArray();
        return new RailwayOperationsSnapshotMessage(1, [], [], [new ProtocolTimetable(1, stops)]);
    }
}