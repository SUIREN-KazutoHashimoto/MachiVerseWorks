using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

/// <summary>
/// Opt-in deterministic fixture used only by end-to-end harnesses. Production behavior is unchanged
/// unless Simulation:RegionalGenerationFixture is explicitly enabled.
/// </summary>
internal sealed class RegionalGenerationFixtureService(
    IConfiguration configuration,
    SimulationRuntime simulation) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!bool.TryParse(configuration["Simulation:RegionalGenerationFixture"], out var enabled) || !enabled)
            return Task.CompletedTask;

        simulation.Mutate(static world =>
        {
            if (!world.TryCreateRegionalGenerationSnapshot(out _))
            {
                _ = world.GenerateRegionalGeneration(
                    new WorldVolume(-2_000d, -2_000d, 0d, 2_000d, 2_000d, 200d),
                    new RegionalGenerationOptions(
                        RegionalGenerationQualityPreset.Draft,
                        settlementCount: 2,
                        iterationBudget: 1));
            }
            return true;
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
