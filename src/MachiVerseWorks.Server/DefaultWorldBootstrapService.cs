using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

/// <summary>
/// Builds the initial playable city for a fresh server world. The bootstrap is configuration-gated so
/// save-file restores and focused test fixtures retain their existing behavior unless explicitly enabled.
/// </summary>
internal sealed class DefaultWorldBootstrapService(
    IConfiguration configuration,
    SimulationRuntime simulation) : IHostedService
{
    private const double DefaultHalfExtentMeters = 1_500d;
    private const int DefaultSettlementCount = 2;
    private const int DefaultIterationBudget = 1;
    private const int DefaultStarterCommuterCount = 12;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!ReadBoolean("Simulation:DefaultWorldBootstrap:Enabled", defaultValue: false))
            return Task.CompletedTask;

        simulation.Mutate(world =>
        {
            // A loaded save, an explicit fixture, or an already initialized world is authoritative.
            if (world.HasRegionalGeneration
                || world.RoadSegmentCount != 0
                || world.BuildingCount != 0
                || world.HouseholdCount != 0
                || world.PersonCount != 0)
            {
                return false;
            }

            var halfExtent = ReadDouble("Simulation:DefaultWorldBootstrap:HalfExtentMeters", DefaultHalfExtentMeters, minimum: 250d, maximum: 25_000d);
            var settlementCount = ReadInt("Simulation:DefaultWorldBootstrap:SettlementCount", DefaultSettlementCount, minimum: 2, maximum: 12);
            var iterationBudget = ReadInt("Simulation:DefaultWorldBootstrap:IterationBudget", DefaultIterationBudget, minimum: 1, maximum: 8);
            var commuterCount = ReadInt("Simulation:DefaultWorldBootstrap:StarterCommuterCount", DefaultStarterCommuterCount, minimum: 0, maximum: 128);

            _ = world.InitializeRegionalWorld(
                new WorldVolume(-halfExtent, -halfExtent, -2_000d, halfExtent, halfExtent, 2_000d),
                new RegionalGenerationOptions(
                    RegionalGenerationQualityPreset.Draft,
                    settlementCount,
                    iterationBudget),
                out _);

            SeedStarterCommuters(world, commuterCount);

            if (ReadBoolean("Simulation:DefaultWorldBootstrap:SeedRailwayOperations", defaultValue: true))
                _ = RailwayOperationsFixtures.SeedDeterministic(world);

            // Prime one authoritative tick so starter commuters become visible immediately instead of
            // waiting for the first hosted simulation timer callback.
            world.Step();
            return true;
        }, roadTopologyChanged: true, railwayTopologyChanged: true);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void SeedStarterCommuters(SimulationWorld world, int commuterCount)
    {
        if (commuterCount <= 0) return;

        var accessibleBuildings = world.CreateRoadNetworkSnapshot()
            .AccessPoints
            .Where(static access => access.BuildingId is not null
                && (access.Mode & RoadAccessMode.Foot) != 0
                && (access.Mode & RoadAccessMode.Motor) != 0)
            .Select(static access => access.BuildingId!.Value)
            .Distinct()
            .OrderBy(static id => id.Value)
            .ToArray();
        if (accessibleBuildings.Length < 2) return;

        var residence = accessibleBuildings[0];
        var workplace = accessibleBuildings[^1];
        if (residence == workplace) return;

        for (var index = 0; index < commuterCount; index++)
        {
            var household = world.CreateHousehold(TripEndpoint.ForBuilding(index % 2 == 0 ? residence : workplace));
            var destination = TripEndpoint.ForBuilding(index % 2 == 0 ? workplace : residence);
            _ = world.CreatePerson(
                household,
                new PersonDemographics(
                    AgeYears: 20 + (index % 45),
                    IsEmployed: true,
                    HasPrivateVehicle: index % 2 == 0),
                [new DailyActivityWindow(ActivityKind.Work, 0, 1440, destination, ActivityPriority.High)]);
        }
    }

    private bool ReadBoolean(string key, bool defaultValue) =>
        bool.TryParse(configuration[key], out var value) ? value : defaultValue;

    private int ReadInt(string key, int defaultValue, int minimum, int maximum)
    {
        if (!int.TryParse(configuration[key], out var value)) return defaultValue;
        return Math.Clamp(value, minimum, maximum);
    }

    private double ReadDouble(string key, double defaultValue, double minimum, double maximum)
    {
        if (!double.TryParse(
                configuration[key],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
            || !double.IsFinite(value))
        {
            return defaultValue;
        }
        return Math.Clamp(value, minimum, maximum);
    }
}
