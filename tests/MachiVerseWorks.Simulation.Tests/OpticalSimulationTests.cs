using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class OpticalSimulationTests
{
    [TestMethod]
    public void FiberFailureReroutesDeterministically()
    {
        var world = CreateRedundantWorld(out _, out var primaryCable, out var alternateCable, out var demand);
        world.Step();
        Assert.IsTrue(world.TryGetOpticalDemandSnapshot(demand, out var initial));
        Assert.IsTrue(initial.AllocatedGigabitsPerSecond > 0d);
        CollectionAssert.Contains(initial.RouteCableIds.ToArray(), primaryCable);
        CollectionAssert.DoesNotContain(initial.RouteCableIds.ToArray(), alternateCable);

        world.SetFiberCableInService(primaryCable, false);
        world.Step();

        Assert.IsTrue(world.TryGetOpticalDemandSnapshot(demand, out var rerouted));
        Assert.IsTrue(rerouted.AllocatedGigabitsPerSecond > 0d);
        CollectionAssert.DoesNotContain(rerouted.RouteCableIds.ToArray(), primaryCable);
        CollectionAssert.Contains(rerouted.RouteCableIds.ToArray(), alternateCable);
    }

    [TestMethod]
    public void NearCapacityFiberReportsCongestion()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2602));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Residential);
        var backbone = world.CreateOpticalNode(new WorldPoint(-10, 5, 0), OpticalNodeKind.BackboneGateway);
        var endpoint = world.CreateOpticalNode(new WorldPoint(5, 5, 0), OpticalNodeKind.Endpoint);
        world.CreateFiberCable(backbone, endpoint, 5d);
        world.CreateOpticalEquipment(backbone, OpticalEquipmentKind.Olt, 10d, requiresPower: false);
        world.CreateOpticalEquipment(endpoint, OpticalEquipmentKind.Onu, 10d, building, requiresPower: false);
        world.CreateOpticalBackhaul(backbone, 10d);
        var demand = world.CreateBuildingOpticalDemand(endpoint, building, 5d);

        world.Step();

        Assert.IsTrue(world.TryGetOpticalDemandSnapshot(demand, out var snapshot));
        Assert.AreEqual(OpticalQualityState.Congested, snapshot.QualityState);
        Assert.IsTrue(world.CreateOpticalStatistics().PeakFiberUtilization >= OpticalDefaults.CongestionThreshold);
    }

    [TestMethod]
    public void PowerOutageStopsPoweredOnu()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2603));
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var powerSource = world.CreatePowerNode(new WorldPoint(-20, 0, 0), PowerNodeKind.GeneratorBus);
        var powerLoadNode = world.CreatePowerNode(new WorldPoint(0, 0, 0), PowerNodeKind.Distribution);
        world.CreateGenerator(powerSource, 5d);
        var powerLine = world.CreatePowerLine(powerSource, powerLoadNode, 5d);
        world.CreatePowerLoad(powerLoadNode, 0.5d, building);

        var backbone = world.CreateOpticalNode(new WorldPoint(-10, 5, 0), OpticalNodeKind.BackboneGateway);
        var endpoint = world.CreateOpticalNode(new WorldPoint(5, 5, 0), OpticalNodeKind.Endpoint);
        world.CreateFiberCable(backbone, endpoint, 20d);
        world.CreateOpticalEquipment(backbone, OpticalEquipmentKind.Olt, 20d, requiresPower: false);
        world.CreateOpticalEquipment(endpoint, OpticalEquipmentKind.Onu, 20d, building);
        world.CreateOpticalBackhaul(backbone, 20d);
        var demand = world.CreateBuildingOpticalDemand(endpoint, building, 5d);

        world.Step();
        Assert.IsTrue(world.TryGetOpticalDemandSnapshot(demand, out var online));
        Assert.AreNotEqual(OpticalQualityState.Unavailable, online.QualityState);

        world.SetPowerLineInService(powerLine, false);
        world.Step();

        Assert.IsTrue(world.TryGetOpticalDemandSnapshot(demand, out var outage));
        Assert.AreEqual(OpticalQualityState.Unavailable, outage.QualityState);
        Assert.IsFalse(world.CreateOpticalSnapshot().Equipment.Single(item => item.Kind == OpticalEquipmentKind.Onu).IsPowered);
    }

    [TestMethod]
    public void CheckpointRestoresOpticalStateAndStableIds()
    {
        var world = CreateRedundantWorld(out _, out var primaryCable, out _, out var demand);
        world.SetFiberCableInService(primaryCable, false);
        world.Step();
        var checkpoint = world.CreateCheckpoint();

        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);

        Assert.AreEqual(world.CreateOpticalStatistics(), restored.CreateOpticalStatistics());
        Assert.IsTrue(world.TryGetOpticalDemandSnapshot(demand, out var expected));
        Assert.IsTrue(restored.TryGetOpticalDemandSnapshot(demand, out var actual));
        Assert.AreEqual(expected.QualityState, actual.QualityState);
        Assert.AreEqual(expected.AllocatedGigabitsPerSecond, actual.AllocatedGigabitsPerSecond, 1e-9);
        CollectionAssert.AreEqual(expected.RouteCableIds.ToArray(), actual.RouteCableIds.ToArray());
        var newNode = restored.CreateOpticalNode(new WorldPoint(100, 0, 0));
        Assert.AreEqual(checkpoint.Economy!.Optical!.NextNodeId, newNode.Value);
    }

    [TestMethod]
    public void CheckpointRejectsDisconnectedOpticalDemandRoute()
    {
        var world = CreateRedundantWorld(out _, out _, out var alternateCable, out var demand);
        world.Step();
        var checkpoint = world.CreateCheckpoint();
        var economy = checkpoint.Economy!;
        var optical = economy.Optical!;
        var demands = optical.Demands
            .Select(item => item.Id == demand ? item with { RouteCableIds = new[] { alternateCable } } : item)
            .ToArray();
        var corrupted = checkpoint with { Economy = economy with { Optical = optical with { Demands = demands } } };

        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(corrupted));
    }

    [TestMethod]
    public void SolverResultRejectsDisconnectedOpticalDemandRoute()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1), opticalRoutingSolver: new DisconnectedRouteSolver());
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var backbone = world.CreateOpticalNode(new WorldPoint(-20, 0, 0), OpticalNodeKind.BackboneGateway);
        var endpoint = world.CreateOpticalNode(new WorldPoint(5, 0, 0), OpticalNodeKind.Endpoint);
        var isolatedA = world.CreateOpticalNode(new WorldPoint(50, 0, 0), OpticalNodeKind.Distribution);
        var isolatedB = world.CreateOpticalNode(new WorldPoint(60, 0, 0), OpticalNodeKind.Distribution);
        world.CreateFiberCable(backbone, endpoint, 20d);
        world.CreateFiberCable(isolatedA, isolatedB, 20d);
        world.CreateOpticalEquipment(backbone, OpticalEquipmentKind.Olt, 20d, requiresPower: false);
        world.CreateOpticalEquipment(endpoint, OpticalEquipmentKind.Onu, 20d, building, requiresPower: false);
        world.CreateOpticalBackhaul(backbone, 20d);
        world.CreateBuildingOpticalDemand(endpoint, building, 5d);

        Assert.ThrowsExactly<InvalidOperationException>(() => world.Step());
    }

    [TestMethod]
    public void OpticalNodeQueryUsesThreeDimensionalVolume()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2605));
        var inside = world.CreateOpticalNode(new WorldPoint(1, 2, 3));
        world.CreateOpticalNode(new WorldPoint(1, 2, 30));
        var result = world.QueryOpticalNodes(new WorldVolume(0, 0, 0, 5, 5, 5));
        Assert.AreEqual(1, result.Length);
        Assert.AreEqual(inside, result[0].Id);
    }

    private sealed class DisconnectedRouteSolver : IOpticalRoutingSolver
    {
        public OpticalRoutingResult Solve(OpticalRoutingRequest request)
        {
            var demand = request.Demands.Single();
            var backhaul = request.Backhauls.Single();
            var disconnectedCable = request.FiberCables[request.FiberCables.Count - 1];
            var allocation = Math.Min(1d, demand.RequestedGigabitsPerSecond);
            return new OpticalRoutingResult(
                new[] { new OpticalDemandRouteResult(demand.Id, backhaul.Id, allocation, new[] { disconnectedCable.Id }) },
                request.FiberCables.Select(static cable => new OpticalFiberLoadResult(cable.Id, 0d)).ToArray(),
                new[] { new OpticalBackhaulLoadResult(backhaul.Id, allocation) });
        }
    }

    private static SimulationWorld CreateRedundantWorld(
        out BuildingId building,
        out FiberCableId primaryCable,
        out FiberCableId alternateCable,
        out OpticalDemandId demand)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2601));
        building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var backbone = world.CreateOpticalNode(new WorldPoint(-20, 5, 0), OpticalNodeKind.BackboneGateway);
        var primary = world.CreateOpticalNode(new WorldPoint(-10, 0, 0), OpticalNodeKind.Distribution);
        var alternate = world.CreateOpticalNode(new WorldPoint(-10, 10, 0), OpticalNodeKind.Distribution);
        var endpoint = world.CreateOpticalNode(new WorldPoint(5, 5, 0), OpticalNodeKind.Endpoint);
        world.CreateFiberCable(backbone, primary, 20d);
        primaryCable = world.CreateFiberCable(primary, endpoint, 20d);
        world.CreateFiberCable(backbone, alternate, 20d);
        alternateCable = world.CreateFiberCable(alternate, endpoint, 20d);
        world.CreateOpticalEquipment(backbone, OpticalEquipmentKind.Olt, 20d, requiresPower: false);
        world.CreateOpticalEquipment(endpoint, OpticalEquipmentKind.Onu, 20d, building, requiresPower: false);
        world.CreateOpticalBackhaul(backbone, 20d);
        demand = world.CreateBuildingOpticalDemand(endpoint, building, 5d);
        return world;
    }
}
