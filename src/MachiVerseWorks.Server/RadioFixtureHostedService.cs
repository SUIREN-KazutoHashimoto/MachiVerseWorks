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
        world.CreateBuilding(new WorldVolume(390d, -45d, 0d, 480d, 45d, 75d), BuildingKind.Commercial);

        var band = world.CreateSpectrumBand("radio-debug", 3_300d, 3_800d);
        var channelA = world.CreateRadioChannel(band, 3_500d, 20d);
        var channelB = world.CreateRadioChannel(band, 3_505d, 20d);
        var channelC = world.CreateRadioChannel(band, 3_700d, 20d);

        var source = world.CreateRadioSite(new WorldPoint(0d, 0d, 0d), RadioSiteKind.Macro, 15d, 30d);
        var receiverSite = world.CreateRadioSite(new WorldPoint(850d, 0d, 0d), RadioSiteKind.Micro, 5d, 12d);
        _interfererSiteId = world.CreateRadioSite(new WorldPoint(150d, 120d, 0d), RadioSiteKind.Macro, 15d, 25d);
        var interferenceReceiverSite = world.CreateRadioSite(new WorldPoint(900d, 120d, 0d), RadioSiteKind.Micro, 5d, 12d);

        var sourceAntenna = world.CreateRadioAntenna(source, new WorldVector(0d, 0d, 30d), new WorldVector(1d, 0d, 0d), 15d, RadioAntennaPatternKind.Directional, 90d, 20d);
        var receiverAntenna = world.CreateRadioAntenna(receiverSite, new WorldVector(0d, 0d, 12d), new WorldVector(-1d, 0d, 0d), 5d, RadioAntennaPatternKind.Directional, 100d, 15d);
        var interfererAntenna = world.CreateRadioAntenna(_interfererSiteId, new WorldVector(0d, 0d, 25d), new WorldVector(1d, 0d, 0d), 15d, RadioAntennaPatternKind.Directional, 120d, 20d);
        var interferenceReceiverAntenna = world.CreateRadioAntenna(interferenceReceiverSite, new WorldVector(0d, 0d, 12d), new WorldVector(-1d, 0d, 0d), 5d, RadioAntennaPatternKind.Directional, 100d, 15d);

        var sourceTransmitter = world.CreateRadioTransmitter(source, sourceAntenna, 46d);
        var interfererTransmitter = world.CreateRadioTransmitter(_interfererSiteId, interfererAntenna, 46d);
        var receiver = world.CreateRadioReceiver(receiverSite, receiverAntenna, 3_300d, 3_800d, -105d);
        var interferenceReceiver = world.CreateRadioReceiver(interferenceReceiverSite, interferenceReceiverAntenna, 3_300d, 3_800d, -105d);

        var primaryEmission = world.CreateRadioEmission(sourceTransmitter, channelA, 43d, 0.72d);
        var secondaryEmission = world.CreateRadioEmission(sourceTransmitter, channelC, 39d, 0.24d);
        var interferingEmission = world.CreateRadioEmission(interfererTransmitter, channelB, 43d, 0.55d);

        world.CreateRadioLink(primaryEmission, receiver, 8d);
        world.CreateRadioLink(secondaryEmission, receiver, 8d);
        world.CreateRadioLink(interferingEmission, interferenceReceiver, 8d);
        world.CreateRadioPeer([source], [receiverSite]);
    }
}
