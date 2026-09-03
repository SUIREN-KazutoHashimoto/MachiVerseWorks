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
        var serviceDefinitionValidator = new Internal.RailwayOperationsStore(new RailwayInfrastructureSnapshot(
            (checkpoint.TrackNodes ?? []).Select(static item => new TrackNodeSnapshot(item.Id, item.Kind, item.Position)).ToArray(),
            (checkpoint.TrackSegments ?? []).Select(static item => new TrackSegmentSnapshot(item.Id, item.StartNodeId, item.EndNodeId, item.Direction, item.GaugeMeters, item.SpeedLimitMetersPerSecond, item.Electrification, item.Usage)).ToArray(),
            (checkpoint.TrackConnections ?? []).Select(static item => new TrackConnectionSnapshot(item.Id, item.FromSegmentId, item.ToSegmentId, item.ViaNodeId)).ToArray(),
            (checkpoint.BlockSections ?? []).Select(static item => new BlockSectionSnapshot(item.Id, item.SegmentIds.ToArray())).ToArray(),
            (checkpoint.Stations ?? []).Select(static item => new StationSnapshot(item.Id, item.Bounds)).ToArray(),
            (checkpoint.Platforms ?? []).Select(static item => new PlatformSnapshot(item.Id, item.StationId, item.TrackSegmentId, item.StartSegmentOffset, item.EndSegmentOffset, item.Bounds)).ToArray(),
            (checkpoint.PlatformAccessPoints ?? []).Select(static item => new PlatformAccessPointSnapshot(item.Id, item.PlatformId, item.RoadAccessPointId)).ToArray(),
            (checkpoint.Depots ?? []).Select(static item => new DepotSnapshot(item.Id, item.Bounds, item.TrackSegmentIds.ToArray())).ToArray()));

        var formationById = new Dictionary<TrainFormationId, TrainFormationSnapshot>();
        foreach (var formation in formations)
        {
            if (formation.Id.Value == 0 || !formationById.TryAdd(formation.Id, formation))
                throw new ArgumentException($"Train formation ID {formation.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (!IsPositiveFiniteRailwayOperation(formation.LengthMeters)
                || !IsPositiveFiniteRailwayOperation(formation.MaximumSpeedMetersPerSecond)
                || !IsPositiveFiniteRailwayOperation(formation.MaximumAccelerationMetersPerSecondSquared)
                || !IsPositiveFiniteRailwayOperation(formation.ServiceDecelerationMetersPerSecondSquared)
                || formation.Capacity <= 0)
                throw new ArgumentException($"Train formation {formation.Id.Value} contains invalid physical values.", nameof(checkpoint));
        }

        var routeById = new Dictionary<RailwayRouteId, RailwayRouteSnapshot>();
        foreach (var route in routes)
        {
            if (route.Id.Value == 0 || !routeById.TryAdd(route.Id, route))
                throw new ArgumentException($"Railway route ID {route.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (route.TrackSegmentIds is null || route.TrackSegmentIds.Count == 0 || !IsPositiveFiniteRailwayOperation(route.LengthMeters))
                throw new ArgumentException($"Railway route {route.Id.Value} is empty or has an invalid length.", nameof(checkpoint));
            var localSegments = new HashSet<TrackSegmentId>();
            foreach (var segmentId in route.TrackSegmentIds)
            {
                if (!segmentIds.Contains(segmentId) || !localSegments.Add(segmentId))
                    throw new ArgumentException($"Railway route {route.Id.Value} references a missing or repeated TrackSegment {segmentId.Value}.", nameof(checkpoint));
            }
            var derivedLength = serviceDefinitionValidator.GetDerivedRouteLength(route);
            var lengthTolerance = Math.Max(1e-7, derivedLength * 1e-9);
            if (Math.Abs(route.LengthMeters - derivedLength) > lengthTolerance)
                throw new ArgumentException($"Railway route {route.Id.Value} length does not match its Track topology.", nameof(checkpoint));
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
                || !routeById.TryGetValue(service.RouteId, out var route)
                || !timetableById.TryGetValue(service.TimetableId, out var timetable)
                || !depotIds.Contains(service.OriginDepotId)
                || !depotIds.Contains(service.DestinationDepotId))
                throw new ArgumentException($"Railway service {service.Id.Value} contains a missing formation, route, timetable, or depot reference.", nameof(checkpoint));
            if (service.NextStopIndex < 0 || service.NextStopIndex > timetable.Stops.Count)
                throw new ArgumentException($"Railway service {service.Id.Value} has an invalid next stop index.", nameof(checkpoint));
            serviceDefinitionValidator.ValidateServiceDefinition(route, timetable, service.OriginDepotId, service.DestinationDepotId);
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

            if (train.CurrentPlatformId is { } currentPlatform)
            {
                if (!platformById.ContainsKey(currentPlatform))
                    throw new ArgumentException($"Train {train.Id.Value} references missing current Platform {currentPlatform.Value}.", nameof(checkpoint));
                if (train.State != TrainMovementState.Dwelling || train.AssignedPlatformId != currentPlatform)
                    throw new ArgumentException($"Train {train.Id.Value} has a current Platform inconsistent with its movement state or assignment.", nameof(checkpoint));
            }
            if (train.State == TrainMovementState.Dwelling
                && (train.CurrentPlatformId is null || train.AssignedPlatformId != train.CurrentPlatformId))
                throw new ArgumentException($"Dwelling Train {train.Id.Value} must occupy and retain assignment of the same Platform.", nameof(checkpoint));
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
            if (service.TrainId is not { } trainId)
            {
                if (service.State != RailwayServiceState.Planned || trains.Any(train => train.ServiceId == service.Id))
                    throw new ArgumentException($"Railway service {service.Id.Value} has invalid Train lifecycle state.", nameof(checkpoint));
                continue;
            }
            if (!trainById.TryGetValue(trainId, out var train) || train.ServiceId != service.Id)
                throw new ArgumentException($"Railway service {service.Id.Value} references a missing or mismatched Train.", nameof(checkpoint));
            var timetable = timetableById[service.TimetableId];
            if ((service.State == RailwayServiceState.Completed) != (train.State == TrainMovementState.Completed))
                throw new ArgumentException($"Railway service {service.Id.Value} and Train {train.Id.Value} disagree about completion.", nameof(checkpoint));
            switch (service.State)
            {
                case RailwayServiceState.Planned:
                    if (train.State is not (TrainMovementState.InDepot or TrainMovementState.WaitingForBlock)
                        || train.RouteDistanceMeters > 1e-7 || train.SpeedMetersPerSecond > 1e-9
                        || train.CurrentDepotId != service.OriginDepotId || service.NextStopIndex != 0)
                        throw new ArgumentException($"Planned Railway service {service.Id.Value} has inconsistent Train state.", nameof(checkpoint));
                    break;
                case RailwayServiceState.Active:
                    if (train.State is TrainMovementState.InDepot or TrainMovementState.Completed || train.CurrentDepotId is not null)
                        throw new ArgumentException($"Active Railway service {service.Id.Value} has inconsistent Train state.", nameof(checkpoint));
                    break;
                case RailwayServiceState.Completed:
                    if (train.CurrentDepotId != service.DestinationDepotId || service.NextStopIndex != timetable.Stops.Count)
                        throw new ArgumentException($"Completed Railway service {service.Id.Value} is not finalized at its destination.", nameof(checkpoint));
                    break;
            }
        }
    }

    private static bool IsPositiveFiniteRailwayOperation(double value) => double.IsFinite(value) && value > 0d;
}
