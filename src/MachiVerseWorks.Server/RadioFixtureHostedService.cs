using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class RadioFixtureHostedService(SimulationRuntime simulation, IConfiguration configuration) : BackgroundService
{
    private RadioSiteId _interfererSiteId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["Simulation:RadioFixture"], out var enabled) || !enabled) return;
        simulation.Mutate(world => { Seed(world); return true; });
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var phase = simulation.TickCount % 90UL;
                simulation.Mutate(world =>
                {
                    world.SetRadioSiteInService(_interfererSiteId, phase < 30UL || phase >= 60UL);
                    return true;
                });
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private void Seed(SimulationWorld world)
    {
        var band = world.CreateSpectrumBand("n78-debug", 3_300d, 3_800d);
        var blockA = world.CreateFrequencyBlock(band, 3_500d, 20d);
        var blockB = world.CreateFrequencyBlock(band, 3_505d, 20d);
        var source = world.CreateRadioSite(new WorldPoint(0d, 0d, 30d), RadioSiteKind.Macro, 15d, 30d);
        var receiver = world.CreateRadioSite(new WorldPoint(850d, 0d, 12d), RadioSiteKind.Micro, 5d, 12d);
        _interfererSiteId = world.CreateRadioSite(new WorldPoint(150d, 120d, 25d), RadioSiteKind.Macro, 15d, 25d);
        var interferenceReceiver = world.CreateRadioSite(new WorldPoint(900d, 120d, 12d), RadioSiteKind.Micro, 5d, 12d);
        var budget = new RadioLinkBudget(new TransmitterPathBudget(new EffectiveRadiatedPower(43d), 1d, 1d), 2d, -100d, 8d);
        world.CreateRadioLink(source, receiver, blockA, budget, 0.72d);
        world.CreateRadioLink(_interfererSiteId, interferenceReceiver, blockB, budget, 0.55d);
        world.CreateRadioPeer([source], [receiver]);
    }
}
