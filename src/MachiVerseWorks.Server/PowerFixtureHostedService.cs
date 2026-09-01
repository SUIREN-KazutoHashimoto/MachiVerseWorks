using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class PowerFixtureHostedService(
    SimulationRuntime simulation,
    IConfiguration configuration) : BackgroundService
{
    private GeneratorId _generatorId;
    private bool _enabled;
    private bool _seeded;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _enabled = bool.TryParse(configuration["Simulation:PowerFixture"], out var enabled) && enabled;
        if (!_enabled) return;

        simulation.Mutate(world =>
        {
            Seed(world);
            return true;
        });
        _seeded = true;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var tick = simulation.TickCount;
                var cycle = tick % 90UL;
                var target = cycle >= 30UL && cycle < 60UL ? GeneratorOperatingState.Offline : GeneratorOperatingState.Online;
                simulation.Mutate(world =>
                {
                    if (_seeded && world.TryGetGeneratorSnapshot(_generatorId, out var generator) && generator.OperatingState != target)
                        world.SetGeneratorOperatingState(_generatorId, target);
                    return true;
                });
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void Seed(SimulationWorld world)
    {
        var industrial = world.CreateBuilding(new WorldVolume(40d, -6d, 0d, 50d, 6d, 8d), BuildingKind.Industrial);
        var generatorNode = world.CreatePowerNode(new WorldPoint(0d, 30d, 0d), PowerNodeKind.GeneratorBus);
        var substation = world.CreatePowerNode(new WorldPoint(25d, 30d, 0d), PowerNodeKind.Substation);
        var loadNode = world.CreatePowerNode(new WorldPoint(45d, 0d, 0d), PowerNodeKind.Load);
        world.CreatePowerLine(generatorNode, substation, 25d);
        world.CreatePowerLine(substation, loadNode, 15d);
        _generatorId = world.CreateGenerator(generatorNode, 20d);
        world.CreatePowerLoad(loadNode, 12d, buildingId: industrial);
    }
}
