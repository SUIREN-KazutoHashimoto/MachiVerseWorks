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
                ["Simulation:DefaultWorldBootstrap:HalfExtentMeters"] = "750",
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
}
