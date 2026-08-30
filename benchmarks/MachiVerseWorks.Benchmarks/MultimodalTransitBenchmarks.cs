using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class MultimodalTransitBenchmarks
{
    private SimulationWorld _journeyWorld = null!;
    private SimulationCheckpoint _dispatchCheckpoint = null!;
    private SimulationCheckpoint _transferCheckpoint = null!;
    private SimulationWorld _dispatchWorld = null!;
    private SimulationWorld _transferWorld = null!;
    private BuildingId _origin;
    private BuildingId _destination;
    private ulong _nextTripRequestId = 1_000_000;

    [Params(25, 100)]
    public int Scale { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _journeyWorld = CreateJourneyWorld(Scale, out _origin, out _destination);
        _dispatchCheckpoint = CreateDispatchWorld(Scale).CreateCheckpoint();
        _transferCheckpoint = CreateTransferWorld().CreateCheckpoint();
    }

    [InvocationSetup(Target = nameof(DispatchNearestTaxi))]
    public void ResetDispatchWorld() => _dispatchWorld = SimulationWorld.RestoreCheckpoint(_dispatchCheckpoint);

    [InvocationSetup(Target = nameof(TransferCheckpointContinuation))]
    public void ResetTransferWorld() => _transferWorld = SimulationWorld.RestoreCheckpoint(_transferCheckpoint);

    [Benchmark]
    public JourneyId JourneyPlanning()
    {
        var request = new TripRequest(
            new TripRequestId(_nextTripRequestId++),
            TripEndpoint.ForBuilding(_origin),
            TripEndpoint.ForBuilding(_destination),
            TravelMode.Any);
        return _journeyWorld.PlanMultimodalJourney(request);
    }

    [Benchmark]
    public TaxiRequestId DispatchNearestTaxi()
    {
        var request = _dispatchWorld.CreateTaxiRequest(
            new TripRequestId(2_000_000),
            new WorldPoint(0d, 0d, 0d),
            new WorldPoint(100d, 0d, 0d));
        _dispatchWorld.DispatchTaxiRequests();
        return request;
    }

    [Benchmark]
    public PassengerSnapshot TransferCheckpointContinuation()
    {
        for (var index = 0; index < 32; index++) _transferWorld.Step();
        return _transferWorld.CreateMultimodalTransitSnapshot().Passengers.Single();
    }

    private static SimulationWorld CreateJourneyWorld(int scale, out BuildingId origin, out BuildingId destination)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 19_015, spatialCellSize: 64d));
        var start = world.CreateRoadNode(new WorldPoint(-500d, 0d, 0d));
        var end = world.CreateRoadNode(new WorldPoint(500d, 0d, 0d));
        var segment = world.CreateRoadSegment(start, end, RoadKind.Local);
        var lane = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 12d);
        origin = world.CreateBuilding(new WorldVolume(-492d, -2d, 0d, -488d, 2d, 4d), BuildingKind.Residential);
        destination = world.CreateBuilding(new WorldVolume(488d, -2d, 0d, 492d, 2d, 4d), BuildingKind.Commercial);
        world.CreateRoadAccessPoint(segment, 0.01d, origin, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        world.CreateRoadAccessPoint(segment, 0.99d, destination, mode: RoadAccessMode.Foot | RoadAccessMode.Motor);
        var line = world.CreateTransitLine(TransitMode.Bus);
        var stops = new TransitPatternStopSnapshot[scale * 2];
        for (var index = 0; index < stops.Length; index++)
        {
            var x = -450d + (900d * index / Math.Max(1, stops.Length - 1));
            var stop = world.CreateBusStop(lane, new WorldPoint(x, 0d, 0d));
            stops[index] = new TransitPatternStopSnapshot(stop, index == 0 ? 0UL : 20UL, 2UL);
        }
        world.CreateTransitServicePattern(line, stops);
        return world;
    }

    private static SimulationWorld CreateDispatchWorld(int scale)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 19_016, spatialCellSize: 64d));
        for (var index = 0; index < scale; index++)
        {
            var x = (index - (scale / 2d)) * 4d;
            world.CreateTaxiVehicle(new WorldPoint(x, index % 7, 0d));
        }
        return world;
    }

    private static SimulationWorld CreateTransferWorld()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 19_017, spatialCellSize: 64d));
        var start = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var end = world.CreateRoadNode(new WorldPoint(100d, 0d, 0d));
        var segment = world.CreateRoadSegment(start, end, RoadKind.Local);
        var lane = world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d);
        var origin = world.CreateBuilding(new WorldVolume(-1d, -1d, 0d, 1d, 1d, 2d), BuildingKind.Residential);
        var destination = world.CreateBuilding(new WorldVolume(99d, -1d, 0d, 101d, 1d, 2d), BuildingKind.Commercial);
        world.CreateRoadAccessPoint(segment, 0d, origin, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 1d, destination, mode: RoadAccessMode.Foot);
        var a = world.CreateBusStop(lane, new WorldPoint(10d, 0d, 0d));
        var b = world.CreateBusStop(lane, new WorldPoint(40d, 0d, 0d));
        var c = world.CreateBusStop(lane, new WorldPoint(60d, 0d, 0d));
        var d = world.CreateBusStop(lane, new WorldPoint(90d, 0d, 0d));
        var firstLine = world.CreateTransitLine(TransitMode.Bus);
        var secondLine = world.CreateTransitLine(TransitMode.Bus);
        world.CreateTransitServicePattern(firstLine, [new TransitPatternStopSnapshot(a, 0, 2), new TransitPatternStopSnapshot(b, 10, 2)]);
        world.CreateTransitServicePattern(secondLine, [new TransitPatternStopSnapshot(c, 0, 2), new TransitPatternStopSnapshot(d, 10, 2)]);
        var request = new TripRequest(new TripRequestId(19_017), TripEndpoint.ForBuilding(origin), TripEndpoint.ForBuilding(destination), TravelMode.Any);
        var journey = world.PlanMultimodalJourney(request);
        world.CreatePassenger(request.Id, journey);
        for (var index = 0; index < 250; index++) world.Step();
        return world;
    }
}
