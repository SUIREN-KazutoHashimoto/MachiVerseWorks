namespace MachiVerseWorks.Simulation;

public sealed record RailwayInfrastructureFixture(
    StationId StationId,
    PlatformId PlatformId,
    PlatformAccessPointId PlatformAccessPointId,
    DepotId DepotId,
    TripEndpoint WalkingOrigin);

public static class RailwayInfrastructureFixtures
{
    public static RailwayInfrastructureFixture SeedDeterministic(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var originBuilding = world.CreateBuilding(new WorldVolume(-42d, -8d, 0d, -38d, -4d, 4d), BuildingKind.Residential);
        var entranceBuilding = world.CreateBuilding(new WorldVolume(-4d, -8d, 0d, 0d, -4d, 4d), BuildingKind.Commercial);
        var roadStart = world.CreateRoadNode(new WorldPoint(-40d, -6d, 0d));
        var roadEnd = world.CreateRoadNode(new WorldPoint(-2d, -6d, 0d));
        var accessRoad = world.CreateRoadSegment(roadStart, roadEnd, RoadKind.Local);
        world.CreateRoadAccessPoint(accessRoad, 0d, originBuilding, mode: RoadAccessMode.Foot);
        var stationRoadAccess = world.CreateRoadAccessPoint(accessRoad, 1d, entranceBuilding, mode: RoadAccessMode.Foot);

        var west = world.CreateTrackNode(new WorldPoint(-60d, 0d, 0d));
        var switchNode = world.CreateTrackNode(new WorldPoint(-10d, 0d, 0d), TrackNodeKind.Switch);
        var east = world.CreateTrackNode(new WorldPoint(60d, 0d, 0d));
        var depotEnd = world.CreateTrackNode(new WorldPoint(10d, 18d, 0d));
        var westMain = world.CreateTrackSegment(west, switchNode, TrackDirection.Bidirectional, 1.067d, 25d, TrackElectrification.Overhead, TrackUsage.Mainline);
        var eastMain = world.CreateTrackSegment(switchNode, east, TrackDirection.Bidirectional, 1.067d, 25d, TrackElectrification.Overhead, TrackUsage.Mainline);
        var depotTrack = world.CreateTrackSegment(switchNode, depotEnd, TrackDirection.Bidirectional, 1.067d, 8d, TrackElectrification.Overhead, TrackUsage.Depot);
        world.CreateTrackConnection(westMain, eastMain, switchNode);
        world.CreateTrackConnection(eastMain, westMain, switchNode);
        world.CreateTrackConnection(westMain, depotTrack, switchNode);
        world.CreateTrackConnection(depotTrack, westMain, switchNode);
        world.CreateBlockSection([westMain]);
        world.CreateBlockSection([eastMain]);
        world.CreateBlockSection([depotTrack]);

        var parallelWest = world.CreateTrackNode(new WorldPoint(-60d, 5d, 0d));
        var parallelEast = world.CreateTrackNode(new WorldPoint(60d, 5d, 0d));
        var parallel = world.CreateTrackSegment(parallelWest, parallelEast, TrackDirection.Bidirectional, 1.067d, 25d, TrackElectrification.Overhead, TrackUsage.Mainline);
        world.CreateBlockSection([parallel]);

        var elevatedSouth = world.CreateTrackNode(new WorldPoint(20d, -30d, 8d));
        var elevatedNorth = world.CreateTrackNode(new WorldPoint(20d, 30d, 8d));
        var elevated = world.CreateTrackSegment(elevatedSouth, elevatedNorth, TrackDirection.Bidirectional, 1.067d, 18d, TrackElectrification.Overhead, TrackUsage.Mainline);
        world.CreateBlockSection([elevated]);

        var undergroundSouth = world.CreateTrackNode(new WorldPoint(20d, -30d, -8d));
        var undergroundNorth = world.CreateTrackNode(new WorldPoint(20d, 30d, -8d));
        var underground = world.CreateTrackSegment(undergroundSouth, undergroundNorth, TrackDirection.Bidirectional, 1.067d, 18d, TrackElectrification.ThirdRail, TrackUsage.Mainline);
        world.CreateBlockSection([underground]);

        var station = world.CreateStation(new WorldVolume(-8d, -10d, -1d, 28d, 10d, 6d));
        var platform = world.CreatePlatform(station, eastMain, 0d, 0.35d, new WorldVolume(-8d, -3d, 0d, 15d, -1d, 1.2d));
        var platformAccess = world.CreatePlatformAccessPoint(platform, stationRoadAccess);
        var depot = world.CreateDepot(new WorldVolume(-12d, 8d, -1d, 14d, 22d, 4d), [depotTrack]);

        return new RailwayInfrastructureFixture(station, platform, platformAccess, depot, TripEndpoint.ForBuilding(originBuilding));
    }
}
