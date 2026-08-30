from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, content: str) -> None:
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")


def replace_once(path: str, old: str, new: str) -> None:
    content = read(path)
    count = content.count(old)
    if count != 1:
        raise RuntimeError(f"Expected exactly one match in {path}, found {count}: {old[:120]!r}")
    write(path, content.replace(old, new, 1))


# Fix block ownership transition when a train lands exactly on a route-step boundary.
replace_once(
    "src/MachiVerseWorks.Simulation/Internal/RailwayOperationsStore.cs",
    "        var stepIndex = route.FindStepIndex(train.RouteDistanceMeters);\n        var step = route.Steps[stepIndex];\n        var targetSpeed = Math.Min(formation.MaximumSpeedMetersPerSecond, step.Segment.SpeedLimitMetersPerSecond);",
    "        var stepIndex = route.FindStepIndex(train.RouteDistanceMeters);\n        var step = route.Steps[stepIndex];\n        if (step.BlockId != train.CurrentBlockId)\n        {\n            if (step.BlockId is { } stepBlock && !TryReserveBlock(stepBlock, train.Id))\n            {\n                train.SpeedMetersPerSecond = 0d;\n                train.State = TrainMovementState.WaitingForBlock;\n                return;\n            }\n            if (train.CurrentBlockId is { } previousBlock) ReleaseBlock(previousBlock, train.Id);\n            train.CurrentBlockId = step.BlockId;\n            if (train.State == TrainMovementState.WaitingForBlock) train.State = TrainMovementState.Running;\n        }\n        var targetSpeed = Math.Min(formation.MaximumSpeedMetersPerSecond, step.Segment.SpeedLimitMetersPerSecond);",
)

