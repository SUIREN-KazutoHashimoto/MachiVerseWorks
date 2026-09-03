using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class Worker1Batch2RegressionTests
{
    [TestMethod]
    public void StepFailureFaultsWorldAndRejectsFurtherStepAndCheckpoint()
    {
        var world = new SimulationWorld(powerDispatchSolver: new ThrowingPowerSolver());

        AssertThrows<InvalidOperationException>(() => world.Step());

        Assert.IsTrue(world.IsFaulted);
        Assert.AreEqual(0UL, world.Time.TickCount);
        AssertThrows<InvalidOperationException>(() => world.Step());
        AssertThrows<InvalidOperationException>(() => world.CreateCheckpoint());
    }

    [TestMethod]
    public void EconomyStatisticsSaturateLegalCashAndVacancyAggregates()
    {
        var world = new SimulationWorld();
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 4, 4, 4));
        var firstCompany = world.CreateCompany(initialCashBalance: long.MaxValue);
        var secondCompany = world.CreateCompany(initialCashBalance: long.MaxValue);
        var firstEstablishment = world.CreateEstablishment(firstCompany, buildingId: building);
        var secondEstablishment = world.CreateEstablishment(secondCompany, buildingId: building);
        world.CreateJob(firstEstablishment, int.MaxValue, 0);
        world.CreateJob(secondEstablishment, int.MaxValue, 0);

        var statistics = world.CreateEconomyStatistics();

        Assert.AreEqual(long.MaxValue, statistics.CompanyCashBalance);
        Assert.AreEqual(int.MaxValue, statistics.VacantPositionCount);
    }

    [TestMethod]
    public void LegalExtremePowerDemandAndCapacityRemainFinite()
    {
        var solver = new ZeroPowerSolver();
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1), powerDispatchSolver: solver);
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 4, 4, 4), BuildingKind.Industrial);
        var company = world.CreateCompany();
        var establishment = world.CreateEstablishment(company, buildingId: building);
        world.CreateJob(establishment, int.MaxValue, 0);
        world.CreateJob(establishment, int.MaxValue, 0);
        var node = world.CreatePowerNode(new WorldPoint(0, 0, 0));
        world.CreateGenerator(node, double.MaxValue);
        world.CreateGenerator(node, double.MaxValue);
        var loadId = world.CreatePowerLoad(node, double.MaxValue, buildingId: building, establishmentId: establishment);

        world.Step();

        Assert.IsFalse(world.IsFaulted);
        Assert.IsNotNull(solver.LastRequest);
        Assert.IsTrue(double.IsFinite(solver.LastRequest!.Loads.Single().DemandMegawatts));
        Assert.IsTrue(world.TryGetPowerLoadSnapshot(loadId, out var load));
        Assert.IsTrue(double.IsFinite(load.DemandMegawatts));
        Assert.IsTrue(load.DemandMegawatts > 0d);
        Assert.AreEqual(double.MaxValue, world.CreatePowerStatistics().GenerationCapacityMegawatts);
    }

    [TestMethod]
    public void UtilityStatisticsSaturateAcrossWaterGasAndOptical()
    {
        var world = new SimulationWorld();

        var waterNode = world.CreateWaterNode(new WorldPoint(0, 0, 0), WaterNodeKind.Source);
        world.CreateWaterSource(waterNode, double.MaxValue);
        world.CreateWaterSource(waterNode, double.MaxValue);
        Assert.AreEqual(double.MaxValue, world.CreateWaterSewerStatistics().WaterSupplyCapacityCubicMetersPerDay);

        var gasNode = world.CreateGasNode(new WorldPoint(10, 0, 0), GasNodeKind.Source);
        world.CreateGasSource(gasNode, double.MaxValue);
        world.CreateGasSource(gasNode, double.MaxValue);
        Assert.AreEqual(double.MaxValue, world.CreateGasStatistics().SupplyCapacityCubicMetersPerDay);

        var opticalNode = world.CreateOpticalNode(new WorldPoint(20, 0, 0));
        world.CreateOpticalBackhaul(opticalNode, double.MaxValue);
        world.CreateOpticalBackhaul(opticalNode, double.MaxValue);
        Assert.AreEqual(double.MaxValue, world.CreateOpticalStatistics().BackhaulCapacityGigabitsPerSecond);
    }

    [TestMethod]
    public void BusVehicleCreationOverflowDoesNotConsumeStableId()
    {
        var world = CreateBusWorld(firstDwellTicks: 1, plannedStartTick: ulong.MaxValue, out var tripId, out _);
        var before = world.CreateCheckpoint().MultimodalTransit!.NextVehicleId;

        AssertThrows<OverflowException>(() => world.CreateBusTransitVehicle(tripId));

        Assert.AreEqual(before, world.CreateCheckpoint().MultimodalTransit!.NextVehicleId);
    }

    [TestMethod]
    public void JourneyArrivalOverflowDoesNotConsumeStableId()
    {
        var world = CreateJourneyWorld(out var request);
        var before = world.CreateCheckpoint().MultimodalTransit!.NextJourneyId;

        AssertThrows<OverflowException>(() => world.PlanMultimodalJourney(request, ulong.MaxValue));

        Assert.AreEqual(before, world.CreateCheckpoint().MultimodalTransit!.NextJourneyId);
    }

    [TestMethod]
    public void BusStopPreventsReferencedLaneRemoval()
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(10, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        var lane = world.CreateLane(segment, LaneDirection.Forward, 0);
        world.CreateBusStop(lane, new WorldPoint(1, 0, 0));

        AssertThrows<InvalidOperationException>(() => world.RemoveLane(lane));
        Assert.IsTrue(world.TryGetLaneSnapshot(lane, out _));
    }

    [TestMethod]
    public void ActiveTransitRoadVehicleCannotBeRemovedThroughPublicApi()
    {
        var world = CreateBusWorld(firstDwellTicks: 0, plannedStartTick: 0, out var tripId, out _);
        var busId = world.CreateBusTransitVehicle(tripId);
        world.Step();
        var bus = world.CreateMultimodalTransitSnapshot().Vehicles.Single(item => item.Id == busId);
        Assert.IsNotNull(bus.RoadVehicleId);

        AssertThrows<InvalidOperationException>(() => world.RemoveVehicle(bus.RoadVehicleId!.Value));

        for (var index = 0; index < 200 && world.CreateMultimodalTransitSnapshot().Vehicles.Single(item => item.Id == busId).State != TransitVehicleMovementState.Completed; index++)
            world.Step();
        Assert.AreEqual(TransitVehicleMovementState.Completed, world.CreateMultimodalTransitSnapshot().Vehicles.Single(item => item.Id == busId).State);
    }

    [TestMethod]
    public void PassengerTransferUsesCurrentLegTransferTicksAndAdvancesOnce()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1));
        var checkpoint = world.CreateCheckpoint();
        var transit = checkpoint.MultimodalTransit!;
        var tripRequestId = new TripRequestId(1);
        var journeyId = new JourneyId(1);
        var passengerId = new PassengerId(1);
        var legs = new[]
        {
            new JourneyLegSnapshot(TransitMode.Walk, null, null, null, null, null, null, 1, 3),
            new JourneyLegSnapshot(TransitMode.Walk, null, null, null, null, null, null, 10),
        };
        world = SimulationWorld.RestoreCheckpoint(checkpoint with
        {
            MultimodalTransit = transit with
            {
                NextJourneyId = 2,
                Journeys = new[] { new JourneySnapshot(journeyId, tripRequestId, 0, 14, legs) },
                NextPassengerId = 2,
                Passengers = new[] { new PassengerSnapshot(passengerId, tripRequestId, journeyId, 0, PassengerState.Alighting, 0, 0) },
            },
        });

        world.Step();
        var transfer = world.CreateMultimodalTransitSnapshot().Passengers.Single();
        Assert.AreEqual(PassengerState.Transfer, transfer.State);
        Assert.AreEqual(0, transfer.LegIndex);

        world.Step();
        world.Step();
        Assert.AreEqual(0, world.CreateMultimodalTransitSnapshot().Passengers.Single().LegIndex);

        world.Step();
        var nextLeg = world.CreateMultimodalTransitSnapshot().Passengers.Single();
        Assert.AreEqual(PassengerState.Waiting, nextLeg.State);
        Assert.AreEqual(1, nextLeg.LegIndex);
    }

    private static SimulationWorld CreateBusWorld(ulong firstDwellTicks, ulong plannedStartTick, out TransitTripId tripId, out LaneId lane)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 10));
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(50, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        lane = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        var first = world.CreateBusStop(lane, new WorldPoint(1, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(49, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        var pattern = world.CreateTransitServicePattern(line, [new(first, 0, firstDwellTicks), new(second, 50, 0)]);
        tripId = world.CreateTransitTrip(pattern, plannedStartTick);
        return world;
    }

    private static SimulationWorld CreateJourneyWorld(out TripRequest request)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1));
        var origin = world.CreateBuilding(new WorldVolume(0, 5, 0, 5, 10, 5));
        var destination = world.CreateBuilding(new WorldVolume(95, 5, 0, 100, 10, 5));
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(100, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        world.CreateRoadAccessPoint(segment, 0.05d, buildingId: origin, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        world.CreateRoadAccessPoint(segment, 0.95d, buildingId: destination, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        request = new TripRequest(new TripRequestId(99), TripEndpoint.ForBuilding(origin), TripEndpoint.ForBuilding(destination));
        return world;
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            Assert.Fail($"Expected {typeof(TException).Name}, but {exception.GetType().Name} was thrown.");
            return;
        }

        Assert.Fail($"Expected {typeof(TException).Name} to be thrown.");
    }

    private sealed class ThrowingPowerSolver : IPowerDispatchSolver
    {
        public PowerDispatchResult Solve(PowerDispatchRequest request) => throw new InvalidOperationException("Injected solver failure.");
    }

    private sealed class ZeroPowerSolver : IPowerDispatchSolver
    {
        public PowerDispatchRequest? LastRequest { get; private set; }

        public PowerDispatchResult Solve(PowerDispatchRequest request)
        {
            LastRequest = request;
            return new PowerDispatchResult(
                request.Generators.Select(static item => new PowerGeneratorDispatch(item.Id, 0d)).ToArray(),
                request.Loads.Select(static item => new PowerLoadDispatch(item.Id, 0d)).ToArray());
        }
    }
}
