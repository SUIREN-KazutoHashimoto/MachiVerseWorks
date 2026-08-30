using System.Text.Json;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Persistence;

public static partial class WorldSaveSerializer
{
    private static void ValidateRailwayOperationsCheckpointWithinLimits(SimulationCheckpoint checkpoint, WorldSaveLimits limits)
    {
        ValidateCount(checkpoint.TrainFormations?.Count ?? 0, limits.MaximumVehicleCount, "TrainFormations");
        ValidateCount(checkpoint.RailwayRoutes?.Count ?? 0, limits.MaximumRoadSegmentCount, "RailwayRoutes");
        ValidateCount(checkpoint.Timetables?.Count ?? 0, limits.MaximumVehicleCount, "Timetables");
        ValidateCount(checkpoint.RailwayServices?.Count ?? 0, limits.MaximumVehicleCount, "RailwayServices");
        ValidateCount(checkpoint.Trains?.Count ?? 0, limits.MaximumVehicleCount, "Trains");
    }

    private static SaveRailwayOperationsData CreateRailwayOperationsData(SimulationCheckpoint checkpoint) => new()
    {
        NextFormationId = checkpoint.NextTrainFormationId,
        Formations = (checkpoint.TrainFormations ?? []).Select(static item => new SaveTrainFormationData
        {
            Id = item.Id.Value,
            LengthMeters = item.LengthMeters,
            MaximumSpeedMetersPerSecond = item.MaximumSpeedMetersPerSecond,
            MaximumAccelerationMetersPerSecondSquared = item.MaximumAccelerationMetersPerSecondSquared,
            ServiceDecelerationMetersPerSecondSquared = item.ServiceDecelerationMetersPerSecondSquared,
            Capacity = item.Capacity,
        }).ToArray(),
        NextRouteId = checkpoint.NextRailwayRouteId,
        Routes = (checkpoint.RailwayRoutes ?? []).Select(static item => new SaveRailwayRouteData
        {
            Id = item.Id.Value,
            TrackSegmentIds = item.TrackSegmentIds.Select(static id => (ulong?)id.Value).ToArray(),
            LengthMeters = item.LengthMeters,
        }).ToArray(),
        NextTimetableId = checkpoint.NextTimetableId,
        Timetables = (checkpoint.Timetables ?? []).Select(static item => new SaveTimetableData
        {
            Id = item.Id.Value,
            Stops = item.Stops.Select(static stop => new SaveTimetableStopData
            {
                StationId = stop.StationId.Value,
                PlannedArrivalTick = stop.PlannedArrivalTick,
                PlannedDepartureTick = stop.PlannedDepartureTick,
                MinimumDwellTicks = stop.MinimumDwellTicks,
                PreferredPlatformId = stop.PreferredPlatformId?.Value,
            }).ToArray(),
        }).ToArray(),
        NextServiceId = checkpoint.NextRailwayServiceId,
        Services = (checkpoint.RailwayServices ?? []).Select(static item => new SaveRailwayServiceData
        {
            Id = item.Id.Value,
            FormationId = item.FormationId.Value,
            RouteId = item.RouteId.Value,
            TimetableId = item.TimetableId.Value,
            OriginDepotId = item.OriginDepotId.Value,
            DestinationDepotId = item.DestinationDepotId.Value,
            PlannedStartTick = item.PlannedStartTick,
            State = (byte)item.State,
            DelayTicks = item.DelayTicks,
            NextStopIndex = item.NextStopIndex,
            TrainId = item.TrainId?.Value,
        }).ToArray(),
        NextTrainId = checkpoint.NextTrainId,
        Trains = (checkpoint.Trains ?? []).Select(static item => new SaveTrainData
        {
            Id = item.Id.Value,
            FormationId = item.FormationId.Value,
            ServiceId = item.ServiceId.Value,
            RouteId = item.RouteId.Value,
            RouteDistanceMeters = item.RouteDistanceMeters,
            X = item.Position.X,
            Y = item.Position.Y,
            Z = item.Position.Z,
            ForwardX = item.Forward.X,
            ForwardY = item.Forward.Y,
            ForwardZ = item.Forward.Z,
            SpeedMetersPerSecond = item.SpeedMetersPerSecond,
            State = (byte)item.State,
            CurrentBlockId = item.CurrentBlockId?.Value,
            CurrentPlatformId = item.CurrentPlatformId?.Value,
            AssignedPlatformId = item.AssignedPlatformId?.Value,
            CurrentDepotId = item.CurrentDepotId?.Value,
            DwellDepartureTick = item.DwellDepartureTick,
            TickCount = item.TickCount,
        }).ToArray(),
    };

