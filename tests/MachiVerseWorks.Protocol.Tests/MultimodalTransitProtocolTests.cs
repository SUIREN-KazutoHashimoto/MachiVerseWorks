using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class MultimodalTransitProtocolTests
{
    [TestMethod]
    public void Protocol28RoundTripsLinesStopsPatternsVehiclesAndArrivals()
    {
        var message = new MultimodalTransitSnapshotMessage(
            123,
            [new ProtocolTransitLine(1, ProtocolTransitMode.Bus), new ProtocolTransitLine(2, ProtocolTransitMode.Railway)],
            [
                new ProtocolTransitStop(11, ProtocolTransitStopKind.Bus, 1, 2, 3, 21, 0, 0),
                new ProtocolTransitStop(12, ProtocolTransitStopKind.Bus, 4, 5, 6, 22, 0, 0),
                new ProtocolTransitStop(13, ProtocolTransitStopKind.Railway, 7, 8, 9, 0, 31, 41),
                new ProtocolTransitStop(14, ProtocolTransitStopKind.Railway, 10, 11, 12, 0, 32, 42),
            ],
            [
                new ProtocolTransitPattern(51, 1, 0, [new ProtocolTransitPatternStop(11, 0, 2), new ProtocolTransitPatternStop(12, 30, 3)]),
                new ProtocolTransitPattern(52, 2, 61, [new ProtocolTransitPatternStop(13, 0, 10), new ProtocolTransitPatternStop(14, 80, 10)]),
            ],
            [
                new ProtocolTransitVehicle(71, ProtocolTransitVehicleKind.Bus, 81, 91, 0, 1, 2, 3, ProtocolTransitVehicleState.EnRouteToStop, 160, 0),
                new ProtocolTransitVehicle(72, ProtocolTransitVehicleKind.Taxi, 0, 92, 0, 4, 5, 6, ProtocolTransitVehicleState.EnRouteToPickup, 170, 0),
            ],
            [new ProtocolTransitArrivalEstimate(12, 1, 71, 160)]);

        var frame = MultimodalTransitProtocolCodec.Serialize(message, ProtocolVersion.Current);
        Assert.IsTrue(MultimodalTransitProtocolCodec.TryDeserialize(frame, out var decoded, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.AreEqual(message.TickCount, decoded.TickCount);
        CollectionAssert.AreEqual(message.Lines.ToArray(), decoded.Lines.ToArray());
        CollectionAssert.AreEqual(message.Stops.ToArray(), decoded.Stops.ToArray());
        CollectionAssert.AreEqual(message.Patterns[0].Stops.ToArray(), decoded.Patterns[0].Stops.ToArray());
        CollectionAssert.AreEqual(message.Patterns[1].Stops.ToArray(), decoded.Patterns[1].Stops.ToArray());
        CollectionAssert.AreEqual(message.Vehicles.ToArray(), decoded.Vehicles.ToArray());
        CollectionAssert.AreEqual(message.ArrivalEstimates.ToArray(), decoded.ArrivalEstimates.ToArray());
    }

    [TestMethod]
    public void Protocol27CannotSerializeMultimodalTransit()
    {
        var message = new MultimodalTransitSnapshotMessage(0, [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MultimodalTransitProtocolCodec.Serialize(message, new ProtocolVersion(2, 7)));
    }

    [TestMethod]
    public void DecoderRejectsPatternReferencingMissingStop()
    {
        var message = new MultimodalTransitSnapshotMessage(
            1,
            [new ProtocolTransitLine(1, ProtocolTransitMode.Bus)],
            [new ProtocolTransitStop(1, ProtocolTransitStopKind.Bus, 0, 0, 0, 1, 0, 0), new ProtocolTransitStop(2, ProtocolTransitStopKind.Bus, 1, 0, 0, 1, 0, 0)],
            [new ProtocolTransitPattern(1, 1, 0, [new ProtocolTransitPatternStop(1, 0, 0), new ProtocolTransitPatternStop(2, 1, 0)])],
            [],
            []);
        var frame = MultimodalTransitProtocolCodec.Serialize(message, ProtocolVersion.Current);
        // Second pattern stop ID begins after frame header + snapshot header + line + two stops + pattern header.
        var secondPatternStopIdOffset = ProtocolFrameHeader.Size + 28 + 9 + (57 * 2) + 28 + 24;
        frame[secondPatternStopIdOffset] = 99;

        Assert.IsFalse(MultimodalTransitProtocolCodec.TryDeserialize(frame, out _, out var error));
        Assert.AreEqual(ProtocolDecodeError.InvalidPayload, error);
    }
}
