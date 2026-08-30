using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class PublishReadModelTests
{
    private static readonly ulong[] LeftAgentIds = [1UL];
    private static readonly ulong[] RightAgentIds = [2UL];

    [TestMethod]
    public void SubscriptionQueriesShareOneAtomicTickAcrossEntityKinds()
    {
        var tick = 73UL;
        var agents = new[]
        {
            new AgentSnapshot(new AgentId(1), new WorldPoint(0, 0, 0), new WorldVector(1, 0, 0), tick),
            new AgentSnapshot(new AgentId(2), new WorldPoint(500, 0, 0), default, tick),
        };
        var pedestrians = new[]
        {
            new PedestrianSnapshot(new PedestrianId(1), new TripRequestId(1), new WorldPoint(5, 0, 0), default, 1.4, PedestrianMovementState.Walking, tick),
        };
        var road = new RoadNetworkSnapshot(
            [new RoadNodeSnapshot(new RoadNodeId(1), RoadNodeKind.Endpoint, new WorldPoint(0, 0, 0)), new RoadNodeSnapshot(new RoadNodeId(2), RoadNodeKind.Endpoint, new WorldPoint(20, 0, 0))],
            [new RoadSegmentSnapshot(new RoadSegmentId(1), RoadKind.Local, new RoadNodeId(1), new RoadNodeId(2))],
            [], [], []);
        var published = new SimulationPublishSnapshot(tick, 64, agents, pedestrians, new RoadNetworkReadModel(1, road));

        var local = published.Query(new WorldVolume(-10, -10, -10, 30, 10, 10));

        Assert.AreEqual(tick, local.TickCount);
        Assert.AreEqual(1, local.Agents.Length);
        Assert.AreEqual(tick, local.Agents[0].TickCount);
        Assert.AreEqual(1, local.Pedestrians.Length);
        Assert.AreEqual(tick, local.Pedestrians[0].TickCount);
        Assert.AreEqual(1, local.RoadNetwork.Segments.Count);
    }

    [TestMethod]
    public void DifferentClientVolumesFilterTheSamePublishedStateWithoutSimulationAccess()
    {
        const ulong tick = 10;
        var agents = new[]
        {
            new AgentSnapshot(new AgentId(1), new WorldPoint(-100, 0, 0), default, tick),
            new AgentSnapshot(new AgentId(2), new WorldPoint(100, 0, 0), default, tick),
        };
        var published = new SimulationPublishSnapshot(tick, 64, agents, [], new RoadNetworkReadModel(1, new RoadNetworkSnapshot([], [], [], [], [])));

        var left = published.Query(new WorldVolume(-150, -10, -10, -50, 10, 10));
        var right = published.Query(new WorldVolume(50, -10, -10, 150, 10, 10));

        CollectionAssert.AreEqual(LeftAgentIds, left.Agents.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(RightAgentIds, right.Agents.Select(static item => item.Id.Value).ToArray());
    }

    [TestMethod]
    public void RoadPlannerTurnsOversizedHundredThousandSegmentSnapshotIntoClientError()
    {
        const int segmentCount = 100_000;
        var nodes = new RoadNodeSnapshot[segmentCount + 1];
        for (var index = 0; index < nodes.Length; index++)
            nodes[index] = new RoadNodeSnapshot(new RoadNodeId((ulong)index + 1), RoadNodeKind.Intersection, new WorldPoint(index, 0, 0));
        var segments = new RoadSegmentSnapshot[segmentCount];
        for (var index = 0; index < segments.Length; index++)
            segments[index] = new RoadSegmentSnapshot(new RoadSegmentId((ulong)index + 1), RoadKind.Local, nodes[index].Id, nodes[index + 1].Id);

        var message = RoadSnapshotMessagePlanner.Create(new RoadNetworkSnapshot(nodes, segments, [], [], []), 1);

        var error = message as ProtocolErrorMessage;
        Assert.IsNotNull(error);
        Assert.AreEqual(ProtocolErrorCode.InvalidRequest, error.Code);
        Assert.IsTrue(error.Parameters.Any(static item => item.Key == ProtocolErrorParameterKeys.DetailCode && item.Value == RoadSnapshotMessagePlanner.TooLargeDetailCode));
    }

    [TestMethod]
    public void RoadPlannerKeepsSmallSnapshotAsRoadMessage()
    {
        var snapshot = new RoadNetworkSnapshot(
            [new RoadNodeSnapshot(new RoadNodeId(1), RoadNodeKind.Endpoint, new WorldPoint(0, 0, 0))],
            [], [], [], []);

        var message = RoadSnapshotMessagePlanner.Create(snapshot, 5);

        Assert.IsInstanceOfType<RoadNetworkSnapshotMessage>(message);
    }
}
