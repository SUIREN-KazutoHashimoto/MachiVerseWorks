using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class DefaultWorldBootstrapTests
{
    [TestMethod]
    public async Task EnabledBootstrapCreatesRegionalCityAndVisibleActivity()
    {
        await using var host = await ServerTestHost.StartAsync(
            initialAgentCount: 0,
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Simulation:DefaultWorldBootstrap:Enabled"] = "true",
                ["Simulation:DefaultWorldBootstrap:HalfExtentMeters"] = "1000000",
                ["Simulation:DefaultWorldBootstrap:SettlementCount"] = "2",
                ["Simulation:DefaultWorldBootstrap:IterationBudget"] = "1",
                ["Simulation:DefaultWorldBootstrap:StarterMobilityCount"] = "8",
                ["Simulation:DefaultWorldBootstrap:SeedRailwayOperations"] = "true",
            });

        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        var state = simulation.Read(static world => new
        {
            world.HasRegionalGeneration,
            world.BuildingCount,
            world.RoadSegmentCount,
            world.HouseholdCount,
            world.PersonCount,
            world.ActivePedestrianCount,
            world.ActiveVehicleCount,
            world.TrainCount,
            Regional = world.CreateRegionalGenerationSnapshot(),
        });

        Assert.IsTrue(state.HasRegionalGeneration);
        Assert.IsTrue(state.Regional.Settlements.Count > 0);
        Assert.IsTrue(state.Regional.Buildings.Count > 0);
        Assert.IsTrue(state.BuildingCount > 0);
        Assert.IsTrue(state.RoadSegmentCount > 0);
        Assert.IsTrue(state.HouseholdCount > 0);
        Assert.IsTrue(state.PersonCount > 0);
        Assert.IsTrue(state.ActivePedestrianCount > 0);
        Assert.IsTrue(state.ActiveVehicleCount > 0);
        Assert.IsTrue(state.TrainCount > 0);

        // Saving must not mutate the live world. Starter mobility identity is part of the
        // checkpoint so a restored world can still retire only those transient subjects when
        // a later Management road edit invalidates their derived routes.
        var checkpointState = simulation.Read(static world =>
        {
            var pedestriansBefore = world.PedestrianCount;
            var vehiclesBefore = world.VehicleCount;
            var checkpoint = world.CreateCheckpoint();
            return new
            {
                Checkpoint = checkpoint,
                PedestriansBefore = pedestriansBefore,
                VehiclesBefore = vehiclesBefore,
                PedestriansAfter = world.PedestrianCount,
                VehiclesAfter = world.VehicleCount,
            };
        });

        Assert.AreEqual(checkpointState.PedestriansBefore, checkpointState.PedestriansAfter);
        Assert.AreEqual(checkpointState.VehiclesBefore, checkpointState.VehiclesAfter);
        Assert.IsTrue(checkpointState.Checkpoint.InitialMobilityPedestrianIds?.Count > 0);
        Assert.IsTrue(checkpointState.Checkpoint.InitialMobilityVehicleIds?.Count > 0);

        var restored = SimulationWorld.RestoreCheckpoint(checkpointState.Checkpoint);
        Assert.AreEqual(checkpointState.PedestriansBefore, restored.PedestrianCount);
        Assert.AreEqual(checkpointState.VehiclesBefore, restored.VehicleCount);
        _ = restored.CreateRoadNode(new WorldPoint(900_000d, 900_000d, 0d));
        Assert.AreEqual(0, restored.PedestrianCount);
        Assert.AreEqual(0, restored.VehicleCount);

        // Bootstrap street activity is explicitly transient and must never make normal road
        // management permanently immutable. A topology mutation retires only bootstrap-owned
        // mobility while preserving the authoritative Regional city itself.
        simulation.Mutate(static world =>
        {
            _ = world.CreateRoadNode(new WorldPoint(900_000d, 900_000d, 0d));
            return true;
        }, roadTopologyChanged: true);

        var afterEdit = simulation.Read(static world => new
        {
            world.HasRegionalGeneration,
            world.PedestrianCount,
            world.VehicleCount,
        });
        Assert.IsTrue(afterEdit.HasRegionalGeneration);
        Assert.AreEqual(0, afterEdit.PedestrianCount);
        Assert.AreEqual(0, afterEdit.VehicleCount);
    }

    [TestMethod]
    public async Task DisabledBootstrapPreservesExplicitEmptyWorldBehavior()
    {
        await using var host = await ServerTestHost.StartAsync(
            initialAgentCount: 0,
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Simulation:DefaultWorldBootstrap:Enabled"] = "false",
            });

        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        var state = simulation.Read(static world => new
        {
            world.HasRegionalGeneration,
            world.BuildingCount,
            world.RoadSegmentCount,
            world.HouseholdCount,
            world.PersonCount,
        });

        Assert.IsFalse(state.HasRegionalGeneration);
        Assert.AreEqual(0, state.BuildingCount);
        Assert.AreEqual(0, state.RoadSegmentCount);
        Assert.AreEqual(0, state.HouseholdCount);
        Assert.AreEqual(0, state.PersonCount);
    }

    [TestMethod]
    public async Task ExplicitRailwayOperationsFixtureSuppressesDefaultBootstrap()
    {
        await using var host = await ServerTestHost.StartAsync(
            initialAgentCount: 0,
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Simulation:DefaultWorldBootstrap:Enabled"] = "true",
                ["Simulation:RailwayOperationsFixture"] = "true",
            });

        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        var state = simulation.Read(static world => new
        {
            world.HasRegionalGeneration,
            world.BuildingCount,
            world.TrainCount,
        });

        Assert.IsFalse(state.HasRegionalGeneration);
        Assert.AreEqual(0, state.BuildingCount);
        Assert.AreEqual(2, state.TrainCount);
    }

    [TestMethod]
    [DataRow("Simulation:LogisticsFixture")]
    [DataRow("Simulation:GasFixture")]
    public async Task ExplicitRoadMutatingFixtureSuppressesDefaultBootstrap(string fixtureKey)
    {
        await using var host = await ServerTestHost.StartAsync(
            initialAgentCount: 0,
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Simulation:DefaultWorldBootstrap:Enabled"] = "true",
                [fixtureKey] = "true",
            });

        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        var state = simulation.Read(static world => new
        {
            world.HasRegionalGeneration,
            world.PedestrianCount,
            world.VehicleCount,
        });

        Assert.IsFalse(state.HasRegionalGeneration);
        Assert.AreEqual(0, state.PedestrianCount);
        Assert.AreEqual(0, state.VehicleCount);
    }
}
