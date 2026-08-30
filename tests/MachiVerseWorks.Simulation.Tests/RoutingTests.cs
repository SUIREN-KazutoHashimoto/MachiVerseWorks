using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RoutingTests
{
    [TestMethod]
    public void ShortestRouteReturnsDirectedImmutableLaneSequence()
    {
        var (world, first, second, _) = CreateLinearFixture();

        var route = world.FindRoadRoute(new RouteRequest(new WorldPoint(2, 0, 0), new WorldPoint(18, 0, 0)));

        Assert.AreEqual(RoutingCostMetric.Distance, route.CostMetric);
        Assert.AreEqual(first, route.OriginLaneId);
        Assert.AreEqual(second, route.DestinationLaneId);
        CollectionAssert.AreEqual(new[] { first, second }, route.Steps.Select(static step => step.LaneId).ToArray());
        Assert.AreEqual(16d, route.TotalDistanceMeters, 1e-9);
        Assert.AreEqual(route.TotalDistanceMeters, route.Cost, 1e-9);
        Assert.AreEqual(0.2d, route.Steps[0].StartSegmentOffset, 1e-9);
        Assert.AreEqual(0.8d, route.Steps[1].EndSegmentOffset, 1e-9);
        Assert.IsNotNull(route.Steps[0].ExitConnectionId);
        Assert.IsNull(route.Steps[^1].ExitConnectionId);
    }

    [TestMethod]
    public void OneWayLaneCannotRouteBackwardWithoutAConnectionLoop()
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(10, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateLane(segment, LaneDirection.Forward, 0);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            world.FindRoadRoute(new RouteRequest(new WorldPoint(8, 0, 0), new WorldPoint(2, 0, 0))));
    }

    [TestMethod]
    public void ClosedLaneAndClosedConnectionAreExcludedFromRouting()
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var firstJunction = world.CreateRoadNode(new WorldPoint(10, 0, 0), RoadNodeKind.Intersection);
        var secondJunction = world.CreateRoadNode(new WorldPoint(20, 0, 0), RoadNodeKind.Intersection);
        var end = world.CreateRoadNode(new WorldPoint(30, 0, 0));
        var first = world.CreateLane(world.CreateRoadSegment(start, firstJunction), LaneDirection.Forward, 0);
        var middle = world.CreateLane(world.CreateRoadSegment(firstJunction, secondJunction), LaneDirection.Forward, 0);
        var last = world.CreateLane(world.CreateRoadSegment(secondJunction, end), LaneDirection.Forward, 0);
        var firstConnection = world.CreateLaneConnection(first, middle, firstJunction);
        world.CreateLaneConnection(middle, last, secondJunction);
        var request = new RouteRequest(new WorldPoint(2, 0, 0), new WorldPoint(28, 0, 0));

        Assert.ThrowsExactly<InvalidOperationException>(() => world.FindRoadRoute(request with
        {
            Constraints = new RouteConstraints(closedLaneIds: [middle]),
        }));
        Assert.ThrowsExactly<InvalidOperationException>(() => world.FindRoadRoute(request with
        {
            Constraints = new RouteConstraints(closedConnectionIds: [firstConnection]),
        }));
    }

    [TestMethod]
    public void EstimatedTravelTimeUsesLaneSpeedLimit()
    {
        var (world, _, _, _) = CreateLinearFixture(firstSpeed: 10d, secondSpeed: 20d);

        var route = world.FindRoadRoute(new RouteRequest(
            new WorldPoint(0, 0, 0),
            new WorldPoint(20, 0, 0),
            RoutingCostMetric.EstimatedTravelTime));

        Assert.AreEqual(1.5d, route.EstimatedTravelTimeSeconds, 1e-9);
        Assert.AreEqual(route.EstimatedTravelTimeSeconds, route.Cost, 1e-9);
        Assert.AreEqual(20d, route.TotalDistanceMeters, 1e-9);
    }

    [TestMethod]
    public void EqualCostAlternativesUseStableConnectionAndLaneIds()
    {
        var world = new SimulationWorld();
        var a = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var firstJunction = world.CreateRoadNode(new WorldPoint(10, 0, 0), RoadNodeKind.Intersection);
        var secondJunction = world.CreateRoadNode(new WorldPoint(20, 0, 0), RoadNodeKind.Intersection);
        var b = world.CreateRoadNode(new WorldPoint(30, 0, 0));
        var incomingSegment = world.CreateRoadSegment(a, firstJunction);
        var middleSegment = world.CreateRoadSegment(firstJunction, secondJunction);
        var outgoingSegment = world.CreateRoadSegment(secondJunction, b);
        var incoming = world.CreateLane(incomingSegment, LaneDirection.Forward, 0);
        var preferredMiddle = world.CreateLane(middleSegment, LaneDirection.Forward, 0);
        var otherMiddle = world.CreateLane(middleSegment, LaneDirection.Forward, 1);
        var outgoing = world.CreateLane(outgoingSegment, LaneDirection.Forward, 0);
        world.CreateLaneConnection(incoming, preferredMiddle, firstJunction);
        world.CreateLaneConnection(incoming, otherMiddle, firstJunction);
        world.CreateLaneConnection(preferredMiddle, outgoing, secondJunction);
        world.CreateLaneConnection(otherMiddle, outgoing, secondJunction);

        var expected = new[] { incoming, preferredMiddle, outgoing };
        for (var iteration = 0; iteration < 5; iteration++)
        {
            var route = world.FindRoadRoute(new RouteRequest(new WorldPoint(1, 0, 0), new WorldPoint(29, 0, 0)));
            CollectionAssert.AreEqual(expected, route.Steps.Select(static step => step.LaneId).ToArray());
        }
    }

    [TestMethod]
    public void ZeroLengthLaneAcceptsPreferredEqualCostPredecessorAfterSettlement()
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var firstJunction = world.CreateRoadNode(new WorldPoint(10, 0, 0), RoadNodeKind.Intersection);
        var secondJunction = world.CreateRoadNode(new WorldPoint(20, 0, 0), RoadNodeKind.Intersection);
        var zeroExit = world.CreateRoadNode(new WorldPoint(20, 0, 0), RoadNodeKind.Intersection);
        var end = world.CreateRoadNode(new WorldPoint(30, 0, 0));
        var incomingSegment = world.CreateRoadSegment(start, firstJunction);
        var middleSegment = world.CreateRoadSegment(firstJunction, secondJunction);
        var zeroSegment = world.CreateRoadSegment(secondJunction, zeroExit);
        var outgoingSegment = world.CreateRoadSegment(zeroExit, end);
        var incoming = world.CreateLane(incomingSegment, LaneDirection.Forward, 0);
        var firstMiddle = world.CreateLane(middleSegment, LaneDirection.Forward, 0);
        var zeroLength = world.CreateLane(zeroSegment, LaneDirection.Forward, 0);
        var preferredMiddle = world.CreateLane(middleSegment, LaneDirection.Forward, 1);
        var outgoing = world.CreateLane(outgoingSegment, LaneDirection.Forward, 0);

        var preferredIntoZero = world.CreateLaneConnection(preferredMiddle, zeroLength, secondJunction);
        world.CreateLaneConnection(incoming, firstMiddle, firstJunction);
        world.CreateLaneConnection(incoming, preferredMiddle, firstJunction);
        var firstIntoZero = world.CreateLaneConnection(firstMiddle, zeroLength, secondJunction);
        world.CreateLaneConnection(zeroLength, outgoing, zeroExit);

        Assert.IsTrue(preferredIntoZero.Value < firstIntoZero.Value);
        Assert.IsTrue(zeroLength.Value < preferredMiddle.Value);

        var route = world.FindRoadRoute(new RouteRequest(new WorldPoint(1, 0, 0), new WorldPoint(29, 0, 0)));

        CollectionAssert.AreEqual(
            new[] { incoming, preferredMiddle, zeroLength, outgoing },
            route.Steps.Select(static step => step.LaneId).ToArray());
        Assert.AreEqual(preferredIntoZero, route.Steps[1].ExitConnectionId);
    }

    [TestMethod]
    public void ThreeDimensionalSnapPrefersMatchingElevation()
    {
        var world = new SimulationWorld();
        var groundStart = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var groundEnd = world.CreateRoadNode(new WorldPoint(100, 0, 0));
        var elevatedStart = world.CreateRoadNode(new WorldPoint(0, 0, 20));
        var elevatedEnd = world.CreateRoadNode(new WorldPoint(100, 0, 20));
        var ground = world.CreateLane(world.CreateRoadSegment(groundStart, groundEnd), LaneDirection.Forward, 0);
        var elevated = world.CreateLane(world.CreateRoadSegment(elevatedStart, elevatedEnd), LaneDirection.Forward, 0);

        var route = world.FindRoadRoute(new RouteRequest(new WorldPoint(10, 0, 19), new WorldPoint(90, 0, 19)));

        Assert.AreEqual(elevated, route.OriginLaneId);
        Assert.AreEqual(elevated, route.DestinationLaneId);
        Assert.AreNotEqual(ground, route.OriginLaneId);
    }

    [TestMethod]
    public void GradeSeparatedCrossingDoesNotCreateImplicitRoute()
    {
        var world = new SimulationWorld();
        var west = world.CreateRoadNode(new WorldPoint(-100, 0, 0));
        var east = world.CreateRoadNode(new WorldPoint(100, 0, 0));
        var south = world.CreateRoadNode(new WorldPoint(0, -100, 20));
        var north = world.CreateRoadNode(new WorldPoint(0, 100, 20));
        world.CreateLane(world.CreateRoadSegment(west, east), LaneDirection.Forward, 0);
        world.CreateLane(world.CreateRoadSegment(south, north), LaneDirection.Forward, 0);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            world.FindRoadRoute(new RouteRequest(new WorldPoint(-90, 0, 0), new WorldPoint(0, 90, 20))));
    }

    [TestMethod]
    public void TopologyMutationInvalidatesCachedRoutes()
    {
        var (world, _, _, connection) = CreateLinearFixture();
        var request = new RouteRequest(new WorldPoint(2, 0, 0), new WorldPoint(18, 0, 0));
        _ = world.FindRoadRoute(request);
        _ = world.FindRoadRoute(request);

        Assert.IsTrue(world.RemoveLaneConnection(connection));
        Assert.ThrowsExactly<InvalidOperationException>(() => world.FindRoadRoute(request));
    }

    [TestMethod]
    public void RouteCacheEvictsLeastRecentlyUsedEntryAtCapacity()
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(10, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateLane(segment, LaneDirection.Forward, 0);
        var firstRequest = new RouteRequest(new WorldPoint(0.001d, 0, 0), new WorldPoint(9d, 0, 0));

        _ = world.FindRoadRoute(firstRequest);
        for (var index = 1; index <= 1024; index++)
        {
            var request = new RouteRequest(
                new WorldPoint(0.001d + index * 1e-9d, 0, 0),
                new WorldPoint(9d, 0, 0));
            _ = world.FindRoadRoute(request);
        }

        var afterFill = world.GetRoutingCacheStatistics();
        Assert.AreEqual(1024, afterFill.Entries);
        Assert.AreEqual(1025L, afterFill.Misses);

        _ = world.FindRoadRoute(firstRequest);
        var afterReplay = world.GetRoutingCacheStatistics();
        Assert.AreEqual(1026L, afterReplay.Misses);
    }

    [TestMethod]
    public void UnknownStableConstraintReferenceIsRejected()
    {
        var (world, _, _, _) = CreateLinearFixture();
        var request = new RouteRequest(
            new WorldPoint(2, 0, 0),
            new WorldPoint(18, 0, 0),
            Constraints: new RouteConstraints(closedLaneIds: [new LaneId(999)]));

        Assert.ThrowsExactly<ArgumentException>(() => world.FindRoadRoute(request));
    }

    private static (SimulationWorld World, LaneId First, LaneId Second, LaneConnectionId Connection) CreateLinearFixture(
        double firstSpeed = 10d,
        double secondSpeed = 10d)
    {
        var world = new SimulationWorld();
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var junction = world.CreateRoadNode(new WorldPoint(10, 0, 0), RoadNodeKind.Intersection);
        var end = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var firstSegment = world.CreateRoadSegment(start, junction);
        var secondSegment = world.CreateRoadSegment(junction, end);
        var first = world.CreateLane(firstSegment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: firstSpeed);
        var second = world.CreateLane(secondSegment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: secondSpeed);
        var connection = world.CreateLaneConnection(first, second, junction, TurnMovement.Straight);
        return (world, first, second, connection);
    }
}
