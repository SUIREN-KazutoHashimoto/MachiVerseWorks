using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class PedestrianBenchmarks
{
    private SimulationWorld _world = null!;
    private TripEndpoint _origin;
    private TripEndpoint _destination;

    [Params(1_000, 10_000)]
    public int PedestrianCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 30, spatialCellSize: 64d));
        var originBuilding = _world.CreateBuilding(new WorldVolume(-2d, -2d, 0d, 2d, 2d, 5d), BuildingKind.Residential);
        var destinationBuilding = _world.CreateBuilding(new WorldVolume(999_998d, -2d, 0d, 1_000_002d, 2d, 5d), BuildingKind.Commercial);
        var start = _world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var end = _world.CreateRoadNode(new WorldPoint(1_000_000d, 0d, 0d));
        var segment = _world.CreateRoadSegment(start, end, RoadKind.Local);
        _world.CreateRoadAccessPoint(segment, 0d, originBuilding, mode: RoadAccessMode.Foot);
        _world.CreateRoadAccessPoint(segment, 1d, destinationBuilding, mode: RoadAccessMode.Foot);
        _origin = TripEndpoint.ForBuilding(originBuilding);
        _destination = TripEndpoint.ForBuilding(destinationBuilding);

        for (var index = 0; index < PedestrianCount; index++)
        {
            _world.CreatePedestrian(
                new TripRequest(new TripRequestId((ulong)index + 1UL), _origin, _destination, TravelMode.Foot),
                1.4d);
        }
    }

    [Benchmark]
    public void FixedTickWithOccupancy() => _world.Step();

    [Benchmark]
    public PedestrianRoute FindWalkingRoute() => _world.FindWalkingRoute(_origin, _destination);
}
