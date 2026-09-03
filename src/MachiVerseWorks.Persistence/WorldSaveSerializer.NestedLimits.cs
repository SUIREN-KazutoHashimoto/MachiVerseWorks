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

    private static void ScanNestedValue(ref Utf8JsonReader reader, NestedSaveContext context, WorldSaveLimits limits, ref NestedSaveScanTotals totals)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            ScanNestedObject(ref reader, context, limits, ref totals);
            return;
        }
        if (reader.TokenType == JsonTokenType.StartArray)
            ScanNestedArray(ref reader, NestedSaveContext.Other, int.MaxValue, null, NestedArrayKind.None, limits, ref totals);
    }

    private static void ScanNestedObject(ref Utf8JsonReader reader, NestedSaveContext context, WorldSaveLimits limits, ref NestedSaveScanTotals totals)
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
            ScanNestedArray(ref reader, GetNestedArrayElementContext(context, property), rule.MaximumCount, rule.Path, rule.Kind, limits, ref totals);
        }
    }

    private static void ScanNestedArray(ref Utf8JsonReader reader, NestedSaveContext elementContext, int maximumCount, string? path, NestedArrayKind kind, WorldSaveLimits limits, ref NestedSaveScanTotals totals)
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
            else if (kind == NestedArrayKind.TransitPatternStops)
            {
                totals.TransitPatternStopCount++;
                if (totals.TransitPatternStopCount > limits.MaximumLaneConnectionCount)
                    throw new InvalidDataException($"Save Data total Transit Pattern stop count exceeds the configured {limits.MaximumLaneConnectionCount}-entry limit before deserialization.");
            }
            else if (kind == NestedArrayKind.JourneyLegs)
            {
                totals.JourneyLegCount++;
                if (totals.JourneyLegCount > limits.MaximumLaneConnectionCount)
                    throw new InvalidDataException($"Save Data total Journey leg count exceeds the configured {limits.MaximumLaneConnectionCount}-entry limit before deserialization.");
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
            if (reader.ValueTextEquals("multimodalTransit")) return NestedSaveProperty.MultimodalTransit;
            if (reader.ValueTextEquals("economy")) return NestedSaveProperty.Economy;
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
        else if (context == NestedSaveContext.MultimodalTransit)
        {
            if (reader.ValueTextEquals("patterns")) return NestedSaveProperty.TransitPatterns;
            if (reader.ValueTextEquals("journeys")) return NestedSaveProperty.TransitJourneys;
        }
        else if (context == NestedSaveContext.TransitPattern && reader.ValueTextEquals("stops")) return NestedSaveProperty.TransitPatternStops;
        else if (context == NestedSaveContext.TransitJourney && reader.ValueTextEquals("legs")) return NestedSaveProperty.JourneyLegs;
        else if (context == NestedSaveContext.Economy)
        {
            if (reader.ValueTextEquals("companies")) return NestedSaveProperty.Companies;
            if (reader.ValueTextEquals("establishments")) return NestedSaveProperty.Establishments;
            if (reader.ValueTextEquals("jobs")) return NestedSaveProperty.Jobs;
            if (reader.ValueTextEquals("employments")) return NestedSaveProperty.Employments;
            if (reader.ValueTextEquals("households")) return NestedSaveProperty.EconomyHouseholds;
            if (reader.ValueTextEquals("logistics")) return NestedSaveProperty.Logistics;
            if (reader.ValueTextEquals("power")) return NestedSaveProperty.Power;
            if (reader.ValueTextEquals("waterSewer")) return NestedSaveProperty.WaterSewer;
            if (reader.ValueTextEquals("gas")) return NestedSaveProperty.Gas;
            if (reader.ValueTextEquals("optical")) return NestedSaveProperty.Optical;
            if (reader.ValueTextEquals("radio")) return NestedSaveProperty.Radio;
            if (reader.ValueTextEquals("worldEnvironment")) return NestedSaveProperty.WorldEnvironment;
            if (reader.ValueTextEquals("regionalGeneration")) return NestedSaveProperty.RegionalGeneration;
        }
        else if (context == NestedSaveContext.Logistics)
        {
            if (reader.ValueTextEquals("commodities")) return NestedSaveProperty.Commodities;
            if (reader.ValueTextEquals("inventories")) return NestedSaveProperty.Inventories;
            if (reader.ValueTextEquals("orders")) return NestedSaveProperty.Orders;
            if (reader.ValueTextEquals("shipments")) return NestedSaveProperty.Shipments;
        }
        else if (context == NestedSaveContext.Power)
        {
            if (reader.ValueTextEquals("nodes")) return NestedSaveProperty.PowerNodes;
            if (reader.ValueTextEquals("lines")) return NestedSaveProperty.PowerLines;
            if (reader.ValueTextEquals("generators")) return NestedSaveProperty.Generators;
            if (reader.ValueTextEquals("loads")) return NestedSaveProperty.PowerLoads;
        }
        else if (context == NestedSaveContext.WaterSewer)
        {
            if (reader.ValueTextEquals("waterNodes")) return NestedSaveProperty.WaterNodes;
            if (reader.ValueTextEquals("waterPipes")) return NestedSaveProperty.WaterPipes;
            if (reader.ValueTextEquals("sewerNodes")) return NestedSaveProperty.SewerNodes;
            if (reader.ValueTextEquals("sewerPipes")) return NestedSaveProperty.SewerPipes;
            if (reader.ValueTextEquals("waterSources")) return NestedSaveProperty.WaterSources;
            if (reader.ValueTextEquals("reservoirs")) return NestedSaveProperty.Reservoirs;
            if (reader.ValueTextEquals("pumps")) return NestedSaveProperty.Pumps;
            if (reader.ValueTextEquals("treatmentPlants")) return NestedSaveProperty.TreatmentPlants;
            if (reader.ValueTextEquals("servicePoints")) return NestedSaveProperty.ServicePoints;
        }
        else if (context == NestedSaveContext.Gas)
        {
            if (reader.ValueTextEquals("nodes")) return NestedSaveProperty.GasNodes;
            if (reader.ValueTextEquals("pipelines")) return NestedSaveProperty.GasPipelines;
            if (reader.ValueTextEquals("sources")) return NestedSaveProperty.GasSources;
            if (reader.ValueTextEquals("importTerminals")) return NestedSaveProperty.GasImportTerminals;
            if (reader.ValueTextEquals("storages")) return NestedSaveProperty.GasStorages;
            if (reader.ValueTextEquals("servicePoints")) return NestedSaveProperty.GasServicePoints;
        }
        else if (context == NestedSaveContext.Optical)
        {
            if (reader.ValueTextEquals("nodes")) return NestedSaveProperty.OpticalNodes;
            if (reader.ValueTextEquals("fiberCables")) return NestedSaveProperty.FiberCables;
            if (reader.ValueTextEquals("equipment")) return NestedSaveProperty.OpticalEquipment;
            if (reader.ValueTextEquals("backhauls")) return NestedSaveProperty.OpticalBackhauls;
            if (reader.ValueTextEquals("demands")) return NestedSaveProperty.OpticalDemands;
        }
        else if (context == NestedSaveContext.OpticalDemand && reader.ValueTextEquals("routeCableIds")) return NestedSaveProperty.OpticalRouteCableIds;
        else if (context == NestedSaveContext.Radio)
        {
            if (reader.ValueTextEquals("sites")) return NestedSaveProperty.RadioSites;
            if (reader.ValueTextEquals("bands")) return NestedSaveProperty.RadioBands;
            if (reader.ValueTextEquals("frequencyBlocks")) return NestedSaveProperty.RadioFrequencyBlocks;
            if (reader.ValueTextEquals("links")) return NestedSaveProperty.RadioLinks;
            if (reader.ValueTextEquals("peers")) return NestedSaveProperty.RadioPeers;
            if (reader.ValueTextEquals("antennas")) return NestedSaveProperty.RadioAntennas;
            if (reader.ValueTextEquals("transmitters")) return NestedSaveProperty.RadioTransmitters;
            if (reader.ValueTextEquals("receivers")) return NestedSaveProperty.RadioReceivers;
            if (reader.ValueTextEquals("emissions")) return NestedSaveProperty.RadioEmissions;
            if (reader.ValueTextEquals("siteInfrastructure")) return NestedSaveProperty.RadioSiteInfrastructure;
            if (reader.ValueTextEquals("linkEntityBindings")) return NestedSaveProperty.RadioLinkEntityBindings;
        }
        else if (context == NestedSaveContext.WorldEnvironment)
        {
            if (reader.ValueTextEquals("features")) return NestedSaveProperty.Features;
            if (reader.ValueTextEquals("toponyms")) return NestedSaveProperty.Toponyms;
        }
        else if (context == NestedSaveContext.GeographicFeature && reader.ValueTextEquals("geometry")) return NestedSaveProperty.Geometry;
        else if (context == NestedSaveContext.RegionalGeneration && reader.ValueTextEquals("snapshot")) return NestedSaveProperty.Snapshot;
        else if (context == NestedSaveContext.RegionalGenerationSnapshot)
        {
            if (reader.ValueTextEquals("settlements")) return NestedSaveProperty.RegionalSettlements;
            if (reader.ValueTextEquals("growthEvents")) return NestedSaveProperty.RegionalGrowthEvents;
            if (reader.ValueTextEquals("corridors")) return NestedSaveProperty.RegionalCorridors;
            if (reader.ValueTextEquals("districts")) return NestedSaveProperty.RegionalDistricts;
            if (reader.ValueTextEquals("parcels")) return NestedSaveProperty.RegionalParcels;
            if (reader.ValueTextEquals("buildings")) return NestedSaveProperty.RegionalBuildings;
            if (reader.ValueTextEquals("pois")) return NestedSaveProperty.RegionalPois;
            if (reader.ValueTextEquals("toponyms")) return NestedSaveProperty.RegionalToponyms;
            if (reader.ValueTextEquals("roadSigns")) return NestedSaveProperty.RegionalRoadSigns;
        }
        else if (context == NestedSaveContext.RegionalCorridor && reader.ValueTextEquals("geometry")) return NestedSaveProperty.RegionalCorridorGeometry;
        return NestedSaveProperty.Other;
    }

    private static NestedSaveContext GetNestedObjectContext(NestedSaveContext context, NestedSaveProperty property) =>
        (context, property) switch
        {
            (NestedSaveContext.Root, NestedSaveProperty.Simulation) => NestedSaveContext.Simulation,
            (NestedSaveContext.Simulation, NestedSaveProperty.RailwayOperations) => NestedSaveContext.RailwayOperations,
            (NestedSaveContext.Simulation, NestedSaveProperty.MultimodalTransit) => NestedSaveContext.MultimodalTransit,
            (NestedSaveContext.Simulation, NestedSaveProperty.Economy) => NestedSaveContext.Economy,
            (NestedSaveContext.Economy, NestedSaveProperty.Logistics) => NestedSaveContext.Logistics,
            (NestedSaveContext.Economy, NestedSaveProperty.Power) => NestedSaveContext.Power,
            (NestedSaveContext.Economy, NestedSaveProperty.WaterSewer) => NestedSaveContext.WaterSewer,
            (NestedSaveContext.Economy, NestedSaveProperty.Gas) => NestedSaveContext.Gas,
            (NestedSaveContext.Economy, NestedSaveProperty.Optical) => NestedSaveContext.Optical,
            (NestedSaveContext.Economy, NestedSaveProperty.Radio) => NestedSaveContext.Radio,
            (NestedSaveContext.Economy, NestedSaveProperty.WorldEnvironment) => NestedSaveContext.WorldEnvironment,
            (NestedSaveContext.Economy, NestedSaveProperty.RegionalGeneration) => NestedSaveContext.RegionalGeneration,
            (NestedSaveContext.RegionalGeneration, NestedSaveProperty.Snapshot) => NestedSaveContext.RegionalGenerationSnapshot,
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
            (NestedSaveContext.MultimodalTransit, NestedSaveProperty.TransitPatterns) => NestedSaveContext.TransitPattern,
            (NestedSaveContext.MultimodalTransit, NestedSaveProperty.TransitJourneys) => NestedSaveContext.TransitJourney,
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalDemands) => NestedSaveContext.OpticalDemand,
            (NestedSaveContext.WorldEnvironment, NestedSaveProperty.Features) => NestedSaveContext.GeographicFeature,
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalCorridors) => NestedSaveContext.RegionalCorridor,
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
            (NestedSaveContext.TransitPattern, NestedSaveProperty.TransitPatternStops) => new(limits.MaximumLaneConnectionCount, "simulation.multimodalTransit.patterns[].stops", NestedArrayKind.TransitPatternStops),
            (NestedSaveContext.TransitJourney, NestedSaveProperty.JourneyLegs) => new(limits.MaximumLaneConnectionCount, "simulation.multimodalTransit.journeys[].legs", NestedArrayKind.JourneyLegs),
            (NestedSaveContext.Economy, NestedSaveProperty.Companies) => new(limits.MaximumBuildingCount, "simulation.economy.companies", NestedArrayKind.None),
            (NestedSaveContext.Economy, NestedSaveProperty.Establishments) => new(limits.MaximumBuildingCount, "simulation.economy.establishments", NestedArrayKind.None),
            (NestedSaveContext.Economy, NestedSaveProperty.Jobs) => new(limits.MaximumPersonCount, "simulation.economy.jobs", NestedArrayKind.None),
            (NestedSaveContext.Economy, NestedSaveProperty.Employments) => new(limits.MaximumPersonCount, "simulation.economy.employments", NestedArrayKind.None),
            (NestedSaveContext.Economy, NestedSaveProperty.EconomyHouseholds) => new(limits.MaximumHouseholdCount, "simulation.economy.households", NestedArrayKind.None),
            (NestedSaveContext.Logistics, NestedSaveProperty.Commodities) => new(limits.MaximumBuildingCount, "simulation.economy.logistics.commodities", NestedArrayKind.None),
            (NestedSaveContext.Logistics, NestedSaveProperty.Inventories) => new(limits.MaximumBuildingCount, "simulation.economy.logistics.inventories", NestedArrayKind.None),
            (NestedSaveContext.Logistics, NestedSaveProperty.Orders) => new(limits.MaximumPersonCount, "simulation.economy.logistics.orders", NestedArrayKind.None),
            (NestedSaveContext.Logistics, NestedSaveProperty.Shipments) => new(limits.MaximumVehicleCount, "simulation.economy.logistics.shipments", NestedArrayKind.None),
            (NestedSaveContext.Power, NestedSaveProperty.PowerNodes) => new(limits.MaximumRoadNodeCount, "simulation.economy.power.nodes", NestedArrayKind.None),
            (NestedSaveContext.Power, NestedSaveProperty.PowerLines) => new(limits.MaximumRoadSegmentCount, "simulation.economy.power.lines", NestedArrayKind.None),
            (NestedSaveContext.Power, NestedSaveProperty.Generators) => new(limits.MaximumBuildingCount, "simulation.economy.power.generators", NestedArrayKind.None),
            (NestedSaveContext.Power, NestedSaveProperty.PowerLoads) => new(limits.MaximumBuildingCount, "simulation.economy.power.loads", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.WaterNodes) => new(limits.MaximumRoadNodeCount, "simulation.economy.waterSewer.waterNodes", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.WaterPipes) => new(limits.MaximumRoadSegmentCount, "simulation.economy.waterSewer.waterPipes", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.SewerNodes) => new(limits.MaximumRoadNodeCount, "simulation.economy.waterSewer.sewerNodes", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.SewerPipes) => new(limits.MaximumRoadSegmentCount, "simulation.economy.waterSewer.sewerPipes", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.WaterSources) => new(limits.MaximumBuildingCount, "simulation.economy.waterSewer.waterSources", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.Reservoirs) => new(limits.MaximumBuildingCount, "simulation.economy.waterSewer.reservoirs", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.Pumps) => new(limits.MaximumBuildingCount, "simulation.economy.waterSewer.pumps", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.TreatmentPlants) => new(limits.MaximumBuildingCount, "simulation.economy.waterSewer.treatmentPlants", NestedArrayKind.None),
            (NestedSaveContext.WaterSewer, NestedSaveProperty.ServicePoints) => new(limits.MaximumBuildingCount, "simulation.economy.waterSewer.servicePoints", NestedArrayKind.None),
            (NestedSaveContext.Gas, NestedSaveProperty.GasNodes) => new(limits.MaximumRoadNodeCount, "simulation.economy.gas.nodes", NestedArrayKind.None),
            (NestedSaveContext.Gas, NestedSaveProperty.GasPipelines) => new(limits.MaximumRoadSegmentCount, "simulation.economy.gas.pipelines", NestedArrayKind.None),
            (NestedSaveContext.Gas, NestedSaveProperty.GasSources) => new(limits.MaximumBuildingCount, "simulation.economy.gas.sources", NestedArrayKind.None),
            (NestedSaveContext.Gas, NestedSaveProperty.GasImportTerminals) => new(limits.MaximumBuildingCount, "simulation.economy.gas.importTerminals", NestedArrayKind.None),
            (NestedSaveContext.Gas, NestedSaveProperty.GasStorages) => new(limits.MaximumBuildingCount, "simulation.economy.gas.storages", NestedArrayKind.None),
            (NestedSaveContext.Gas, NestedSaveProperty.GasServicePoints) => new(limits.MaximumBuildingCount, "simulation.economy.gas.servicePoints", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalNodes) => new(limits.MaximumInfrastructureNodeCount, "simulation.economy.optical.nodes", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.FiberCables) => new(limits.MaximumInfrastructureSegmentCount, "simulation.economy.optical.fiberCables", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalEquipment) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.optical.equipment", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalBackhauls) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.optical.backhauls", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalDemands) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.optical.demands", NestedArrayKind.None),
            (NestedSaveContext.OpticalDemand, NestedSaveProperty.OpticalRouteCableIds) => new(limits.MaximumOpticalRouteCableCount, "simulation.economy.optical.demands[].routeCableIds", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioSites) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.sites", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioBands) => new(limits.MaximumInfrastructureNodeCount, "simulation.economy.radio.bands", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioFrequencyBlocks) => new(limits.MaximumInfrastructureSegmentCount, "simulation.economy.radio.frequencyBlocks", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioLinks) => new(limits.MaximumInfrastructureConnectionCount, "simulation.economy.radio.links", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioPeers) => new(limits.MaximumPersonCount, "simulation.economy.radio.peers", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioAntennas) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.antennas", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioTransmitters) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.transmitters", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioReceivers) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.receivers", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioEmissions) => new(limits.MaximumInfrastructureSegmentCount, "simulation.economy.radio.emissions", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioSiteInfrastructure) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.siteInfrastructure", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioLinkEntityBindings) => new(limits.MaximumInfrastructureConnectionCount, "simulation.economy.radio.linkEntityBindings", NestedArrayKind.None),
            (NestedSaveContext.WorldEnvironment, NestedSaveProperty.Features) => new(limits.MaximumGeographicFeatureCount, "simulation.economy.worldEnvironment.features", NestedArrayKind.None),
            (NestedSaveContext.WorldEnvironment, NestedSaveProperty.Toponyms) => new(limits.MaximumNaturalToponymCount, "simulation.economy.worldEnvironment.toponyms", NestedArrayKind.None),
            (NestedSaveContext.GeographicFeature, NestedSaveProperty.Geometry) => new(limits.MaximumGeographicFeatureGeometryPointCount, "simulation.economy.worldEnvironment.features[].geometry", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalSettlements) => new(limits.MaximumBuildingCount, "simulation.economy.regionalGeneration.snapshot.settlements", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalGrowthEvents) => new(limits.MaximumPersonCount, "simulation.economy.regionalGeneration.snapshot.growthEvents", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalCorridors) => new(limits.MaximumRoadSegmentCount, "simulation.economy.regionalGeneration.snapshot.corridors", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalDistricts) => new(limits.MaximumBuildingCount, "simulation.economy.regionalGeneration.snapshot.districts", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalParcels) => new(limits.MaximumBuildingCount, "simulation.economy.regionalGeneration.snapshot.parcels", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalBuildings) => new(limits.MaximumBuildingCount, "simulation.economy.regionalGeneration.snapshot.buildings", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalPois) => new(limits.MaximumPoiCount, "simulation.economy.regionalGeneration.snapshot.pois", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalToponyms) => new(limits.MaximumNaturalToponymCount, "simulation.economy.regionalGeneration.snapshot.toponyms", NestedArrayKind.None),
            (NestedSaveContext.RegionalGenerationSnapshot, NestedSaveProperty.RegionalRoadSigns) => new(limits.MaximumRoadAccessPointCount, "simulation.economy.regionalGeneration.snapshot.roadSigns", NestedArrayKind.None),
            (NestedSaveContext.RegionalCorridor, NestedSaveProperty.RegionalCorridorGeometry) => new(limits.MaximumGeographicFeatureGeometryPointCount, "simulation.economy.regionalGeneration.snapshot.corridors[].geometry", NestedArrayKind.None),
            _ => new(int.MaxValue, null, NestedArrayKind.None),
        };

    private static bool IsJsonValueStart(JsonTokenType tokenType) => tokenType is
        JsonTokenType.StartObject or JsonTokenType.StartArray or JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null;

    private enum NestedSaveContext : byte
    {
        Other, Root, Simulation, Vehicle, Person, BlockSection, Depot, RailwayOperations, RailwayRoute, Timetable, MultimodalTransit, TransitPattern, TransitJourney, Economy, Logistics, Power, WaterSewer, Gas, Optical, OpticalDemand, Radio, WorldEnvironment, GeographicFeature,
        RegionalGeneration, RegionalGenerationSnapshot, RegionalCorridor,
    }

    private enum NestedSaveProperty : byte
    {
        Other, Simulation, Vehicles, Persons, BlockSections, Depots, RailwayOperations, MultimodalTransit, TransitPatterns, TransitJourneys, TransitPatternStops, JourneyLegs, RouteSteps, Schedule, Needs, SegmentIds, TrackSegmentIds, Routes, Timetables, Stops,
        Economy, Companies, Establishments, Jobs, Employments, EconomyHouseholds, Logistics, Commodities, Inventories, Orders, Shipments, Power, PowerNodes, PowerLines, Generators, PowerLoads,
        WaterSewer, WaterNodes, WaterPipes, SewerNodes, SewerPipes, WaterSources, Reservoirs, Pumps, TreatmentPlants, ServicePoints,
        Gas, GasNodes, GasPipelines, GasSources, GasImportTerminals, GasStorages, GasServicePoints,
        Optical, OpticalNodes, FiberCables, OpticalEquipment, OpticalBackhauls, OpticalDemands, OpticalRouteCableIds,
        Radio, RadioSites, RadioBands, RadioFrequencyBlocks, RadioLinks, RadioPeers, RadioAntennas, RadioTransmitters, RadioReceivers, RadioEmissions, RadioSiteInfrastructure, RadioLinkEntityBindings,
        WorldEnvironment, Features, Toponyms, Geometry,
        RegionalGeneration, Snapshot, RegionalSettlements, RegionalGrowthEvents, RegionalCorridors, RegionalDistricts, RegionalParcels, RegionalBuildings, RegionalPois, RegionalToponyms, RegionalRoadSigns, RegionalCorridorGeometry,
    }

    private enum NestedArrayKind : byte { None, TimetableStops, TransitPatternStops, JourneyLegs }
    private readonly record struct NestedArrayRule(int MaximumCount, string? Path, NestedArrayKind Kind);
    private struct NestedSaveScanTotals { public int TimetableStopCount; public int TransitPatternStopCount; public int JourneyLegCount; }
}
