using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RailwayOperationsMessageMapperTests
{
    [TestMethod]
    public void MapperPublishesVisibleTrainServiceDelayPlatformAndTimetable()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        for (var tick = 0; tick < 500; tick++) world.Step();
        var operations = world.CreateRailwayOperationsSnapshot();
        var message = RailwayOperationsMessageMapper.Create(operations, operations.Trains.ToArray(), world.Time.TickCount);
        Assert.AreEqual(2, message.Trains.Count);
        Assert.AreEqual(2, message.Services.Count);
        Assert.AreEqual(2, message.Timetables.Count);
        Assert.IsTrue(message.Services.Any(static service => service.DelayTicks > 0));
        Assert.IsTrue(message.Trains.Any(static train => train.AssignedPlatformId > 0 || train.CurrentPlatformId > 0));
    }
}
