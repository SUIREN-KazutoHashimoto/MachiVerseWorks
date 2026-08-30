using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class PedestrianSimulationTests
{
    [TestMethod]
    public void DerivedNetworkConnectsFootAccessAndFindsDeterministicThreeDimensionalRoute()
    {
        var fixture = CreateFixture(destinationAltitude: 10d);

        var network = fixture.World.CreatePedestrianNetworkSnapshot();
        var route = fixture.World.FindWalkingRoute(TripEndpoint.ForBuilding(fixture.Origin), TripEndpoint.ForBuilding(fixture.Destination));

        Assert.IsTrue(network.Nodes.Count >= 5);
        Assert.IsTrue(network.Edges.Count >= 4);
        Assert.AreEqual(1, network.Crossings.Count);
        Assert.IsTrue(route.TotalLengthMeters > 0d);
        Assert.IsTrue(route.Legs.Count >= 2);
        var secondRoute = fixture.World.FindWalkingRoute(TripEndpoint.ForBuilding(fixture.Origin), TripEndpoint.ForBuilding(fixture.Destination));
        CollectionAssert.AreEqual(route.Legs.ToArray(), secondRoute.Legs.ToArray());
    }

    [TestMethod]
    public void RoutingConsidersEveryFootAccessForEachEndpoint()
    {
        var world = new SimulationWorld();
        var origin = world.CreateBuilding(new WorldVolume(-2, -2, 0, 2, 2, 3));
        var destination = world.CreateBuilding(new WorldVolume(18, -2, 0, 22, 2, 3));

        var isolatedStart = world.CreateRoadNode(new WorldPoint(-100, 50, 0));
        var isolatedEnd = world.CreateRoadNode(new WorldPoint(-80, 50, 0));
        var isolatedSegment = world.CreateRoadSegment(isolatedStart, isolatedEnd);
        var isolatedAccess = world.CreateRoadAccessPoint(isolatedSegment, 0.5, origin, mode: RoadAccessMode.Foot);

        var connectedStart = world.CreateRoadNode(new WorldPoint(-10, 0, 0));
        var connectedEnd = world.CreateRoadNode(new WorldPoint(30, 0, 0));
        var connectedSegment = world.CreateRoadSegment(connectedStart, connectedEnd);
        var connectedOriginAccess = world.CreateRoadAccessPoint(connectedSegment, 0.25, origin, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(connectedSegment, 0.75, destination, mode: RoadAccessMode.Foot);

        Assert.IsTrue(isolatedAccess.Value < connectedOriginAccess.Value);
        var route = world.FindWalkingRoute(TripEndpoint.ForBuilding(origin), TripEndpoint.ForBuilding(destination));

        Assert.AreEqual((1UL << 63) | connectedOriginAccess.Value, route.StartNodeId.Value);
        Assert.AreEqual(20d, route.TotalLengthMeters, 1e-9);
    }

    [TestMethod]
    public void FixedTickWalkingMovesBuildingToBuildingAndArrives()
    {
        var fixture = CreateFixture();
        var pedestrian = fixture.World.CreatePedestrian(CreateRequest(fixture), walkingSpeedMetersPerSecond: 3d);
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var start));

        for (var tick = 0; tick < 1_000 && fixture.World.ActivePedestrianCount > 0; tick++) fixture.World.Step();

        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var arrived));
        Assert.AreEqual(PedestrianMovementState.Arrived, arrived.State);
        Assert.IsTrue(arrived.Position.X > start.Position.X + 20d);
        Assert.AreEqual(0d, arrived.Velocity.X, 1e-9);
        Assert.AreEqual(0d, arrived.Velocity.Y, 1e-9);
        Assert.AreEqual(0d, arrived.Velocity.Z, 1e-9);
    }

    [TestMethod]
    public void ClosedCrossingBlocksPedestrianUntilPermissionOpens()
    {
        var fixture = CreateFixture();
        var crossing = fixture.World.CreatePedestrianNetworkSnapshot().Crossings.Single();
        Assert.IsTrue(fixture.World.SetPedestrianCrossingOpen(crossing.Id, false));
        var pedestrian = fixture.World.CreatePedestrian(CreateRequest(fixture), walkingSpeedMetersPerSecond: 6d);

        for (var tick = 0; tick < 500; tick++)
        {
            fixture.World.Step();
            Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var snapshot));
            if (snapshot.State == PedestrianMovementState.WaitingForCrossing) break;
        }
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var waiting));
        Assert.AreEqual(PedestrianMovementState.WaitingForCrossing, waiting.State);
        var waitingPosition = waiting.Position;
        for (var tick = 0; tick < 10; tick++) fixture.World.Step();
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var stillWaiting));
        Assert.AreEqual(waitingPosition, stillWaiting.Position);

        Assert.IsTrue(fixture.World.SetPedestrianCrossingOpen(crossing.Id, true));
        fixture.World.Step();
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var resumed));
        Assert.AreNotEqual(PedestrianMovementState.WaitingForCrossing, resumed.State);
    }

    [TestMethod]
    public void CheckpointPreservesClosedCrossingPermissionAndWaitingState()
    {
        var fixture = CreateFixture();
        var crossing = fixture.World.CreatePedestrianNetworkSnapshot().Crossings.Single();
        Assert.IsTrue(fixture.World.SetPedestrianCrossingOpen(crossing.Id, false));
        var pedestrian = fixture.World.CreatePedestrian(CreateRequest(fixture), walkingSpeedMetersPerSecond: 6d);

        for (var tick = 0; tick < 500; tick++)
        {
            fixture.World.Step();
            Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var snapshot));
            if (snapshot.State == PedestrianMovementState.WaitingForCrossing) break;
        }
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var expected));
        Assert.AreEqual(PedestrianMovementState.WaitingForCrossing, expected.State);

        var restored = SimulationWorld.RestoreCheckpoint(fixture.World.CreateCheckpoint());
        Assert.IsFalse(restored.CreatePedestrianNetworkSnapshot().Crossings.Single(item => item.Id == crossing.Id).IsOpen);
        restored.Step();

        Assert.IsTrue(restored.TryGetPedestrianSnapshot(pedestrian, out var actual));
        Assert.AreEqual(PedestrianMovementState.WaitingForCrossing, actual.State);
        Assert.AreEqual(expected.Position, actual.Position);
    }

    [TestMethod]
    public void OccupancyBinsPreventPedestriansFromStackingAtTheSameProgress()
    {
        var fixture = CreateFixture();
        var first = fixture.World.CreatePedestrian(CreateRequest(fixture, 1), walkingSpeedMetersPerSecond: 3d);
        var second = fixture.World.CreatePedestrian(CreateRequest(fixture, 2), walkingSpeedMetersPerSecond: 3d);

        fixture.World.Step();

        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(first, out var firstSnapshot));
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(second, out var secondSnapshot));
        Assert.AreNotEqual(firstSnapshot.Position, secondSnapshot.Position);
        Assert.AreEqual(PedestrianMovementState.WaitingForOccupancy, secondSnapshot.State);
    }

    [TestMethod]
    public void OppositeDirectionOccupancyUsesCanonicalEdgeProgress()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var firstBuilding = world.CreateBuilding(new WorldVolume(0, -2, 0, 2, 2, 3));
        var secondBuilding = world.CreateBuilding(new WorldVolume(8, -2, 0, 10, 2, 3));
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(10, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateRoadAccessPoint(segment, 0.1, firstBuilding, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.9, secondBuilding, mode: RoadAccessMode.Foot);

        var first = world.CreatePedestrian(new TripRequest(new TripRequestId(1), TripEndpoint.ForBuilding(firstBuilding), TripEndpoint.ForBuilding(secondBuilding), TravelMode.Foot), 2d);
        var second = world.CreatePedestrian(new TripRequest(new TripRequestId(2), TripEndpoint.ForBuilding(secondBuilding), TripEndpoint.ForBuilding(firstBuilding), TravelMode.Foot), 6d);

        var sawOccupancyWait = false;
        for (var tick = 0; tick < 45; tick++)
        {
            world.Step();
            Assert.IsTrue(world.TryGetPedestrianSnapshot(first, out var firstSnapshot));
            Assert.IsTrue(world.TryGetPedestrianSnapshot(second, out var secondSnapshot));
            Assert.IsTrue(firstSnapshot.Position.X <= secondSnapshot.Position.X + 1e-9, "Opposite-direction pedestrians crossed through each other on the same physical edge.");
            sawOccupancyWait |= firstSnapshot.State == PedestrianMovementState.WaitingForOccupancy || secondSnapshot.State == PedestrianMovementState.WaitingForOccupancy;
        }
        Assert.IsTrue(sawOccupancyWait);
    }

    [TestMethod]
    public void PedestrianSpatialSnapshotTracksMovementAndRemoval()
    {
        var fixture = CreateFixture();
        var pedestrian = fixture.World.CreatePedestrian(CreateRequest(fixture), walkingSpeedMetersPerSecond: 6d);
        var originVolume = new WorldVolume(-20, -4, -4, -5, 4, 4);
        var destinationVolume = new WorldVolume(5, -4, -4, 20, 4, 4);

        Assert.AreEqual(1, fixture.World.CreatePedestrianSnapshot(originVolume).Length);
        for (var tick = 0; tick < 150; tick++) fixture.World.Step();
        Assert.AreEqual(0, fixture.World.CreatePedestrianSnapshot(originVolume).Length);
        Assert.AreEqual(1, fixture.World.CreatePedestrianSnapshot(destinationVolume).Length);

        Assert.IsTrue(fixture.World.RemovePedestrian(pedestrian));
        Assert.AreEqual(0, fixture.World.CreatePedestrianSnapshot(destinationVolume).Length);
    }

    [TestMethod]
    public void CheckpointRestoresRouteProgressAndDeterministicContinuation()
    {
        var fixture = CreateFixture(destinationAltitude: 8d);
        var pedestrian = fixture.World.CreatePedestrian(CreateRequest(fixture), walkingSpeedMetersPerSecond: 2d);
        for (var tick = 0; tick < 60; tick++) fixture.World.Step();

        var restored = SimulationWorld.RestoreCheckpoint(fixture.World.CreateCheckpoint());
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var expectedBefore));
        Assert.IsTrue(restored.TryGetPedestrianSnapshot(pedestrian, out var actualBefore));
        Assert.AreEqual(expectedBefore, actualBefore);

        for (var tick = 0; tick < 50; tick++) { fixture.World.Step(); restored.Step(); }
        Assert.IsTrue(fixture.World.TryGetPedestrianSnapshot(pedestrian, out var expectedAfter));
        Assert.IsTrue(restored.TryGetPedestrianSnapshot(pedestrian, out var actualAfter));
        Assert.AreEqual(expectedAfter, actualAfter);
    }

    [TestMethod]
    public void RoadTopologyMutationIsRejectedWhilePedestrianRouteReferencesIt()
    {
        var fixture = CreateFixture();
        fixture.World.CreatePedestrian(CreateRequest(fixture));

        Assert.ThrowsExactly<InvalidOperationException>(() => fixture.World.CreateRoadNode(new WorldPoint(100, 100, 0)));
        Assert.AreEqual(3, fixture.World.RoadNodeCount);
    }

    private static TripRequest CreateRequest(Fixture fixture, ulong id = 1) => new(
        new TripRequestId(id),
        TripEndpoint.ForBuilding(fixture.Origin),
        TripEndpoint.ForBuilding(fixture.Destination),
        TravelMode.Foot);

    private static Fixture CreateFixture(double destinationAltitude = 0d)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, spatialCellSize: 32d));
        var origin = world.CreateBuilding(new WorldVolume(-16, -2, 0, -14, 2, 3), BuildingKind.Residential);
        var destination = world.CreateBuilding(new WorldVolume(14, -2, destinationAltitude, 16, 2, destinationAltitude + 3), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(-20, 0, 0));
        var intersection = world.CreateRoadNode(new WorldPoint(0, 0, 0), RoadNodeKind.Intersection);
        var end = world.CreateRoadNode(new WorldPoint(20, 0, destinationAltitude));
        var firstSegment = world.CreateRoadSegment(start, intersection, RoadKind.Local);
        var secondSegment = world.CreateRoadSegment(intersection, end, RoadKind.Local);
        world.CreateRoadAccessPoint(firstSegment, 0.25, origin, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(secondSegment, 0.75, destination, mode: RoadAccessMode.Foot);
        return new Fixture(world, origin, destination);
    }

    private sealed record Fixture(SimulationWorld World, BuildingId Origin, BuildingId Destination);
}
