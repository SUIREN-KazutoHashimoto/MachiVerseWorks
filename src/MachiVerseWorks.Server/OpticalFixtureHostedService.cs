using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class OpticalFixtureHostedService(SimulationRuntime simulation, IConfiguration configuration) : BackgroundService
{
    private FiberCableId _primaryCableId;
    private OpticalBackhaulId _backhaulId;
    private GeneratorId _generatorId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["Simulation:OpticalFixture"], out var enabled) || !enabled) return;
        simulation.Mutate(world => { Seed(world); return true; });
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var phase = simulation.TickCount % 105UL;
                simulation.Mutate(world =>
                {
                    world.SetFiberCableInService(_primaryCableId, phase < 40UL || phase >= 60UL);
                    world.SetGeneratorOperatingState(_generatorId, phase >= 60UL && phase < 75UL ? GeneratorOperatingState.Offline : GeneratorOperatingState.Online);
                    world.SetOpticalBackhaulInService(_backhaulId, phase < 75UL || phase >= 90UL);
                    return true;
                });
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private void Seed(SimulationWorld world)
    {
        var officeBuilding = world.CreateBuilding(new WorldVolume(110d, 40d, 0d, 125d, 55d, 10d), BuildingKind.Commercial);

        var powerSource = world.CreatePowerNode(new WorldPoint(90d, 25d, 0d), PowerNodeKind.GeneratorBus);
        var powerLoad = world.CreatePowerNode(new WorldPoint(117d, 35d, 0d), PowerNodeKind.Load);
        world.CreatePowerLine(powerSource, powerLoad, 5d);
        _generatorId = world.CreateGenerator(powerSource, 4d);
        world.CreatePowerLoad(powerLoad, 1d, buildingId: officeBuilding);

        var backbone = world.CreateOpticalNode(new WorldPoint(0d, 45d, 2d), OpticalNodeKind.BackboneGateway);
        var central = world.CreateOpticalNode(new WorldPoint(45d, 45d, 2d), OpticalNodeKind.CentralOffice);
        var alternate = world.CreateOpticalNode(new WorldPoint(45d, 65d, 2d), OpticalNodeKind.Distribution);
        var access = world.CreateOpticalNode(new WorldPoint(117d, 47d, 2d), OpticalNodeKind.Endpoint);
        _primaryCableId = world.CreateFiberCable(backbone, central, 10d);
        world.CreateFiberCable(central, access, 10d);
        world.CreateFiberCable(backbone, alternate, 10d);
        world.CreateFiberCable(alternate, access, 10d);
        _backhaulId = world.CreateOpticalBackhaul(backbone, 10d);
        world.CreateOpticalEquipment(backbone, OpticalEquipmentKind.Router, 10d, requiresPower: false);
        world.CreateOpticalEquipment(access, OpticalEquipmentKind.Onu, 10d, officeBuilding, requiresPower: true);
        world.CreateBuildingOpticalDemand(access, officeBuilding, 8.8d);
        world.CreateRadioBackhaulDemand(access, 0.5d);
    }
}
