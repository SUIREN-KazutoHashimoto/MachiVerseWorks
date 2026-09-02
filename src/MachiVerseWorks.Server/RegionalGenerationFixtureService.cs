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
                    new WorldVolume(-500_000d, -500_000d, -12_000d, 500_000d, 500_000d, 12_000d),
                    new RegionalGenerationOptions(
                        RegionalGenerationQualityPreset.Draft,
                        settlementCount: 2,
                        iterationBudget: 1));
            }

            // Phase 31 overlays must travel through the same live Server/Gateway/Web path as the
            // Phase 30 geometry. Advancing the deterministic E2E fixture gives the browser a
            // non-trivial authoritative evolution snapshot whose stable IDs can be joined back to
            // the RegionalGeneration baseline without View-side semantic inference.
            if (!world.HasPersistentRegionalEvolution)
            {
                _ = world.CreatePersistentRegionalEvolutionSnapshot();
                world.AdvancePersistentRegionalEvolutionYears(25);
            }
            return true;
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
