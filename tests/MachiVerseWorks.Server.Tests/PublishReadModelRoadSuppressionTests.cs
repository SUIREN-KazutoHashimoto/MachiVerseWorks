using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class PublishReadModelRoadSuppressionTests
{
    [TestMethod]
    public void EntityOnlyQueryDoesNotMaterializeRoadTopology()
    {
        var invalidRoad = new RoadNetworkSnapshot(
            [],
            [new RoadSegmentSnapshot(new RoadSegmentId(1), RoadKind.Local, new RoadNodeId(10), new RoadNodeId(11))],
            [], [], []);
        var published = new SimulationPublishSnapshot(
            7,
            64d,
            [new AgentSnapshot(new AgentId(1), new WorldPoint(0, 0, 0), default, 7)],
            [],
            new RoadNetworkReadModel(1, invalidRoad));
        var volume = new WorldVolume(-10, -10, -10, 10, 10, 10);

        var entities = published.QueryEntities(volume);

        Assert.AreEqual(7UL, entities.TickCount);
        Assert.AreEqual(1, entities.Agents.Length);
        Assert.ThrowsExactly<InvalidOperationException>(() => published.RoadNetwork.Query(volume));
    }
}
