from pathlib import Path


def replace_once(path_name: str, old: str, new: str) -> None:
    path = Path(path_name)
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path_name}: expected exactly one patch target, found {count}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


# #268: keep the Protocol README aligned with the authoritative current contract.
Path("src/MachiVerseWorks.Protocol/README.md").write_text("""# MachiVerseWorks.Protocol

ServerとWeb Clientのbinary wire contractを管理します。Application `VERSION`やSave formatとは独立してversioningします。

## Current contract

現在のProtocolは **2.20** です。

- 2.0: 16-byte little-endian frame header、1 MiB payload上限、3D `SubscribeVolume`、Agent
- 2.1: `RoadNetworkSnapshot`
- 2.2: Pedestrian spawn / update / remove
- 2.3: Vehicle spawn / update / remove
- 2.4: `IntersectionControlSnapshot`
- 2.5: `InspectPerson` / `PopulationStatistics` / `PersonDebug`
- 2.6: `RailwayInfrastructureSnapshot` (700)
- 2.7: `RailwayOperationsSnapshot` (710)
- 2.8: `MultimodalTransitSnapshot` (720)
- 2.9: `ClearPersonInspection`を追加し、Person inspectorの明示clear lifecycleを定義
- 2.10: Economy observation
- 2.11: Logistics observation
- 2.12: Power observation
- 2.13: Water / Sewer observation
- 2.14: Gas observation
- 2.15: Optical observation
- 2.16: Radio / Spectrum observation
- 2.17: World Environment observation
- 2.18: Regional Generation observation
- 2.19: Persistent Regional Evolution observation
- 2.20: Entity Inspection request / response

同一majorではClientがServer current以下のminorを要求できます。negotiated minorより新しいmessageは送信しません。Protocol 1.x / `SubscribeArea` / 2D wire contractは現行経路にありません。

`SubscribeVolume` / `InspectPerson` / `ClearPersonInspection` / Entity Inspection requestはClient→Serverのread-only Observation Requestです。Worldを変更するcommandはObservation Protocolへ追加せず、ServerのAdministration / Management command boundaryへ分離します。

## Domain codecs

core frame / Agent / Road / Pedestrianは`ProtocolCodec`、domain固有の可変layoutは専用codecへ分離します。

- `IntersectionControlProtocolCodec`
- `PopulationProtocolCodec`
- `RailwayInfrastructureProtocolCodec` + `RailwayInfrastructureProtocolChunker`
- `RailwayOperationsProtocolCodec`
- `MultimodalTransitProtocolCodec`
- `EconomyProtocolCodec`
- `LogisticsProtocolCodec`
- `PowerProtocolCodec`
- `WaterSewerProtocolCodec`
- `GasProtocolCodec`
- `OpticalProtocolCodec`
- `RadioProtocolCodec`
- `WorldEnvironmentProtocolCodec`
- `RegionalGenerationProtocolCodec`
- `PersistentRegionalEvolutionProtocolCodec`
- `EntityInspectionProtocol`

Railway Infrastructureは1 MiBを超えるsnapshotをentity境界で複数frameへ分割できます。world-wide single-frame contractは送信前にpayload長をpreflightし、上限超過時はstructured Errorへ変換します。

codecはstable ID、enum、finite値、payload length、collection構造などwire境界で検証します。Simulationのmutable storeやWeb UI表示文言はProtocolへ持ち込みません。

Current contractのversion表記は`ProtocolVersion.Current`と同期させ、Protocolのversion変更時には両方を更新します。この同期はProtocol testでも検証します。

binary layout、message ID、chunk semantics、互換性ルールの正本は[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)を参照してください。
""", encoding="utf-8")

# #269: enforce the same bounded ProtocolError contract in the Web decoder as C#.
replace_once(
    "src/web/src/protocol.ts",
    "const PEDESTRIAN_STATE_LENGTH = 81;\n",
    "const PEDESTRIAN_STATE_LENGTH = 81;\nconst MAXIMUM_ERROR_PARAMETERS = 16;\nconst MAXIMUM_ERROR_PARAMETER_KEY_BYTES = 64;\nconst MAXIMUM_ERROR_PARAMETER_VALUE_BYTES = 256;\n")