# Save Data v9: keep v8 migratable and add a nested railwayOperations section.
replace_once(
    "src/MachiVerseWorks.Persistence/SaveFormatVersion.cs",
    "    public const int RailwayInfrastructure = 8;\n    public const int Current = RailwayInfrastructure;",
    "    public const int RailwayInfrastructure = 8;\n    public const int RailwayOperations = 9;\n    public const int Current = RailwayOperations;",
)
replace_once(
    "src/MachiVerseWorks.Persistence/SaveDataDocument.cs",
    "    public SaveDepotData?[]? Depots { get; init; }\n}",
    "    public SaveDepotData?[]? Depots { get; init; }\n    public SaveRailwayOperationsData? RailwayOperations { get; init; }\n}",
)
write(
    "src/MachiVerseWorks.Persistence/RailwayOperationsSaveData.cs",
    r'''namespace MachiVerseWorks.Persistence;

internal sealed class SaveRailwayOperationsData
{
    public ulong? NextFormationId { get; init; }
    public SaveTrainFormationData?[]? Formations { get; init; }
    public ulong? NextRouteId { get; init; }
    public SaveRailwayRouteData?[]? Routes { get; init; }
    public ulong? NextTimetableId { get; init; }
    public SaveTimetableData?[]? Timetables { get; init; }
    public ulong? NextServiceId { get; init; }
    public SaveRailwayServiceData?[]? Services { get; init; }
    public ulong? NextTrainId { get; init; }
    public SaveTrainData?[]? Trains { get; init; }
}

internal sealed class SaveTrainFormationData
{
    public ulong? Id { get; init; }
    public double? LengthMeters { get; init; }
    public double? MaximumSpeedMetersPerSecond { get; init; }
    public double? MaximumAccelerationMetersPerSecondSquared { get; init; }
    public double? ServiceDecelerationMetersPerSecondSquared { get; init; }
    public int? Capacity { get; init; }
}

internal sealed class SaveRailwayRouteData
{
    public ulong? Id { get; init; }
    public ulong?[]? TrackSegmentIds { get; init; }
    public double? LengthMeters { get; init; }
}

internal sealed class SaveTimetableData
{
    public ulong? Id { get; init; }
    public SaveTimetableStopData?[]? Stops { get; init; }
}

internal sealed class SaveTimetableStopData
{
    public ulong? StationId { get; init; }
    public ulong? PlannedArrivalTick { get; init; }
    public ulong? PlannedDepartureTick { get; init; }
    public ulong? MinimumDwellTicks { get; init; }
    public required ulong? PreferredPlatformId { get; init; }
}

internal sealed class SaveRailwayServiceData
{
    public ulong? Id { get; init; }
    public ulong? FormationId { get; init; }
    public ulong? RouteId { get; init; }
    public ulong? TimetableId { get; init; }
    public ulong? OriginDepotId { get; init; }
    public ulong? DestinationDepotId { get; init; }
    public ulong? PlannedStartTick { get; init; }
    public byte? State { get; init; }
    public ulong? DelayTicks { get; init; }
    public int? NextStopIndex { get; init; }
    public required ulong? TrainId { get; init; }
}

internal sealed class SaveTrainData
{
    public ulong? Id { get; init; }
    public ulong? FormationId { get; init; }
    public ulong? ServiceId { get; init; }
    public ulong? RouteId { get; init; }
    public double? RouteDistanceMeters { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public double? ForwardX { get; init; }
    public double? ForwardY { get; init; }
    public double? ForwardZ { get; init; }
    public double? SpeedMetersPerSecond { get; init; }
    public byte? State { get; init; }
    public required ulong? CurrentBlockId { get; init; }
    public required ulong? CurrentPlatformId { get; init; }
    public required ulong? AssignedPlatformId { get; init; }
    public required ulong? CurrentDepotId { get; init; }
    public ulong? DwellDepartureTick { get; init; }
    public ulong? TickCount { get; init; }
}
''',
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "public static class WorldSaveSerializer",
    "public static partial class WorldSaveSerializer",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "        ValidateCount(checkpoint.Depots?.Count ?? 0, limits.MaximumBuildingCount, \"Depots\");\n    }",
    "        ValidateCount(checkpoint.Depots?.Count ?? 0, limits.MaximumBuildingCount, \"Depots\");\n        ValidateRailwayOperationsCheckpointWithinLimits(checkpoint, limits);\n    }",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "                NextDepotId = checkpoint.NextDepotId,\n                Depots = depots,",
    "                NextDepotId = checkpoint.NextDepotId,\n                Depots = depots,\n                RailwayOperations = CreateRailwayOperationsData(checkpoint),",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "SaveFormatVersion.Population or SaveFormatVersion.RailwayInfrastructure))",
    "SaveFormatVersion.Population or SaveFormatVersion.RailwayInfrastructure or SaveFormatVersion.RailwayOperations))",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "        var hasRailway = format >= SaveFormatVersion.RailwayInfrastructure;",
    "        var hasRailway = format >= SaveFormatVersion.RailwayInfrastructure;\n        var hasRailwayOperations = format >= SaveFormatVersion.RailwayOperations;",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "        var depotData = hasRailway ? simulation.Depots ?? throw new InvalidDataException(\"Save Data is missing Depot state.\") : [];",
    "        var depotData = hasRailway ? simulation.Depots ?? throw new InvalidDataException(\"Save Data is missing Depot state.\") : [];\n        var railwayOperationsData = hasRailwayOperations ? simulation.RailwayOperations ?? throw new InvalidDataException(\"Save Data is missing RailwayOperations state.\") : null;",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "        ValidateCount(depotData.Length, limits.MaximumBuildingCount, \"Depots\");",
    "        ValidateCount(depotData.Length, limits.MaximumBuildingCount, \"Depots\");\n        ValidateRailwayOperationsDataCounts(railwayOperationsData, hasRailwayOperations, limits);",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "        var checkpoint = new SimulationCheckpoint(\n",
    "        var railwayOperations = RestoreRailwayOperations(railwayOperationsData, hasRailwayOperations);\n\n        var checkpoint = new SimulationCheckpoint(\n",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "            hasRailway ? Require(simulation.NextDepotId, \"simulation.nextDepotId\") : 1UL, depots);",
    "            hasRailway ? Require(simulation.NextDepotId, \"simulation.nextDepotId\") : 1UL, depots,\n            railwayOperations.NextFormationId, railwayOperations.Formations,\n            railwayOperations.NextRouteId, railwayOperations.Routes,\n            railwayOperations.NextTimetableId, railwayOperations.Timetables,\n            railwayOperations.NextServiceId, railwayOperations.Services,\n            railwayOperations.NextTrainId, railwayOperations.Trains);",
)
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "            else if (reader.ValueTextEquals(\"depots\")) ValidateNamedArrayElementCount(ref reader, limits.MaximumBuildingCount, \"Depot\");",
    "            else if (reader.ValueTextEquals(\"depots\")) ValidateNamedArrayElementCount(ref reader, limits.MaximumBuildingCount, \"Depot\");\n            else if (reader.ValueTextEquals(\"railwayOperations\")) ValidateRailwayOperationsArrayCounts(ref reader, limits);",
)
write(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.RailwayOperations.cs",
    r'''using System.Text.Json;
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
''',
)

