using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class MultimodalTransitMessageMapperTests
{
    [TestMethod]
    public void MapperPublishesBusRouteVehicleAndNextStopArrival()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        var a = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var b = world.CreateRoadNode(new WorldPoint(100, 0, 0));
        var segment = world.CreateRoadSegment(a, b);
        var lane = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        var first = world.CreateBusStop(lane, new WorldPoint(5, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(95, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        var pattern = world.CreateTransitServicePattern(line, [new(first, 0, 2), new(second, 100, 2)]);
        var trip = world.CreateTransitTrip(pattern, 0);
        world.CreateBusTransitVehicle(trip);
        world.Step();

        var message = MultimodalTransitMessageMapper.CreateSnapshot(world.CreateMultimodalTransitSnapshot(), world.Time.TickCount);

        Assert.AreEqual(1, message.Lines.Count);
        Assert.AreEqual(2, message.Stops.Count);
        Assert.AreEqual(1, message.Patterns.Count);
        Assert.AreEqual(1, message.Vehicles.Count);
        Assert.AreEqual(1, message.ArrivalEstimates.Count);
        Assert.AreEqual(ProtocolTransitMode.Bus, message.Lines[0].Mode);
        Assert.AreEqual(second.Value, message.ArrivalEstimates[0].StopId);
        Assert.IsTrue(message.ArrivalEstimates[0].EstimatedArrivalTick > world.Time.TickCount);
    }

    [TestMethod]
    public void OversizedSnapshotBecomesStructuredErrorBeforeSerialization()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        for (var index = 0; index < 116_506; index++) world.CreateTransitLine(TransitMode.Bus);

        var planned = MultimodalTransitMessageMapper.Create(world.CreateMultimodalTransitSnapshot(), world.Time.TickCount);

        var error = planned as ProtocolErrorMessage;
        Assert.IsNotNull(error);
        Assert.AreEqual(ProtocolErrorCode.InvalidRequest, error.Code);
        Assert.IsTrue(error.Parameters.Any(parameter => parameter.Key == ProtocolErrorParameterKeys.Field && parameter.Value == "snapshot"));
        Assert.IsTrue(error.Parameters.Any(parameter => parameter.Key == ProtocolErrorParameterKeys.DetailCode && parameter.Value == MultimodalTransitMessageMapper.TooLargeDetailCode));
        var payloadText = error.Parameters.Single(parameter => parameter.Key == "payloadBytes").Value;
        Assert.IsTrue(ulong.Parse(payloadText, System.Globalization.CultureInfo.InvariantCulture) > ProtocolFrameHeader.MaxPayloadLength);
    }

    [TestMethod]
    public void VolumeScopedSnapshotKeepsCompletePatternReferenceClosure()
    {
        var firstStopId = new TransitStopId(1);
        var secondStopId = new TransitStopId(2);
        var lineId = new TransitLineId(1);
        var patternId = new TransitServicePatternId(1);
        var transit = new MultimodalTransitSnapshot(
            Stops:
            [
                new TransitStopSnapshot(firstStopId, TransitStopKind.Bus, new WorldPoint(0, 0, 0), new LaneId(1)),
                new TransitStopSnapshot(secondStopId, TransitStopKind.Bus, new WorldPoint(1_000, 0, 0), new LaneId(2)),
            ],
            Lines: [new TransitLineSnapshot(lineId, TransitMode.Bus)],
            Patterns:
            [
                new TransitServicePatternSnapshot(
                    patternId,
                    lineId,
                    [new TransitPatternStopSnapshot(firstStopId, 0, 1), new TransitPatternStopSnapshot(secondStopId, 100, 1)]),
            ],
            Trips: [],
            Vehicles: [],
            TaxiRequests: [],
            Journeys: [],
            Passengers: []);

        var message = MultimodalTransitMessageMapper.CreateSnapshot(
            transit,
            tickCount: 1,
            new WorldVolume(-10, -10, -10, 10, 10, 10));

        Assert.AreEqual(1, message.Lines.Count);
        Assert.AreEqual(1, message.Patterns.Count);
        Assert.AreEqual(2, message.Stops.Count, "The out-of-volume stop is still required by the selected pattern reference closure.");
        CollectionAssert.AreEquivalent(
            new[] { firstStopId.Value, secondStopId.Value },
            message.Stops.Select(static item => item.Id).ToArray());
    }

    [TestMethod]
    public void VolumeScopedDeliveryRecoversWhenTwentyThousandVehicleGlobalSnapshotIsOversized()
    {
        var vehicles = Enumerable.Range(0, 20_000)
            .Select(index => new TransitVehicleSnapshot(
                new TransitVehicleId(checked((ulong)index + 1UL)),
                TransitVehicleKind.Bus,
                TripId: null,
                RoadVehicleId: null,
                StopIndex: 0,
                Position: new WorldPoint(index < 32 ? index : 1_000_000 + index, 0, 0),
                State: TransitVehicleMovementState.Idle,
                EstimatedArrivalTick: 0,
                DwellUntilTick: 0,
                TickCount: 1))
            .ToArray();
        var transit = new MultimodalTransitSnapshot(
            Stops: [],
            Lines: [],
            Patterns: [],
            Trips: [],
            Vehicles: vehicles,
            TaxiRequests: [],
            Journeys: [],
            Passengers: []);

        var global = MultimodalTransitMessageMapper.Create(transit, tickCount: 1);
        Assert.IsInstanceOfType<ProtocolErrorMessage>(global);

        var scoped = MultimodalTransitMessageMapper.Create(
            transit,
            tickCount: 1,
            new WorldVolume(-1, -1, -1, 31.5, 1, 1));
        var snapshot = scoped as MultimodalTransitSnapshotMessage;
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(32, snapshot.Vehicles.Count);
        Assert.IsTrue((ulong)MultimodalTransitProtocolCodec.GetPayloadLength(snapshot) <= ProtocolFrameHeader.MaxPayloadLength);
    }
}
