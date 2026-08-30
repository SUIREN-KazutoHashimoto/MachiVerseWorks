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

        var message = MultimodalTransitMessageMapper.Create(world.CreateMultimodalTransitSnapshot(), world.Time.TickCount);

        Assert.AreEqual(1, message.Lines.Count);
        Assert.AreEqual(2, message.Stops.Count);
        Assert.AreEqual(1, message.Patterns.Count);
        Assert.AreEqual(1, message.Vehicles.Count);
        Assert.AreEqual(1, message.ArrivalEstimates.Count);
        Assert.AreEqual(ProtocolTransitMode.Bus, message.Lines[0].Mode);
        Assert.AreEqual(second.Value, message.ArrivalEstimates[0].StopId);
        Assert.IsTrue(message.ArrivalEstimates[0].EstimatedArrivalTick > world.Time.TickCount);
    }
}