write(
    "tests/MachiVerseWorks.Persistence.Tests/RailwayOperationsSaveTests.cs",
    r'''using System.Text;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class RailwayOperationsSaveTests
{
    [TestMethod]
    public void SaveV9RoundTripPreservesRailwayOperationsAndDeterministicContinuation()
    {
        var original = new SimulationWorld(new SimulationConfig(seed: 0x1809UL));
        RailwayOperationsFixtures.SeedDeterministic(original);
        for (var tick = 0; tick < 180; tick++) original.Step();

        var json = WorldSaveSerializer.Serialize(original);
        StringAssert.Contains(Encoding.UTF8.GetString(json), "\"formatVersion\": 9");
        StringAssert.Contains(Encoding.UTF8.GetString(json), "\"railwayOperations\"");
        var restored = WorldSaveSerializer.Deserialize(json);

        for (var tick = 0; tick < 240; tick++) { original.Step(); restored.Step(); }
        var expected = original.CreateRailwayOperationsSnapshot();
        var actual = restored.CreateRailwayOperationsSnapshot();
        Assert.AreEqual(expected.Services.Length, actual.Services.Length);
        Assert.AreEqual(expected.Trains.Length, actual.Trains.Length);
        for (var index = 0; index < expected.Services.Length; index++) Assert.AreEqual(expected.Services[index], actual.Services[index]);
        for (var index = 0; index < expected.Trains.Length; index++) Assert.AreEqual(expected.Trains[index], actual.Trains[index]);
    }

    [TestMethod]
    public void RailwayInfrastructureV8MigratesWithEmptyOperations()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "phase17-railway.save.json");
        using var stream = File.OpenRead(Path.GetFullPath(path));
        var world = WorldSaveSerializer.Load(stream);
        var operations = world.CreateRailwayOperationsSnapshot();
        Assert.AreEqual(0, operations.Formations.Length);
        Assert.AreEqual(0, operations.Services.Length);
        Assert.AreEqual(0, operations.Trains.Length);
    }
}
''',
)

