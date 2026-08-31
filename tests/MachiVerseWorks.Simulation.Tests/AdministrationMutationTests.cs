using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class AdministrationMutationTests
{
    [TestMethod]
    public void AgentUpdatePreservesStableIdAndUpdatesSpatialState()
    {
        var world = new SimulationWorld();
        var id = world.CreateAgent(new WorldPoint(0d, 0d, 0d), new WorldVector(0d, 0d, 0d));

        Assert.IsTrue(world.UpdateAgent(id, new WorldPoint(10d, 20d, 30d), new WorldVector(1d, 2d, 3d)));
        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var snapshot));
        Assert.AreEqual(id, snapshot.Id);
        Assert.AreEqual(new WorldPoint(10d, 20d, 30d), snapshot.Position);
        Assert.AreEqual(new WorldVector(1d, 2d, 3d), snapshot.Velocity);
    }

    [TestMethod]
    public void BuildingUpdateRejectsBoundsThatExcludeLinkedPoi()
    {
        var world = new SimulationWorld();
        var building = world.CreateBuilding(new WorldVolume(0d, 0d, 0d, 10d, 10d, 10d));
        _ = world.CreatePoi(new WorldPoint(5d, 5d, 5d), buildingId: building);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            world.UpdateBuilding(building, new WorldVolume(0d, 0d, 0d, 2d, 2d, 2d), BuildingKind.Residential));

        Assert.IsTrue(world.TryGetBuildingSnapshot(building, out var snapshot));
        Assert.AreEqual(new WorldVolume(0d, 0d, 0d, 10d, 10d, 10d), snapshot.Bounds);
    }

    [TestMethod]
    public void PoiUpdateRejectsPositionOutsideLinkedBuilding()
    {
        var world = new SimulationWorld();
        var building = world.CreateBuilding(new WorldVolume(0d, 0d, 0d, 10d, 10d, 10d));
        var poi = world.CreatePoi(new WorldPoint(5d, 5d, 5d), buildingId: building);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            world.UpdatePoi(poi, new WorldPoint(20d, 20d, 20d), PoiKind.Generic, building));
    }

    [TestMethod]
    public void RailwayUpdateUsesCheckpointValidationAndPreservesReferences()
    {
        var world = new SimulationWorld();
        var first = world.CreateTrackNode(new WorldPoint(0d, 0d, 0d));
        var second = world.CreateTrackNode(new WorldPoint(100d, 0d, 0d));
        var segment = world.CreateTrackSegment(first, second);

        Assert.IsTrue(world.UpdateTrackSegment(segment, first, second, TrackDirection.StartToEnd, 1.435d, 30d, TrackElectrification.Overhead, TrackUsage.Mainline));
        var snapshot = world.CreateRailwayInfrastructureSnapshot();
        var updated = snapshot.Segments.Single(x => x.Id == segment);
        Assert.AreEqual(TrackDirection.StartToEnd, updated.Direction);
        Assert.AreEqual(30d, updated.SpeedLimitMetersPerSecond);

        Assert.ThrowsExactly<ArgumentException>(() => world.RemoveTrackNode(first));
        Assert.IsTrue(world.CreateRailwayInfrastructureSnapshot().Nodes.Any(x => x.Id == first));
    }
}