    private static void ValidateRailwayOperationsDataCounts(SaveRailwayOperationsData? data, bool enabled, WorldSaveLimits limits)
    {
        if (!enabled) return;
        ArgumentNullException.ThrowIfNull(data);
        ValidateCount(data.Formations?.Length ?? throw new InvalidDataException("Save Data is missing RailwayOperations Formation state."), limits.MaximumVehicleCount, "TrainFormations");
        ValidateCount(data.Routes?.Length ?? throw new InvalidDataException("Save Data is missing RailwayOperations Route state."), limits.MaximumRoadSegmentCount, "RailwayRoutes");
        ValidateCount(data.Timetables?.Length ?? throw new InvalidDataException("Save Data is missing RailwayOperations Timetable state."), limits.MaximumVehicleCount, "Timetables");
        ValidateCount(data.Services?.Length ?? throw new InvalidDataException("Save Data is missing RailwayOperations Service state."), limits.MaximumVehicleCount, "RailwayServices");
        ValidateCount(data.Trains?.Length ?? throw new InvalidDataException("Save Data is missing RailwayOperations Train state."), limits.MaximumVehicleCount, "Trains");
        var stopCount = 0;
        foreach (var timetable in data.Timetables)
        {
            if (timetable?.Stops is null) throw new InvalidDataException("Save Data is missing a RailwayOperations Timetable stop list.");
            stopCount = checked(stopCount + timetable.Stops.Length);
        }
        ValidateCount(stopCount, limits.MaximumRoadAccessPointCount, "TimetableStops");
    }

