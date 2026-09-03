from pathlib import Path


def replace_once(path_name: str, old: str, new: str) -> None:
    path = Path(path_name)
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path_name}: expected exactly one patch target, found {count}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def append_before(path_name: str, marker: str, addition: str) -> None:
    replace_once(path_name, marker, addition + marker)


# #288: expose topology-derived route length and reject forged checkpoint lengths.
replace_once(
    "src/MachiVerseWorks.Simulation/Internal/RailwayOperationsStore.cs",
    """    private double[] ValidateServiceDefinition(
        RouteState route,
""",
    """    internal double GetDerivedRouteLength(RailwayRouteSnapshot routeSnapshot) =>
        BuildRoute(routeSnapshot.Id, routeSnapshot.TrackSegmentIds).LengthMeters;

    private double[] ValidateServiceDefinition(
        RouteState route,
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RailwayOperations.Validation.cs",
    """            foreach (var segmentId in route.TrackSegmentIds)
            {
                if (!segmentIds.Contains(segmentId) || !localSegments.Add(segmentId))
                    throw new ArgumentException($\"Railway route {route.Id.Value} references a missing or repeated TrackSegment {segmentId.Value}.\", nameof(checkpoint));
            }
        }
""",
    """            foreach (var segmentId in route.TrackSegmentIds)
            {
                if (!segmentIds.Contains(segmentId) || !localSegments.Add(segmentId))
                    throw new ArgumentException($\"Railway route {route.Id.Value} references a missing or repeated TrackSegment {segmentId.Value}.\", nameof(checkpoint));
            }
            var derivedLength = serviceDefinitionValidator.GetDerivedRouteLength(route);
            var lengthTolerance = Math.Max(1e-7, derivedLength * 1e-9);
            if (Math.Abs(route.LengthMeters - derivedLength) > lengthTolerance)
                throw new ArgumentException($\"Railway route {route.Id.Value} length does not match its Track topology.\", nameof(checkpoint));
        }
""")

# #284: enforce Service/Train state-machine invariants at checkpoint restore.
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RailwayOperations.Validation.cs",
    """        foreach (var service in services)
        {
            if (service.TrainId is { } trainId)
            {
                if (!trainById.TryGetValue(trainId, out var train) || train.ServiceId != service.Id)
                    throw new ArgumentException($\"Railway service {service.Id.Value} references a missing or mismatched Train.\", nameof(checkpoint));
            }
            else if (trains.Any(train => train.ServiceId == service.Id))
            {
                throw new ArgumentException($\"Railway service {service.Id.Value} is missing its reverse Train reference.\", nameof(checkpoint));
            }
        }
""",
    """        foreach (var service in services)
        {
            if (service.TrainId is not { } trainId)
            {
                if (service.State != RailwayServiceState.Planned || trains.Any(train => train.ServiceId == service.Id))
                    throw new ArgumentException($\"Railway service {service.Id.Value} has invalid Train lifecycle state.\", nameof(checkpoint));
                continue;
            }
            if (!trainById.TryGetValue(trainId, out var train) || train.ServiceId != service.Id)
                throw new ArgumentException($\"Railway service {service.Id.Value} references a missing or mismatched Train.\", nameof(checkpoint));
            var timetable = timetableById[service.TimetableId];
            if ((service.State == RailwayServiceState.Completed) != (train.State == TrainMovementState.Completed))
                throw new ArgumentException($\"Railway service {service.Id.Value} and Train {train.Id.Value} disagree about completion.\", nameof(checkpoint));
            if (train.State == TrainMovementState.Dwelling && train.CurrentPlatformId is null)
                throw new ArgumentException($\"Dwelling Train {train.Id.Value} must occupy a Platform.\", nameof(checkpoint));
            switch (service.State)
            {
                case RailwayServiceState.Planned:
                    if (train.State is not (TrainMovementState.InDepot or TrainMovementState.WaitingForBlock)
                        || train.RouteDistanceMeters > 1e-7 || train.SpeedMetersPerSecond > 1e-9
                        || train.CurrentDepotId != service.OriginDepotId || service.NextStopIndex != 0)
                        throw new ArgumentException($\"Planned Railway service {service.Id.Value} has inconsistent Train state.\", nameof(checkpoint));
                    break;
                case RailwayServiceState.Active:
                    if (train.State is TrainMovementState.InDepot or TrainMovementState.Completed || train.CurrentDepotId is not null)
                        throw new ArgumentException($\"Active Railway service {service.Id.Value} has inconsistent Train state.\", nameof(checkpoint));
                    break;
                case RailwayServiceState.Completed:
                    if (train.CurrentDepotId != service.DestinationDepotId || service.NextStopIndex != timetable.Stops.Count)
                        throw new ArgumentException($\"Completed Railway service {service.Id.Value} is not finalized at its destination.\", nameof(checkpoint));
                    break;
            }
        }
""")

# #287: Passenger and Journey must belong to the same TripRequest.
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.MultimodalTransit.cs",
    """            if (!journeyById.TryGetValue(passenger.JourneyId, out var journey)) throw new ArgumentException($\"Passenger {passenger.Id.Value} references a missing Journey.\", nameof(checkpoint));
            if (passenger.LegIndex < 0 || passenger.LegIndex >= journey.Legs.Count) throw new ArgumentException($\"Passenger {passenger.Id.Value} has an invalid Journey leg index.\", nameof(checkpoint));
""",
    """            if (!journeyById.TryGetValue(passenger.JourneyId, out var journey)) throw new ArgumentException($\"Passenger {passenger.Id.Value} references a missing Journey.\", nameof(checkpoint));
            if (passenger.TripRequestId != journey.TripRequestId) throw new ArgumentException($\"Passenger {passenger.Id.Value} belongs to a different TripRequest than its Journey.\", nameof(checkpoint));
            if (passenger.LegIndex < 0 || passenger.LegIndex >= journey.Legs.Count) throw new ArgumentException($\"Passenger {passenger.Id.Value} has an invalid Journey leg index.\", nameof(checkpoint));
""")

# #291: Railway Operations codec snapshot-level identity/reference validation.
replace_once(
    "src/MachiVerseWorks.Protocol/RailwayOperationsProtocolCodec.cs",
    """            message = new RailwayOperationsSnapshotMessage(tickCount, trains, services, timetables);
            error = ProtocolDecodeError.None;
""",
    """            message = new RailwayOperationsSnapshotMessage(tickCount, trains, services, timetables);
            ValidateSnapshotReferences(message);
            error = ProtocolDecodeError.None;
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RailwayOperationsProtocolCodec.cs",
    """        foreach (var timetable in message.Timetables)
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
""",
    """        foreach (var timetable in message.Timetables)
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
        ValidateSnapshotReferences(message);
    }

    private static void ValidateSnapshotReferences(RailwayOperationsSnapshotMessage message)
    {
        var trainById = new Dictionary<ulong, ProtocolTrainState>();
        foreach (var train in message.Trains) if (!trainById.TryAdd(train.Id, train)) throw new InvalidDataException(\"Duplicate Train ID.\");
        var serviceById = new Dictionary<ulong, ProtocolRailwayServiceState>();
        foreach (var service in message.Services) if (!serviceById.TryAdd(service.Id, service)) throw new InvalidDataException(\"Duplicate Railway Service ID.\");
        var timetableById = new Dictionary<ulong, ProtocolTimetable>();
        foreach (var timetable in message.Timetables) if (!timetableById.TryAdd(timetable.Id, timetable)) throw new InvalidDataException(\"Duplicate Timetable ID.\");

        foreach (var train in message.Trains)
        {
            if (!serviceById.TryGetValue(train.ServiceId, out var service) || service.TrainId != train.Id)
                throw new InvalidDataException(\"Train references a missing or mismatched Railway Service.\");
        }
        foreach (var service in message.Services)
        {
            if (!timetableById.TryGetValue(service.TimetableId, out var timetable) || service.NextStopIndex > timetable.Stops.Count)
                throw new InvalidDataException(\"Railway Service references an invalid Timetable position.\");
            if (service.TrainId != 0 && (!trainById.TryGetValue(service.TrainId, out var train) || train.ServiceId != service.Id))
                throw new InvalidDataException(\"Railway Service references a missing or mismatched Train.\");
        }
    }
""")

replace_once(
    "src/web/src/railway-operations.ts",
    """  if (cursor !== end) throw new ProtocolDecodeFailure('Railway operations payload contains trailing bytes.');
  return { type: RailwayOperationsMessageType.RailwayOperationsSnapshot, tickCount, trains, services, timetables };
}
""",
    """  if (cursor !== end) throw new ProtocolDecodeFailure('Railway operations payload contains trailing bytes.');
  validateSnapshotReferences(trains, services, timetables);
  return { type: RailwayOperationsMessageType.RailwayOperationsSnapshot, tickCount, trains, services, timetables };
}

function validateSnapshotReferences(trains: readonly TrainState[], services: readonly RailwayServiceStateMessage[], timetables: readonly RailwayTimetable[]): void {
  const trainById = new Map<bigint, TrainState>();
  for (const train of trains) { if (trainById.has(train.id)) throw new ProtocolDecodeFailure('Duplicate Railway Train ID.'); trainById.set(train.id, train); }
  const serviceById = new Map<bigint, RailwayServiceStateMessage>();
  for (const service of services) { if (serviceById.has(service.id)) throw new ProtocolDecodeFailure('Duplicate Railway Service ID.'); serviceById.set(service.id, service); }
  const timetableById = new Map<bigint, RailwayTimetable>();
  for (const timetable of timetables) { if (timetableById.has(timetable.id)) throw new ProtocolDecodeFailure('Duplicate Railway Timetable ID.'); timetableById.set(timetable.id, timetable); }
  for (const train of trains) {
    const service = serviceById.get(train.serviceId);
    if (service === undefined || service.trainId !== train.id) throw new ProtocolDecodeFailure('Railway Train service reference is inconsistent.');
  }
  for (const service of services) {
    const timetable = timetableById.get(service.timetableId);
    if (timetable === undefined || service.nextStopIndex > timetable.stops.length) throw new ProtocolDecodeFailure('Railway Service timetable reference is inconsistent.');
    if (service.trainId !== null) {
      const train = trainById.get(service.trainId);
      if (train === undefined || train.serviceId !== service.id) throw new ProtocolDecodeFailure('Railway Service train reference is inconsistent.');
    }
  }
}
""")

# #292: preflight nested Multimodal pattern stops and journey legs before JSON materialization.
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """            if (reader.ValueTextEquals(\"railwayOperations\")) return NestedSaveProperty.RailwayOperations;
            if (reader.ValueTextEquals(\"economy\")) return NestedSaveProperty.Economy;
""",
    """            if (reader.ValueTextEquals(\"railwayOperations\")) return NestedSaveProperty.RailwayOperations;
            if (reader.ValueTextEquals(\"multimodalTransit\")) return NestedSaveProperty.MultimodalTransit;
            if (reader.ValueTextEquals(\"economy\")) return NestedSaveProperty.Economy;
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """        else if (context == NestedSaveContext.Timetable && reader.ValueTextEquals(\"stops\")) return NestedSaveProperty.Stops;
        else if (context == NestedSaveContext.Economy)
""",
    """        else if (context == NestedSaveContext.Timetable && reader.ValueTextEquals(\"stops\")) return NestedSaveProperty.Stops;
        else if (context == NestedSaveContext.MultimodalTransit)
        {
            if (reader.ValueTextEquals(\"patterns\")) return NestedSaveProperty.TransitPatterns;
            if (reader.ValueTextEquals(\"journeys\")) return NestedSaveProperty.TransitJourneys;
        }
        else if (context == NestedSaveContext.TransitPattern && reader.ValueTextEquals(\"stops\")) return NestedSaveProperty.TransitPatternStops;
        else if (context == NestedSaveContext.TransitJourney && reader.ValueTextEquals(\"legs\")) return NestedSaveProperty.JourneyLegs;
        else if (context == NestedSaveContext.Economy)
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """            (NestedSaveContext.Simulation, NestedSaveProperty.RailwayOperations) => NestedSaveContext.RailwayOperations,
            (NestedSaveContext.Simulation, NestedSaveProperty.Economy) => NestedSaveContext.Economy,
""",
    """            (NestedSaveContext.Simulation, NestedSaveProperty.RailwayOperations) => NestedSaveContext.RailwayOperations,
            (NestedSaveContext.Simulation, NestedSaveProperty.MultimodalTransit) => NestedSaveContext.MultimodalTransit,
            (NestedSaveContext.Simulation, NestedSaveProperty.Economy) => NestedSaveContext.Economy,
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """            (NestedSaveContext.RailwayOperations, NestedSaveProperty.Timetables) => NestedSaveContext.Timetable,
            (NestedSaveContext.WorldEnvironment, NestedSaveProperty.Features) => NestedSaveContext.GeographicFeature,
""",
    """            (NestedSaveContext.RailwayOperations, NestedSaveProperty.Timetables) => NestedSaveContext.Timetable,
            (NestedSaveContext.MultimodalTransit, NestedSaveProperty.TransitPatterns) => NestedSaveContext.TransitPattern,
            (NestedSaveContext.MultimodalTransit, NestedSaveProperty.TransitJourneys) => NestedSaveContext.TransitJourney,
            (NestedSaveContext.WorldEnvironment, NestedSaveProperty.Features) => NestedSaveContext.GeographicFeature,
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """            (NestedSaveContext.Timetable, NestedSaveProperty.Stops) => new(limits.MaximumTimetableStopCount, \"simulation.railwayOperations.timetables[].stops\", NestedArrayKind.TimetableStops),
            (NestedSaveContext.Economy, NestedSaveProperty.Companies) => new(limits.MaximumBuildingCount, \"simulation.economy.companies\", NestedArrayKind.None),
""",
    """            (NestedSaveContext.Timetable, NestedSaveProperty.Stops) => new(limits.MaximumTimetableStopCount, \"simulation.railwayOperations.timetables[].stops\", NestedArrayKind.TimetableStops),
            (NestedSaveContext.TransitPattern, NestedSaveProperty.TransitPatternStops) => new(limits.MaximumLaneConnectionCount, \"simulation.multimodalTransit.patterns[].stops\", NestedArrayKind.TransitPatternStops),
            (NestedSaveContext.TransitJourney, NestedSaveProperty.JourneyLegs) => new(limits.MaximumLaneConnectionCount, \"simulation.multimodalTransit.journeys[].legs\", NestedArrayKind.JourneyLegs),
            (NestedSaveContext.Economy, NestedSaveProperty.Companies) => new(limits.MaximumBuildingCount, \"simulation.economy.companies\", NestedArrayKind.None),
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """            if (kind == NestedArrayKind.TimetableStops)
            {
                totals.TimetableStopCount++;
                if (totals.TimetableStopCount > limits.MaximumTimetableStopTotalCount)
                    throw new InvalidDataException($\"Save Data total RailwayOperations Timetable stop count exceeds the configured {limits.MaximumTimetableStopTotalCount}-entry limit before deserialization.\");
            }
""",
    """            if (kind == NestedArrayKind.TimetableStops)
            {
                totals.TimetableStopCount++;
                if (totals.TimetableStopCount > limits.MaximumTimetableStopTotalCount)
                    throw new InvalidDataException($\"Save Data total RailwayOperations Timetable stop count exceeds the configured {limits.MaximumTimetableStopTotalCount}-entry limit before deserialization.\");
            }
            else if (kind == NestedArrayKind.TransitPatternStops)
            {
                totals.TransitPatternStopCount++;
                if (totals.TransitPatternStopCount > limits.MaximumLaneConnectionCount)
                    throw new InvalidDataException($\"Save Data total Transit Pattern stop count exceeds the configured {limits.MaximumLaneConnectionCount}-entry limit before deserialization.\");
            }
            else if (kind == NestedArrayKind.JourneyLegs)
            {
                totals.JourneyLegCount++;
                if (totals.JourneyLegCount > limits.MaximumLaneConnectionCount)
                    throw new InvalidDataException($\"Save Data total Journey leg count exceeds the configured {limits.MaximumLaneConnectionCount}-entry limit before deserialization.\");
            }
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """        Other, Root, Simulation, Vehicle, Person, BlockSection, Depot, RailwayOperations, RailwayRoute, Timetable, Economy, Logistics, Power, WaterSewer, Gas, Optical, Radio, WorldEnvironment, GeographicFeature,
        RegionalGeneration, RegionalGenerationSnapshot, RegionalCorridor,
""",
    """        Other, Root, Simulation, Vehicle, Person, BlockSection, Depot, RailwayOperations, RailwayRoute, Timetable, MultimodalTransit, TransitPattern, TransitJourney, Economy, Logistics, Power, WaterSewer, Gas, Optical, Radio, WorldEnvironment, GeographicFeature,
        RegionalGeneration, RegionalGenerationSnapshot, RegionalCorridor,
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """        Other, Simulation, Vehicles, Persons, BlockSections, Depots, RailwayOperations, RouteSteps, Schedule, Needs, SegmentIds, TrackSegmentIds, Routes, Timetables, Stops,
""",
    """        Other, Simulation, Vehicles, Persons, BlockSections, Depots, RailwayOperations, MultimodalTransit, TransitPatterns, TransitJourneys, TransitPatternStops, JourneyLegs, RouteSteps, Schedule, Needs, SegmentIds, TrackSegmentIds, Routes, Timetables, Stops,
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """    private enum NestedArrayKind : byte { None, TimetableStops }
    private readonly record struct NestedArrayRule(int MaximumCount, string? Path, NestedArrayKind Kind);
    private struct NestedSaveScanTotals { public int TimetableStopCount; }
""",
    """    private enum NestedArrayKind : byte { None, TimetableStops, TransitPatternStops, JourneyLegs }
    private readonly record struct NestedArrayRule(int MaximumCount, string? Path, NestedArrayKind Kind);
    private struct NestedSaveScanTotals { public int TimetableStopCount; public int TransitPatternStopCount; public int JourneyLegCount; }
""")

# #295: Stable ID uniqueness and Spectrum internal references in C#.
replace_once(
    "src/MachiVerseWorks.Protocol/RadioProtocolCodec.cs",
    """        foreach (var item in message.Receivers)
        {
            if (item.ReceiverId == 0 || !siteIds.Contains(item.SiteId) || !antennaIds.Contains(item.AntennaId) || !Positive(item.MinimumFrequencyMegahertz) || !Positive(item.MaximumFrequencyMegahertz) || item.MaximumFrequencyMegahertz <= item.MinimumFrequencyMegahertz || !Finite(item.SensitivityDbm) || item.SensitivityDbm >= 0d) return false;
        }
""",
    """        var receiverIds = new HashSet<ulong>();
        foreach (var item in message.Receivers)
        {
            if (item.ReceiverId == 0 || !receiverIds.Add(item.ReceiverId) || !siteIds.Contains(item.SiteId) || !antennaIds.Contains(item.AntennaId) || !Positive(item.MinimumFrequencyMegahertz) || !Positive(item.MaximumFrequencyMegahertz) || item.MaximumFrequencyMegahertz <= item.MinimumFrequencyMegahertz || !Finite(item.SensitivityDbm) || item.SensitivityDbm >= 0d) return false;
        }
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RadioProtocolCodec.cs",
    """        foreach (var item in message.Links)
        {
            if (item.LinkId == 0 || item.FromSiteId == 0 || item.ToSiteId == 0 || item.FromSiteId == item.ToSiteId || !siteIds.Contains(item.FromSiteId) || !siteIds.Contains(item.ToSiteId) || item.FrequencyBlockId == 0 || !NonNegative(item.DistanceMeters) || !Finite(item.PathLossDb) || !Finite(item.ReceivedPowerDbm) || !Finite(item.InterferenceDbm) || !Finite(item.SinrDb) || !NonNegative(item.Utilization) || item.Utilization > 1d + 1e-9 || !Enum.IsDefined(item.State)) return false;
        }
""",
    """        var linkIds = new HashSet<ulong>();
        foreach (var item in message.Links)
        {
            if (item.LinkId == 0 || !linkIds.Add(item.LinkId) || item.FromSiteId == 0 || item.ToSiteId == 0 || item.FromSiteId == item.ToSiteId || !siteIds.Contains(item.FromSiteId) || !siteIds.Contains(item.ToSiteId) || item.FrequencyBlockId == 0 || !NonNegative(item.DistanceMeters) || !Finite(item.PathLossDb) || !Finite(item.ReceivedPowerDbm) || !Finite(item.InterferenceDbm) || !Finite(item.SinrDb) || !NonNegative(item.Utilization) || item.Utilization > 1d + 1e-9 || !Enum.IsDefined(item.State)) return false;
        }
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RadioProtocolCodec.cs",
    """    private static bool IsValidSpectrum(SpectrumSnapshotMessage message)
    {
        foreach (var item in message.Bands) if (item.BandId == 0 || string.IsNullOrWhiteSpace(item.Name) || Utf8.GetByteCount(item.Name) > ushort.MaxValue || !Positive(item.MinimumFrequencyMegahertz) || !Positive(item.MaximumFrequencyMegahertz) || item.MaximumFrequencyMegahertz <= item.MinimumFrequencyMegahertz) return false;
        foreach (var item in message.FrequencyBlocks) if (item.FrequencyBlockId == 0 || item.BandId == 0 || !Positive(item.CenterFrequencyMegahertz) || !Positive(item.BandwidthMegahertz)) return false;
        foreach (var item in message.Conflicts) if (item.FirstBlockId == 0 || item.SecondBlockId == 0 || item.FirstSiteId == 0 || item.SecondSiteId == 0 || string.IsNullOrWhiteSpace(item.Reason) || Utf8.GetByteCount(item.Reason) > ushort.MaxValue) return false;
        return true;
    }
""",
    """    private static bool IsValidSpectrum(SpectrumSnapshotMessage message)
    {
        var bandIds = new HashSet<ulong>();
        foreach (var item in message.Bands) if (item.BandId == 0 || !bandIds.Add(item.BandId) || string.IsNullOrWhiteSpace(item.Name) || Utf8.GetByteCount(item.Name) > ushort.MaxValue || !Positive(item.MinimumFrequencyMegahertz) || !Positive(item.MaximumFrequencyMegahertz) || item.MaximumFrequencyMegahertz <= item.MinimumFrequencyMegahertz) return false;
        var blockIds = new HashSet<ulong>();
        foreach (var item in message.FrequencyBlocks) if (item.FrequencyBlockId == 0 || !blockIds.Add(item.FrequencyBlockId) || !bandIds.Contains(item.BandId) || !Positive(item.CenterFrequencyMegahertz) || !Positive(item.BandwidthMegahertz)) return false;
        foreach (var item in message.Conflicts) if (!blockIds.Contains(item.FirstBlockId) || !blockIds.Contains(item.SecondBlockId) || item.FirstSiteId == 0 || item.SecondSiteId == 0 || string.IsNullOrWhiteSpace(item.Reason) || Utf8.GetByteCount(item.Reason) > ushort.MaxValue) return false;
        return true;
    }
""")

# #295 Web parity.
replace_once(
    "src/web/src/radio-protocol.ts",
    """  if(c!==end)throw new ProtocolDecodeFailure('Spectrum snapshot contains trailing data.');
  if(bands.some(x=>x.bandId===0n||x.name.trim().length===0||!positive(x.minimumFrequencyMegahertz)||!positive(x.maximumFrequencyMegahertz)||x.maximumFrequencyMegahertz<=x.minimumFrequencyMegahertz)||frequencyBlocks.some(x=>x.frequencyBlockId===0n||x.bandId===0n||!positive(x.centerFrequencyMegahertz)||!positive(x.bandwidthMegahertz))||conflicts.some(x=>x.firstBlockId===0n||x.secondBlockId===0n||x.firstSiteId===0n||x.secondSiteId===0n||x.reason.trim().length===0))throw new ProtocolDecodeFailure('Spectrum snapshot contains invalid values.');
  return {type:SPECTRUM_SNAPSHOT_MESSAGE_TYPE,tickCount,bands,frequencyBlocks,conflicts};
""",
    """  if(c!==end)throw new ProtocolDecodeFailure('Spectrum snapshot contains trailing data.');
  const bandIds=new Set(bands.map(x=>x.bandId)); const blockIds=new Set(frequencyBlocks.map(x=>x.frequencyBlockId));
  if(bandIds.has(0n)||bandIds.size!==bands.length||bands.some(x=>x.name.trim().length===0||!positive(x.minimumFrequencyMegahertz)||!positive(x.maximumFrequencyMegahertz)||x.maximumFrequencyMegahertz<=x.minimumFrequencyMegahertz)
    ||blockIds.has(0n)||blockIds.size!==frequencyBlocks.length||frequencyBlocks.some(x=>!bandIds.has(x.bandId)||!positive(x.centerFrequencyMegahertz)||!positive(x.bandwidthMegahertz))
    ||conflicts.some(x=>!blockIds.has(x.firstBlockId)||!blockIds.has(x.secondBlockId)||x.firstSiteId===0n||x.secondSiteId===0n||x.reason.trim().length===0))throw new ProtocolDecodeFailure('Spectrum snapshot contains invalid values or references.');
  return {type:SPECTRUM_SNAPSHOT_MESSAGE_TYPE,tickCount,bands,frequencyBlocks,conflicts};
""")
replace_once(
    "src/web/src/radio-protocol.ts",
    """  const siteIds=new Set(sites.map(x=>x.siteId)); const antennaIds=new Set(antennas.map(x=>x.antennaId)); const transmitterIds=new Set(transmitters.map(x=>x.transmitterId));
""",
    """  const siteIds=new Set(sites.map(x=>x.siteId)); const antennaIds=new Set(antennas.map(x=>x.antennaId)); const transmitterIds=new Set(transmitters.map(x=>x.transmitterId)); const receiverIds=new Set(receivers.map(x=>x.receiverId)); const linkIds=new Set(links.map(x=>x.linkId));
""")
replace_once(
    "src/web/src/radio-protocol.ts",
    """  if(receivers.some(x=>x.receiverId===0n||!siteIds.has(x.siteId)||!antennaIds.has(x.antennaId)||!positive(x.minimumFrequencyMegahertz)||!positive(x.maximumFrequencyMegahertz)||x.maximumFrequencyMegahertz<=x.minimumFrequencyMegahertz||!finite(x.sensitivityDbm)||x.sensitivityDbm>=0))throw new ProtocolDecodeFailure('Radio receivers contain invalid values.');
""",
    """  if(receiverIds.has(0n)||receiverIds.size!==receivers.length||receivers.some(x=>!siteIds.has(x.siteId)||!antennaIds.has(x.antennaId)||!positive(x.minimumFrequencyMegahertz)||!positive(x.maximumFrequencyMegahertz)||x.maximumFrequencyMegahertz<=x.minimumFrequencyMegahertz||!finite(x.sensitivityDbm)||x.sensitivityDbm>=0))throw new ProtocolDecodeFailure('Radio receivers contain invalid values.');
""")
replace_once(
    "src/web/src/radio-protocol.ts",
    """  if(links.some(x=>x.linkId===0n||x.fromSiteId===0n||x.toSiteId===0n||x.fromSiteId===x.toSiteId||!siteIds.has(x.fromSiteId)||!siteIds.has(x.toSiteId)||x.frequencyBlockId===0n||!nonNegative(x.distanceMeters)||!finite(x.pathLossDb)||!finite(x.receivedPowerDbm)||!finite(x.interferenceDbm)||!finite(x.sinrDb)||!nonNegative(x.utilization)||x.utilization>1+1e-9||!enumRange(x.state,0,4)))throw new ProtocolDecodeFailure('Radio links contain invalid values.');
""",
    """  if(linkIds.has(0n)||linkIds.size!==links.length||links.some(x=>x.fromSiteId===0n||x.toSiteId===0n||x.fromSiteId===x.toSiteId||!siteIds.has(x.fromSiteId)||!siteIds.has(x.toSiteId)||x.frequencyBlockId===0n||!nonNegative(x.distanceMeters)||!finite(x.pathLossDb)||!finite(x.receivedPowerDbm)||!finite(x.interferenceDbm)||!finite(x.sinrDb)||!nonNegative(x.utilization)||x.utilization>1+1e-9||!enumRange(x.state,0,4)))throw new ProtocolDecodeFailure('Radio links contain invalid values.');
""")

# #303: generic DAG validation for World Environment checkpoint; reused by Regional checkpoint.
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.Environment.cs",
    """        foreach (var feature in worldEnvironment.Features)
        {
            if (feature.ParentId is { } parentId && !featureIds.Contains(parentId)) throw new ArgumentException(\"Geographic feature references a missing parent.\", nameof(checkpoint));
        }
""",
    """        foreach (var feature in worldEnvironment.Features)
        {
            if (feature.ParentId is { } parentId && !featureIds.Contains(parentId)) throw new ArgumentException(\"Geographic feature references a missing parent.\", nameof(checkpoint));
        }
        ValidateAcyclicParentGraph(worldEnvironment.Features.Select(static item => (item.Id, item.ParentId)), \"Geographic feature\", nameof(checkpoint));
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.Environment.cs",
    """        foreach (var toponym in worldEnvironment.Toponyms)
        {
            if (toponym.Provenance.ParentToponymId is { } parentId && !toponymIds.Contains(parentId))
                throw new ArgumentException(\"Toponym provenance references a missing parent toponym.\", nameof(checkpoint));
        }
    }
}
""",
    """        foreach (var toponym in worldEnvironment.Toponyms)
        {
            if (toponym.Provenance.ParentToponymId is { } parentId && !toponymIds.Contains(parentId))
                throw new ArgumentException(\"Toponym provenance references a missing parent toponym.\", nameof(checkpoint));
        }
        ValidateAcyclicParentGraph(worldEnvironment.Toponyms.Select(static item => (item.Id, item.Provenance.ParentToponymId)), \"Natural toponym\", nameof(checkpoint));
    }

    private static void ValidateAcyclicParentGraph<T>(IEnumerable<(T Id, T? ParentId)> nodes, string entityName, string parameterName)
        where T : struct, IEquatable<T>
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        foreach (var start in parents.Keys)
        {
            var seen = new HashSet<T>();
            var current = start;
            while (parents.TryGetValue(current, out var parent) && parent is { } parentId)
            {
                if (!seen.Add(current)) throw new ArgumentException($\"{entityName} parent graph contains a cycle.\", parameterName);
                current = parentId;
            }
        }
    }
}
""")

# #297/#302/#303 checkpoint-level Regional ownership, reciprocal links, containment, DAG.
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    """        var buildingIds = ValidateRegionalIds(snapshot.Buildings.Select(static item => item.Id.Value), \"Generated building\");
        _ = ValidateRegionalIds(snapshot.Pois.Select(static item => item.Id.Value), \"Generated POI\");
""",
    """        var buildingIds = ValidateRegionalIds(snapshot.Buildings.Select(static item => item.Id.Value), \"Generated building\");
        _ = ValidateRegionalIds(snapshot.Pois.Select(static item => item.Id.Value), \"Generated POI\");
        var districtById = snapshot.Districts.ToDictionary(static item => item.Id);
        var parcelById = snapshot.Parcels.ToDictionary(static item => item.Id);
        var buildingById = snapshot.Buildings.ToDictionary(static item => item.Id);
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    """        foreach (var settlement in snapshot.Settlements)
""",
    """        ValidateAcyclicParentGraph(snapshot.Toponyms.Select(static item => (item.Id, item.Provenance.ParentHumanToponymId)), \"Human toponym\", nameof(checkpoint));

        foreach (var settlement in snapshot.Settlements)
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    """            if (!settlementIds.Contains(parcel.SettlementId.Value) || !districtIds.Contains(parcel.DistrictId.Value))
                throw new ArgumentException(\"Parcel references invalid settlement or district state.\", nameof(checkpoint));
""",
    """            if (!settlementIds.Contains(parcel.SettlementId.Value) || !districtById.TryGetValue(parcel.DistrictId, out var district) || district.SettlementId != parcel.SettlementId)
                throw new ArgumentException(\"Parcel references an invalid or cross-Settlement district.\", nameof(checkpoint));
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    """            if (parcel.BuildingId is { } parcelBuildingId && !buildingIds.Contains(parcelBuildingId.Value))
                throw new ArgumentException(\"Parcel references a missing generated building.\", nameof(checkpoint));
""",
    """            if (parcel.BuildingId is { } parcelBuildingId
                && (!buildingById.TryGetValue(parcelBuildingId, out var building) || building.ParcelId != parcel.Id))
                throw new ArgumentException(\"Parcel and generated building references are not reciprocal.\", nameof(checkpoint));
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    """        foreach (var building in snapshot.Buildings)
        {
            ArgumentNullException.ThrowIfNull(building);
            if (!parcelIds.Contains(building.ParcelId.Value))
                throw new ArgumentException(\"Generated building references a missing parcel.\", nameof(checkpoint));
            if (!Enum.IsDefined(building.Use) || building.Floors <= 0 || building.Floors > 256 || building.Capacity < 0 || building.HistoricalStage < 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }
""",
    """        var occupiedParcels = new HashSet<ParcelId>();
        foreach (var building in snapshot.Buildings)
        {
            ArgumentNullException.ThrowIfNull(building);
            if (!parcelById.TryGetValue(building.ParcelId, out var parcel) || parcel.BuildingId != building.Id || !occupiedParcels.Add(building.ParcelId))
                throw new ArgumentException(\"Generated building ownership is missing, duplicated, or not reciprocal.\", nameof(checkpoint));
            if (!ContainsHorizontal(parcel.Bounds, building.Bounds))
                throw new ArgumentException(\"Generated building bounds must be contained by its Parcel.\", nameof(checkpoint));
            if (!Enum.IsDefined(building.Use) || building.Floors <= 0 || building.Floors > 256 || building.Capacity < 0 || building.HistoricalStage < 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    """            if (poi.BuildingId is { } poiBuildingId && !buildingIds.Contains(poiBuildingId.Value))
                throw new ArgumentException(\"Generated POI references a missing building.\", nameof(checkpoint));
""",
    """            if (poi.BuildingId is { } poiBuildingId)
            {
                if (!buildingById.TryGetValue(poiBuildingId, out var building) || !parcelById.TryGetValue(building.ParcelId, out var parcel) || parcel.SettlementId != poi.SettlementId)
                    throw new ArgumentException(\"Generated POI references a Building in a different Settlement hierarchy.\", nameof(checkpoint));
            }
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    """    private static HashSet<ulong> ValidateRegionalIds(IEnumerable<ulong> ids, string name)
""",
    """    private static bool ContainsHorizontal(WorldVolume outer, WorldVolume inner) =>
        inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX && inner.MinY >= outer.MinY && inner.MaxY <= outer.MaxY;

    private static HashSet<ulong> ValidateRegionalIds(IEnumerable<ulong> ids, string name)
""")

# #297/#298/#302/#303 C# Regional protocol semantic parity.
replace_once(
    "src/MachiVerseWorks.Protocol/RegionalGenerationProtocolCodec.cs",
    """        foreach (var item in message.Toponyms)
        {
            if (item.ParentHumanToponymId != 0UL && !toponymIds.Contains(item.ParentHumanToponymId)) return false;
        }
""",
    """        foreach (var item in message.Toponyms)
        {
            if (item.ParentHumanToponymId != 0UL && !toponymIds.Contains(item.ParentHumanToponymId)) return false;
        }
        if (!AcyclicParents(message.Toponyms.Select(static item => (item.ToponymId, item.ParentHumanToponymId)))) return false;
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RegionalGenerationProtocolCodec.cs",
    """        var parcelIds = new HashSet<ulong>();
        foreach (var item in message.Parcels)
        {
            if (item is null || item.ParcelId == 0UL || !parcelIds.Add(item.ParcelId) || !settlementIds.Contains(item.SettlementId)
                || !districtIds.Contains(item.DistrictId) || item.Zone > 6 || item.DevelopmentState > 3
""",
    """        var districtById = message.Districts.ToDictionary(static item => item.DistrictId);
        var parcelIds = new HashSet<ulong>();
        foreach (var item in message.Parcels)
        {
            if (item is null || item.ParcelId == 0UL || !parcelIds.Add(item.ParcelId) || !settlementIds.Contains(item.SettlementId)
                || !districtById.TryGetValue(item.DistrictId, out var district) || district.SettlementId != item.SettlementId || item.Zone > 6 || item.DevelopmentState > 3
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RegionalGenerationProtocolCodec.cs",
    """        foreach (var parcel in message.Parcels)
        {
            if (parcel.BuildingId != 0UL && !buildingIds.Contains(parcel.BuildingId)) return false;
        }
""",
    """        var parcelById = message.Parcels.ToDictionary(static item => item.ParcelId);
        var buildingById = message.Buildings.ToDictionary(static item => item.BuildingId);
        var occupiedParcels = new HashSet<ulong>();
        foreach (var building in message.Buildings)
        {
            if (!parcelById.TryGetValue(building.ParcelId, out var parcel) || parcel.BuildingId != building.BuildingId || !occupiedParcels.Add(building.ParcelId)
                || !ContainsHorizontal(parcel.MinX, parcel.MinY, parcel.MaxX, parcel.MaxY, building.MinX, building.MinY, building.MaxX, building.MaxY)) return false;
        }
        foreach (var parcel in message.Parcels)
        {
            if (parcel.BuildingId != 0UL && (!buildingById.TryGetValue(parcel.BuildingId, out var building) || building.ParcelId != parcel.ParcelId)) return false;
        }
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RegionalGenerationProtocolCodec.cs",
    """                || (item.BuildingId != 0UL && !buildingIds.Contains(item.BuildingId))
                || (item.NameId != 0UL && !toponymIds.Contains(item.NameId))) return false;
""",
    """                || (item.BuildingId != 0UL && (!buildingById.TryGetValue(item.BuildingId, out var building) || !parcelById.TryGetValue(building.ParcelId, out var parcel) || parcel.SettlementId != item.SettlementId))
                || (item.NameId != 0UL && !toponymIds.Contains(item.NameId))) return false;
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RegionalGenerationProtocolCodec.cs",
    """            if (item is null || item.RoadSignId == 0UL || !signIds.Add(item.RoadSignId) || item.Kind > 8
""",
    """            if (item is null || item.RoadSignId == 0UL || !signIds.Add(item.RoadSignId) || item.Kind > 9
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RegionalGenerationProtocolCodec.cs",
    """    private static bool ValidSuitability(ProtocolSettlementSuitability value) =>
""",
    """    private static bool ContainsHorizontal(double outerMinX, double outerMinY, double outerMaxX, double outerMaxY, double innerMinX, double innerMinY, double innerMaxX, double innerMaxY) =>
        innerMinX >= outerMinX && innerMaxX <= outerMaxX && innerMinY >= outerMinY && innerMaxY <= outerMaxY;

    private static bool AcyclicParents(IEnumerable<(ulong Id, ulong ParentId)> nodes)
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        foreach (var start in parents.Keys)
        {
            var seen = new HashSet<ulong>();
            var current = start;
            while (parents.TryGetValue(current, out var parent) && parent != 0UL)
            {
                if (!seen.Add(current)) return false;
                current = parent;
            }
        }
        return true;
    }

    private static bool ValidSuitability(ProtocolSettlementSuitability value) =>
""")

# #303 World Environment C# Protocol DAG.
replace_once(
    "src/MachiVerseWorks.Protocol/WorldEnvironmentProtocolCodec.cs",
    """        foreach (var feature in message.Features) if (feature.ParentFeatureId != 0 && !featureIds.Contains(feature.ParentFeatureId)) return false;
""",
    """        foreach (var feature in message.Features) if (feature.ParentFeatureId != 0 && !featureIds.Contains(feature.ParentFeatureId)) return false;
        if (!AcyclicParents(message.Features.Select(static item => (item.FeatureId, item.ParentFeatureId)))) return false;
""")
replace_once(
    "src/MachiVerseWorks.Protocol/WorldEnvironmentProtocolCodec.cs",
    """        foreach (var toponym in message.Toponyms) if (toponym.ParentToponymId != 0 && !toponymIds.Contains(toponym.ParentToponymId)) return false;
        return true;
    }

    private static bool Finite(double value) => double.IsFinite(value);
""",
    """        foreach (var toponym in message.Toponyms) if (toponym.ParentToponymId != 0 && !toponymIds.Contains(toponym.ParentToponymId)) return false;
        if (!AcyclicParents(message.Toponyms.Select(static item => (item.ToponymId, item.ParentToponymId)))) return false;
        return true;
    }

    private static bool AcyclicParents(IEnumerable<(ulong Id, ulong ParentId)> nodes)
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        foreach (var start in parents.Keys)
        {
            var seen = new HashSet<ulong>();
            var current = start;
            while (parents.TryGetValue(current, out var parent) && parent != 0UL)
            {
                if (!seen.Add(current)) return false;
                current = parent;
            }
        }
        return true;
    }

    private static bool Finite(double value) => double.IsFinite(value);
""")

# #303 Web World Environment DAG validation.
replace_once(
    "src/web/src/world-environment-protocol.ts",
    """  for (const feature of features) if (feature.parentFeatureId !== 0n && !featureIds.has(feature.parentFeatureId)) throw new ProtocolDecodeFailure('WorldEnvironment GeographicFeature parent reference is invalid.');

  const toponyms = Object.freeze(raw.toponyms.map((toponym) => normalizeToponym(toponym)));
""",
    """  for (const feature of features) if (feature.parentFeatureId !== 0n && !featureIds.has(feature.parentFeatureId)) throw new ProtocolDecodeFailure('WorldEnvironment GeographicFeature parent reference is invalid.');
  assertAcyclicParents(features.map((feature) => [feature.featureId, feature.parentFeatureId] as const), 'WorldEnvironment GeographicFeature');

  const toponyms = Object.freeze(raw.toponyms.map((toponym) => normalizeToponym(toponym)));
""")
replace_once(
    "src/web/src/world-environment-protocol.ts",
    """    if (toponym.parentToponymId !== 0n && !toponymIds.has(toponym.parentToponymId)) throw new ProtocolDecodeFailure('WorldEnvironment Toponym parent reference is invalid.');
  }

  return Object.freeze({
""",
    """    if (toponym.parentToponymId !== 0n && !toponymIds.has(toponym.parentToponymId)) throw new ProtocolDecodeFailure('WorldEnvironment Toponym parent reference is invalid.');
  }
  assertAcyclicParents(toponyms.map((toponym) => [toponym.toponymId, toponym.parentToponymId] as const), 'WorldEnvironment Toponym');

  return Object.freeze({
""")
append_before(
    "src/web/src/world-environment-protocol.ts",
    "function validateVolume(",
    """function assertAcyclicParents(nodes: readonly (readonly [bigint, bigint])[], name: string): void {
  const parents = new Map(nodes);
  for (const start of parents.keys()) {
    const seen = new Set<bigint>(); let current = start;
    while (true) {
      const parent = parents.get(current); if (parent === undefined || parent === 0n) break;
      if (seen.has(current)) throw new ProtocolDecodeFailure(`${name} parent graph contains a cycle.`);
      seen.add(current); current = parent;
    }
  }
}

""")

# #297/#302/#303 Web Regional ownership and DAG parity.
replace_once(
    "src/web/src/regional-generation-protocol.ts",
    """  for (const item of toponyms) if (item.parentHumanToponymId !== 0n && !toponymIds.has(item.parentHumanToponymId)) throw new ProtocolDecodeFailure('RegionalGeneration Toponym parent reference is invalid.');

  const settlements = Object.freeze(raw.settlements.map(normalizeSettlement));
""",
    """  for (const item of toponyms) if (item.parentHumanToponymId !== 0n && !toponymIds.has(item.parentHumanToponymId)) throw new ProtocolDecodeFailure('RegionalGeneration Toponym parent reference is invalid.');
  assertAcyclicParents(toponyms.map((item) => [item.toponymId, item.parentHumanToponymId] as const), 'RegionalGeneration Toponym');

  const settlements = Object.freeze(raw.settlements.map(normalizeSettlement));
""")
replace_once(
    "src/web/src/regional-generation-protocol.ts",
    """  const parcels = Object.freeze(raw.parcels.map(normalizeParcel));
  const parcelIds = uniquePositiveIds(parcels.map((item) => item.parcelId), 'Parcel');
  for (const item of parcels) if (!settlementIds.has(item.settlementId) || !districtIds.has(item.districtId)) throw new ProtocolDecodeFailure('RegionalGeneration Parcel stable reference is invalid.');

  const buildings = Object.freeze(raw.buildings.map(normalizeBuilding));
  const buildingIds = uniquePositiveIds(buildings.map((item) => item.buildingId), 'Building');
""",
    """  const districtById = new Map(districts.map((item) => [item.districtId, item] as const));
  const parcels = Object.freeze(raw.parcels.map(normalizeParcel));
  const parcelIds = uniquePositiveIds(parcels.map((item) => item.parcelId), 'Parcel');
  for (const item of parcels) {
    const district = districtById.get(item.districtId);
    if (!settlementIds.has(item.settlementId) || district === undefined || district.settlementId !== item.settlementId) throw new ProtocolDecodeFailure('RegionalGeneration Parcel hierarchy is invalid.');
  }

  const buildings = Object.freeze(raw.buildings.map(normalizeBuilding));
  const buildingIds = uniquePositiveIds(buildings.map((item) => item.buildingId), 'Building');
  const parcelById = new Map(parcels.map((item) => [item.parcelId, item] as const));
  const buildingById = new Map(buildings.map((item) => [item.buildingId, item] as const));
  const occupiedParcels = new Set<bigint>();
  for (const building of buildings) {
    const parcel = parcelById.get(building.parcelId);
    if (parcel === undefined || parcel.buildingId !== building.buildingId || occupiedParcels.has(building.parcelId) || !containsHorizontal(parcel, building)) throw new ProtocolDecodeFailure('RegionalGeneration Building ownership or containment is invalid.');
    occupiedParcels.add(building.parcelId);
  }
  for (const parcel of parcels) if (parcel.buildingId !== 0n && buildingById.get(parcel.buildingId)?.parcelId !== parcel.parcelId) throw new ProtocolDecodeFailure('RegionalGeneration Parcel/Building reciprocal reference is invalid.');
""")
replace_once(
    "src/web/src/regional-generation-protocol.ts",
    """    if (item.buildingId !== 0n && !buildingIds.has(item.buildingId)) throw new ProtocolDecodeFailure('RegionalGeneration POI building reference is invalid.');
""",
    """    if (item.buildingId !== 0n) {
      const building = buildingById.get(item.buildingId); const parcel = building === undefined ? undefined : parcelById.get(building.parcelId);
      if (building === undefined || parcel === undefined || parcel.settlementId !== item.settlementId) throw new ProtocolDecodeFailure('RegionalGeneration POI building hierarchy is invalid.');
    }
""")
append_before(
    "src/web/src/regional-generation-protocol.ts",
    "function normalizeSettlement(",
    """function assertAcyclicParents(nodes: readonly (readonly [bigint, bigint])[], name: string): void {
  const parents = new Map(nodes);
  for (const start of parents.keys()) {
    const seen = new Set<bigint>(); let current = start;
    while (true) {
      const parent = parents.get(current); if (parent === undefined || parent === 0n) break;
      if (seen.has(current)) throw new ProtocolDecodeFailure(`${name} parent graph contains a cycle.`);
      seen.add(current); current = parent;
    }
  }
}

function containsHorizontal(outer: { readonly minX:number; readonly minY:number; readonly maxX:number; readonly maxY:number }, inner: { readonly minX:number; readonly minY:number; readonly maxX:number; readonly maxY:number }): boolean {
  return inner.minX >= outer.minX && inner.maxX <= outer.maxX && inner.minY >= outer.minY && inner.maxY <= outer.maxY;
}

""")

# Regression tests: #284/#288.
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/RailwayOperationsTests.cs",
    """    [TestMethod]
    public void CheckpointRestoreContinuesWithIdenticalOperationState()
""",
    """    [TestMethod]
    public void CheckpointRejectsForgedRouteLengthAndServiceTrainCompletionMismatch()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        var checkpoint = world.CreateCheckpoint();
        var routes = checkpoint.RailwayRoutes!.ToArray();
        routes[0] = routes[0] with { LengthMeters = routes[0].LengthMeters * 10d };
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { RailwayRoutes = routes }));

        for (var tick = 0; tick < 3000 && world.CreateRailwayOperationsSnapshot().Services.Any(static service => service.State != RailwayServiceState.Completed); tick++) world.Step();
        checkpoint = world.CreateCheckpoint();
        var services = checkpoint.RailwayServices!.ToArray();
        services[0] = services[0] with { State = RailwayServiceState.Active };
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { RailwayServices = services }));
    }

    [TestMethod]
    public void CheckpointRestoreContinuesWithIdenticalOperationState()
""")

# Regression test: #287.
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/MultimodalTransitTests.cs",
    """    [TestMethod]
    public void DeterministicFixtureContainsWalkRailwayWalkBusAndTaxi()
""",
    """    [TestMethod]
    public void CheckpointRejectsPassengerWhoseTripRequestDiffersFromJourney()
    {
        var world = CreateRoadWorld();
        var journey = world.CreateJourney(new TripRequestId(100), 0, [new JourneyLegSnapshot(TransitMode.Walk, null, null, null, null, null, null, 10)]);
        world.CreatePassenger(new TripRequestId(100), journey);
        var checkpoint = world.CreateCheckpoint();
        var transit = checkpoint.MultimodalTransit!;
        var passengers = transit.Passengers.Select(item => item with { TripRequestId = new TripRequestId(101) }).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { Passengers = passengers } }));
    }

    [TestMethod]
    public void DeterministicFixtureContainsWalkRailwayWalkBusAndTaxi()
""")

# Regression tests: #291.
replace_once(
    "tests/MachiVerseWorks.Protocol.Tests/RailwayOperationsProtocolTests.cs",
    """    [TestMethod]
    public void Protocol26CannotSerializeRailwayOperations()
""",
    """    [TestMethod]
    public void RailwayOperationsRejectDuplicateAndBrokenSnapshotReferences()
    {
        var timetable = new ProtocolTimetable(5, [new ProtocolTimetableStop(11, 80, 100, 10, 0)]);
        var service = new ProtocolRailwayServiceState(3, 2, 4, 5, 6, 7, 1, 1, 0, 0, 1);
        var train = new ProtocolTrainState(1, 2, 3, 4, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 6, 0);
        var valid = new RailwayOperationsSnapshotMessage(1, [train], [service], [timetable]);
        _ = RailwayOperationsProtocolCodec.Serialize(valid, ProtocolVersion.Current);
        Assert.ThrowsExactly<InvalidDataException>(() => RailwayOperationsProtocolCodec.Serialize(valid with { Trains = [train, train] }, ProtocolVersion.Current));
        Assert.ThrowsExactly<InvalidDataException>(() => RailwayOperationsProtocolCodec.Serialize(valid with { Services = [service with { TimetableId = 999 }] }, ProtocolVersion.Current));
    }

    [TestMethod]
    public void Protocol26CannotSerializeRailwayOperations()
""")

# Regression tests: #295.
replace_once(
    "tests/MachiVerseWorks.Protocol.Tests/RadioProtocolTests.cs",
    """    [TestMethod]
    public void Protocol215RejectsRadioSnapshots()
""",
    """    [TestMethod]
    public void RadioAndSpectrumRejectDuplicateStableIdsAndDanglingReferences()
    {
        var radio = new RadioSnapshotMessage(
            new ProtocolRadioStatistics(1, 0, 0, 0, 0, 0, 0, 0, 0, 0d, 1),
            [new ProtocolRadioSite(1, ProtocolRadioSiteKind.Macro, 0, 0, 0, 0, 1, true)],
            [new ProtocolRadioAntenna(1, 1, 0, 0, 0, 1, 0, 0, 1, ProtocolRadioAntennaPatternKind.Omnidirectional, 360, 0, true)],
            [],
            [new ProtocolRadioReceiver(1, 1, 1, 100, 200, -100, true, true), new ProtocolRadioReceiver(1, 1, 1, 100, 200, -100, true, true)],
            [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RadioProtocolCodec.Serialize(radio, ProtocolVersion.Current));

        var spectrum = new SpectrumSnapshotMessage(1, [new ProtocolSpectrumBand(1, \"band\", 100, 200)], [new ProtocolFrequencyBlock(1, 999, 150, 10)], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RadioProtocolCodec.Serialize(spectrum, ProtocolVersion.Current));
    }

    [TestMethod]
    public void Protocol215RejectsRadioSnapshots()
""")

# Regression tests: #297/#298/#302/#303.
replace_once(
    "tests/MachiVerseWorks.Protocol.Tests/RegionalGenerationProtocolCodecTests.cs",
    """    [TestMethod]
    public void CurrentProtocolAdvertisesRegionalGenerationSupport()
""",
    """    [TestMethod]
    public void RegionalSnapshotAcceptsRockSlopeAndRejectsOwnershipHierarchyAndParentCycles()
    {
        var version = new ProtocolVersion(2, 18);
        var message = CreateMessage();
        _ = RegionalGenerationProtocolCodec.Serialize(message with { RoadSigns = [message.RoadSigns[0] with { Kind = 9 }] }, version);
        AssertThrows<ArgumentOutOfRangeException>(() => RegionalGenerationProtocolCodec.Serialize(message with { Parcels = [message.Parcels[0] with { BuildingId = 0 }] }, version));
        AssertThrows<ArgumentOutOfRangeException>(() => RegionalGenerationProtocolCodec.Serialize(message with { Parcels = [message.Parcels[0] with { SettlementId = 2 }] }, version));
        AssertThrows<ArgumentOutOfRangeException>(() => RegionalGenerationProtocolCodec.Serialize(message with { Buildings = [message.Buildings[0] with { MinX = -50d }] }, version));
        var cycle = message.Toponyms.Select(item => item.ToponymId switch { 100 => item with { ParentHumanToponymId = 103 }, 103 => item with { ParentHumanToponymId = 100 }, _ => item }).ToArray();
        AssertThrows<ArgumentOutOfRangeException>(() => RegionalGenerationProtocolCodec.Serialize(message with { Toponyms = cycle }, version));
    }

    [TestMethod]
    public void CurrentProtocolAdvertisesRegionalGenerationSupport()
""")

# Regression tests: #292 nested preflight.
replace_once(
    "tests/MachiVerseWorks.Persistence.Tests/NestedSaveLimitTests.cs",
    """    [TestMethod]
    public void SerializeAppliesVehicleAndPersonNestedLimitsBeforeDtoProjection()
""",
    """    [TestMethod]
    public void MultimodalPatternStopsAndJourneyLegsAreRejectedBeforeMaterialization()
    {
        var limits = new WorldSaveLimits(maximumBytes: 100_000, maximumLaneConnectionCount: 1);
        AssertNestedBoundary(
            CreateSimulationJson(\"\\\"multimodalTransit\\\":{\\\"patterns\\\":[{\\\"stops\\\":[{}]}]}\"),
            CreateSimulationJson(\"\\\"multimodalTransit\\\":{\\\"patterns\\\":[{\\\"stops\\\":[{},{}]}]}\"), limits, \"simulation.multimodalTransit.patterns[].stops\");
        AssertNestedBoundary(
            CreateSimulationJson(\"\\\"multimodalTransit\\\":{\\\"journeys\\\":[{\\\"legs\\\":[{}]}]}\"),
            CreateSimulationJson(\"\\\"multimodalTransit\\\":{\\\"journeys\\\":[{\\\"legs\\\":[{},{}]}]}\"), limits, \"simulation.multimodalTransit.journeys[].legs\");
    }

    [TestMethod]
    public void SerializeAppliesVehicleAndPersonNestedLimitsBeforeDtoProjection()
""")

print("Batch 3 patches applied")
