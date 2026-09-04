using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

/// <summary>
/// Builds the initial playable city for a fresh server world. Save-file restores and explicit fixtures
/// are authoritative startup sources and therefore suppress the default-world bootstrap entirely.
/// </summary>
internal sealed class DefaultWorldBootstrapService(
    IConfiguration configuration,
    SimulationRuntime simulation) : IHostedService
{
    private static readonly string[] ExplicitFixtureKeys =
    [
        "Simulation:PedestrianFixture",
        "Simulation:RoadTrafficFixture",
        "Simulation:TrafficFixture",
        "Simulation:PopulationFixture",
        "Simulation:RailwayFixture",
        "Simulation:RailwayOperationsFixture",
        "Simulation:MultimodalTransitFixture",
        "Simulation:EconomyFixture",
    ];

    // Regional generation operates on a continental terrain scale (250 km by default).
    // The established regional tests use 1.4-2.0 Mm wide volumes so the deterministic
    // environment contains viable land candidates across seeds.
    private const double DefaultHalfExtentMeters = 1_000_000d;
    private const int DefaultSettlementCount = 2;
    private const int DefaultIterationBudget = 1;
    private const int DefaultStarterMobilityCount = 12;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!ReadBoolean("Simulation:DefaultWorldBootstrap:Enabled", defaultValue: false)
            || HasAuthoritativeStartupSource())
        {
            return Task.CompletedTask;
        }

        simulation.Mutate(world =>
        {
            if (world.HasRegionalGeneration
                || world.ActiveAgentCount != 0
                || world.RoadSegmentCount != 0
                || world.TrackSegmentCount != 0
                || world.TrainCount != 0
                || world.BuildingCount != 0
                || world.HouseholdCount != 0
                || world.PersonCount != 0)
            {
                return false;
            }

            var halfExtent = ReadDouble("Simulation:DefaultWorldBootstrap:HalfExtentMeters", DefaultHalfExtentMeters, minimum: 25_000d, maximum: 5_000_000d);
            var settlementCount = ReadInt("Simulation:DefaultWorldBootstrap:SettlementCount", DefaultSettlementCount, minimum: 2, maximum: 12);
            var iterationBudget = ReadInt("Simulation:DefaultWorldBootstrap:IterationBudget", DefaultIterationBudget, minimum: 1, maximum: 8);
            var mobilityCount = ReadInt("Simulation:DefaultWorldBootstrap:StarterMobilityCount", DefaultStarterMobilityCount, minimum: 0, maximum: 128);

            _ = world.InitializeRegionalWorld(
                new WorldVolume(-halfExtent, -halfExtent, -12_000d, halfExtent, halfExtent, 12_000d),
                new RegionalGenerationOptions(
                    RegionalGenerationQualityPreset.Draft,
                    settlementCount,
                    iterationBudget),
                out _);

            // The semantic policy for initial street activity belongs to Simulation. This command
            // places a few Pedestrian/Vehicle entities on the materialized city network without
            // advancing the whole world or creating synthetic Population/Economy state.
            _ = world.SeedInitialMobility(mobilityCount);

            // Railway infrastructure is still mutable here because initial street activity does not
            // advance the simulation or initialize Railway Operations.
            if (ReadBoolean("Simulation:DefaultWorldBootstrap:SeedRailwayOperations", defaultValue: true))
                _ = RailwayOperationsFixtures.SeedDeterministic(world);

            return true;
        }, roadTopologyChanged: true, railwayTopologyChanged: true);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool HasAuthoritativeStartupSource()
    {
        if (!string.IsNullOrWhiteSpace(configuration["Simulation:SavePath"])) return true;
        return ExplicitFixtureKeys.Any(key => ReadBoolean(key, defaultValue: false));
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