# Protocol 2.7 railway operations snapshot.
write(
    "src/MachiVerseWorks.Protocol/RailwayOperationsProtocol.cs",
    r'''namespace MachiVerseWorks.Protocol;

public readonly record struct ProtocolTrainState(
    ulong Id,
    ulong FormationId,
    ulong ServiceId,
    ulong RouteId,
    double X,
    double Y,
    double Z,
    double ForwardX,
    double ForwardY,
    double ForwardZ,
    double SpeedMetersPerSecond,
    byte State,
    ulong CurrentBlockId,
    ulong CurrentPlatformId,
    ulong AssignedPlatformId,
    ulong CurrentDepotId,
    ulong DwellDepartureTick);

public readonly record struct ProtocolRailwayServiceState(
    ulong Id,
    ulong FormationId,
    ulong RouteId,
    ulong TimetableId,
    ulong OriginDepotId,
    ulong DestinationDepotId,
    ulong PlannedStartTick,
    byte State,
    ulong DelayTicks,
    int NextStopIndex,
    ulong TrainId);

public readonly record struct ProtocolTimetableStop(
    ulong StationId,
    ulong PlannedArrivalTick,
    ulong PlannedDepartureTick,
    ulong MinimumDwellTicks,
    ulong PreferredPlatformId);

public sealed record ProtocolTimetable(ulong Id, IReadOnlyList<ProtocolTimetableStop> Stops);

public sealed record RailwayOperationsSnapshotMessage(
    ulong TickCount,
    IReadOnlyList<ProtocolTrainState> Trains,
    IReadOnlyList<ProtocolRailwayServiceState> Services,
    IReadOnlyList<ProtocolTimetable> Timetables) : IProtocolMessage
{
    public MessageType Type => MessageType.RailwayOperationsSnapshot;
}
''',
)
write(
    "src/MachiVerseWorks.Protocol/RailwayOperationsProtocolCodec.cs",
    r'''using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class RailwayOperationsProtocolCodec
{
    private const int SnapshotHeaderLength = 20;
    private const int TrainLength = 129;
    private const int ServiceLength = 77;
    private const int TimetableHeaderLength = 12;
    private const int TimetableStopLength = 40;

    public static byte[] Serialize(RailwayOperationsSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRailwayOperations) throw new ArgumentOutOfRangeException(nameof(version), version, "Railway operations snapshots require Protocol 2.7 or newer.");
        Validate(message);
        var payloadLength = checked(
            SnapshotHeaderLength
            + checked(message.Trains.Count * TrainLength)
            + checked(message.Services.Count * ServiceLength)
            + message.Timetables.Sum(static item => checked(TimetableHeaderLength + checked(item.Stops.Count * TimetableStopLength))));
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Railway operations snapshot exceeds the maximum protocol payload size.");
        var frame = new byte[checked(ProtocolFrameHeader.Size + payloadLength)];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.RailwayOperationsSnapshot, checked((uint)payloadLength)));
        var writer = new SpanWriter(frame.AsSpan(ProtocolFrameHeader.Size));
        writer.WriteUInt64(message.TickCount);
        writer.WriteUInt32(checked((uint)message.Trains.Count));
        writer.WriteUInt32(checked((uint)message.Services.Count));
        writer.WriteUInt32(checked((uint)message.Timetables.Count));
        foreach (var train in message.Trains)
        {
            writer.WriteUInt64(train.Id); writer.WriteUInt64(train.FormationId); writer.WriteUInt64(train.ServiceId); writer.WriteUInt64(train.RouteId);
            writer.WriteDouble(train.X); writer.WriteDouble(train.Y); writer.WriteDouble(train.Z);
            writer.WriteDouble(train.ForwardX); writer.WriteDouble(train.ForwardY); writer.WriteDouble(train.ForwardZ);
            writer.WriteDouble(train.SpeedMetersPerSecond); writer.WriteByte(train.State);
            writer.WriteUInt64(train.CurrentBlockId); writer.WriteUInt64(train.CurrentPlatformId); writer.WriteUInt64(train.AssignedPlatformId); writer.WriteUInt64(train.CurrentDepotId); writer.WriteUInt64(train.DwellDepartureTick);
        }
        foreach (var service in message.Services)
        {
            writer.WriteUInt64(service.Id); writer.WriteUInt64(service.FormationId); writer.WriteUInt64(service.RouteId); writer.WriteUInt64(service.TimetableId);
            writer.WriteUInt64(service.OriginDepotId); writer.WriteUInt64(service.DestinationDepotId); writer.WriteUInt64(service.PlannedStartTick); writer.WriteByte(service.State);
            writer.WriteUInt64(service.DelayTicks); writer.WriteInt32(service.NextStopIndex); writer.WriteUInt64(service.TrainId);
        }
        foreach (var timetable in message.Timetables)
        {
            writer.WriteUInt64(timetable.Id); writer.WriteUInt32(checked((uint)timetable.Stops.Count));
            foreach (var stop in timetable.Stops)
            {
                writer.WriteUInt64(stop.StationId); writer.WriteUInt64(stop.PlannedArrivalTick); writer.WriteUInt64(stop.PlannedDepartureTick); writer.WriteUInt64(stop.MinimumDwellTicks); writer.WriteUInt64(stop.PreferredPlatformId);
            }
        }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out RailwayOperationsSnapshotMessage message, out ProtocolDecodeError error)
    {
        message = null!;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.RailwayOperationsSnapshot) { error = ProtocolDecodeError.UnknownMessageType; return false; }
        if (!header.Version.SupportsRailwayOperations || header.PayloadLength < SnapshotHeaderLength) { error = ProtocolDecodeError.InvalidPayload; return false; }
        try
        {
            var reader = new SpanReader(frame[ProtocolFrameHeader.Size..]);
            var tickCount = reader.ReadUInt64();
            var trainCount = reader.ReadCount(TrainLength);
            var serviceCount = reader.ReadCount(ServiceLength);
            var timetableCount = reader.ReadCount(TimetableHeaderLength);
            var trains = new ProtocolTrainState[trainCount];
            for (var index = 0; index < trains.Length; index++)
            {
                var item = new ProtocolTrainState(
                    reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(),
                    reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadByte(),
                    reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
                ValidateTrain(item);
                trains[index] = item;
            }
            var services = new ProtocolRailwayServiceState[serviceCount];
            for (var index = 0; index < services.Length; index++)
            {
                var item = new ProtocolRailwayServiceState(
                    reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadByte(), reader.ReadUInt64(), reader.ReadInt32(), reader.ReadUInt64());
                ValidateService(item);
                services[index] = item;
            }
            var timetables = new ProtocolTimetable[timetableCount];
            for (var index = 0; index < timetables.Length; index++)
            {
                var id = reader.ReadUInt64();
                var stopCount = reader.ReadCount(TimetableStopLength);
                if (id == 0 || stopCount == 0) throw new InvalidDataException();
                var stops = new ProtocolTimetableStop[stopCount];
                ulong previousDeparture = 0;
                for (var stopIndex = 0; stopIndex < stops.Length; stopIndex++)
                {
                    var stop = new ProtocolTimetableStop(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
                    if (stop.StationId == 0 || stop.PlannedDepartureTick < stop.PlannedArrivalTick || (stopIndex > 0 && stop.PlannedArrivalTick < previousDeparture)) throw new InvalidDataException();
                    previousDeparture = stop.PlannedDepartureTick;
                    stops[stopIndex] = stop;
                }
                timetables[index] = new ProtocolTimetable(id, stops);
            }
            if (!reader.IsComplete) throw new InvalidDataException();
            message = new RailwayOperationsSnapshotMessage(tickCount, trains, services, timetables);
            error = ProtocolDecodeError.None;
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException or ArgumentOutOfRangeException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
    }

    private static void Validate(RailwayOperationsSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Trains); ArgumentNullException.ThrowIfNull(message.Services); ArgumentNullException.ThrowIfNull(message.Timetables);
        foreach (var train in message.Trains) ValidateTrain(train);
        foreach (var service in message.Services) ValidateService(service);
        foreach (var timetable in message.Timetables)
        {
            ArgumentNullException.ThrowIfNull(timetable); ArgumentNullException.ThrowIfNull(timetable.Stops);
            if (timetable.Id == 0 || timetable.Stops.Count == 0) throw new ArgumentOutOfRangeException(nameof(message));
            ulong previousDeparture = 0;
            for (var index = 0; index < timetable.Stops.Count; index++)
            {
                var stop = timetable.Stops[index];
                if (stop.StationId == 0 || stop.PlannedDepartureTick < stop.PlannedArrivalTick || (index > 0 && stop.PlannedArrivalTick < previousDeparture)) throw new ArgumentOutOfRangeException(nameof(message));
                previousDeparture = stop.PlannedDepartureTick;
            }
        }
    }

    private static void ValidateTrain(ProtocolTrainState item)
    {
        if (item.Id == 0 || item.FormationId == 0 || item.ServiceId == 0 || item.RouteId == 0 || item.State > 5 || !Finite(item.X, item.Y, item.Z) || !Finite(item.ForwardX, item.ForwardY, item.ForwardZ) || !double.IsFinite(item.SpeedMetersPerSecond) || item.SpeedMetersPerSecond < 0d) throw new ArgumentOutOfRangeException(nameof(item));
    }

    private static void ValidateService(ProtocolRailwayServiceState item)
    {
        if (item.Id == 0 || item.FormationId == 0 || item.RouteId == 0 || item.TimetableId == 0 || item.OriginDepotId == 0 || item.DestinationDepotId == 0 || item.State > 2 || item.NextStopIndex < 0) throw new ArgumentOutOfRangeException(nameof(item));
    }

    private static bool Finite(double x, double y, double z) => double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z);

    private ref struct SpanWriter
    {
        private Span<byte> buffer; private int offset;
        public SpanWriter(Span<byte> buffer) { this.buffer = buffer; offset = 0; }
        public void WriteByte(byte value) => buffer[offset++] = value;
        public void WriteUInt32(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(buffer[offset..], value); offset += sizeof(uint); }
        public void WriteInt32(int value) { BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], value); offset += sizeof(int); }
        public void WriteUInt64(ulong value) { BinaryPrimitives.WriteUInt64LittleEndian(buffer[offset..], value); offset += sizeof(ulong); }
        public void WriteDouble(double value) { BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], BitConverter.DoubleToInt64Bits(value)); offset += sizeof(double); }
    }

    private ref struct SpanReader
    {
        private readonly ReadOnlySpan<byte> buffer; private int offset;
        public SpanReader(ReadOnlySpan<byte> buffer) { this.buffer = buffer; offset = 0; }
        public bool IsComplete => offset == buffer.Length;
        private int Remaining => buffer.Length - offset;
        public byte ReadByte() { Ensure(1); return buffer[offset++]; }
        public uint ReadUInt32() { Ensure(sizeof(uint)); var value = BinaryPrimitives.ReadUInt32LittleEndian(buffer[offset..]); offset += sizeof(uint); return value; }
        public int ReadInt32() { Ensure(sizeof(int)); var value = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]); offset += sizeof(int); return value; }
        public ulong ReadUInt64() { Ensure(sizeof(ulong)); var value = BinaryPrimitives.ReadUInt64LittleEndian(buffer[offset..]); offset += sizeof(ulong); return value; }
        public double ReadDouble() { Ensure(sizeof(double)); var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..])); offset += sizeof(double); return value; }
        public int ReadCount(int minimumBytesPerItem)
        {
            var count = ReadUInt32();
            if (count > int.MaxValue) throw new InvalidDataException();
            var value = (int)count;
            if (minimumBytesPerItem > 0 && value > Remaining / minimumBytesPerItem) throw new InvalidDataException();
            return value;
        }
        private void Ensure(int length) { if (length < 0 || Remaining < length) throw new InvalidDataException(); }
    }
}
''',
)
write(
    "tests/MachiVerseWorks.Protocol.Tests/RailwayOperationsProtocolTests.cs",
    r'''using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RailwayOperationsProtocolTests
{
    [TestMethod]
    public void Protocol27RoundTripsTrainServiceDelayPlatformAndTimetableState()
    {
        var message = new RailwayOperationsSnapshotMessage(123,
        [
            new ProtocolTrainState(1, 2, 3, 4, 10, 20, 3, 1, 0, 0, 12.5, 4, 8, 9, 10, 0, 140),
        ],
        [
            new ProtocolRailwayServiceState(3, 2, 4, 5, 6, 7, 1, 1, 18, 1, 1),
        ],
        [
            new ProtocolTimetable(5, [new ProtocolTimetableStop(11, 80, 100, 10, 9), new ProtocolTimetableStop(12, 170, 190, 10, 0)]),
        ]);

        var frame = RailwayOperationsProtocolCodec.Serialize(message, ProtocolVersion.Current);
        Assert.IsTrue(RailwayOperationsProtocolCodec.TryDeserialize(frame, out var decoded, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.AreEqual(message.TickCount, decoded.TickCount);
        Assert.AreEqual(message.Trains[0], decoded.Trains[0]);
        Assert.AreEqual(message.Services[0], decoded.Services[0]);
        CollectionAssert.AreEqual(message.Timetables[0].Stops.ToArray(), decoded.Timetables[0].Stops.ToArray());
    }

    [TestMethod]
    public void Protocol26CannotSerializeRailwayOperations()
    {
        var message = new RailwayOperationsSnapshotMessage(0, [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RailwayOperationsProtocolCodec.Serialize(message, new ProtocolVersion(2, 6)));
    }
}
''',
)