    private static RestoredRailwayOperations RestoreRailwayOperations(SaveRailwayOperationsData? data, bool enabled)
    {
        if (!enabled) return new RestoredRailwayOperations(1, [], 1, [], 1, [], 1, [], 1, []);
        ArgumentNullException.ThrowIfNull(data);
        var formationsData = data.Formations ?? throw new InvalidDataException("Save Data is missing RailwayOperations Formation state.");
        var routesData = data.Routes ?? throw new InvalidDataException("Save Data is missing RailwayOperations Route state.");
        var timetablesData = data.Timetables ?? throw new InvalidDataException("Save Data is missing RailwayOperations Timetable state.");
        var servicesData = data.Services ?? throw new InvalidDataException("Save Data is missing RailwayOperations Service state.");
        var trainsData = data.Trains ?? throw new InvalidDataException("Save Data is missing RailwayOperations Train state.");

        var formations = new TrainFormationSnapshot[formationsData.Length];
        for (var index = 0; index < formations.Length; index++)
        {
            var item = formationsData[index] ?? throw new InvalidDataException($"TrainFormation entry {index} is null.");
            formations[index] = new TrainFormationSnapshot(
                new TrainFormationId(Require(item.Id, $"railwayOperations.formations[{index}].id")),
                Require(item.LengthMeters, $"railwayOperations.formations[{index}].lengthMeters"),
                Require(item.MaximumSpeedMetersPerSecond, $"railwayOperations.formations[{index}].maximumSpeedMetersPerSecond"),
                Require(item.MaximumAccelerationMetersPerSecondSquared, $"railwayOperations.formations[{index}].maximumAccelerationMetersPerSecondSquared"),
                Require(item.ServiceDecelerationMetersPerSecondSquared, $"railwayOperations.formations[{index}].serviceDecelerationMetersPerSecondSquared"),
                Require(item.Capacity, $"railwayOperations.formations[{index}].capacity"));
        }

        var routes = new RailwayRouteSnapshot[routesData.Length];
        for (var index = 0; index < routes.Length; index++)
        {
            var item = routesData[index] ?? throw new InvalidDataException($"RailwayRoute entry {index} is null.");
            var idsData = item.TrackSegmentIds ?? throw new InvalidDataException($"RailwayRoute entry {index} is missing TrackSegment IDs.");
            var ids = new TrackSegmentId[idsData.Length];
            for (var step = 0; step < ids.Length; step++) ids[step] = new TrackSegmentId(Require(idsData[step], $"railwayOperations.routes[{index}].trackSegmentIds[{step}]"));
            routes[index] = new RailwayRouteSnapshot(new RailwayRouteId(Require(item.Id, $"railwayOperations.routes[{index}].id")), ids, Require(item.LengthMeters, $"railwayOperations.routes[{index}].lengthMeters"));
        }

        var timetables = new TimetableSnapshot[timetablesData.Length];
        for (var index = 0; index < timetables.Length; index++)
        {
            var item = timetablesData[index] ?? throw new InvalidDataException($"Timetable entry {index} is null.");
            var stopData = item.Stops ?? throw new InvalidDataException($"Timetable entry {index} is missing Stops.");
            var stops = new TimetableStopSnapshot[stopData.Length];
            for (var stopIndex = 0; stopIndex < stops.Length; stopIndex++)
            {
                var stop = stopData[stopIndex] ?? throw new InvalidDataException($"Timetable stop entry {index}:{stopIndex} is null.");
                stops[stopIndex] = new TimetableStopSnapshot(
                    new StationId(Require(stop.StationId, $"railwayOperations.timetables[{index}].stops[{stopIndex}].stationId")),
                    Require(stop.PlannedArrivalTick, $"railwayOperations.timetables[{index}].stops[{stopIndex}].plannedArrivalTick"),
                    Require(stop.PlannedDepartureTick, $"railwayOperations.timetables[{index}].stops[{stopIndex}].plannedDepartureTick"),
                    Require(stop.MinimumDwellTicks, $"railwayOperations.timetables[{index}].stops[{stopIndex}].minimumDwellTicks"),
                    stop.PreferredPlatformId is { } platformId ? new PlatformId(platformId) : null);
            }
            timetables[index] = new TimetableSnapshot(new TimetableId(Require(item.Id, $"railwayOperations.timetables[{index}].id")), stops);
        }

        var services = new RailwayServiceSnapshot[servicesData.Length];
        for (var index = 0; index < services.Length; index++)
        {
            var item = servicesData[index] ?? throw new InvalidDataException($"RailwayService entry {index} is null.");
            var state = (RailwayServiceState)Require(item.State, $"railwayOperations.services[{index}].state");
            if (!Enum.IsDefined(state)) throw new InvalidDataException($"RailwayService entry {index} has an invalid state.");
            services[index] = new RailwayServiceSnapshot(
                new RailwayServiceId(Require(item.Id, $"railwayOperations.services[{index}].id")),
                new TrainFormationId(Require(item.FormationId, $"railwayOperations.services[{index}].formationId")),
                new RailwayRouteId(Require(item.RouteId, $"railwayOperations.services[{index}].routeId")),
                new TimetableId(Require(item.TimetableId, $"railwayOperations.services[{index}].timetableId")),
                new DepotId(Require(item.OriginDepotId, $"railwayOperations.services[{index}].originDepotId")),
                new DepotId(Require(item.DestinationDepotId, $"railwayOperations.services[{index}].destinationDepotId")),
                Require(item.PlannedStartTick, $"railwayOperations.services[{index}].plannedStartTick"),
                state,
                Require(item.DelayTicks, $"railwayOperations.services[{index}].delayTicks"),
                Require(item.NextStopIndex, $"railwayOperations.services[{index}].nextStopIndex"),
                item.TrainId is { } trainId ? new TrainId(trainId) : null);
        }

        var trains = new TrainSnapshot[trainsData.Length];
        for (var index = 0; index < trains.Length; index++)
        {
            var item = trainsData[index] ?? throw new InvalidDataException($"Train entry {index} is null.");
            var state = (TrainMovementState)Require(item.State, $"railwayOperations.trains[{index}].state");
            if (!Enum.IsDefined(state)) throw new InvalidDataException($"Train entry {index} has an invalid state.");
            trains[index] = new TrainSnapshot(
                new TrainId(Require(item.Id, $"railwayOperations.trains[{index}].id")),
                new TrainFormationId(Require(item.FormationId, $"railwayOperations.trains[{index}].formationId")),
                new RailwayServiceId(Require(item.ServiceId, $"railwayOperations.trains[{index}].serviceId")),
                new RailwayRouteId(Require(item.RouteId, $"railwayOperations.trains[{index}].routeId")),
                Require(item.RouteDistanceMeters, $"railwayOperations.trains[{index}].routeDistanceMeters"),
                new WorldPoint(Require(item.X, $"railwayOperations.trains[{index}].x"), Require(item.Y, $"railwayOperations.trains[{index}].y"), Require(item.Z, $"railwayOperations.trains[{index}].z")),
                new WorldVector(Require(item.ForwardX, $"railwayOperations.trains[{index}].forwardX"), Require(item.ForwardY, $"railwayOperations.trains[{index}].forwardY"), Require(item.ForwardZ, $"railwayOperations.trains[{index}].forwardZ")),
                Require(item.SpeedMetersPerSecond, $"railwayOperations.trains[{index}].speedMetersPerSecond"),
                state,
                item.CurrentBlockId is { } blockId ? new BlockSectionId(blockId) : null,
                item.CurrentPlatformId is { } currentPlatformId ? new PlatformId(currentPlatformId) : null,
                item.AssignedPlatformId is { } assignedPlatformId ? new PlatformId(assignedPlatformId) : null,
                item.CurrentDepotId is { } depotId ? new DepotId(depotId) : null,
                Require(item.DwellDepartureTick, $"railwayOperations.trains[{index}].dwellDepartureTick"),
                Require(item.TickCount, $"railwayOperations.trains[{index}].tickCount"));
        }

        return new RestoredRailwayOperations(
            Require(data.NextFormationId, "railwayOperations.nextFormationId"), formations,
            Require(data.NextRouteId, "railwayOperations.nextRouteId"), routes,
            Require(data.NextTimetableId, "railwayOperations.nextTimetableId"), timetables,
            Require(data.NextServiceId, "railwayOperations.nextServiceId"), services,
            Require(data.NextTrainId, "railwayOperations.nextTrainId"), trains);
    }

