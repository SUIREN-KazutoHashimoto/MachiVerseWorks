using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class WaterSewerSimulationTests
{
    [TestMethod]
    public void ConnectedNetworksSupplyWaterAndTreatWastewater()
    {
        var world = CreateUtilityWorld(100d, 100d, out _, out _, out _, out var servicePoint);

        world.Step();

        Assert.IsTrue(world.TryGetWaterSewerServicePointSnapshot(servicePoint, out var snapshot));
        Assert.AreEqual(WaterServiceState.Supplied, snapshot.WaterState);
        Assert.AreEqual(SewerServiceState.Available, snapshot.SewerState);
        Assert.AreEqual(snapshot.WaterDemandCubicMetersPerDay, snapshot.WaterServedCubicMetersPerDay, 1e-9);
        Assert.AreEqual(snapshot.WastewaterGeneratedCubicMetersPerDay, snapshot.WastewaterProcessedCubicMetersPerDay, 1e-9);
        Assert.AreEqual(0d, snapshot.WastewaterOverflowCubicMetersPerDay, 1e-9);
    }

    [TestMethod]
    public void WaterPipeCutCreatesUnavailableServiceAndRecovery()
    {
        var world = CreateUtilityWorld(100d, 100d, out var waterPipe, out _, out _, out var servicePoint);
        world.Step();
        Assert.AreEqual(WaterServiceState.Supplied, world.CreateWaterSewerSnapshot().ServicePoints.Single().WaterState);

        world.SetWaterPipeInService(waterPipe, false);
        world.Step();
        Assert.IsTrue(world.TryGetWaterSewerServicePointSnapshot(servicePoint, out var outage));
        Assert.AreEqual(WaterServiceState.Unavailable, outage.WaterState);
        Assert.IsTrue(outage.WaterUnservedCubicMetersPerDay > 0d);

        world.SetWaterPipeInService(waterPipe, true);
        world.Step();
        Assert.IsTrue(world.TryGetWaterSewerServicePointSnapshot(servicePoint, out var recovered));
        Assert.AreEqual(WaterServiceState.Supplied, recovered.WaterState);
    }

    [TestMethod]
    public void TreatmentShutdownCreatesSewerUnavailableAndRecovery()
    {
        var world = CreateUtilityWorld(100d, 100d, out _, out _, out var treatment, out var servicePoint);
        world.Step();

        world.SetSewageTreatmentPlantOperatingState(treatment, UtilityOperatingState.Offline);
        world.Step();
        Assert.IsTrue(world.TryGetWaterSewerServicePointSnapshot(servicePoint, out var outage));
        Assert.AreEqual(SewerServiceState.Unavailable, outage.SewerState);
        Assert.IsTrue(outage.WastewaterOverflowCubicMetersPerDay > 0d);

        world.SetSewageTreatmentPlantOperatingState(treatment, UtilityOperatingState.Online);
        world.Step();
        Assert.IsTrue(world.TryGetWaterSewerServicePointSnapshot(servicePoint, out var recovered));
        Assert.AreEqual(SewerServiceState.Available, recovered.SewerState);
    }

    [TestMethod]
    public void PumpPowerOutageStopsWaterSupply()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 24));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var sourceNode = world.CreateWaterNode(new WorldPoint(-20, 0, 0), WaterNodeKind.Source);
        var pumpNode = world.CreateWaterNode(new WorldPoint(-10, 0, 0), WaterNodeKind.Pump);
        var serviceNode = world.CreateWaterNode(new WorldPoint(0, 0, 0), WaterNodeKind.Service);
        world.CreateWaterPipe(sourceNode, pumpNode, 100d);
        world.CreateWaterPipe(pumpNode, serviceNode, 100d);
        world.CreateWaterSource(sourceNode, 100d);
        var sewerService = world.CreateSewerNode(new WorldPoint(0, 0, -2), SewerNodeKind.Service);
        var treatmentNode = world.CreateSewerNode(new WorldPoint(20, 0, -2), SewerNodeKind.Treatment);
        world.CreateSewerPipe(sewerService, treatmentNode, 100d);
        world.CreateSewageTreatmentPlant(treatmentNode, 100d);
        var generatorNode = world.CreatePowerNode(new WorldPoint(-20, 10, 0), PowerNodeKind.GeneratorBus);
        var pumpPowerNode = world.CreatePowerNode(new WorldPoint(-10, 10, 0), PowerNodeKind.Load);
        world.CreatePowerLine(generatorNode, pumpPowerNode, 10d);
        var generator = world.CreateGenerator(generatorNode, 10d);
        var pumpPower = world.CreatePowerLoad(pumpPowerNode, 1d, buildingId: building);
        world.CreateWaterPump(pumpNode, 100d, pumpPower);
        var servicePoint = world.CreateWaterSewerServicePoint(serviceNode, sewerService, 10d, buildingId: building);

        world.Step();
        Assert.AreEqual(WaterServiceState.Supplied, world.CreateWaterSewerSnapshot().ServicePoints.Single().WaterState);
        world.SetGeneratorOperatingState(generator, GeneratorOperatingState.Offline);
        world.Step();

        Assert.IsTrue(world.TryGetWaterSewerServicePointSnapshot(servicePoint, out var outage));
        Assert.AreEqual(WaterServiceState.Unavailable, outage.WaterState);
    }

    [TestMethod]
    public void CheckpointRestoresTopologyServiceStateAndSpatialQuery()
    {
        var world = CreateUtilityWorld(100d, 100d, out var waterPipe, out _, out _, out var servicePoint);
        world.Step();
        world.SetWaterPipeInService(waterPipe, false);
        world.Step();

        var checkpoint = world.CreateCheckpoint();
        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);

        Assert.AreEqual(world.CreateWaterSewerStatistics(), restored.CreateWaterSewerStatistics());
        CollectionAssert.AreEqual(world.CreateWaterSewerSnapshot().WaterNodes.ToArray(), restored.CreateWaterSewerSnapshot().WaterNodes.ToArray());
        Assert.IsTrue(restored.TryGetWaterSewerServicePointSnapshot(servicePoint, out var restoredPoint));
        Assert.AreEqual(WaterServiceState.Unavailable, restoredPoint.WaterState);
        Assert.IsTrue(restored.QueryWaterNodes(new WorldVolume(-100, -100, -100, 100, 100, 100)).Length >= 2);
        var newNode = restored.CreateWaterNode(new WorldPoint(100, 0, 0));
        Assert.AreEqual(checkpoint.Economy!.WaterSewer!.NextWaterNodeId, newNode.Value);
    }

    [TestMethod]
    public void CustomWaterAndSewerSolversCanReplaceDefaultBoundaries()
    {
        var waterSolver = new FixedWaterSolver(2d);
        var sewerSolver = new FixedSewerSolver(1d);
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2401), waterSupplySolver: waterSolver, sewerSolver: sewerSolver);
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var sourceNode = world.CreateWaterNode(new WorldPoint(-10, 0, 0), WaterNodeKind.Source);
        var waterNode = world.CreateWaterNode(new WorldPoint(0, 0, 0), WaterNodeKind.Service);
        world.CreateWaterPipe(sourceNode, waterNode, 100d);
        world.CreateWaterSource(sourceNode, 100d);
        var sewerNode = world.CreateSewerNode(new WorldPoint(0, 0, -1), SewerNodeKind.Service);
        var treatmentNode = world.CreateSewerNode(new WorldPoint(10, 0, -1), SewerNodeKind.Treatment);
        world.CreateSewerPipe(sewerNode, treatmentNode, 100d);
        world.CreateSewageTreatmentPlant(treatmentNode, 100d);
        var servicePoint = world.CreateWaterSewerServicePoint(waterNode, sewerNode, 10d, buildingId: building);

        world.Step();

        Assert.AreEqual(1, waterSolver.CallCount);
        Assert.AreEqual(1, sewerSolver.CallCount);
        Assert.IsTrue(world.TryGetWaterSewerServicePointSnapshot(servicePoint, out var snapshot));
        Assert.AreEqual(2d, snapshot.WaterServedCubicMetersPerDay, 1e-9);
        Assert.AreEqual(WaterServiceState.Constrained, snapshot.WaterState);
        Assert.AreEqual(1d, snapshot.WastewaterProcessedCubicMetersPerDay, 1e-9);
    }

    private static SimulationWorld CreateUtilityWorld(double waterCapacity, double treatmentCapacity, out WaterPipeId waterPipe, out SewerPipeId sewerPipe, out SewageTreatmentPlantId treatment, out WaterSewerServicePointId servicePoint)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 24));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var sourceNode = world.CreateWaterNode(new WorldPoint(-10, 0, 0), WaterNodeKind.Source);
        var serviceNode = world.CreateWaterNode(new WorldPoint(0, 0, 0), WaterNodeKind.Service);
        waterPipe = world.CreateWaterPipe(sourceNode, serviceNode, waterCapacity);
        world.CreateWaterSource(sourceNode, waterCapacity);
        var sewerService = world.CreateSewerNode(new WorldPoint(0, 0, -2), SewerNodeKind.Service);
        var treatmentNode = world.CreateSewerNode(new WorldPoint(10, 0, -2), SewerNodeKind.Treatment);
        sewerPipe = world.CreateSewerPipe(sewerService, treatmentNode, treatmentCapacity);
        treatment = world.CreateSewageTreatmentPlant(treatmentNode, treatmentCapacity);
        servicePoint = world.CreateWaterSewerServicePoint(serviceNode, sewerService, 10d, buildingId: building);
        return world;
    }

    private sealed class FixedWaterSolver(double served) : IWaterSupplySolver
    {
        public int CallCount { get; private set; }
        public WaterSupplyResult Solve(WaterSupplyRequest request)
        {
            CallCount++;
            return new WaterSupplyResult(
                request.Sources.Select(item => new WaterSourceDispatch(item.Id, Math.Min(item.AvailableCapacityCubicMetersPerDay, served))).ToArray(),
                request.Reservoirs.Select(item => new ReservoirDispatch(item.Id, 0d)).ToArray(),
                request.Pumps.Select(item => new PumpDispatch(item.Id, Math.Min(item.AvailableCapacityCubicMetersPerDay, served))).ToArray(),
                request.Loads.Select(item => new WaterLoadDispatch(item.Id, Math.Min(item.DemandCubicMetersPerDay, served))).ToArray());
        }
    }

    private sealed class FixedSewerSolver(double processed) : ISewerSolver
    {
        public int CallCount { get; private set; }
        public SewerFlowResult Solve(SewerFlowRequest request)
        {
            CallCount++;
            return new SewerFlowResult(
                request.Pumps.Select(item => new PumpDispatch(item.Id, Math.Min(item.AvailableCapacityCubicMetersPerDay, processed))).ToArray(),
                request.Treatments.Select(item => new SewerTreatmentDispatch(item.Id, Math.Min(item.AvailableCapacityCubicMetersPerDay, processed))).ToArray(),
                request.Loads.Select(item => new SewerLoadDispatch(item.Id, Math.Min(item.GeneratedCubicMetersPerDay, processed))).ToArray());
        }
    }
}
