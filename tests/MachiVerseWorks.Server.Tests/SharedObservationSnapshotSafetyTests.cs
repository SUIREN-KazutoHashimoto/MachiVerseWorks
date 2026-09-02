using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class SharedObservationSnapshotSafetyTests
{
    [TestMethod]
    public void EntityMessagePlannersDoNotMutateSharedSnapshotArrays()
    {
        var agents = new[]
        {
            new AgentSnapshot(new AgentId(2), new WorldPoint(2, 0, 0), default, 10),
            new AgentSnapshot(new AgentId(1), new WorldPoint(1, 0, 0), default, 10),
        };
        var pedestrians = new[]
        {
            new PedestrianSnapshot(new PedestrianId(2), new TripRequestId(2), new WorldPoint(2, 0, 0), default, 1.4, PedestrianMovementState.Walking, 10),
            new PedestrianSnapshot(new PedestrianId(1), new TripRequestId(1), new WorldPoint(1, 0, 0), default, 1.4, PedestrianMovementState.Walking, 10),
        };
        var vehicles = new[]
        {
            new VehicleSnapshot(new VehicleId(2), new LaneId(1), 0, 0, 0, new WorldPoint(2, 0, 0), default, new WorldVector(1, 0, 0), 1, VehicleDimensions.PassengerCar, VehicleMovementState.Driving, 10),
            new VehicleSnapshot(new VehicleId(1), new LaneId(1), 0, 0, 0, new WorldPoint(1, 0, 0), default, new WorldVector(1, 0, 0), 1, VehicleDimensions.PassengerCar, VehicleMovementState.Driving, 10),
        };

        _ = SnapshotMessagePlanner.Create(agents, new HashSet<ulong>(), 10);
        _ = PedestrianSnapshotMessagePlanner.Create(pedestrians, new HashSet<ulong>(), 10);
        _ = VehicleSnapshotMessagePlanner.Create(vehicles, new HashSet<ulong>(), 10);

        CollectionAssert.AreEqual(new ulong[] { 2, 1 }, agents.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(new ulong[] { 2, 1 }, pedestrians.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(new ulong[] { 2, 1 }, vehicles.Select(static item => item.Id.Value).ToArray());
    }

    [TestMethod]
    public void RailwayOperationsMapperDoesNotMutateSharedVisibleTrainArray()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        for (var tick = 0; tick < 500; tick++) world.Step();
        var operations = world.CreateRailwayOperationsSnapshot();
        var visibleTrains = operations.Trains.OrderByDescending(static item => item.Id.Value).ToArray();
        var sourceOrder = visibleTrains.Select(static item => item.Id.Value).ToArray();

        var message = RailwayOperationsMessageMapper.Create(operations, visibleTrains, world.Time.TickCount);

        CollectionAssert.AreEqual(sourceOrder, visibleTrains.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(sourceOrder.OrderBy(static id => id).ToArray(), message.Trains.Select(static item => item.Id).ToArray());
    }
}
