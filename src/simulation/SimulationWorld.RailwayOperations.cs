using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private RailwayOperationsStore? _railwayOperations;

    public bool HasRailwayOperations => _railwayOperations is not null;
    public int TrainFormationCount => RailwayOperations.FormationCount;
    public int RailwayRouteCount => RailwayOperations.RouteCount;
    public int TimetableCount => RailwayOperations.TimetableCount;
    public int RailwayServiceCount => RailwayOperations.ServiceCount;
    public int TrainCount => RailwayOperations.TrainCount;

    public TrainFormationId CreateTrainFormation(
        double lengthMeters,
        double maximumSpeedMetersPerSecond,
        double maximumAccelerationMetersPerSecondSquared,
        double serviceDecelerationMetersPerSecondSquared,
        int capacity) => RailwayOperations.CreateFormation(
            lengthMeters,
            maximumSpeedMetersPerSecond,
            maximumAccelerationMetersPerSecondSquared,
            serviceDecelerationMetersPerSecondSquared,
            capacity);

    public RailwayRouteId CreateRailwayRoute(IReadOnlyList<TrackSegmentId> trackSegmentIds) => RailwayOperations.CreateRoute(trackSegmentIds);

    public TimetableId CreateTimetable(IReadOnlyList<TimetableStopSnapshot> stops) => RailwayOperations.CreateTimetable(stops);

    public RailwayServiceId CreateRailwayService(
        TrainFormationId formationId,
        RailwayRouteId routeId,
        TimetableId timetableId,
        DepotId originDepotId,
        DepotId destinationDepotId,
        ulong plannedStartTick = 0) => RailwayOperations.CreateService(
            formationId,
            routeId,
            timetableId,
            originDepotId,
            destinationDepotId,
            plannedStartTick);

    public TrainId CreateTrain(RailwayServiceId serviceId) => RailwayOperations.CreateTrain(serviceId);

    public RailwayOperationsSnapshot CreateRailwayOperationsSnapshot() => RailwayOperations.CreateSnapshot();

    public TrainSnapshot[] CreateTrainSnapshot() => RailwayOperations.CreateTrainSnapshot();

    public bool TryGetTrainSnapshot(TrainId id, out TrainSnapshot snapshot) =>
        RailwayOperations.TryGetTrainSnapshot(id, out snapshot);

    private RailwayOperationsStore RailwayOperations => _railwayOperations ??= new RailwayOperationsStore(_railway.CreateSnapshot());

    private void StepRailwayOperations(double deltaSeconds, ulong tickCount)
    {
        if (_railwayOperations is not null) _railwayOperations.Step(deltaSeconds, tickCount);
    }

    private void RestoreRailwayOperations(SimulationCheckpoint checkpoint)
    {
        var store = new RailwayOperationsStore(_railway.CreateSnapshot());
        store.Restore(
            checkpoint.NextTrainFormationId,
            checkpoint.TrainFormations ?? Array.Empty<TrainFormationSnapshot>(),
            checkpoint.NextRailwayRouteId,
            checkpoint.RailwayRoutes ?? Array.Empty<RailwayRouteSnapshot>(),
            checkpoint.NextTimetableId,
            checkpoint.Timetables ?? Array.Empty<TimetableSnapshot>(),
            checkpoint.NextRailwayServiceId,
            checkpoint.RailwayServices ?? Array.Empty<RailwayServiceSnapshot>(),
            checkpoint.NextTrainId,
            checkpoint.Trains ?? Array.Empty<TrainSnapshot>());
        _railwayOperations = store;
    }
}
