namespace MachiVerseWorks.Simulation;

public sealed record RailwayOperationsFixture(
    TrainFormationId FormationId,
    RailwayRouteId RouteId,
    TimetableId FirstTimetableId,
    TimetableId SecondTimetableId,
    RailwayServiceId FirstServiceId,
    RailwayServiceId SecondServiceId,
    TrainId FirstTrainId,
    TrainId SecondTrainId,
    StationId FirstStationId,
    StationId SecondStationId,
    PlatformId FirstPlatformId,
    PlatformId SecondPlatformId,
    DepotId OriginDepotId,
    DepotId DestinationDepotId);

public static class RailwayOperationsFixtures
{
    public static RailwayOperationsFixture SeedDeterministic(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var n0 = world.CreateTrackNode(new WorldPoint(-100d, 24d, 2d));
        var n1 = world.CreateTrackNode(new WorldPoint(-60d, 24d, 2d), TrackNodeKind.Junction);
        var n2 = world.CreateTrackNode(new WorldPoint(0d, 24d, 2d), TrackNodeKind.Junction);
        var n3 = world.CreateTrackNode(new WorldPoint(60d, 24d, 2d), TrackNodeKind.Junction);
        var n4 = world.CreateTrackNode(new WorldPoint(100d, 24d, 2d));
        var depotOut = world.CreateTrackSegment(n0, n1, TrackDirection.StartToEnd, 1.067d, 10d, TrackElectrification.Overhead, TrackUsage.Depot);
        var westMain = world.CreateTrackSegment(n1, n2, TrackDirection.StartToEnd, 1.067d, 18d, TrackElectrification.Overhead, TrackUsage.Mainline);
        var eastMain = world.CreateTrackSegment(n2, n3, TrackDirection.StartToEnd, 1.067d, 18d, TrackElectrification.Overhead, TrackUsage.Mainline);
        var depotIn = world.CreateTrackSegment(n3, n4, TrackDirection.StartToEnd, 1.067d, 10d, TrackElectrification.Overhead, TrackUsage.Depot);
        world.CreateTrackConnection(depotOut, westMain, n1);
        world.CreateTrackConnection(westMain, eastMain, n2);
        world.CreateTrackConnection(eastMain, depotIn, n3);
        world.CreateBlockSection([depotOut]);
        world.CreateBlockSection([westMain]);
        world.CreateBlockSection([eastMain]);
        world.CreateBlockSection([depotIn]);

        var stationA = world.CreateStation(new WorldVolume(-38d, 18d, 0d, -12d, 30d, 7d));
        var platformA = world.CreatePlatform(stationA, westMain, 0.55d, 0.85d, new WorldVolume(-30d, 20d, 1d, -8d, 22d, 3d));
        var stationB = world.CreateStation(new WorldVolume(22d, 18d, 0d, 50d, 30d, 7d));
        var platformB = world.CreatePlatform(stationB, eastMain, 0.45d, 0.75d, new WorldVolume(24d, 20d, 1d, 48d, 22d, 3d));
        var originDepot = world.CreateDepot(new WorldVolume(-105d, 16d, 0d, -55d, 32d, 7d), [depotOut]);
        var destinationDepot = world.CreateDepot(new WorldVolume(55d, 16d, 0d, 105d, 32d, 7d), [depotIn]);

        var formation = world.CreateTrainFormation(42d, 18d, 1.4d, 1.8d, 180);
        var route = world.CreateRailwayRoute([depotOut, westMain, eastMain, depotIn]);
        var timetable1 = world.CreateTimetable([
            new TimetableStopSnapshot(stationA, 80, 100, 10, platformA),
            new TimetableStopSnapshot(stationB, 170, 190, 10, platformB),
        ]);
        var timetable2 = world.CreateTimetable([
            new TimetableStopSnapshot(stationA, 105, 125, 10, platformA),
            new TimetableStopSnapshot(stationB, 195, 215, 10, platformB),
        ]);
        var service1 = world.CreateRailwayService(formation, route, timetable1, originDepot, destinationDepot, plannedStartTick: 1);
        var service2 = world.CreateRailwayService(formation, route, timetable2, originDepot, destinationDepot, plannedStartTick: 2);
        var train1 = world.CreateTrain(service1);
        var train2 = world.CreateTrain(service2);

        return new RailwayOperationsFixture(formation, route, timetable1, timetable2, service1, service2, train1, train2, stationA, stationB, platformA, platformB, originDepot, destinationDepot);
    }
}
