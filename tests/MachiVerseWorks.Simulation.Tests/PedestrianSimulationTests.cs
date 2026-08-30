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
