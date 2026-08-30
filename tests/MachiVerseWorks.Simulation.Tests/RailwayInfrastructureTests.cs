using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RailwayInfrastructureTests
{
    [TestMethod]
    public void DeterministicFixtureBuildsValidatedThreeDimensionalTopology()
    {
        var world = new SimulationWorld(new SimulationConfig(spatialCellSize: 16d));
        var fixture = RailwayInfrastructureFixtures.SeedDeterministic(world);
        var snapshot = world.CreateRailwayInfrastructureSnapshot();
        var validation = world.ValidateRailwayInfrastructure();

        Assert.IsTrue(snapshot.Nodes.Count >= 10);
        Assert.IsTrue(snapshot.Segments.Count >= 6);
        Assert.IsTrue(snapshot.Blocks.Count >= 6);
        Assert.AreEqual(1, snapshot.Stations.Count);
        Assert.AreEqual(fixture.StationId, snapshot.Stations[0].Id);
        Assert.AreEqual(fixture.PlatformId, snapshot.Platforms.Single().Id);
        Assert.AreEqual(fixture.DepotId, snapshot.Depots.Single().Id);
        Assert.IsTrue(validation.TrackComponentCount >= 3);
        Assert.AreEqual(snapshot.Connections.Count, validation.TraversableConnectionCount);

        var elevated = world.CreateRailwayInfrastructureSnapshot(new WorldVolume(15d, -5d, 7d, 25d, 5d, 9d));
        var underground = world.CreateRailwayInfrastructureSnapshot(new WorldVolume(15d, -5d, -9d, 25d, 5d, -7d));
        Assert.AreEqual(1, elevated.Segments.Count);
        Assert.AreEqual(1, underground.Segments.Count);
        Assert.AreNotEqual(elevated.Segments[0].Id, underground.Segments[0].Id);
        Assert.AreEqual(0, elevated.Connections.Count);
        Assert.AreEqual(0, underground.Connections.Count);
    }

    [TestMethod]
    public void PlatformAccessUsesPedestrianRoadAccess()
    {
        var world = new SimulationWorld();
        var fixture = RailwayInfrastructureFixtures.SeedDeterministic(world);

        var route = world.FindWalkingRouteToPlatform(fixture.WalkingOrigin, fixture.PlatformId);

        Assert.IsTrue(route.TotalLengthMeters > 0d);
        Assert.IsTrue(route.Legs.Count > 0);
    }

    [TestMethod]
    public void ReferencedRoadAccessPointCannotBeRemovedOrMadeNonWalkable()
    {
        var world = new SimulationWorld();
        RailwayInfrastructureFixtures.SeedDeterministic(world);
        var accessId = world.CreateRailwayInfrastructureSnapshot().PlatformAccessPoints.Single().RoadAccessPointId;
        Assert.IsTrue(world.TryGetRoadAccessPointSnapshot(accessId, out var access));

        Assert.ThrowsExactly<InvalidOperationException>(() => world.UpdateRoadAccessPoint(
            access.Id,
            access.SegmentId,
            access.SegmentOffset,
            access.BuildingId,
            access.PoiId,
            RoadAccessMode.Motor));
        Assert.ThrowsExactly<InvalidOperationException>(() => world.RemoveRoadAccessPoint(access.Id));
        Assert.IsTrue(world.TryGetRoadAccessPointSnapshot(accessId, out var unchanged));
        Assert.IsTrue((unchanged.Mode & RoadAccessMode.Foot) != 0);
    }

    [TestMethod]
    public void CheckpointRoundTripPreservesRailwayStateAndNextIds()
    {
        var world = new SimulationWorld();
        RailwayInfrastructureFixtures.SeedDeterministic(world);
        var expected = world.CreateCheckpoint();

        var restored = SimulationWorld.RestoreCheckpoint(expected);
        var actual = restored.CreateCheckpoint();

        Assert.AreEqual(expected.NextTrackNodeId, actual.NextTrackNodeId);
        Assert.AreEqual(expected.NextTrackSegmentId, actual.NextTrackSegmentId);
        Assert.AreEqual(expected.NextTrackConnectionId, actual.NextTrackConnectionId);
        Assert.AreEqual(expected.NextBlockSectionId, actual.NextBlockSectionId);
        Assert.AreEqual(expected.NextStationId, actual.NextStationId);
        Assert.AreEqual(expected.NextPlatformId, actual.NextPlatformId);
        Assert.AreEqual(expected.NextPlatformAccessPointId, actual.NextPlatformAccessPointId);
        Assert.AreEqual(expected.NextDepotId, actual.NextDepotId);
        CollectionAssert.AreEqual(expected.TrackNodes!.Select(static item => item.Id.Value).ToArray(), actual.TrackNodes!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.TrackSegments!.Select(static item => item.Id.Value).ToArray(), actual.TrackSegments!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.TrackConnections!.Select(static item => item.Id.Value).ToArray(), actual.TrackConnections!.Select(static item => item.Id.Value).ToArray());
    }

    [TestMethod]
    public void SpatialCrossingWithoutSharedNodeNeverCreatesConnectivity()
    {
        var world = new SimulationWorld();
        var west = world.CreateTrackNode(new WorldPoint(-20d, 0d, 0d));
        var east = world.CreateTrackNode(new WorldPoint(20d, 0d, 0d));
        var south = world.CreateTrackNode(new WorldPoint(0d, -20d, 0d));
        var north = world.CreateTrackNode(new WorldPoint(0d, 20d, 0d));
        world.CreateTrackSegment(west, east);
        world.CreateTrackSegment(south, north);

        var snapshot = world.CreateRailwayInfrastructureSnapshot(new WorldVolume(-2d, -2d, -1d, 2d, 2d, 1d));

        Assert.AreEqual(2, snapshot.Segments.Count);
        Assert.AreEqual(0, snapshot.Connections.Count);
        Assert.AreEqual(2, world.ValidateRailwayInfrastructure().TrackComponentCount);
    }
}
