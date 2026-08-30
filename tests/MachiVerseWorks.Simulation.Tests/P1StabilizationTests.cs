using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class P1StabilizationTests
{
    [TestMethod]
    public void PopulationMotorSpawnConflictFallsBackWithoutFaultingTick()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30));
        var home = world.CreateBuilding(new WorldVolume(0, -3, 0, 4, 3, 4), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(996, -3, 0, 1000, 3, 4), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(1000, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateLane(segment, LaneDirection.Forward, 0);
        world.CreateRoadAccessPoint(segment, 0.01, home, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.99, work, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var schedule = new[]
        {
            new DailyActivityWindow(ActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(work), ActivityPriority.High),
        };
        var first = world.CreatePerson(household, new PersonDemographics(40, IsEmployed: true, HasPrivateVehicle: true), schedule);
        var second = world.CreatePerson(household, new PersonDemographics(41, IsEmployed: true, HasPrivateVehicle: true), schedule);

        world.Step();

        Assert.IsTrue(world.TryGetPersonSnapshot(first, out var firstSnapshot));
        Assert.IsTrue(world.TryGetPersonSnapshot(second, out var secondSnapshot));
        Assert.AreEqual(PersonTravelState.Driving, firstSnapshot.TravelState);
        Assert.AreEqual(PersonTravelState.Walking, secondSnapshot.TravelState);
        Assert.AreEqual(1, world.ActiveVehicleCount);
        Assert.AreEqual(1, world.ActivePedestrianCount);
        Assert.AreEqual(1UL, world.CreateCheckpoint().TickCount);
    }

    [TestMethod]
    public void RoadNetworkRejectsZeroLengthGeometryBeforeTrafficRebuild()
    {
        var world = new SimulationWorld();
        var a = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var b = world.CreateRoadNode(new WorldPoint(0, 0, 0));

        Assert.ThrowsExactly<ArgumentException>(() => world.CreateRoadSegment(a, b));
        Assert.AreEqual(0, world.RoadSegmentCount);

        var c = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        var segment = world.CreateRoadSegment(a, c);
        world.CreateLane(segment, LaneDirection.Forward, 0);

        Assert.ThrowsExactly<ArgumentException>(() => world.UpdateRoadNode(c, new WorldPoint(0, 0, 0), RoadNodeKind.Endpoint));
        Assert.IsTrue(world.TryGetRoadNodeSnapshot(c, out var unchanged));
        Assert.AreEqual(new WorldPoint(20, 0, 0), unchanged.Position);
        world.Step();
    }

    [TestMethod]
    public void RoadCheckpointRejectsZeroLengthGeometry()
    {
        var world = new SimulationWorld();
        var a = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var b = world.CreateRoadNode(new WorldPoint(20, 0, 0));
        world.CreateRoadSegment(a, b);
        var checkpoint = world.CreateCheckpoint();
        var invalidNodes = checkpoint.RoadNodes
            .Select(item => item.Id == b ? item with { Position = new WorldPoint(0, 0, 0) } : item)
            .ToArray();

        var invalid = checkpoint with { RoadNodes = invalidNodes };

        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(invalid));
    }

    [TestMethod]
    public void RailwayAggregateMembershipLimitIsEnforcedForCreateAndRestore()
    {
        var oversized = Enumerable.Repeat(
            new TrackSegmentId(1),
            RailwayInfrastructureLimits.MaximumBlockSectionSegmentCount + 1).ToArray();
        var world = new SimulationWorld();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => world.CreateBlockSection(oversized));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => world.CreateDepot(new WorldVolume(0, 0, 0, 1, 1, 1), oversized));

        var checkpoint = world.CreateCheckpoint();
        var invalidBlock = checkpoint with
        {
            NextBlockSectionId = 2,
            BlockSections = [new SimulationBlockSectionCheckpoint(new BlockSectionId(1), oversized)],
        };
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(invalidBlock));

        var invalidDepot = checkpoint with
        {
            NextDepotId = 2,
            Depots = [new SimulationDepotCheckpoint(new DepotId(1), new WorldVolume(0, 0, 0, 1, 1, 1), oversized)],
        };
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(invalidDepot));
    }
}