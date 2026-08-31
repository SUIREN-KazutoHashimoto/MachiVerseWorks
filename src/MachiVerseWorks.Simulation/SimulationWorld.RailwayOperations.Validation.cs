namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private static void ValidateRailwayOperationsCheckpoint(SimulationCheckpoint checkpoint)
    {
        var formations = checkpoint.TrainFormations ?? [];
        var routes = checkpoint.RailwayRoutes ?? [];
        var timetables = checkpoint.Timetables ?? [];
        var services = checkpoint.RailwayServices ?? [];
        var trains = checkpoint.Trains ?? [];

        ValidateNextId(checkpoint.NextTrainFormationId, formations.Select(static item => item.Id.Value), "Train formation");
        ValidateNextId(checkpoint.NextRailwayRouteId, routes.Select(static item => item.Id.Value), "Railway route");
        ValidateNextId(checkpoint.NextTimetableId, timetables.Select(static item => item.Id.Value), "Timetable");
        ValidateNextId(checkpoint.NextRailwayServiceId, services.Select(static item => item.Id.Value), "Railway service");
        ValidateNextId(checkpoint.NextTrainId, trains.Select(static item => item.Id.Value), "Train");

        var segmentIds = (checkpoint.TrackSegments ?? []).Select(static item => item.Id).ToHashSet();
        var stationIds = (checkpoint.Stations ?? []).Select(static item => item.Id).ToHashSet();
        var platformById = (checkpoint.Platforms ?? []).ToDictionary(static item => item.Id);
        var depotIds = (checkpoint.Depots ?? []).Select(static item => item.Id).ToHashSet();
        var blockIds = (checkpoint.BlockSections ?? []).Select(static item => item.Id).ToHashSet();

        var formationById = new Dictionary<TrainFormationId, TrainFormationSnapshot>();
        foreach (var formation in formations)
        {
            if (formation.Id.Value == 0 || !formationById.TryAdd(formation.Id, formation))
                throw new ArgumentException($"Train formation ID {formation.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (!IsPositiveFinite(formation.LengthMeters)
                || !IsPositiveFinite(formation.MaximumSpeedMetersPerSecond)
                || !IsPositiveFinite(formation.MaximumAccelerationMetersPerSecondSquared)
                || !IsPositiveFinite(formation.ServiceDecelerationMetersPerSecondSquared)
                || formation.Capacity <= 0)
                throw new ArgumentException($"Train formation {formation.Id.Value} contains invalid physical values.", nameof(checkpoint));
        }

        var routeById = new Dictionary<RailwayRouteId, RailwayRouteSnapshot>();
        foreach (var route in routes)
        {
            if (route.Id.Value == 0 || !routeById.TryAdd(route.Id, route))
                throw new ArgumentException($"Railway route ID {route.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (route.TrackSegmentIds is null || route.TrackSegmentIds.Count == 0 || !IsPositiveFinite(route.LengthMeters))
                throw new ArgumentException($"Railway route {route.Id.Value} is empty or has an invalid length.", nameof(checkpoint));
            var localSegments = new HashSet<TrackSegmentId>();
            foreach (var segmentId in route.TrackSegmentIds)
            {
                if (!segmentIds.Contains(segmentId) || !localSegments.Add(segmentId))
                    throw new ArgumentException($"Railway route {route.Id.Value} references a missing or repeated TrackSegment {segmentId.Value}.", nameof(checkpoint));
            }
        }

        var timetableById = new Dictionary<TimetableId, TimetableSnapshot>();
        foreach (var timetable in timetables)
        {
            if (timetable.Id.Value == 0 || !timetableById.TryAdd(timetable.Id, timetable))
                throw new ArgumentException($"Timetable ID {timetable.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (timetable.Stops is null || timetable.Stops.Count == 0)
                throw new ArgumentException($"Timetable {timetable.Id.Value} must contain at least one stop.", nameof(checkpoint));
            ulong previousDeparture = 0;
            for (var index = 0; index < timetable.Stops.Count; index++)
            {
                var stop = timetable.Stops[index];
                if (!stationIds.Contains(stop.StationId))
                    throw new ArgumentException($"Timetable {timetable.Id.Value} references missing Station {stop.StationId.Value}.", nameof(checkpoint));
                if (stop.PlannedDepartureTick < stop.PlannedArrivalTick || (index > 0 && stop.PlannedArrivalTick < previousDeparture))
                    throw new ArgumentException($"Timetable {timetable.Id.Value} has invalid or decreasing planned times.", nameof(checkpoint));
                if (stop.PreferredPlatformId is { } preferred
                    && (!platformById.TryGetValue(preferred, out var platform) || platform.StationId != stop.StationId))
                    throw new ArgumentException($"Timetable {timetable.Id.Value} preferred Platform is missing or belongs to another Station.", nameof(checkpoint));
                previousDeparture = stop.PlannedDepartureTick;
            }
        }

        var serviceById = new Dictionary<RailwayServiceId, RailwayServiceSnapshot>();
        foreach (var service in services)
        {
            if (service.Id.Value == 0 || !serviceById.TryAdd(service.Id, service))
                throw new ArgumentException($"Railway service ID {service.Id.Value} is zero or duplicated.", nameof(checkpoint));
            ValidateEnum(service.State, nameof(checkpoint));
            if (!formationById.ContainsKey(service.FormationId)
                || !routeById.ContainsKey(service.RouteId)
                || !timetableById.TryGetValue(service.TimetableId, out var timetable)
                || !depotIds.Contains(service.OriginDepotId)
                || !depotIds.Contains(service.DestinationDepotId))
                throw new ArgumentException($"Railway service {service.Id.Value} contains a missing formation, route, timetable, or depot reference.", nameof(checkpoint));
            if (service.NextStopIndex < 0 || service.NextStopIndex > timetable.Stops.Count)
                throw new ArgumentException($"Railway service {service.Id.Value} has an invalid next stop index.", nameof(checkpoint));
            if (service.State != RailwayServiceState.Completed && service.NextStopIndex >= timetable.Stops.Count)
                throw new ArgumentException($"Incomplete Railway service {service.Id.Value} has no remaining timetable stop.", nameof(checkpoint));
        }

        var trainById = new Dictionary<TrainId, TrainSnapshot>();
        var blockOwners = new HashSet<BlockSectionId>();
        var platformOwners = new HashSet<PlatformId>();
        foreach (var train in trains)
        {
            if (train.Id.Value == 0 || !trainById.TryAdd(train.Id, train))
                throw new ArgumentException($"Train ID {train.Id.Value} is zero or duplicated.", nameof(checkpoint));
            ValidateEnum(train.State, nameof(checkpoint));
            if (!formationById.ContainsKey(train.FormationId)
                || !serviceById.TryGetValue(train.ServiceId, out var service)
                || !routeById.TryGetValue(train.RouteId, out var route))
                throw new ArgumentException($"Train {train.Id.Value} contains a missing formation, service, or route reference.", nameof(checkpoint));
            if (service.FormationId != train.FormationId || service.RouteId != train.RouteId)
                throw new ArgumentException($"Train {train.Id.Value} does not match its Railway service formation and route.", nameof(checkpoint));
            if (!double.IsFinite(train.RouteDistanceMeters) || train.RouteDistanceMeters < 0d || train.RouteDistanceMeters > route.LengthMeters + 1e-7)
                throw new ArgumentException($"Train {train.Id.Value} has an invalid route distance.", nameof(checkpoint));
            ValidatePoint(train.Position);
            ValidateVector(train.Forward);
            if (!double.IsFinite(train.SpeedMetersPerSecond) || train.SpeedMetersPerSecond < 0d)
                throw new ArgumentException($"Train {train.Id.Value} has an invalid speed.", nameof(checkpoint));
            if (train.TickCount > checkpoint.TickCount)
                throw new ArgumentException($"Train {train.Id.Value} tick is ahead of the Simulation checkpoint.", nameof(checkpoint));
            if (train.CurrentBlockId is { } block && (!blockIds.Contains(block) || !blockOwners.Add(block)))
                throw new ArgumentException($"Train {train.Id.Value} contains a missing or conflicting current BlockSection.", nameof(checkpoint));
            if (train.CurrentPlatformId is { } currentPlatform && !platformById.ContainsKey(currentPlatform))
                throw new ArgumentException($"Train {train.Id.Value} references missing current Platform {currentPlatform.Value}.", nameof(checkpoint));
            if (train.AssignedPlatformId is { } assignedPlatform
                && (!platformById.ContainsKey(assignedPlatform) || !platformOwners.Add(assignedPlatform)))
                throw new ArgumentException($"Train {train.Id.Value} contains a missing or conflicting assigned Platform.", nameof(checkpoint));
            if (train.CurrentDepotId is { } depot && !depotIds.Contains(depot))
                throw new ArgumentException($"Train {train.Id.Value} references missing Depot {depot.Value}.", nameof(checkpoint));
            if (train.State == TrainMovementState.Completed && (train.SpeedMetersPerSecond > 1e-9 || train.RouteDistanceMeters + 1e-7 < route.LengthMeters))
                throw new ArgumentException($"Completed Train {train.Id.Value} is not stopped at the route end.", nameof(checkpoint));
        }

        foreach (var service in services)
        {
            if (service.TrainId is { } trainId)
            {
                if (!trainById.TryGetValue(trainId, out var train) || train.ServiceId != service.Id)
                    throw new ArgumentException($"Railway service {service.Id.Value} references a missing or mismatched Train.", nameof(checkpoint));
            }
            else if (trains.Any(train => train.ServiceId == service.Id))
            {
                throw new ArgumentException($"Railway service {service.Id.Value} is missing its reverse Train reference.", nameof(checkpoint));
            }
        }
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0d;
}