replace_once(
    "src/web/src/protocol.ts",
    """function decodeProtocolError(view: DataView, offset: number, payloadLength: number): ProtocolErrorMessage {
  if (payloadLength < 4) throw new ProtocolDecodeFailure('Error payload is too short.');
  const end = offset + payloadLength; const code = view.getUint16(offset, true) as ProtocolErrorCode; const parameterCount = view.getUint16(offset + 2, true); let cursor = offset + 4; const parameters: ProtocolErrorParameter[] = [];
  for (let index = 0; index < parameterCount; index += 1) {
    const key = readUtf8String(view, cursor, end); cursor = key.nextOffset; const value = readUtf8String(view, cursor, end); cursor = value.nextOffset; parameters.push({ key: key.value, value: value.value });
  }
  if (cursor !== end) throw new ProtocolDecodeFailure('Error payload contains trailing bytes.');
  return { type: MessageType.Error, code, parameters };
}

function readUtf8String(view: DataView, offset: number, end: number): { readonly value: string; readonly nextOffset: number } {
  if (offset + 2 > end) throw new ProtocolDecodeFailure('Error string length is truncated.'); const length = view.getUint16(offset, true); const start = offset + 2; const nextOffset = start + length;
  if (nextOffset > end) throw new ProtocolDecodeFailure('Error string payload is truncated.');
  try { return { value: utf8Decoder.decode(new Uint8Array(view.buffer, view.byteOffset + start, length)), nextOffset }; } catch { throw new ProtocolDecodeFailure('Error payload contains invalid UTF-8.'); }
}
""",
    """function decodeProtocolError(view: DataView, offset: number, payloadLength: number): ProtocolErrorMessage {
  if (payloadLength < 4) throw new ProtocolDecodeFailure('Error payload is too short.');
  const end = offset + payloadLength; const code = view.getUint16(offset, true) as ProtocolErrorCode; const parameterCount = view.getUint16(offset + 2, true); let cursor = offset + 4; const parameters: ProtocolErrorParameter[] = [];
  if (parameterCount > MAXIMUM_ERROR_PARAMETERS) throw new ProtocolDecodeFailure('Error parameter count exceeds the supported limit.');
  for (let index = 0; index < parameterCount; index += 1) {
    const key = readUtf8String(view, cursor, end, MAXIMUM_ERROR_PARAMETER_KEY_BYTES, 'key'); cursor = key.nextOffset;
    const value = readUtf8String(view, cursor, end, MAXIMUM_ERROR_PARAMETER_VALUE_BYTES, 'value'); cursor = value.nextOffset;
    parameters.push({ key: key.value, value: value.value });
  }
  if (cursor !== end) throw new ProtocolDecodeFailure('Error payload contains trailing bytes.');
  return { type: MessageType.Error, code, parameters };
}

function readUtf8String(view: DataView, offset: number, end: number, maximumBytes: number, fieldName: string): { readonly value: string; readonly nextOffset: number } {
  if (offset + 2 > end) throw new ProtocolDecodeFailure('Error string length is truncated.');
  const length = view.getUint16(offset, true); const start = offset + 2; const nextOffset = start + length;
  if (length > maximumBytes) throw new ProtocolDecodeFailure(`Error parameter ${fieldName} exceeds the supported UTF-8 byte limit.`);
  if (nextOffset > end) throw new ProtocolDecodeFailure('Error string payload is truncated.');
  try { return { value: utf8Decoder.decode(new Uint8Array(view.buffer, view.byteOffset + start, length)), nextOffset }; } catch { throw new ProtocolDecodeFailure('Error payload contains invalid UTF-8.'); }
}
""")

