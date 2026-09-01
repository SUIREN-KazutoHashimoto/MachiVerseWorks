using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class PowerSimulationTests
{
    [TestMethod]
    public void LineCapacityCreatesConstrainedLoadAndUnservedDemand()
    {
        var world = CreatePowerWorld(generatorCapacity: 20d, lineCapacity: 5d, baseDemand: 10d, out _, out _, out var load, out _);

        world.Step();

        Assert.IsTrue(world.TryGetPowerLoadSnapshot(load, out var snapshot));
        Assert.AreEqual(PowerSupplyState.Constrained, snapshot.SupplyState);
        Assert.AreEqual(5d, snapshot.ServedMegawatts, 1e-9);
        Assert.IsTrue(snapshot.UnservedMegawatts > 0d);
        Assert.AreEqual(snapshot.DemandMegawatts, snapshot.ServedMegawatts + snapshot.UnservedMegawatts, 1e-9);
    }

    [TestMethod]
    public void GeneratorShutdownCreatesOutageAndRestartRestoresSupply()
    {
        var world = CreatePowerWorld(generatorCapacity: 20d, lineCapacity: 20d, baseDemand: 10d, out var generator, out _, out var load, out _);
        world.Step();
        Assert.AreEqual(PowerSupplyState.Supplied, world.CreatePowerSnapshot().Loads.Single().SupplyState);

        world.SetGeneratorOperatingState(generator, GeneratorOperatingState.Offline);
        world.Step();
        Assert.IsTrue(world.TryGetPowerLoadSnapshot(load, out var outage));
        Assert.AreEqual(PowerSupplyState.Outage, outage.SupplyState);
        Assert.AreEqual(0d, outage.ServedMegawatts, 1e-9);
        Assert.IsTrue(outage.UnservedMegawatts > 0d);

        world.SetGeneratorOperatingState(generator, GeneratorOperatingState.Online);
        world.Step();
        Assert.IsTrue(world.TryGetPowerLoadSnapshot(load, out var recovered));
        Assert.AreEqual(PowerSupplyState.Supplied, recovered.SupplyState);
        Assert.AreEqual(0d, recovered.UnservedMegawatts, 1e-9);
    }

    [TestMethod]
    public void PowerLineOutageIsTopologyAwareAndRecoverable()
    {
        var world = CreatePowerWorld(generatorCapacity: 20d, lineCapacity: 20d, baseDemand: 10d, out _, out var line, out var load, out _);
        world.Step();
        Assert.AreEqual(PowerSupplyState.Supplied, world.CreatePowerSnapshot().Loads.Single().SupplyState);

        world.SetPowerLineInService(line, false);
        world.Step();
        Assert.IsTrue(world.TryGetPowerLoadSnapshot(load, out var outage));
        Assert.AreEqual(PowerSupplyState.Outage, outage.SupplyState);

        world.SetPowerLineInService(line, true);
        world.Step();
        Assert.IsTrue(world.TryGetPowerLoadSnapshot(load, out var recovered));
        Assert.AreEqual(PowerSupplyState.Supplied, recovered.SupplyState);
    }

    [TestMethod]
    public void DemandRuleVariesWithTimeUseAndActivity()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 23));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var home = world.CreateBuilding(new WorldVolume(20, 0, 0, 30, 10, 10), BuildingKind.Residential);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(household, new PersonDemographics(35, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        var company = world.CreateCompany(IndustrySector.Manufacturing, 1000, 10d);
        var establishment = world.CreateEstablishment(company, buildingId: building);
        var job = world.CreateJob(establishment, 2, 0);
        world.AssignEmployment(person, job);
        var generatorNode = world.CreatePowerNode(new WorldPoint(-10, 5, 0), PowerNodeKind.GeneratorBus);
        var loadNode = world.CreatePowerNode(new WorldPoint(5, 5, 0), PowerNodeKind.Load);
        world.CreatePowerLine(generatorNode, loadNode, 100d);
        world.CreateGenerator(generatorNode, 100d);
        var load = world.CreatePowerLoad(loadNode, 10d, building, establishment);

        world.Step();
        Assert.IsTrue(world.TryGetPowerLoadSnapshot(load, out var first));
        Assert.IsTrue(first.DemandMegawatts > 0d);

        for (var tick = 1; tick < 21_601; tick++) world.Step();
        Assert.IsTrue(world.TryGetPowerLoadSnapshot(load, out var later));
        Assert.AreNotEqual(first.DemandMegawatts, later.DemandMegawatts, 1e-9);
    }

    [TestMethod]
    public void IndustryProductionScalesWithPowerAvailability()
    {
        var world = CreatePowerWorld(generatorCapacity: 5d, lineCapacity: 20d, baseDemand: 10d, out _, out _, out _, out var company);
        for (ulong tick = 0; tick < EconomyDefaults.TicksPerEconomicDay; tick++) world.Step();

        Assert.IsTrue(world.TryGetCompanySnapshot(company, out var companySnapshot));
        Assert.IsTrue(companySnapshot.ProducedUnits > 0d);
        Assert.IsTrue(companySnapshot.ProducedUnits < companySnapshot.DailyProductionCapacity);
    }

    [TestMethod]
    public void CheckpointRestoresPowerStateAndStableIds()
    {
        var world = CreatePowerWorld(generatorCapacity: 20d, lineCapacity: 20d, baseDemand: 10d, out var generator, out var line, out var load, out _);
        world.Step();
        world.SetGeneratorOperatingState(generator, GeneratorOperatingState.Offline);
        world.SetPowerLineInService(line, false);
        world.Step();

        var checkpoint = world.CreateCheckpoint();
        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);

        Assert.AreEqual(world.CreatePowerStatistics(), restored.CreatePowerStatistics());
        CollectionAssert.AreEqual(world.CreatePowerSnapshot().Nodes.ToArray(), restored.CreatePowerSnapshot().Nodes.ToArray());
        CollectionAssert.AreEqual(world.CreatePowerSnapshot().Lines.ToArray(), restored.CreatePowerSnapshot().Lines.ToArray());
        CollectionAssert.AreEqual(world.CreatePowerSnapshot().Generators.ToArray(), restored.CreatePowerSnapshot().Generators.ToArray());
        CollectionAssert.AreEqual(world.CreatePowerSnapshot().Loads.ToArray(), restored.CreatePowerSnapshot().Loads.ToArray());
        Assert.IsTrue(restored.TryGetPowerLoadSnapshot(load, out var restoredLoad));
        Assert.AreEqual(PowerSupplyState.Outage, restoredLoad.SupplyState);

        var newNode = restored.CreatePowerNode(new WorldPoint(100, 0, 0));
        Assert.AreEqual(checkpoint.Economy!.Power!.NextNodeId, newNode.Value);
    }

    [TestMethod]
    public void CustomSolverCanReplaceDefaultDispatchBoundary()
    {
        var solver = new FixedPowerDispatchSolver(2d);
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2301), solver);
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var generatorNode = world.CreatePowerNode(new WorldPoint(-10, 5, 0), PowerNodeKind.GeneratorBus);
        var loadNode = world.CreatePowerNode(new WorldPoint(5, 5, 0), PowerNodeKind.Load);
        world.CreatePowerLine(generatorNode, loadNode, 50d);
        world.CreateGenerator(generatorNode, 50d);
        var load = world.CreatePowerLoad(loadNode, 10d, buildingId: building);

        world.Step();

        Assert.AreEqual(1, solver.CallCount);
        Assert.IsTrue(world.TryGetPowerLoadSnapshot(load, out var snapshot));
        Assert.AreEqual(2d, snapshot.ServedMegawatts, 1e-9);
        Assert.AreEqual(PowerSupplyState.Constrained, snapshot.SupplyState);
    }

    private static SimulationWorld CreatePowerWorld(
        double generatorCapacity,
        double lineCapacity,
        double baseDemand,
        out GeneratorId generator,
        out PowerLineId line,
        out PowerLoadId load,
        out CompanyId company)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 23));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var home = world.CreateBuilding(new WorldVolume(20, 0, 0, 30, 10, 10), BuildingKind.Residential);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(household, new PersonDemographics(30, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        company = world.CreateCompany(IndustrySector.Manufacturing, 100_000, 20d);
        var establishment = world.CreateEstablishment(company, buildingId: building);
        var job = world.CreateJob(establishment, 1, 0);
        world.AssignEmployment(person, job);
        var generatorNode = world.CreatePowerNode(new WorldPoint(-10, 5, 0), PowerNodeKind.GeneratorBus);
        var loadNode = world.CreatePowerNode(new WorldPoint(5, 5, 0), PowerNodeKind.Load);
        line = world.CreatePowerLine(generatorNode, loadNode, lineCapacity);
        generator = world.CreateGenerator(generatorNode, generatorCapacity);
        load = world.CreatePowerLoad(loadNode, baseDemand, building, establishment);
        return world;
    }

    private sealed class FixedPowerDispatchSolver(double servedMegawatts) : IPowerDispatchSolver
    {
        public int CallCount { get; private set; }

        public PowerDispatchResult Solve(PowerDispatchRequest request)
        {
            CallCount++;
            var generators = request.Generators.Select(generator => new PowerGeneratorDispatch(generator.Id, Math.Min(generator.AvailableCapacityMegawatts, servedMegawatts))).ToArray();
            var loads = request.Loads.Select(load => new PowerLoadDispatch(load.Id, Math.Min(load.DemandMegawatts, servedMegawatts))).ToArray();
            return new PowerDispatchResult(generators, loads);
        }
    }
}
