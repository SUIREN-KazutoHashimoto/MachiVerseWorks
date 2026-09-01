using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class WaterSewerFixtureHostedService(
    SimulationRuntime simulation,
    IConfiguration configuration) : BackgroundService
{
    private WaterPipeId _waterPipeId;
    private SewageTreatmentPlantId _treatmentPlantId;
    private WaterNodeId _serviceWaterNodeId;
    private SewerNodeId _serviceSewerNodeId;
    private BuildingId _additionalBuildingId;
    private bool _enabled;
    private bool _seeded;
    private bool _additionalDemandAdded;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _enabled = bool.TryParse(configuration["Simulation:WaterSewerFixture"], out var enabled) && enabled;
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
                var cycle = tick % 100UL;
                simulation.Mutate(world =>
                {
                    if (!_seeded) return true;
                    world.SetWaterPipeInService(_waterPipeId, cycle is < 60UL or >= 80UL);
                    world.SetSewageTreatmentPlantOperatingState(
                        _treatmentPlantId,
                        cycle is >= 20UL and < 40UL ? UtilityOperatingState.Offline : UtilityOperatingState.Online);
                    if (!_additionalDemandAdded && tick >= 10UL)
                    {
                        world.CreateWaterSewerServicePoint(
                            _serviceWaterNodeId,
                            _serviceSewerNodeId,
                            7d,
                            buildingId: _additionalBuildingId);
                        _additionalDemandAdded = true;
                    }
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
        var primaryBuilding = world.CreateBuilding(new WorldVolume(40d, -8d, 0d, 50d, 2d, 8d), BuildingKind.Industrial);
        _additionalBuildingId = world.CreateBuilding(new WorldVolume(40d, 4d, 0d, 50d, 14d, 8d), BuildingKind.Commercial);

        var sourceNode = world.CreateWaterNode(new WorldPoint(0d, -20d, 0d), WaterNodeKind.Source);
        var pumpNode = world.CreateWaterNode(new WorldPoint(20d, -20d, 0d), WaterNodeKind.Pump);
        _serviceWaterNodeId = world.CreateWaterNode(new WorldPoint(45d, 0d, 0d), WaterNodeKind.Service);
        world.CreateWaterPipe(sourceNode, pumpNode, 40d);
        _waterPipeId = world.CreateWaterPipe(pumpNode, _serviceWaterNodeId, 30d);
        world.CreateWaterSource(sourceNode, 40d);
        world.CreateWaterPump(pumpNode, 30d);

        _serviceSewerNodeId = world.CreateSewerNode(new WorldPoint(45d, 0d, -3d), SewerNodeKind.Service);
        var treatmentNode = world.CreateSewerNode(new WorldPoint(5d, 20d, -3d), SewerNodeKind.Treatment);
        world.CreateSewerPipe(_serviceSewerNodeId, treatmentNode, 30d);
        _treatmentPlantId = world.CreateSewageTreatmentPlant(treatmentNode, 30d);
        world.CreateWaterSewerServicePoint(_serviceWaterNodeId, _serviceSewerNodeId, 12d, buildingId: primaryBuilding);
    }
}