# #276: apply Economy core collection limits before JSON DTO materialization.
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """        else if (context == NestedSaveContext.Economy)
        {
            if (reader.ValueTextEquals(\"logistics\")) return NestedSaveProperty.Logistics;
""",
    """        else if (context == NestedSaveContext.Economy)
        {
            if (reader.ValueTextEquals(\"companies\")) return NestedSaveProperty.Companies;
            if (reader.ValueTextEquals(\"establishments\")) return NestedSaveProperty.Establishments;
            if (reader.ValueTextEquals(\"jobs\")) return NestedSaveProperty.Jobs;
            if (reader.ValueTextEquals(\"employments\")) return NestedSaveProperty.Employments;
            if (reader.ValueTextEquals(\"households\")) return NestedSaveProperty.EconomyHouseholds;
            if (reader.ValueTextEquals(\"logistics\")) return NestedSaveProperty.Logistics;
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """            (NestedSaveContext.Timetable, NestedSaveProperty.Stops) => new(limits.MaximumTimetableStopCount, \"simulation.railwayOperations.timetables[].stops\", NestedArrayKind.TimetableStops),
            (NestedSaveContext.Logistics, NestedSaveProperty.Commodities) => new(limits.MaximumBuildingCount, \"simulation.economy.logistics.commodities\", NestedArrayKind.None),
""",
    """            (NestedSaveContext.Timetable, NestedSaveProperty.Stops) => new(limits.MaximumTimetableStopCount, \"simulation.railwayOperations.timetables[].stops\", NestedArrayKind.TimetableStops),
            (NestedSaveContext.Economy, NestedSaveProperty.Companies) => new(limits.MaximumBuildingCount, \"simulation.economy.companies\", NestedArrayKind.None),
            (NestedSaveContext.Economy, NestedSaveProperty.Establishments) => new(limits.MaximumBuildingCount, \"simulation.economy.establishments\", NestedArrayKind.None),
            (NestedSaveContext.Economy, NestedSaveProperty.Jobs) => new(limits.MaximumPersonCount, \"simulation.economy.jobs\", NestedArrayKind.None),
            (NestedSaveContext.Economy, NestedSaveProperty.Employments) => new(limits.MaximumPersonCount, \"simulation.economy.employments\", NestedArrayKind.None),
            (NestedSaveContext.Economy, NestedSaveProperty.EconomyHouseholds) => new(limits.MaximumHouseholdCount, \"simulation.economy.households\", NestedArrayKind.None),
            (NestedSaveContext.Logistics, NestedSaveProperty.Commodities) => new(limits.MaximumBuildingCount, \"simulation.economy.logistics.commodities\", NestedArrayKind.None),
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """        Economy, Logistics, Commodities, Inventories, Orders, Shipments, Power, PowerNodes, PowerLines, Generators, PowerLoads,
""",
    """        Economy, Companies, Establishments, Jobs, Employments, EconomyHouseholds, Logistics, Commodities, Inventories, Orders, Shipments, Power, PowerNodes, PowerLines, Generators, PowerLoads,
""")

# #282: live mutation must obey the same Railway/Bus pattern invariant as restore.
replace_once(
    "src/MachiVerseWorks.Simulation/Internal/MultimodalTransitStore.cs",
    """        if (!lines.TryGetValue(lineId, out var line)) throw new ArgumentException($\"Transit line {lineId.Value} does not exist.\", nameof(lineId));
        if (railwayServiceId is not null && line.Mode != TransitMode.Railway) throw new ArgumentException(\"Railway Service can only be linked to a Railway line.\", nameof(railwayServiceId));
""",
    """        if (!lines.TryGetValue(lineId, out var line)) throw new ArgumentException($\"Transit line {lineId.Value} does not exist.\", nameof(lineId));
        if (line.Mode == TransitMode.Railway && railwayServiceId is null) throw new ArgumentException(\"Railway transit patterns require a Railway Service.\", nameof(railwayServiceId));
        if (line.Mode != TransitMode.Railway && railwayServiceId is not null) throw new ArgumentException(\"Railway Service can only be linked to a Railway line.\", nameof(railwayServiceId));
""")

# #279/#280/#281: validate Multimodal cross references and state machines as one restore boundary.
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.MultimodalTransit.cs",
    """        var roadVehicleIds = (checkpoint.Vehicles ?? []).Select(static item => item.Id).ToHashSet();
        var transitVehicleIds = transit.Vehicles.Select(static item => item.Id).ToHashSet();
        var tripIds = transit.Trips.Select(static item => item.Id).ToHashSet();
        var journeyById = transit.Journeys.ToDictionary(static item => item.Id);
""",
    """        var roadVehicleIds = (checkpoint.Vehicles ?? []).Select(static item => item.Id).ToHashSet();
        var transitVehicleById = transit.Vehicles.ToDictionary(static item => item.Id);
        var tripById = transit.Trips.ToDictionary(static item => item.Id);
        var journeyById = transit.Journeys.ToDictionary(static item => item.Id);
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.MultimodalTransit.cs",
    """        foreach (var trip in transit.Trips)
            if (!patternById.ContainsKey(trip.PatternId)) throw new ArgumentException($\"Transit Trip {trip.Id.Value} references a missing Pattern.\", nameof(checkpoint));
        foreach (var vehicle in transit.Vehicles)
        {
            if (!Enum.IsDefined(vehicle.Kind) || !Enum.IsDefined(vehicle.State)) throw new ArgumentException($\"Transit Vehicle {vehicle.Id.Value} has invalid state.\", nameof(checkpoint));
            if (vehicle.Kind == TransitVehicleKind.Bus && (vehicle.TripId is not { } busTripId || !tripIds.Contains(busTripId))) throw new ArgumentException($\"Bus Vehicle {vehicle.Id.Value} references a missing Trip.\", nameof(checkpoint));
            if (vehicle.Kind == TransitVehicleKind.Taxi && vehicle.TripId is not null) throw new ArgumentException($\"Taxi Vehicle {vehicle.Id.Value} cannot reference a scheduled Transit Trip.\", nameof(checkpoint));
            if (vehicle.RoadVehicleId is { } roadVehicleId && !roadVehicleIds.Contains(roadVehicleId)) throw new ArgumentException($\"Transit Vehicle {vehicle.Id.Value} references a missing Road Vehicle.\", nameof(checkpoint));
        }
        foreach (var request in transit.TaxiRequests)
            if (request.AssignedVehicleId is { } vehicleId && !transitVehicleIds.Contains(vehicleId)) throw new ArgumentException($\"Taxi Request {request.Id.Value} references a missing Transit Vehicle.\", nameof(checkpoint));
""",
    """        foreach (var trip in transit.Trips)
        {
            if (!patternById.ContainsKey(trip.PatternId)) throw new ArgumentException($\"Transit Trip {trip.Id.Value} references a missing Pattern.\", nameof(checkpoint));
            if (trip.VehicleId is { } vehicleId
                && (!transitVehicleById.TryGetValue(vehicleId, out var vehicle)
                    || vehicle.Kind != TransitVehicleKind.Bus
                    || vehicle.TripId != trip.Id))
                throw new ArgumentException($\"Transit Trip {trip.Id.Value} has a mismatched Bus Vehicle reference.\", nameof(checkpoint));
        }
        foreach (var vehicle in transit.Vehicles)
        {
            if (!Enum.IsDefined(vehicle.Kind) || !Enum.IsDefined(vehicle.State)) throw new ArgumentException($\"Transit Vehicle {vehicle.Id.Value} has invalid state.\", nameof(checkpoint));
            if (vehicle.RoadVehicleId is { } roadVehicleId && !roadVehicleIds.Contains(roadVehicleId)) throw new ArgumentException($\"Transit Vehicle {vehicle.Id.Value} references a missing Road Vehicle.\", nameof(checkpoint));

            if (vehicle.Kind == TransitVehicleKind.Bus)
            {
                if (vehicle.TripId is not { } busTripId || !tripById.TryGetValue(busTripId, out var trip) || trip.VehicleId != vehicle.Id)
                    throw new ArgumentException($\"Bus Vehicle {vehicle.Id.Value} has a missing or mismatched Transit Trip.\", nameof(checkpoint));
                if (!patternById.TryGetValue(trip.PatternId, out var pattern) || !lineById.TryGetValue(pattern.LineId, out var line) || line.Mode != TransitMode.Bus)
                    throw new ArgumentException($\"Bus Vehicle {vehicle.Id.Value} does not reference a Bus pattern.\", nameof(checkpoint));
                if (vehicle.StopIndex < 0 || vehicle.StopIndex >= pattern.Stops.Count)
                    throw new ArgumentException($\"Bus Vehicle {vehicle.Id.Value} has an invalid StopIndex.\", nameof(checkpoint));
                if (vehicle.State is TransitVehicleMovementState.Idle or TransitVehicleMovementState.EnRouteToPickup or TransitVehicleMovementState.EnRouteToDropOff)
                    throw new ArgumentException($\"Bus Vehicle {vehicle.Id.Value} has a Taxi-only movement state.\", nameof(checkpoint));
                if (vehicle.State == TransitVehicleMovementState.EnRouteToStop)
                {
                    if (vehicle.RoadVehicleId is null || vehicle.StopIndex >= pattern.Stops.Count - 1)
                        throw new ArgumentException($\"Bus Vehicle {vehicle.Id.Value} cannot continue its en-route state.\", nameof(checkpoint));
                }
                else if (vehicle.RoadVehicleId is not null)
                {
                    throw new ArgumentException($\"Bus Vehicle {vehicle.Id.Value} has a Road Vehicle outside its en-route state.\", nameof(checkpoint));
                }
                if (vehicle.State == TransitVehicleMovementState.AwaitingDeparture && vehicle.StopIndex != 0)
                    throw new ArgumentException($\"Bus Vehicle {vehicle.Id.Value} must await departure at its first stop.\", nameof(checkpoint));
                if (vehicle.State == TransitVehicleMovementState.Completed && vehicle.StopIndex != pattern.Stops.Count - 1)
                    throw new ArgumentException($\"Completed Bus Vehicle {vehicle.Id.Value} must be at its final stop.\", nameof(checkpoint));
            }
            else
            {
                if (vehicle.TripId is not null) throw new ArgumentException($\"Taxi Vehicle {vehicle.Id.Value} cannot reference a scheduled Transit Trip.\", nameof(checkpoint));
                if (vehicle.State is not (TransitVehicleMovementState.Idle or TransitVehicleMovementState.EnRouteToPickup or TransitVehicleMovementState.EnRouteToDropOff))
                    throw new ArgumentException($\"Taxi Vehicle {vehicle.Id.Value} has a Bus-only movement state.\", nameof(checkpoint));
                if (vehicle.State == TransitVehicleMovementState.Idle && vehicle.RoadVehicleId is not null)
                    throw new ArgumentException($\"Idle Taxi Vehicle {vehicle.Id.Value} cannot retain a Road Vehicle.\", nameof(checkpoint));
                if (vehicle.State is (TransitVehicleMovementState.EnRouteToPickup or TransitVehicleMovementState.EnRouteToDropOff) && vehicle.RoadVehicleId is null)
                    throw new ArgumentException($\"En-route Taxi Vehicle {vehicle.Id.Value} requires a Road Vehicle.\", nameof(checkpoint));
            }
        }

        var activeTaxiVehicles = new HashSet<TransitVehicleId>();
        foreach (var request in transit.TaxiRequests)
        {
            if (!Enum.IsDefined(request.State)) throw new ArgumentException($\"Taxi Request {request.Id.Value} has an invalid state.\", nameof(checkpoint));
            var isActive = request.State is TaxiRequestState.Assigned or TaxiRequestState.PickingUp or TaxiRequestState.Riding;
            if (request.State == TaxiRequestState.Requested && request.AssignedVehicleId is not null)
                throw new ArgumentException($\"Requested Taxi Request {request.Id.Value} cannot already have an assigned Vehicle.\", nameof(checkpoint));
            if (isActive && request.AssignedVehicleId is null)
                throw new ArgumentException($\"Active Taxi Request {request.Id.Value} requires an assigned Vehicle.\", nameof(checkpoint));
            if (request.AssignedVehicleId is not { } vehicleId) continue;
            if (!transitVehicleById.TryGetValue(vehicleId, out var vehicle) || vehicle.Kind != TransitVehicleKind.Taxi)
                throw new ArgumentException($\"Taxi Request {request.Id.Value} references a missing or non-Taxi Vehicle.\", nameof(checkpoint));
            if (isActive && !activeTaxiVehicles.Add(vehicleId))
                throw new ArgumentException($\"Taxi Vehicle {vehicleId.Value} is assigned to multiple active Taxi Requests.\", nameof(checkpoint));
            if (request.State == TaxiRequestState.Riding && vehicle.State != TransitVehicleMovementState.EnRouteToDropOff)
                throw new ArgumentException($\"Riding Taxi Request {request.Id.Value} is not paired with a drop-off Vehicle state.\", nameof(checkpoint));
            if (request.State == TaxiRequestState.Assigned && vehicle.State is not (TransitVehicleMovementState.Idle or TransitVehicleMovementState.EnRouteToPickup))
                throw new ArgumentException($\"Assigned Taxi Request {request.Id.Value} is paired with an invalid Vehicle state.\", nameof(checkpoint));
        }
""")
# Preserve failure state when starting the drop-off leg fails.
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.MultimodalTransit.cs",
    """                    StartTaxiRoadLeg(vehicle, request, request.Pickup, request.DropOff, TransitVehicleMovementState.EnRouteToDropOff);
                    request.State = TaxiRequestState.Riding;
""",
    """                    StartTaxiRoadLeg(vehicle, request, request.Pickup, request.DropOff, TransitVehicleMovementState.EnRouteToDropOff);
                    if (vehicle.State == TransitVehicleMovementState.EnRouteToDropOff) request.State = TaxiRequestState.Riding;
""")

# #273: validate Optical route topology before accepting a solver result or checkpoint.
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.Optical.cs",
    """            foreach (var cableId in route.RouteCableIds)
            {
                if (!_fiberCableIndex.ContainsKey(cableId))
                    throw new InvalidOperationException($\"Optical routing solver returned unknown FiberCable {cableId.Value}.\");
            }
            if (route.BackhaulId is { } backhaulId && !_opticalBackhaulIndex.ContainsKey(backhaulId))
                throw new InvalidOperationException($\"Optical routing solver returned unknown Backhaul {backhaulId.Value}.\");

""",
    """            if (!IsValidOpticalRouteTopology(
                    demand.NodeId,
                    route.BackhaulId,
                    route.AllocatedGigabitsPerSecond,
                    route.RouteCableIds,
                    id => _opticalBackhaulIndex.TryGetValue(id, out var backhaul) ? backhaul.NodeId : null,
                    id => _fiberCableIndex.TryGetValue(id, out var cable)
                        ? new OpticalRouteCableValidation(cable.FromNodeId, cable.ToNodeId, cable.IsInService)
                        : null,
                    requireInService: true))
                throw new InvalidOperationException($\"Optical routing solver returned a disconnected or inconsistent route for Demand {demand.Id.Value}.\");

""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.Optical.cs",
    """    private OpticalQualityState CalculateOpticalQuality(OpticalDemandState demand)
""",
    """    private static bool IsValidOpticalRouteTopology(
        OpticalNodeId demandNodeId,
        OpticalBackhaulId? backhaulId,
        double allocatedGigabitsPerSecond,
        IReadOnlyList<FiberCableId>? routeCableIds,
        Func<OpticalBackhaulId, OpticalNodeId?> resolveBackhaulNode,
        Func<FiberCableId, OpticalRouteCableValidation?> resolveCable,
        bool requireInService)
    {
        if (routeCableIds is null) return false;
        if (allocatedGigabitsPerSecond <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
            return backhaulId is null && routeCableIds.Count == 0;
        if (backhaulId is not { } selectedBackhaul || resolveBackhaulNode(selectedBackhaul) is not { } cursor)
            return false;

        var seen = new HashSet<FiberCableId>();
        foreach (var cableId in routeCableIds)
        {
            if (!seen.Add(cableId) || resolveCable(cableId) is not { } cable || (requireInService && !cable.IsInService))
                return false;
            if (cable.FromNodeId == cursor) cursor = cable.ToNodeId;
            else if (cable.ToNodeId == cursor) cursor = cable.FromNodeId;
            else return false;
        }
        return cursor == demandNodeId;
    }

    private readonly record struct OpticalRouteCableValidation(
        OpticalNodeId FromNodeId,
        OpticalNodeId ToNodeId,
        bool IsInService);

    private OpticalQualityState CalculateOpticalQuality(OpticalDemandState demand)
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.Optical.Persistence.cs",
    """        var cableIds = ValidateOpticalCheckpointIds(
            optical.FiberCables.Select(static item => item.Id.Value), optical.NextFiberCableId, \"Fiber cable\");
""",
    """        _ = ValidateOpticalCheckpointIds(
            optical.FiberCables.Select(static item => item.Id.Value), optical.NextFiberCableId, \"Fiber cable\");
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.Optical.Persistence.cs",
    """        var buildings = checkpoint.Buildings.Select(static item => item.Id).ToHashSet();
        var establishments = checkpoint.Economy?.Establishments.Select(static item => item.Id).ToHashSet() ?? [];
""",
    """        var buildings = checkpoint.Buildings.Select(static item => item.Id).ToHashSet();
        var establishments = checkpoint.Economy?.Establishments.Select(static item => item.Id).ToHashSet() ?? [];
        var cableById = optical.FiberCables.ToDictionary(static item => item.Id);
        var backhaulById = optical.Backhauls.ToDictionary(static item => item.Id);
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.Optical.Persistence.cs",
    """            foreach (var cableId in item.RouteCableIds)
                if (!cableIds.Contains(cableId.Value))
                    throw new ArgumentException($\"Optical demand {item.Id.Value} route references a missing FiberCable.\", nameof(checkpoint));
""",
    """            if (!IsValidOpticalRouteTopology(
                    item.NodeId,
                    item.BackhaulId,
                    item.AllocatedGigabitsPerSecond,
                    item.RouteCableIds,
                    id => backhaulById.TryGetValue(id, out var backhaul) ? backhaul.NodeId : null,
                    id => cableById.TryGetValue(id, out var cable)
                        ? new OpticalRouteCableValidation(cable.FromNodeId, cable.ToNodeId, cable.IsInService)
                        : null,
                    requireInService: false))
                throw new ArgumentException($\"Optical demand {item.Id.Value} contains a disconnected or inconsistent route.\", nameof(checkpoint));
""")

# #283: share Railway Service definition invariants between live creation, validation, and restore.
replace_once(
    "src/MachiVerseWorks.Simulation/Internal/RailwayOperationsStore.cs",
    """        if (!_depots.TryGetValue(originDepotId, out var originDepot)) throw new ArgumentException(\"Origin depot does not exist.\", nameof(originDepotId));
        if (!_depots.TryGetValue(destinationDepotId, out var destinationDepot)) throw new ArgumentException(\"Destination depot does not exist.\", nameof(destinationDepotId));
        if (!ContainsTrack(originDepot.TrackSegmentIds, route.Steps[0].Segment.Id)) throw new ArgumentException(\"Route must begin on an origin depot track.\", nameof(routeId));
        if (!ContainsTrack(destinationDepot.TrackSegmentIds, route.Steps[^1].Segment.Id)) throw new ArgumentException(\"Route must end on a destination depot track.\", nameof(routeId));

        var stopRouteDistances = new double[timetable.Stops.Count];
        double previousDistance = -1d;
        for (var index = 0; index < timetable.Stops.Count; index++)
        {
            var stop = timetable.Stops[index];
            if (!TryFindStopDistance(route, stop, out var stopDistance)) throw new ArgumentException($\"Stop station {stop.StationId.Value} has no platform on the route.\", nameof(timetableId));
            if (stopDistance <= previousDistance) throw new ArgumentException(\"Timetable stops must appear in route order.\", nameof(timetableId));
            stopRouteDistances[index] = stopDistance;
            previousDistance = stopDistance;
        }
""",
    """        if (!_depots.TryGetValue(originDepotId, out var originDepot)) throw new ArgumentException(\"Origin depot does not exist.\", nameof(originDepotId));
        if (!_depots.TryGetValue(destinationDepotId, out var destinationDepot)) throw new ArgumentException(\"Destination depot does not exist.\", nameof(destinationDepotId));
        var stopRouteDistances = ValidateServiceDefinition(route, timetable, originDepot, destinationDepot, nameof(timetableId));
""")
replace_once(
    "src/MachiVerseWorks.Simulation/Internal/RailwayOperationsStore.cs",
    """    public TrainId CreateTrain(RailwayServiceId serviceId)
""",
    """    internal void ValidateServiceDefinition(
        RailwayRouteSnapshot routeSnapshot,
        TimetableSnapshot timetable,
        DepotId originDepotId,
        DepotId destinationDepotId)
    {
        if (!_depots.TryGetValue(originDepotId, out var originDepot)) throw new ArgumentException(\"Origin depot does not exist.\", nameof(originDepotId));
        if (!_depots.TryGetValue(destinationDepotId, out var destinationDepot)) throw new ArgumentException(\"Destination depot does not exist.\", nameof(destinationDepotId));
        var route = BuildRoute(routeSnapshot.Id, routeSnapshot.TrackSegmentIds);
        _ = ValidateServiceDefinition(route, timetable, originDepot, destinationDepot, nameof(routeSnapshot));
    }

    private double[] ValidateServiceDefinition(
        RouteState route,
        TimetableSnapshot timetable,
        DepotSnapshot originDepot,
        DepotSnapshot destinationDepot,
        string parameterName)
    {
        if (!ContainsTrack(originDepot.TrackSegmentIds, route.Steps[0].Segment.Id)) throw new ArgumentException(\"Route must begin on an origin depot track.\", parameterName);
        if (!ContainsTrack(destinationDepot.TrackSegmentIds, route.Steps[^1].Segment.Id)) throw new ArgumentException(\"Route must end on a destination depot track.\", parameterName);

        var stopRouteDistances = new double[timetable.Stops.Count];
        double previousDistance = -1d;
        for (var index = 0; index < timetable.Stops.Count; index++)
        {
            var stop = timetable.Stops[index];
            if (!_stationPlatforms.ContainsKey(stop.StationId) || !TryFindStopDistance(route, stop, out var stopDistance))
                throw new ArgumentException($\"Stop station {stop.StationId.Value} has no platform on the route.\", parameterName);
            if (stopDistance <= previousDistance) throw new ArgumentException(\"Timetable stops must appear in route order.\", parameterName);
            stopRouteDistances[index] = stopDistance;
            previousDistance = stopDistance;
        }
        return stopRouteDistances;
    }

    public TrainId CreateTrain(RailwayServiceId serviceId)
""")
replace_once(
    "src/MachiVerseWorks.Simulation/Internal/RailwayOperationsStore.cs",
    """        foreach (var service in services)
        {
            var timetable = _timetables[service.TimetableId];
            var route = _routes[service.RouteId];
            var distances = new double[timetable.Stops.Count];
            for (var index = 0; index < distances.Length; index++)
            {
                if (!TryFindStopDistance(route, timetable.Stops[index], out distances[index])) throw new InvalidOperationException(\"Saved timetable stop is not on its route.\");
            }
            _services.Add(service.Id, ServiceState.FromSnapshot(service, distances));
        }
""",
    """        foreach (var service in services)
        {
            var timetable = _timetables[service.TimetableId];
            var route = _routes[service.RouteId];
            if (!_depots.TryGetValue(service.OriginDepotId, out var originDepot) || !_depots.TryGetValue(service.DestinationDepotId, out var destinationDepot))
                throw new InvalidOperationException(\"Saved Railway Service references a missing Depot.\");
            var distances = ValidateServiceDefinition(route, timetable, originDepot, destinationDepot, nameof(services));
            _services.Add(service.Id, ServiceState.FromSnapshot(service, distances));
        }
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RailwayOperations.Validation.cs",
    """        var blockIds = (checkpoint.BlockSections ?? []).Select(static item => item.Id).ToHashSet();

        var formationById = new Dictionary<TrainFormationId, TrainFormationSnapshot>();
""",
    """        var blockIds = (checkpoint.BlockSections ?? []).Select(static item => item.Id).ToHashSet();
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
""")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RailwayOperations.Validation.cs",
    """            if (service.NextStopIndex < 0 || service.NextStopIndex > timetable.Stops.Count)
                throw new ArgumentException($\"Railway service {service.Id.Value} has an invalid next stop index.\", nameof(checkpoint));
""",
    """            if (service.NextStopIndex < 0 || service.NextStopIndex > timetable.Stops.Count)
                throw new ArgumentException($\"Railway service {service.Id.Value} has an invalid next stop index.\", nameof(checkpoint));
            serviceDefinitionValidator.ValidateServiceDefinition(routeById[service.RouteId], timetable, service.OriginDepotId, service.DestinationDepotId);
""")

# Regression tests for #268/#269.
Path("tests/MachiVerseWorks.Protocol.Tests/ProtocolDocumentationTests.cs").write_text("""using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class ProtocolDocumentationTests
{
    [TestMethod]
    public void ReadmeDeclaresAuthoritativeCurrentProtocolVersion()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, \"MachiVerseWorks.slnx\"))) directory = directory.Parent;
        Assert.IsNotNull(directory, \"Repository root could not be located from the test output directory.\");
        var readme = File.ReadAllText(Path.Combine(directory.FullName, \"src\", \"MachiVerseWorks.Protocol\", \"README.md\"));
        StringAssert.Contains(readme, $\"現在のProtocolは **{ProtocolVersion.Current}** です。\");
    }
}
""", encoding="utf-8")

protocol_test = Path("src/web/tests/protocol.test.mjs")
protocol_text = protocol_test.read_text(encoding="utf-8")
protocol_text += r'''

function createProtocolErrorFrame(parameters) {
  const encoder = new TextEncoder();
  const encoded = parameters.map(({ key, value }) => ({ key: encoder.encode(key), value: encoder.encode(value) }));
  const payloadLength = 4 + encoded.reduce((total, item) => total + 4 + item.key.byteLength + item.value.byteLength, 0);
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength); const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 20, true); view.setUint16(8, MessageType.Error, true); view.setUint32(12, payloadLength, true);
  let offset = PROTOCOL_HEADER_SIZE; view.setUint16(offset, 4, true); view.setUint16(offset + 2, encoded.length, true); offset += 4;
  for (const item of encoded) {
    view.setUint16(offset, item.key.byteLength, true); offset += 2; new Uint8Array(frame, offset, item.key.byteLength).set(item.key); offset += item.key.byteLength;
    view.setUint16(offset, item.value.byteLength, true); offset += 2; new Uint8Array(frame, offset, item.value.byteLength).set(item.value); offset += item.value.byteLength;
  }
  return frame;
}

test('Protocol Error decoder enforces C# parameter count and UTF-8 byte limits', () => {
  const boundary = decodeFrame(createProtocolErrorFrame([{ key: 'k'.repeat(64), value: 'v'.repeat(256) }])).message;
  assert.equal(boundary.type, MessageType.Error); assert.equal(boundary.parameters.length, 1);
  assert.throws(() => decodeFrame(createProtocolErrorFrame(Array.from({ length: 17 }, (_, index) => ({ key: `k${String(index)}`, value: 'v' })))), /parameter count/i);
  assert.throws(() => decodeFrame(createProtocolErrorFrame([{ key: 'k'.repeat(65), value: 'v' }])), /key.*byte limit/i);
  assert.throws(() => decodeFrame(createProtocolErrorFrame([{ key: 'k', value: 'v'.repeat(257) }])), /value.*byte limit/i);
  assert.throws(() => decodeFrame(createProtocolErrorFrame([{ key: 'あ'.repeat(22), value: 'v' }])), /key.*byte limit/i);
});
'''
protocol_test.write_text(protocol_text, encoding="utf-8")

# Regression tests for #272/#276.
replace_once(
    "tests/MachiVerseWorks.Persistence.Tests/NestedSaveLimitTests.cs",
    """    [TestMethod]
    public void SerializeAppliesVehicleAndPersonNestedLimitsBeforeDtoProjection()
""",
    """    [TestMethod]
    public void OpticalCollectionsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        AssertNestedBoundary(
            CreateSimulationJson(\"\\\"economy\\\":{\\\"optical\\\":{\\\"nodes\\\":[{}]}}\"),
            CreateSimulationJson(\"\\\"economy\\\":{\\\"optical\\\":{\\\"nodes\\\":[{},{}]}}\"),
            new WorldSaveLimits(maximumBytes: 100_000, maximumRoadNodeCount: 1),
            \"simulation.economy.optical.nodes\");
        AssertNestedBoundary(
            CreateSimulationJson(\"\\\"economy\\\":{\\\"optical\\\":{\\\"fiberCables\\\":[{}]}}\"),
            CreateSimulationJson(\"\\\"economy\\\":{\\\"optical\\\":{\\\"fiberCables\\\":[{},{}]}}\"),
            new WorldSaveLimits(maximumBytes: 100_000, maximumRoadSegmentCount: 1),
            \"simulation.economy.optical.fiberCables\");
        foreach (var property in new[] { \"equipment\", \"backhauls\", \"demands\" })
        {
            AssertNestedBoundary(
                CreateSimulationJson($\"\\\"economy\\\":{{\\\"optical\\\":{{\\\"{property}\\\":[{{}}]}}}}\"),
                CreateSimulationJson($\"\\\"economy\\\":{{\\\"optical\\\":{{\\\"{property}\\\":[{{}},{{}}]}}}}\"),
                new WorldSaveLimits(maximumBytes: 100_000, maximumBuildingCount: 1),
                $\"simulation.economy.optical.{property}\");
        }
    }

    [TestMethod]
    public void EconomyCoreCollectionsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        foreach (var property in new[] { \"companies\", \"establishments\" })
        {
            AssertNestedBoundary(
                CreateSimulationJson($\"\\\"economy\\\":{{\\\"{property}\\\":[{{}}]}}\"),
                CreateSimulationJson($\"\\\"economy\\\":{{\\\"{property}\\\":[{{}},{{}}]}}\"),
                new WorldSaveLimits(maximumBytes: 100_000, maximumBuildingCount: 1),
                $\"simulation.economy.{property}\");
        }
        foreach (var property in new[] { \"jobs\", \"employments\" })
        {
            AssertNestedBoundary(
                CreateSimulationJson($\"\\\"economy\\\":{{\\\"{property}\\\":[{{}}]}}\"),
                CreateSimulationJson($\"\\\"economy\\\":{{\\\"{property}\\\":[{{}},{{}}]}}\"),
                new WorldSaveLimits(maximumBytes: 100_000, maximumPersonCount: 1),
                $\"simulation.economy.{property}\");
        }
    }

    [TestMethod]
    public void SerializeAppliesVehicleAndPersonNestedLimitsBeforeDtoProjection()
""")

# Regression tests for #273.
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/OpticalSimulationTests.cs",
    """    [TestMethod]
    public void OpticalNodeQueryUsesThreeDimensionalVolume()
""",
    """    [TestMethod]
    public void CheckpointRejectsDisconnectedOpticalDemandRoute()
    {
        var world = CreateRedundantWorld(out _, out _, out var alternateCable, out var demand);
        world.Step();
        var checkpoint = world.CreateCheckpoint();
        var economy = checkpoint.Economy!;
        var optical = economy.Optical!;
        var demands = optical.Demands
            .Select(item => item.Id == demand ? item with { RouteCableIds = new[] { alternateCable } } : item)
            .ToArray();
        var corrupted = checkpoint with { Economy = economy with { Optical = optical with { Demands = demands } } };

        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(corrupted));
    }

    [TestMethod]
    public void SolverResultRejectsDisconnectedOpticalDemandRoute()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1), opticalRoutingSolver: new DisconnectedRouteSolver());
        var building = world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Commercial);
        var backbone = world.CreateOpticalNode(new WorldPoint(-20, 0, 0), OpticalNodeKind.BackboneGateway);
        var endpoint = world.CreateOpticalNode(new WorldPoint(5, 0, 0), OpticalNodeKind.Endpoint);
        var isolatedA = world.CreateOpticalNode(new WorldPoint(50, 0, 0), OpticalNodeKind.Distribution);
        var isolatedB = world.CreateOpticalNode(new WorldPoint(60, 0, 0), OpticalNodeKind.Distribution);
        world.CreateFiberCable(backbone, endpoint, 20d);
        world.CreateFiberCable(isolatedA, isolatedB, 20d);
        world.CreateOpticalEquipment(backbone, OpticalEquipmentKind.Olt, 20d, requiresPower: false);
        world.CreateOpticalEquipment(endpoint, OpticalEquipmentKind.Onu, 20d, building, requiresPower: false);
        world.CreateOpticalBackhaul(backbone, 20d);
        world.CreateBuildingOpticalDemand(endpoint, building, 5d);

        Assert.ThrowsExactly<InvalidOperationException>(() => world.Step());
    }

    [TestMethod]
    public void OpticalNodeQueryUsesThreeDimensionalVolume()
""")
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/OpticalSimulationTests.cs",
    """    private static SimulationWorld CreateRedundantWorld(
""",
    """    private sealed class DisconnectedRouteSolver : IOpticalRoutingSolver
    {
        public OpticalRoutingResult Solve(OpticalRoutingRequest request)
        {
            var demand = request.Demands.Single();
            var backhaul = request.Backhauls.Single();
            var disconnectedCable = request.FiberCables[request.FiberCables.Count - 1];
            var allocation = Math.Min(1d, demand.RequestedGigabitsPerSecond);
            return new OpticalRoutingResult(
                new[] { new OpticalDemandRouteResult(demand.Id, backhaul.Id, allocation, new[] { disconnectedCable.Id }) },
                request.FiberCables.Select(static cable => new OpticalFiberLoadResult(cable.Id, 0d)).ToArray(),
                new[] { new OpticalBackhaulLoadResult(backhaul.Id, allocation) });
        }
    }

    private static SimulationWorld CreateRedundantWorld(
""")

# Regression tests for #279/#280/#281/#282.
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/MultimodalTransitTests.cs",
    """    [TestMethod]
    public void DeterministicFixtureContainsWalkRailwayWalkBusAndTaxi()
""",
    """    [TestMethod]
    public void CheckpointRejectsBrokenTaxiAssignmentInvariants()
    {
        var world = CreateRoadWorld();
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        var pattern = world.CreateTransitServicePattern(line, [new(first, 0, 1), new(second, 10, 1)]);
        var trip = world.CreateTransitTrip(pattern, 0);
        var bus = world.CreateBusTransitVehicle(trip);
        var taxi = world.CreateTaxiVehicle(new WorldPoint(5, 0, 0));
        var firstRequest = world.CreateTaxiRequest(new TripRequestId(100), new WorldPoint(10, 0, 0), new WorldPoint(90, 0, 0));
        var secondRequest = world.CreateTaxiRequest(new TripRequestId(101), new WorldPoint(20, 0, 0), new WorldPoint(70, 0, 0));
        world.DispatchTaxiRequests();
        var checkpoint = world.CreateCheckpoint();
        var transit = checkpoint.MultimodalTransit!;

        var duplicateActive = transit.TaxiRequests.Select(item => item.Id == secondRequest ? item with { State = TaxiRequestState.Assigned, AssignedVehicleId = taxi } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { TaxiRequests = duplicateActive } }));

        var busAssigned = transit.TaxiRequests.Select(item => item.Id == firstRequest ? item with { AssignedVehicleId = bus } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { TaxiRequests = busAssigned } }));

        var missingAssignment = transit.TaxiRequests.Select(item => item.Id == firstRequest ? item with { AssignedVehicleId = null } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { TaxiRequests = missingAssignment } }));
    }

    [TestMethod]
    public void CheckpointRejectsBrokenTripBusBijectionAndBusState()
    {
        var world = CreateRoadWorld();
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        var pattern = world.CreateTransitServicePattern(line, [new(first, 0, 1), new(second, 10, 1)]);
        var tripId = world.CreateTransitTrip(pattern, 0);
        var busId = world.CreateBusTransitVehicle(tripId);
        var checkpoint = world.CreateCheckpoint();
        var transit = checkpoint.MultimodalTransit!;

        var brokenTrips = transit.Trips.Select(item => item.Id == tripId ? item with { VehicleId = null } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { Trips = brokenTrips } }));

        var negativeStop = transit.Vehicles.Select(item => item.Id == busId ? item with { StopIndex = -1 } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { Vehicles = negativeStop } }));

        var stuckEnRoute = transit.Vehicles.Select(item => item.Id == busId ? item with { State = TransitVehicleMovementState.EnRouteToStop, RoadVehicleId = null } : item).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { MultimodalTransit = transit with { Vehicles = stuckEnRoute } }));
    }

    [TestMethod]
    public void GenericPatternCreationRejectsRailwayLineWithoutRailwayService()
    {
        var world = CreateRoadWorld();
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var railwayLine = world.CreateTransitLine(TransitMode.Railway);

        Assert.ThrowsExactly<ArgumentException>(() => world.CreateTransitServicePattern(railwayLine, [new(first, 0, 1), new(second, 10, 1)]));
    }

    [TestMethod]
    public void DeterministicFixtureContainsWalkRailwayWalkBusAndTaxi()
""")

# Regression test for #283.
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/RailwayOperationsTests.cs",
    """    [TestMethod]
    public void CheckpointRestoreContinuesWithIdenticalOperationState()
""",
    """    [TestMethod]
    public void CheckpointRejectsServiceWhoseOriginDepotDoesNotOwnRouteStart()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        var checkpoint = world.CreateCheckpoint();
        var services = checkpoint.RailwayServices!.ToArray();
        var first = services[0];
        Assert.AreNotEqual(first.OriginDepotId, first.DestinationDepotId);
        services[0] = first with { OriginDepotId = first.DestinationDepotId };

        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(checkpoint with { RailwayServices = services }));
    }

    [TestMethod]
    public void CheckpointRestoreContinuesWithIdenticalOperationState()
""")

print("Batch 2 patches applied")
