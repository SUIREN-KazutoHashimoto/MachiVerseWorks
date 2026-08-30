using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

public class RailwayOperationsBenchmarks
{
    private SimulationWorld _world = null!;

    [Params(100, 1_000)]
    public int TrainCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 0x18UL, spatialCellSize: 64d));
        var n0 = _world.CreateTrackNode(new WorldPoint(-100d, 0d, 2d));
        var n1 = _world.CreateTrackNode(new WorldPoint(-60d, 0d, 2d), TrackNodeKind.Junction);
        var n2 = _world.CreateTrackNode(new WorldPoint(0d, 0d, 2d), TrackNodeKind.Junction);
        var n3 = _world.CreateTrackNode(new WorldPoint(60d, 0d, 2d), TrackNodeKind.Junction);
        var n4 = _world.CreateTrackNode(new WorldPoint(100d, 0d, 2d));
        var depotOut = _world.CreateTrackSegment(n0, n1, TrackDirection.StartToEnd, 1.067d, 10d, TrackElectrification.Overhead, TrackUsage.Depot);
        var westMain = _world.CreateTrackSegment(n1, n2, TrackDirection.StartToEnd, 1.067d, 18d, TrackElectrification.Overhead, TrackUsage.Mainline);
        var eastMain = _world.CreateTrackSegment(n2, n3, TrackDirection.StartToEnd, 1.067d, 18d, TrackElectrification.Overhead, TrackUsage.Mainline);
        var depotIn = _world.CreateTrackSegment(n3, n4, TrackDirection.StartToEnd, 1.067d, 10d, TrackElectrification.Overhead, TrackUsage.Depot);
        _world.CreateTrackConnection(depotOut, westMain, n1);
        _world.CreateTrackConnection(westMain, eastMain, n2);
        _world.CreateTrackConnection(eastMain, depotIn, n3);
        _world.CreateBlockSection([depotOut]);
        _world.CreateBlockSection([westMain]);
        _world.CreateBlockSection([eastMain]);
        _world.CreateBlockSection([depotIn]);

        var stationA = _world.CreateStation(new WorldVolume(-38d, -6d, 0d, -12d, 6d, 7d));
        var platformA = _world.CreatePlatform(stationA, westMain, 0.55d, 0.85d, new WorldVolume(-30d, -4d, 1d, -8d, -2d, 3d));
        var stationB = _world.CreateStation(new WorldVolume(22d, -6d, 0d, 50d, 6d, 7d));
        var platformB = _world.CreatePlatform(stationB, eastMain, 0.45d, 0.75d, new WorldVolume(24d, -4d, 1d, 48d, -2d, 3d));
        var originDepot = _world.CreateDepot(new WorldVolume(-105d, -8d, 0d, -55d, 8d, 7d), [depotOut]);
        var destinationDepot = _world.CreateDepot(new WorldVolume(55d, -8d, 0d, 105d, 8d, 7d), [depotIn]);
        var formation = _world.CreateTrainFormation(42d, 18d, 1.4d, 1.8d, 180);
        var route = _world.CreateRailwayRoute([depotOut, westMain, eastMain, depotIn]);
        var timetable = _world.CreateTimetable([
            new TimetableStopSnapshot(stationA, 330, 345, 10, platformA),
            new TimetableStopSnapshot(stationB, 580, 595, 10, platformB),
        ]);

        for (var index = 0; index < TrainCount; index++)
        {
            var service = _world.CreateRailwayService(formation, route, timetable, originDepot, destinationDepot, plannedStartTick: checked((ulong)(index + 1)));
            _world.CreateTrain(service);
        }
        for (var tick = 0; tick < 60; tick++) _world.Step();
    }

    [Benchmark]
    public void FixedTickOperations() => _world.Step();

    [Benchmark]
    public RailwayOperationsSnapshot CreateOperationsSnapshot() => _world.CreateRailwayOperationsSnapshot();

    [Benchmark]
    public TrainSnapshot[] CreateTrainSnapshot() => _world.CreateTrainSnapshot();
}
