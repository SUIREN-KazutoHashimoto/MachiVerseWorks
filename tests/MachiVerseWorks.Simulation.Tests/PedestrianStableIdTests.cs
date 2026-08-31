using MachiVerseWorks.Simulation.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class PedestrianStableIdTests
{
    private const ulong LegacyMaximum = (1UL << 63) - 1;

    [TestMethod]
    public void RebuildSupportsFull64BitRoadAndAccessIdsWithoutCollisions()
    {
        var snapshot = CreateBoundarySnapshot(reverse: false);
        var store = new PedestrianNetworkStore();

        store.Rebuild(snapshot);
        var pedestrian = store.CreateSnapshot();

        Assert.AreEqual(6, pedestrian.Nodes.Count);
        Assert.AreEqual(6, pedestrian.Nodes.Select(static item => item.Id.Value).Distinct().Count());
        Assert.IsTrue(pedestrian.Nodes.Any(item => item.RoadNodeId == new RoadNodeId(LegacyMaximum) && item.Id.Value == LegacyMaximum));
        Assert.IsTrue(pedestrian.Nodes.Any(item => item.RoadAccessPointId == new RoadAccessPointId(LegacyMaximum) && item.Id.Value == ulong.MaxValue));
        Assert.IsTrue(pedestrian.Nodes.Any(item => item.RoadNodeId == new RoadNodeId(LegacyMaximum + 1)));
        Assert.IsTrue(pedestrian.Nodes.Any(item => item.RoadNodeId == new RoadNodeId(ulong.MaxValue)));
        Assert.IsTrue(pedestrian.Nodes.Any(item => item.RoadAccessPointId == new RoadAccessPointId(LegacyMaximum + 1)));
        Assert.IsTrue(pedestrian.Nodes.Any(item => item.RoadAccessPointId == new RoadAccessPointId(ulong.MaxValue)));
    }

    [TestMethod]
    public void Full64BitDerivedIdsAreDeterministicAcrossInputOrdering()
    {
        var forward = new PedestrianNetworkStore();
        var reverse = new PedestrianNetworkStore();

        forward.Rebuild(CreateBoundarySnapshot(reverse: false));
        reverse.Rebuild(CreateBoundarySnapshot(reverse: true));

        CollectionAssert.AreEqual(
            forward.CreateSnapshot().Nodes.OrderBy(static item => item.Id.Value).ToArray(),
            reverse.CreateSnapshot().Nodes.OrderBy(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(
            forward.CreateSnapshot().Edges.OrderBy(static item => item.Id.Value).ToArray(),
            reverse.CreateSnapshot().Edges.OrderBy(static item => item.Id.Value).ToArray());
    }

    private static RoadNetworkSnapshot CreateBoundarySnapshot(bool reverse)
    {
        var nodeIds = new[]
        {
            new RoadNodeId(LegacyMaximum),
            new RoadNodeId(LegacyMaximum + 1),
            new RoadNodeId(ulong.MaxValue),
        };
        var nodes = new[]
        {
            new RoadNodeSnapshot(nodeIds[0], RoadNodeKind.Endpoint, new WorldPoint(0d, 0d, 0d)),
            new RoadNodeSnapshot(nodeIds[1], RoadNodeKind.Intersection, new WorldPoint(10d, 0d, 0d)),
            new RoadNodeSnapshot(nodeIds[2], RoadNodeKind.Endpoint, new WorldPoint(20d, 0d, 0d)),
        };
        var segments = new[]
        {
            new RoadSegmentSnapshot(new RoadSegmentId(1), RoadKind.Local, nodeIds[0], nodeIds[1]),
            new RoadSegmentSnapshot(new RoadSegmentId(2), RoadKind.Local, nodeIds[1], nodeIds[2]),
        };
        var access = new[]
        {
            new RoadAccessPointSnapshot(new RoadAccessPointId(LegacyMaximum), new RoadSegmentId(1), 0.25d, new BuildingId(1), null, RoadAccessMode.Foot),
            new RoadAccessPointSnapshot(new RoadAccessPointId(LegacyMaximum + 1), new RoadSegmentId(1), 0.75d, new BuildingId(2), null, RoadAccessMode.Foot),
            new RoadAccessPointSnapshot(new RoadAccessPointId(ulong.MaxValue), new RoadSegmentId(2), 0.50d, null, new PoiId(1), RoadAccessMode.Foot),
        };
        if (reverse)
        {
            Array.Reverse(nodes);
            Array.Reverse(segments);
            Array.Reverse(access);
        }
        return new RoadNetworkSnapshot(nodes, segments, [], [], access);
    }
}
