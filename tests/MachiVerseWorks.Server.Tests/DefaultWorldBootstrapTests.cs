using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachiVerseWorks.Server.Tests;

public sealed class DefaultWorldBootstrapTests
{
    [Fact]
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

        Assert.True(state.HasRegionalGeneration);
        Assert.NotEmpty(state.Regional.Settlements);
        Assert.NotEmpty(state.Regional.Buildings);
        Assert.True(state.BuildingCount > 0);
        Assert.True(state.RoadSegmentCount > 0);
        Assert.True(state.HouseholdCount > 0);
        Assert.True(state.PersonCount > 0);
        Assert.True(state.ActivePedestrianCount > 0);
        Assert.True(state.ActiveVehicleCount > 0);
        Assert.True(state.TrainCount > 0);
    }

    [Fact]
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

        Assert.False(state.HasRegionalGeneration);
        Assert.Equal(0, state.BuildingCount);
        Assert.Equal(0, state.RoadSegmentCount);
        Assert.Equal(0, state.HouseholdCount);
        Assert.Equal(0, state.PersonCount);
    }

    [Fact]
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

        Assert.False(state.HasRegionalGeneration);
        Assert.Equal(0, state.BuildingCount);
        Assert.Equal(2, state.TrainCount);
    }
}