    private static void ValidateRailwayOperationsArrayCounts(ref Utf8JsonReader reader, WorldSaveLimits limits)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return;
        var depth = reader.CurrentDepth;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == depth) return;
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != depth + 1) continue;
            if (reader.ValueTextEquals("formations")) ValidateNamedArrayElementCount(ref reader, limits.MaximumVehicleCount, "TrainFormation");
            else if (reader.ValueTextEquals("routes")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadSegmentCount, "RailwayRoute");
            else if (reader.ValueTextEquals("timetables")) ValidateNamedArrayElementCount(ref reader, limits.MaximumVehicleCount, "Timetable");
            else if (reader.ValueTextEquals("services")) ValidateNamedArrayElementCount(ref reader, limits.MaximumVehicleCount, "RailwayService");
            else if (reader.ValueTextEquals("trains")) ValidateNamedArrayElementCount(ref reader, limits.MaximumVehicleCount, "Train");
        }
    }

    private sealed record RestoredRailwayOperations(
        ulong NextFormationId, TrainFormationSnapshot[] Formations,
        ulong NextRouteId, RailwayRouteSnapshot[] Routes,
        ulong NextTimetableId, TimetableSnapshot[] Timetables,
        ulong NextServiceId, RailwayServiceSnapshot[] Services,
        ulong NextTrainId, TrainSnapshot[] Trains);
}
