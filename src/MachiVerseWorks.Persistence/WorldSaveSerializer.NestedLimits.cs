using System.Text.Json;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Persistence;

public static partial class WorldSaveSerializer
{
    private static void ValidateNestedCollectionCountsBeforeMaterialization(ReadOnlySpan<byte> json, WorldSaveLimits limits)
    {
        var reader = new Utf8JsonReader(json, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = JsonOptions.MaxDepth,
        });
        if (!reader.Read()) return;
        var totals = new NestedSaveScanTotals();
        ScanNestedValue(ref reader, NestedSaveContext.Root, limits, ref totals);
    }

    private static void ValidateNestedCheckpointWithinLimits(SimulationCheckpoint checkpoint, WorldSaveLimits limits)
    {
        foreach (var vehicle in checkpoint.Vehicles ?? [])
            ValidateCount(vehicle.RouteSteps.Count, limits.MaximumVehicleRouteStepCount, "VehicleRouteSteps");

        foreach (var person in checkpoint.Persons ?? [])
        {
            ValidateCount(person.Schedule.Count, limits.MaximumPersonScheduleEntryCount, "PersonScheduleEntries");
            ValidateCount(person.Needs.Count, limits.MaximumPersonNeedCount, "PersonNeeds");
        }

        foreach (var block in checkpoint.BlockSections ?? [])
            ValidateCount(block.SegmentIds.Count, limits.MaximumBlockSectionSegmentCount, "BlockSectionSegmentIds");

        foreach (var depot in checkpoint.Depots ?? [])
            ValidateCount(depot.TrackSegmentIds.Count, limits.MaximumDepotTrackSegmentCount, "DepotTrackSegmentIds");

        foreach (var route in checkpoint.RailwayRoutes ?? [])
            ValidateCount(route.TrackSegmentIds.Count, limits.MaximumRailwayRouteSegmentCount, "RailwayRouteTrackSegmentIds");

        var totalTimetableStops = 0;
        foreach (var timetable in checkpoint.Timetables ?? [])
        {
            ValidateCount(timetable.Stops.Count, limits.MaximumTimetableStopCount, "TimetableStopsPerTimetable");
            totalTimetableStops = checked(totalTimetableStops + timetable.Stops.Count);
        }
        ValidateCount(totalTimetableStops, limits.MaximumTimetableStopTotalCount, "TimetableStops");
    }

    private static void ScanNestedValue(
        ref Utf8JsonReader reader,
        NestedSaveContext context,
        WorldSaveLimits limits,
        ref NestedSaveScanTotals totals)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            ScanNestedObject(ref reader, context, limits, ref totals);
            return;
        }
        if (reader.TokenType == JsonTokenType.StartArray)
            ScanNestedArray(ref reader, NestedSaveContext.Other, int.MaxValue, null, NestedArrayKind.None, limits, ref totals);
    }

    private static void ScanNestedObject(
        ref Utf8JsonReader reader,
        NestedSaveContext context,
        WorldSaveLimits limits,
        ref NestedSaveScanTotals totals)
    {
        var objectDepth = reader.CurrentDepth;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == objectDepth) return;
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != objectDepth + 1) continue;

            var property = GetNestedProperty(context, ref reader);
            if (!reader.Read()) return;

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                ScanNestedObject(ref reader, GetNestedObjectContext(context, property), limits, ref totals);
                continue;
            }

            if (reader.TokenType != JsonTokenType.StartArray) continue;
            var rule = GetNestedArrayRule(context, property, limits);
            ScanNestedArray(
                ref reader,
                GetNestedArrayElementContext(context, property),
                rule.MaximumCount,
                rule.Path,
                rule.Kind,
                limits,
                ref totals);
        }
    }

    private static void ScanNestedArray(
        ref Utf8JsonReader reader,
        NestedSaveContext elementContext,
        int maximumCount,
        string? path,
        NestedArrayKind kind,
        WorldSaveLimits limits,
        ref NestedSaveScanTotals totals)
    {
        var arrayDepth = reader.CurrentDepth;
        var count = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == arrayDepth) return;
            if (reader.CurrentDepth != arrayDepth + 1 || !IsJsonValueStart(reader.TokenType)) continue;

            count++;
            if (path is not null && count > maximumCount)
                throw new InvalidDataException($"Save Data nested collection '{path}' exceeds the configured {maximumCount}-entry limit before deserialization.");

            if (kind == NestedArrayKind.TimetableStops)
            {
                totals.TimetableStopCount++;
                if (totals.TimetableStopCount > limits.MaximumTimetableStopTotalCount)
                    throw new InvalidDataException($"Save Data total RailwayOperations Timetable stop count exceeds the configured {limits.MaximumTimetableStopTotalCount}-entry limit before deserialization.");
            }

            if (reader.TokenType == JsonTokenType.StartObject)
                ScanNestedObject(ref reader, elementContext, limits, ref totals);
            else if (reader.TokenType == JsonTokenType.StartArray)
                ScanNestedArray(ref reader, NestedSaveContext.Other, int.MaxValue, null, NestedArrayKind.None, limits, ref totals);
        }
    }

    private static NestedSaveProperty GetNestedProperty(NestedSaveContext context, ref Utf8JsonReader reader)
    {
        if (context == NestedSaveContext.Root && reader.ValueTextEquals("simulation")) return NestedSaveProperty.Simulation;
        if (context == NestedSaveContext.Simulation)
        {
            if (reader.ValueTextEquals("vehicles")) return NestedSaveProperty.Vehicles;
            if (reader.ValueTextEquals("persons")) return NestedSaveProperty.Persons;
            if (reader.ValueTextEquals("blockSections")) return NestedSaveProperty.BlockSections;
            if (reader.ValueTextEquals("depots")) return NestedSaveProperty.Depots;
            if (reader.ValueTextEquals("railwayOperations")) return NestedSaveProperty.RailwayOperations;
        }
        else if (context == NestedSaveContext.Vehicle && reader.ValueTextEquals("routeSteps")) return NestedSaveProperty.RouteSteps;
        else if (context == NestedSaveContext.Person)
        {
            if (reader.ValueTextEquals("schedule")) return NestedSaveProperty.Schedule;
            if (reader.ValueTextEquals("needs")) return NestedSaveProperty.Needs;
        }
        else if (context == NestedSaveContext.BlockSection && reader.ValueTextEquals("segmentIds")) return NestedSaveProperty.SegmentIds;
        else if (context == NestedSaveContext.Depot && reader.ValueTextEquals("trackSegmentIds")) return NestedSaveProperty.TrackSegmentIds;
        else if (context == NestedSaveContext.RailwayOperations)
        {
            if (reader.ValueTextEquals("routes")) return NestedSaveProperty.Routes;
            if (reader.ValueTextEquals("timetables")) return NestedSaveProperty.Timetables;
        }
        else if (context == NestedSaveContext.RailwayRoute && reader.ValueTextEquals("trackSegmentIds")) return NestedSaveProperty.TrackSegmentIds;
        else if (context == NestedSaveContext.Timetable && reader.ValueTextEquals("stops")) return NestedSaveProperty.Stops;
        return NestedSaveProperty.Other;
    }

    private static NestedSaveContext GetNestedObjectContext(NestedSaveContext context, NestedSaveProperty property) =>
        (context, property) switch
        {
            (NestedSaveContext.Root, NestedSaveProperty.Simulation) => NestedSaveContext.Simulation,
            (NestedSaveContext.Simulation, NestedSaveProperty.RailwayOperations) => NestedSaveContext.RailwayOperations,
            _ => NestedSaveContext.Other,
        };

    private static NestedSaveContext GetNestedArrayElementContext(NestedSaveContext context, NestedSaveProperty property) =>
        (context, property) switch
        {
            (NestedSaveContext.Simulation, NestedSaveProperty.Vehicles) => NestedSaveContext.Vehicle,
            (NestedSaveContext.Simulation, NestedSaveProperty.Persons) => NestedSaveContext.Person,
            (NestedSaveContext.Simulation, NestedSaveProperty.BlockSections) => NestedSaveContext.BlockSection,
            (NestedSaveContext.Simulation, NestedSaveProperty.Depots) => NestedSaveContext.Depot,
            (NestedSaveContext.RailwayOperations, NestedSaveProperty.Routes) => NestedSaveContext.RailwayRoute,
            (NestedSaveContext.RailwayOperations, NestedSaveProperty.Timetables) => NestedSaveContext.Timetable,
            _ => NestedSaveContext.Other,
        };

    private static NestedArrayRule GetNestedArrayRule(NestedSaveContext context, NestedSaveProperty property, WorldSaveLimits limits) =>
        (context, property) switch
        {
            (NestedSaveContext.Vehicle, NestedSaveProperty.RouteSteps) => new(limits.MaximumVehicleRouteStepCount, "simulation.vehicles[].routeSteps", NestedArrayKind.None),
            (NestedSaveContext.Person, NestedSaveProperty.Schedule) => new(limits.MaximumPersonScheduleEntryCount, "simulation.persons[].schedule", NestedArrayKind.None),
            (NestedSaveContext.Person, NestedSaveProperty.Needs) => new(limits.MaximumPersonNeedCount, "simulation.persons[].needs", NestedArrayKind.None),
            (NestedSaveContext.BlockSection, NestedSaveProperty.SegmentIds) => new(limits.MaximumBlockSectionSegmentCount, "simulation.blockSections[].segmentIds", NestedArrayKind.None),
            (NestedSaveContext.Depot, NestedSaveProperty.TrackSegmentIds) => new(limits.MaximumDepotTrackSegmentCount, "simulation.depots[].trackSegmentIds", NestedArrayKind.None),
            (NestedSaveContext.RailwayRoute, NestedSaveProperty.TrackSegmentIds) => new(limits.MaximumRailwayRouteSegmentCount, "simulation.railwayOperations.routes[].trackSegmentIds", NestedArrayKind.None),
            (NestedSaveContext.Timetable, NestedSaveProperty.Stops) => new(limits.MaximumTimetableStopCount, "simulation.railwayOperations.timetables[].stops", NestedArrayKind.TimetableStops),
            _ => new(int.MaxValue, null, NestedArrayKind.None),
        };

    private static bool IsJsonValueStart(JsonTokenType tokenType) => tokenType is
        JsonTokenType.StartObject or
        JsonTokenType.StartArray or
        JsonTokenType.String or
        JsonTokenType.Number or
        JsonTokenType.True or
        JsonTokenType.False or
        JsonTokenType.Null;

    private enum NestedSaveContext : byte
    {
        Other,
        Root,
        Simulation,
        Vehicle,
        Person,
        BlockSection,
        Depot,
        RailwayOperations,
        RailwayRoute,
        Timetable,
    }

    private enum NestedSaveProperty : byte
    {
        Other,
        Simulation,
        Vehicles,
        Persons,
        BlockSections,
        Depots,
        RailwayOperations,
        RouteSteps,
        Schedule,
        Needs,
        SegmentIds,
        TrackSegmentIds,
        Routes,
        Timetables,
        Stops,
    }

    private enum NestedArrayKind : byte
    {
        None,
        TimetableStops,
    }

    private readonly record struct NestedArrayRule(int MaximumCount, string? Path, NestedArrayKind Kind);

    private struct NestedSaveScanTotals
    {
        public int TimetableStopCount;
    }
}
