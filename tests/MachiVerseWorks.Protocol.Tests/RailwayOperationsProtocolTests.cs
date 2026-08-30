using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RailwayOperationsProtocolTests
{
    [TestMethod]
    public void Protocol27RoundTripsTrainServiceDelayPlatformAndTimetableState()
    {
        var message = new RailwayOperationsSnapshotMessage(123,
        [
            new ProtocolTrainState(1, 2, 3, 4, 10, 20, 3, 1, 0, 0, 12.5, 4, 8, 9, 10, 0, 140),
        ],
        [
            new ProtocolRailwayServiceState(3, 2, 4, 5, 6, 7, 1, 1, 18, 1, 1),
        ],
        [
            new ProtocolTimetable(5, [new ProtocolTimetableStop(11, 80, 100, 10, 9), new ProtocolTimetableStop(12, 170, 190, 10, 0)]),
        ]);

        var frame = RailwayOperationsProtocolCodec.Serialize(message, ProtocolVersion.Current);
        Assert.IsTrue(RailwayOperationsProtocolCodec.TryDeserialize(frame, out var decoded, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.AreEqual(message.TickCount, decoded.TickCount);
        Assert.AreEqual(message.Trains[0], decoded.Trains[0]);
        Assert.AreEqual(message.Services[0], decoded.Services[0]);
        CollectionAssert.AreEqual(message.Timetables[0].Stops.ToArray(), decoded.Timetables[0].Stops.ToArray());
    }

    [TestMethod]
    public void Protocol26CannotSerializeRailwayOperations()
    {
        var message = new RailwayOperationsSnapshotMessage(0, [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RailwayOperationsProtocolCodec.Serialize(message, new ProtocolVersion(2, 6)));
    }
}