# Server publish path.
write(
    "src/MachiVerseWorks.Server/RailwayOperationsMessageMapper.cs",
    r'''using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class RailwayOperationsMessageMapper
{
    public static RailwayOperationsSnapshotMessage Create(RailwayOperationsSnapshot operations, TrainSnapshot[] visibleTrains, ulong tickCount)
    {
        ArgumentNullException.ThrowIfNull(operations); ArgumentNullException.ThrowIfNull(visibleTrains);
        Array.Sort(visibleTrains, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        var serviceIds = visibleTrains.Select(static train => train.ServiceId).ToHashSet();
        var services = operations.Services.Where(service => serviceIds.Contains(service.Id)).OrderBy(static service => service.Id.Value).ToArray();
        var timetableIds = services.Select(static service => service.TimetableId).ToHashSet();
        var timetables = operations.Timetables.Where(timetable => timetableIds.Contains(timetable.Id)).OrderBy(static timetable => timetable.Id.Value).ToArray();
        return new RailwayOperationsSnapshotMessage(
            tickCount,
            visibleTrains.Select(static train => new ProtocolTrainState(
                train.Id.Value, train.FormationId.Value, train.ServiceId.Value, train.RouteId.Value,
                train.Position.X, train.Position.Y, train.Position.Z, train.Forward.X, train.Forward.Y, train.Forward.Z,
                train.SpeedMetersPerSecond, (byte)train.State,
                train.CurrentBlockId?.Value ?? 0, train.CurrentPlatformId?.Value ?? 0, train.AssignedPlatformId?.Value ?? 0, train.CurrentDepotId?.Value ?? 0, train.DwellDepartureTick)).ToArray(),
            services.Select(static service => new ProtocolRailwayServiceState(
                service.Id.Value, service.FormationId.Value, service.RouteId.Value, service.TimetableId.Value,
                service.OriginDepotId.Value, service.DestinationDepotId.Value, service.PlannedStartTick, (byte)service.State,
                service.DelayTicks, service.NextStopIndex, service.TrainId?.Value ?? 0)).ToArray(),
            timetables.Select(static timetable => new ProtocolTimetable(
                timetable.Id.Value,
                timetable.Stops.Select(static stop => new ProtocolTimetableStop(stop.StationId.Value, stop.PlannedArrivalTick, stop.PlannedDepartureTick, stop.MinimumDwellTicks, stop.PreferredPlatformId?.Value ?? 0)).ToArray())).ToArray());
    }
}
''',
)
replace_once(
    "src/MachiVerseWorks.Server/ClientConnections.cs",
    "                RailwayInfrastructureSnapshotMessage railway => RailwayInfrastructureProtocolCodec.Serialize(railway, version),",
    "                RailwayInfrastructureSnapshotMessage railway => RailwayInfrastructureProtocolCodec.Serialize(railway, version),\n                RailwayOperationsSnapshotMessage railwayOperations => RailwayOperationsProtocolCodec.Serialize(railwayOperations, version),",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationPublishSnapshot.cs",
    "    private readonly PublishedEntitySpatialIndex<IntersectionControllerSnapshot> _intersections;",
    "    private readonly PublishedEntitySpatialIndex<IntersectionControllerSnapshot> _intersections;\n    private readonly PublishedEntitySpatialIndex<TrainSnapshot> _trains;",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationPublishSnapshot.cs",
    "        RailwayInfrastructureReadModel railwayInfrastructure)\n    {",
    "        RailwayInfrastructureReadModel railwayInfrastructure,\n        TrainSnapshot[]? trains = null,\n        RailwayOperationsSnapshot? railwayOperations = null)\n    {",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationPublishSnapshot.cs",
    "        RailwayInfrastructure = railwayInfrastructure ?? throw new ArgumentNullException(nameof(railwayInfrastructure));\n        _agents =",
    "        RailwayInfrastructure = railwayInfrastructure ?? throw new ArgumentNullException(nameof(railwayInfrastructure));\n        trains ??= [];\n        RailwayOperations = railwayOperations ?? EmptyRailwayOperations();\n        _agents =",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationPublishSnapshot.cs",
    "        _intersections = new PublishedEntitySpatialIndex<IntersectionControllerSnapshot>(intersectionControl.Controllers.ToArray(), spatialCellSize, item => roadNetwork.GetNodePosition(item.IntersectionNodeId));",
    "        _intersections = new PublishedEntitySpatialIndex<IntersectionControllerSnapshot>(intersectionControl.Controllers.ToArray(), spatialCellSize, item => roadNetwork.GetNodePosition(item.IntersectionNodeId));\n        _trains = new PublishedEntitySpatialIndex<TrainSnapshot>(trains, spatialCellSize, static item => item.Position);",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationPublishSnapshot.cs",
    "    public RailwayInfrastructureReadModel RailwayInfrastructure { get; }",
    "    public RailwayInfrastructureReadModel RailwayInfrastructure { get; }\n    public RailwayOperationsSnapshot RailwayOperations { get; }",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationPublishSnapshot.cs",
    "    public EntityPublishSnapshot QueryEntities(WorldVolume volume) => new(TickCount, _agents.Query(volume), _pedestrians.Query(volume), _vehicles.Query(volume), _intersections.Query(volume));",
    "    public EntityPublishSnapshot QueryEntities(WorldVolume volume) => new(TickCount, _agents.Query(volume), _pedestrians.Query(volume), _vehicles.Query(volume), _intersections.Query(volume), _trains.Query(volume));",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationPublishSnapshot.cs",
    "    private static RailwayInfrastructureSnapshot EmptyRailway() => new([], [], [], [], [], [], [], []);\n}",
    "    private static RailwayInfrastructureSnapshot EmptyRailway() => new([], [], [], [], [], [], [], []);\n    private static RailwayOperationsSnapshot EmptyRailwayOperations() => new([], [], [], [], []);\n}",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationPublishSnapshot.cs",
    "internal sealed record EntityPublishSnapshot(ulong TickCount, AgentSnapshot[] Agents, PedestrianSnapshot[] Pedestrians, VehicleSnapshot[] Vehicles, IntersectionControllerSnapshot[] Intersections);",
    "internal sealed record EntityPublishSnapshot(ulong TickCount, AgentSnapshot[] Agents, PedestrianSnapshot[] Pedestrians, VehicleSnapshot[] Vehicles, IntersectionControllerSnapshot[] Intersections, TrainSnapshot[] Trains);",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationRuntime.cs",
    "    private bool _railwayFixturePending;",
    "    private bool _railwayFixturePending;\n    private bool _railwayOperationsFixturePending;",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationRuntime.cs",
    "        _railwayFixturePending = bool.TryParse(configuration[\"Simulation:RailwayFixture\"], out var railwayFixture) && railwayFixture;",
    "        _railwayFixturePending = bool.TryParse(configuration[\"Simulation:RailwayFixture\"], out var railwayFixture) && railwayFixture;\n        _railwayOperationsFixturePending = bool.TryParse(configuration[\"Simulation:RailwayOperationsFixture\"], out var railwayOperationsFixture) && railwayOperationsFixture;",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationRuntime.cs",
    "        ulong tickCount; AgentSnapshot[] agents; PedestrianSnapshot[] pedestrians; VehicleSnapshot[] vehicles; IntersectionControlSnapshot intersectionControl; RoadNetworkReadModel roadReadModel; RailwayInfrastructureReadModel railwayReadModel;",
    "        ulong tickCount; AgentSnapshot[] agents; PedestrianSnapshot[] pedestrians; VehicleSnapshot[] vehicles; TrainSnapshot[] trains; RailwayOperationsSnapshot railwayOperations; IntersectionControlSnapshot intersectionControl; RoadNetworkReadModel roadReadModel; RailwayInfrastructureReadModel railwayReadModel;",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationRuntime.cs",
    "            agents = _world.CreateAllAgentSnapshots(); pedestrians = _world.CreateAllPedestrianSnapshots(); vehicles = _world.CreateAllVehicleSnapshots(); intersectionControl = _world.CreateIntersectionControlSnapshot();",
    "            agents = _world.CreateAllAgentSnapshots(); pedestrians = _world.CreateAllPedestrianSnapshots(); vehicles = _world.CreateAllVehicleSnapshots(); trains = _world.CreateTrainSnapshot(); railwayOperations = _world.CreateRailwayOperationsSnapshot(); intersectionControl = _world.CreateIntersectionControlSnapshot();",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationRuntime.cs",
    "        return new SimulationPublishSnapshot(tickCount, SpatialCellSize, agents, pedestrians, vehicles, intersectionControl, roadReadModel, railwayReadModel);",
    "        return new SimulationPublishSnapshot(tickCount, SpatialCellSize, agents, pedestrians, vehicles, intersectionControl, roadReadModel, railwayReadModel, trains, railwayOperations);",
)
replace_once(
    "src/MachiVerseWorks.Server/SimulationRuntime.cs",
    "        if (_railwayFixturePending) { RailwayInfrastructureFixtures.SeedDeterministic(_world); _railwayFixturePending = false; _roadReadModel = null; _railwayReadModel = null; }",
    "        if (_railwayFixturePending) { RailwayInfrastructureFixtures.SeedDeterministic(_world); _railwayFixturePending = false; _roadReadModel = null; _railwayReadModel = null; }\n        if (_railwayOperationsFixturePending) { RailwayOperationsFixtures.SeedDeterministic(_world); _railwayOperationsFixturePending = false; _railwayReadModel = null; }",
)
replace_once(
    "src/MachiVerseWorks.Server/HostedServices.cs",
    "            var intersectionMessages = connection.NegotiatedVersion.SupportsIntersectionControl ? snapshot.Intersections.Select(IntersectionControlMessageMapper.Create).ToArray() : [];",
    "            var intersectionMessages = connection.NegotiatedVersion.SupportsIntersectionControl ? snapshot.Intersections.Select(IntersectionControlMessageMapper.Create).ToArray() : [];\n            var railwayOperationsMessage = connection.NegotiatedVersion.SupportsRailwayOperations ? RailwayOperationsMessageMapper.Create(publishSnapshot.RailwayOperations, snapshot.Trains, snapshot.TickCount) : null;",
)
replace_once(
    "src/MachiVerseWorks.Server/HostedServices.cs",
    "            var messageCount = agentPlan.Messages.Count + pedestrianPlan.Messages.Count + vehiclePlan.Messages.Count + intersectionMessages.Length + (roadMessage is null ? 0 : 1) + railwayMessages.Count;",
    "            var messageCount = agentPlan.Messages.Count + pedestrianPlan.Messages.Count + vehiclePlan.Messages.Count + intersectionMessages.Length + (roadMessage is null ? 0 : 1) + railwayMessages.Count + (railwayOperationsMessage is null ? 0 : 1);",
)
replace_once(
    "src/MachiVerseWorks.Server/HostedServices.cs",
    "            foreach (var railwayMessage in railwayMessages) { sendCancellation.CancelAfter(ClientSendTimeout); var sent = await connection.SendAsync(railwayMessage, connection.NegotiatedVersion, sendCancellation.Token); bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs; }",
    "            foreach (var railwayMessage in railwayMessages) { sendCancellation.CancelAfter(ClientSendTimeout); var sent = await connection.SendAsync(railwayMessage, connection.NegotiatedVersion, sendCancellation.Token); bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs; }\n            if (railwayOperationsMessage is not null) { sendCancellation.CancelAfter(ClientSendTimeout); var sent = await connection.SendAsync(railwayOperationsMessage, connection.NegotiatedVersion, sendCancellation.Token); bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs; }",
)
replace_once(
    "src/MachiVerseWorks.Server/HostedServices.cs",
    "            metrics.RecordSnapshotDelivery(snapshot.Agents.Length + snapshot.Vehicles.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);\n            ServerLog.SnapshotDeliveryMetrics(logger, connection.Id, snapshot.Agents.Length + snapshot.Vehicles.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);",
    "            metrics.RecordSnapshotDelivery(snapshot.Agents.Length + snapshot.Vehicles.Length + snapshot.Trains.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);\n            ServerLog.SnapshotDeliveryMetrics(logger, connection.Id, snapshot.Agents.Length + snapshot.Vehicles.Length + snapshot.Trains.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);",
)
write(
    "tests/MachiVerseWorks.Server.Tests/RailwayOperationsMessageMapperTests.cs",
    r'''using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RailwayOperationsMessageMapperTests
{
    [TestMethod]
    public void MapperPublishesVisibleTrainServiceDelayPlatformAndTimetable()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        for (var tick = 0; tick < 150; tick++) world.Step();
        var operations = world.CreateRailwayOperationsSnapshot();
        var message = RailwayOperationsMessageMapper.Create(operations, operations.Trains.ToArray(), world.Time.TickCount);
        Assert.AreEqual(2, message.Trains.Count);
        Assert.AreEqual(2, message.Services.Count);
        Assert.AreEqual(2, message.Timetables.Count);
        Assert.IsTrue(message.Services.Any(static service => service.DelayTicks > 0));
        Assert.IsTrue(message.Trains.Any(static train => train.AssignedPlatformId > 0 || train.CurrentPlatformId > 0));
    }
}
''',
)

print("Phase18 .NET integration patch applied.")
