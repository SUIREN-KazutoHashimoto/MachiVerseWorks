using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class IntersectionControlBenchmarks
{
    private SimulationWorld _world = null!;

    [Params(10, 100, 500)]
    public int IntersectionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = BuildQueuedWorld(IntersectionCount);
        for (var tick = 0; tick < _world.Config.TickRate * 5; tick++) _world.Step();
    }

    [Benchmark]
    public TrafficMetrics QueuedIntersectionTick()
    {
        _world.Step();
        return _world.CreateTrafficMetrics();
    }

    [Benchmark]
    public IntersectionControlSnapshot ControllerSnapshot() => _world.CreateIntersectionControlSnapshot();

    private static SimulationWorld BuildQueuedWorld(int intersectionCount)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, spatialCellSize: 64d));
        var flows = new List<Flow>(intersectionCount * 4);
        for (var index = 0; index < intersectionCount; index++)
        {
            var x = index * 100d;
            var center = world.CreateRoadNode(new WorldPoint(x, 0d, 0d), RoadNodeKind.Intersection);
            var west = CreateArm(world, center, new WorldPoint(x - 30d, 0d, 0d));
            var east = CreateArm(world, center, new WorldPoint(x + 30d, 0d, 0d));
            var south = CreateArm(world, center, new WorldPoint(x, -30d, 0d));
            var north = CreateArm(world, center, new WorldPoint(x, 30d, 0d));
            flows.Add(CreateFlow(world, center, west, east));
            flows.Add(CreateFlow(world, center, east, west));
            flows.Add(CreateFlow(world, center, south, north));
            flows.Add(CreateFlow(world, center, north, south));
        }

        foreach (var flow in flows)
        {
            world.CreateVehicle([
                new RouteLaneStep(flow.To.Outbound, flow.To.Segment, 0d, 0d, 0d, 0d, null),
            ]);
            world.CreateVehicle([
                new RouteLaneStep(flow.From.Inbound, flow.From.Segment, 1d, 0d, 30d, 3d, flow.Connection),
                new RouteLaneStep(flow.To.Outbound, flow.To.Segment, 0d, 1d, 30d, 3d, null),
            ], initialSpeedMetersPerSecond: 8d);
        }
        return world;
    }

    private static Flow CreateFlow(SimulationWorld world, RoadNodeId center, Arm from, Arm to) =>
        new(from, to, world.CreateLaneConnection(from.Inbound, to.Outbound, center, TurnMovement.Straight));

    private static Arm CreateArm(SimulationWorld world, RoadNodeId center, WorldPoint endpoint)
    {
        var endpointId = world.CreateRoadNode(endpoint);
        var segment = world.CreateRoadSegment(center, endpointId, RoadKind.Local);
        return new Arm(
            segment,
            world.CreateLane(segment, LaneDirection.Reverse, 0, speedLimitMetersPerSecond: 10d),
            world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 10d));
    }

    private readonly record struct Arm(RoadSegmentId Segment, LaneId Inbound, LaneId Outbound);
    private readonly record struct Flow(Arm From, Arm To, LaneConnectionId Connection);
}
