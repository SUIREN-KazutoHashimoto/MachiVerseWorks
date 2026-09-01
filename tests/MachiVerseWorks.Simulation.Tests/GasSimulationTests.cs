using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class GasSimulationTests
{
    [TestMethod]
    public void PipelineCapacityCreatesConstrainedGasService()
    {
        var world = CreatePipedWorld(20d, 5d, 10d, out _, out _, out var servicePoint, out _);

        world.Step();

        Assert.IsTrue(world.TryGetGasServicePointSnapshot(servicePoint, out var snapshot));
        Assert.AreEqual(GasServiceState.Constrained, snapshot.ServiceState);
        Assert.AreEqual(5d, snapshot.ServedCubicMetersPerDay, 1e-9);
        Assert.AreEqual(5d, snapshot.UnservedCubicMetersPerDay, 1e-9);
    }

    [TestMethod]
    public void PipelineOutageIsObservableAndRecoverable()
    {
        var world = CreatePipedWorld(20d, 20d, 10d, out _, out var pipeline, out var servicePoint, out _);
        world.Step();
        Assert.AreEqual(GasServiceState.Supplied, world.CreateGasSnapshot().ServicePoints.Single().ServiceState);

        world.SetGasPipelineInService(pipeline, false);
        world.Step();
        Assert.IsTrue(world.TryGetGasServicePointSnapshot(servicePoint, out var outage));
        Assert.AreEqual(GasServiceState.Unavailable, outage.ServiceState);

        world.SetGasPipelineInService(pipeline, true);
        world.Step();
        Assert.IsTrue(world.TryGetGasServicePointSnapshot(servicePoint, out var recovered));
        Assert.AreEqual(GasServiceState.Supplied, recovered.ServiceState);
    }

    [TestMethod]
    public void DeliveredGasUsesLogisticsInventory()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2501));
        var building = world.CreateBuilding(new WorldVolume(-1, -2, 0, 3, 2, 4), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var segment = world.CreateRoadSegment(start, end, RoadKind.Local);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 12d);
        var access = world.CreateRoadAccessPoint(segment, 0.05, building, mode: RoadAccessMode.Motor);
        var company = world.CreateCompany(IndustrySector.Services, 100_000, 0d);
        var establishment = world.CreateEstablishment(company, buildingId: building);
        var commodity = world.CreateCommodity(CommodityKind.Gas);
        world.ConfigureInventory(establishment, commodity, access, InventoryRole.Consumer, capacity: 20d, initialQuantity: 4d, reorderPoint: 2d, targetQuantity: 10d, dailyConsumptionUnits: 10d);
        var servicePoint = world.CreateDeliveredGasServicePoint(establishment, commodity, 10d, building);

        world.Step();

        Assert.IsTrue(world.TryGetGasServicePointSnapshot(servicePoint, out var snapshot));
        Assert.AreEqual(GasDeliveryMode.Delivered, snapshot.DeliveryMode);
        Assert.AreEqual(GasServiceState.Constrained, snapshot.ServiceState);
        Assert.AreEqual(4d, snapshot.ServedCubicMetersPerDay, 1e-9);
    }

    [TestMethod]
    public void GasAvailabilityLimitsIndustryProduction()
    {
        var world = CreatePipedWorld(5d, 20d, 10d, out _, out _, out _, out var company);
        for (ulong tick = 0; tick < EconomyDefaults.TicksPerEconomicDay; tick++) world.Step();

        Assert.IsTrue(world.TryGetCompanySnapshot(company, out var snapshot));
        Assert.IsTrue(snapshot.ProducedUnits > 0d);
        Assert.IsTrue(snapshot.ProducedUnits < snapshot.DailyProductionCapacity);
    }

    [TestMethod]
    public void CheckpointRestoresGasStateAndStableIds()
    {
        var world = CreatePipedWorld(20d, 20d, 10d, out _, out var pipeline, out _, out _);
        world.SetGasPipelineInService(pipeline, false);
        world.Step();
        var checkpoint = world.CreateCheckpoint();

        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);

        Assert.AreEqual(world.CreateGasStatistics(), restored.CreateGasStatistics());
        CollectionAssert.AreEqual(world.CreateGasSnapshot().ServicePoints.ToArray(), restored.CreateGasSnapshot().ServicePoints.ToArray());
        var newNode = restored.CreateGasNode(new WorldPoint(100, 0, 0));
        Assert.AreEqual(checkpoint.Economy!.Gas!.NextNodeId, newNode.Value);
    }

    private static SimulationWorld CreatePipedWorld(
        double sourceCapacity,
        double pipelineCapacity,
        double demand,
        out GasSourceId source,
        out GasPipelineId pipeline,
        out GasServicePointId servicePoint,
        out CompanyId company)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 25));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var home = world.CreateBuilding(new WorldVolume(20, 0, 0, 30, 10, 10), BuildingKind.Residential);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(household, new PersonDemographics(30, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        company = world.CreateCompany(IndustrySector.Manufacturing, 100_000, 20d);
        var establishment = world.CreateEstablishment(company, buildingId: building);
        var job = world.CreateJob(establishment, 1, 0);
        world.AssignEmployment(person, job);
        var sourceNode = world.CreateGasNode(new WorldPoint(-10, 5, 0), GasNodeKind.Source);
        var serviceNode = world.CreateGasNode(new WorldPoint(5, 5, 0), GasNodeKind.Service);
        pipeline = world.CreateGasPipeline(sourceNode, serviceNode, pipelineCapacity);
        source = world.CreateGasSource(sourceNode, sourceCapacity);
        servicePoint = world.CreatePipedGasServicePoint(serviceNode, demand, building, establishment);
        return world;
    }
}
