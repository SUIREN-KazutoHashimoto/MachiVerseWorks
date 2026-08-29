using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class BuildingPoiTests
{
    [TestMethod]
    public void BuildingsAndPoisUseStableMonotonicIdsAndDetachedSnapshots()
    {
        var world = new SimulationWorld(new SimulationConfig(spatialCellSize: 16d));
        var firstBuilding = world.CreateBuilding(
            new WorldVolume(0d, 0d, 0d, 20d, 30d, 40d),
            BuildingKind.Residential);
        var secondBuilding = world.CreateBuilding(
            new WorldVolume(100d, 100d, 0d, 140d, 150d, 60d),
            BuildingKind.Commercial);
        var firstPoi = world.CreatePoi(
            new WorldPoint(10d, 10d, 5d),
            PoiKind.Residence,
            firstBuilding);

        Assert.AreEqual(1UL, firstBuilding.Value);
        Assert.AreEqual(2UL, secondBuilding.Value);
        Assert.AreEqual(1UL, firstPoi.Value);
        Assert.AreEqual(2, world.BuildingCount);
        Assert.AreEqual(1, world.PoiCount);

        var buildings = world.CreateBuildingSnapshot();
        var pois = world.CreatePoiSnapshot();
        Assert.AreEqual(firstBuilding, buildings[0].Id);
        Assert.AreEqual(secondBuilding, buildings[1].Id);
        Assert.AreEqual(firstBuilding, pois[0].BuildingId.GetValueOrDefault());

        buildings[0] = default;
        pois[0] = default;
        Assert.IsTrue(world.TryGetBuildingSnapshot(firstBuilding, out var currentBuilding));
        Assert.IsTrue(world.TryGetPoiSnapshot(firstPoi, out var currentPoi));
        Assert.AreEqual(BuildingKind.Residential, currentBuilding.Kind);
        Assert.AreEqual(PoiKind.Residence, currentPoi.Kind);
    }

    [TestMethod]
    public void PoiBuildingReferenceMustExistAndContainPoiPositionWithoutConsumingIds()
    {
        var world = new SimulationWorld(new SimulationConfig(spatialCellSize: 16d));
        var building = world.CreateBuilding(new WorldVolume(0d, 0d, 0d, 10d, 10d, 10d));
        var before = world.CreateCheckpoint();

        Assert.ThrowsExactly<ArgumentException>(() =>
            world.CreatePoi(
                new WorldPoint(1d, 1d, 1d),
                PoiKind.Generic,
                new BuildingId(999)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            world.CreatePoi(
                new WorldPoint(11d, 1d, 1d),
                PoiKind.Generic,
                building));

        var after = world.CreateCheckpoint();
        Assert.AreEqual(before.NextPoiId, after.NextPoiId);
        Assert.AreEqual(0, world.PoiCount);

        var validPoi = world.CreatePoi(new WorldPoint(5d, 5d, 5d), PoiKind.Service, building);
        Assert.AreEqual(1UL, validPoi.Value);
    }

    [TestMethod]
    public void ReferencedBuildingCannotBeRemovedUntilPoiIsRemoved()
    {
        var world = new SimulationWorld();
        var building = world.CreateBuilding(new WorldVolume(0d, 0d, 0d, 10d, 10d, 10d));
        var poi = world.CreatePoi(new WorldPoint(5d, 5d, 5d), buildingId: building);

        Assert.ThrowsExactly<InvalidOperationException>(() => world.RemoveBuilding(building));
        Assert.AreEqual(1, world.BuildingCount);
        Assert.IsTrue(world.RemovePoi(poi));
        Assert.IsTrue(world.RemoveBuilding(building));
        Assert.AreEqual(0, world.BuildingCount);
    }

    [TestMethod]
    public void CheckpointRestorePreservesBuildingsPoisAndIdContinuation()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 20, seed: 77, spatialCellSize: 8d));
        var building = world.CreateBuilding(
            new WorldVolume(-20d, -10d, -5d, 20d, 10d, 25d),
            BuildingKind.MixedUse);
        var poi = world.CreatePoi(
            new WorldPoint(0d, 0d, 12d),
            PoiKind.Workplace,
            building);
        world.CreatePoi(new WorldPoint(200d, 300d, 0d), PoiKind.Transit);

        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());

        Assert.IsTrue(restored.TryGetBuildingSnapshot(building, out var restoredBuilding));
        Assert.IsTrue(restored.TryGetPoiSnapshot(poi, out var restoredPoi));
        Assert.AreEqual(BuildingKind.MixedUse, restoredBuilding.Kind);
        Assert.AreEqual(new WorldPoint(0d, 0d, 12d), restoredPoi.Position);
        Assert.AreEqual(building, restoredPoi.BuildingId.GetValueOrDefault());

        var nextBuilding = restored.CreateBuilding(new WorldVolume(500d, 500d, 0d, 510d, 510d, 10d));
        var nextPoi = restored.CreatePoi(new WorldPoint(600d, 600d, 0d));
        Assert.AreEqual(2UL, nextBuilding.Value);
        Assert.AreEqual(3UL, nextPoi.Value);
    }

    [TestMethod]
    public void RestoreRejectsPoiReferencingMissingBuilding()
    {
        var checkpoint = new SimulationCheckpoint(
            TickRate: 30,
            Seed: 1,
            SpatialCellSize: 64d,
            TickCount: 0,
            ElapsedTicks: 0,
            RandomState: 1,
            NextAgentId: 1,
            Agents: Array.Empty<SimulationAgentCheckpoint>(),
            NextBuildingId: 1,
            Buildings: Array.Empty<SimulationBuildingCheckpoint>(),
            NextPoiId: 2,
            Pois:
            [
                new SimulationPoiCheckpoint(
                    new PoiId(1),
                    PoiKind.Generic,
                    new WorldPoint(0d, 0d, 0d),
                    new BuildingId(99)),
            ]);

        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint));
    }

    [TestMethod]
    public void InvalidKindsAreRejectedBeforeStateMutation()
    {
        var world = new SimulationWorld();
        var before = world.CreateCheckpoint();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            world.CreateBuilding(
                new WorldVolume(0d, 0d, 0d, 1d, 1d, 1d),
                (BuildingKind)byte.MaxValue));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            world.CreatePoi(
                new WorldPoint(0d, 0d, 0d),
                (PoiKind)byte.MaxValue));

        var after = world.CreateCheckpoint();
        Assert.AreEqual(before.NextBuildingId, after.NextBuildingId);
        Assert.AreEqual(before.NextPoiId, after.NextPoiId);
        Assert.AreEqual(0, world.BuildingCount);
        Assert.AreEqual(0, world.PoiCount);
    }

    [TestMethod]
    public void ExhaustedBuildingAndPoiIdsRejectCreationWithoutMutation()
    {
        var baseCheckpoint = new SimulationWorld().CreateCheckpoint();
        var buildingWorld = SimulationWorld.RestoreCheckpoint(baseCheckpoint with
        {
            NextBuildingId = ulong.MaxValue,
        });
        var poiWorld = SimulationWorld.RestoreCheckpoint(baseCheckpoint with
        {
            NextPoiId = ulong.MaxValue,
        });

        Assert.ThrowsExactly<OverflowException>(() =>
            buildingWorld.CreateBuilding(new WorldVolume(0d, 0d, 0d, 1d, 1d, 1d)));
        Assert.ThrowsExactly<OverflowException>(() =>
            poiWorld.CreatePoi(new WorldPoint(0d, 0d, 0d)));

        Assert.AreEqual(0, buildingWorld.BuildingCount);
        Assert.AreEqual(ulong.MaxValue, buildingWorld.CreateCheckpoint().NextBuildingId);
        Assert.AreEqual(0, poiWorld.PoiCount);
        Assert.AreEqual(ulong.MaxValue, poiWorld.CreateCheckpoint().NextPoiId);
    }
}
