using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

public class RailwayInfrastructureBenchmarks
{
    private SimulationWorld _world = null!;
    private WorldVolume _queryVolume;

    [Params(10_000, 100_000)]
    public int TrackSegmentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(spatialCellSize: 64d));
        var previousNode = _world.CreateTrackNode(new WorldPoint(0d, 0d, 0d));
        TrackSegmentId? previousSegment = null;
        for (var index = 0; index < TrackSegmentCount; index++)
        {
            var altitude = (index % 5) * 4d;
            var nextKind = index == TrackSegmentCount - 1 ? TrackNodeKind.Endpoint : TrackNodeKind.Junction;
            var nextNode = _world.CreateTrackNode(new WorldPoint((index + 1) * 10d, 0d, altitude), nextKind);
            var segment = _world.CreateTrackSegment(previousNode, nextNode, TrackDirection.Bidirectional, 1.067d, 25d, TrackElectrification.Overhead);
            if (previousSegment is { } incoming)
            {
                _world.CreateTrackConnection(incoming, segment, previousNode);
                _world.CreateTrackConnection(segment, incoming, previousNode);
            }
            previousNode = nextNode;
            previousSegment = segment;
        }

        var middle = TrackSegmentCount * 5d;
        _queryVolume = new WorldVolume(middle - 2_500d, -10d, -1d, middle + 2_500d, 10d, 17d);
    }

    [Benchmark]
    public RailwayInfrastructureSnapshot QuerySpatialVolume() => _world.CreateRailwayInfrastructureSnapshot(_queryVolume);

    [Benchmark]
    public RailwayInfrastructureSnapshot CreateFullTopologySnapshot() => _world.CreateRailwayInfrastructureSnapshot();

    [Benchmark]
    public RailwayInfrastructureValidationResult ValidateConnectivity() => _world.ValidateRailwayInfrastructure();
}
