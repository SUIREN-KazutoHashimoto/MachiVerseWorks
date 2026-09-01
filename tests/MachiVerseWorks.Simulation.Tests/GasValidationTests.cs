using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class GasValidationTests
{
    [TestMethod]
    public void GasSolverRejectsNonFiniteDispatchBeforeMutatingState()
    {
        var world = CreatePipedWorld(new DelegateGasSolver(request => new GasSupplyResult(
            [new GasSourceDispatch(request.Sources[0].Id, double.NaN)],
            [],
            [],
            [new GasLoadDispatch(request.Loads[0].Id, 0d)])));

        Assert.ThrowsException<InvalidOperationException>(() => world.Step());
        Assert.AreEqual(0d, world.CreateGasSnapshot().Sources.Single().OutputCubicMetersPerDay, 1e-9);
    }

    [TestMethod]
    public void GasSolverRejectsUnknownDuplicateAndOverBoundDispatches()
    {
        var unknown = CreatePipedWorld(new DelegateGasSolver(request => new GasSupplyResult(
            [new GasSourceDispatch(new GasSourceId(999), 1d)], [], [], [])));
        Assert.ThrowsException<InvalidOperationException>(() => unknown.Step());

        var duplicate = CreatePipedWorld(new DelegateGasSolver(request => new GasSupplyResult(
            [], [], [],
            [new GasLoadDispatch(request.Loads[0].Id, 1d), new GasLoadDispatch(request.Loads[0].Id, 1d)])));
        Assert.ThrowsException<InvalidOperationException>(() => duplicate.Step());

        var overBound = CreatePipedWorld(new DelegateGasSolver(request => new GasSupplyResult(
            [new GasSourceDispatch(request.Sources[0].Id, request.Sources[0].AvailableCapacityCubicMetersPerDay + 1d)],
            [], [], [])));
        Assert.ThrowsException<InvalidOperationException>(() => overBound.Step());
    }

    [TestMethod]
    public void RestoreRejectsDeliveredGasWithoutConsumerInventory()
    {
        var checkpoint = CreateDeliveredGasCheckpoint();
        var logistics = checkpoint.Economy!.Logistics! with { Inventories = Array.Empty<SimulationInventoryCheckpoint>() };
        var invalid = checkpoint with { Economy = checkpoint.Economy with { Logistics = logistics } };

        Assert.ThrowsException<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(invalid));
    }

    [TestMethod]
    public void RestoreRejectsDeliveredGasWithNonConsumerInventory()
    {
        var checkpoint = CreateDeliveredGasCheckpoint();
        var inventory = checkpoint.Economy!.Logistics!.Inventories.Single();
        var logistics = checkpoint.Economy.Logistics with { Inventories = [inventory with { Role = InventoryRole.Supplier }] };
        var invalid = checkpoint with { Economy = checkpoint.Economy with { Logistics = logistics } };

        Assert.ThrowsException<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(invalid));
    }

    private static SimulationWorld CreatePipedWorld(IGasSupplySolver solver)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2508), gasSupplySolver: solver);
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Industrial);
        var sourceNode = world.CreateGasNode(new WorldPoint(-10, 5, 0), GasNodeKind.Source);
        var serviceNode = world.CreateGasNode(new WorldPoint(5, 5, 0), GasNodeKind.Service);
        world.CreateGasPipeline(sourceNode, serviceNode, 10d);
        world.CreateGasSource(sourceNode, 10d);
        world.CreatePipedGasServicePoint(serviceNode, 5d, building);
        return world;
    }

    private static SimulationCheckpoint CreateDeliveredGasCheckpoint()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2509));
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
        world.CreateDeliveredGasServicePoint(establishment, commodity, 10d, building);
        return world.CreateCheckpoint();
    }

    private sealed class DelegateGasSolver(Func<GasSupplyRequest, GasSupplyResult> solve) : IGasSupplySolver
    {
        public GasSupplyResult Solve(GasSupplyRequest request) => solve(request);
    }
}
