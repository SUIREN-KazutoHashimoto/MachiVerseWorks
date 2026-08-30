# Railway Operations Architecture

## Boundary

Railway Operations is a mutable Simulation subsystem layered on the immutable Railway Infrastructure read model. It consumes TrackNode, TrackSegment, TrackConnection, BlockSection, Station, Platform, and Depot definitions but does not own or mutate them.

`SimulationWorld.RailwayOperations` is the public façade. `RailwayOperationsStore` owns Formation/Route/Timetable/Service/Train state plus the runtime Block and Platform ownership maps.

## Authoritative state

Definitions are stored once and referenced by stable ID. Route construction resolves Track geometry into ordered route steps and cumulative distance. Service construction resolves each Timetable stop to a route distance.

Mutable Train state is compact and checkpointable: route distance, pose, speed, movement state, Block/Platform/Depot references, and dwell departure tick. Service state holds lifecycle, delay, next-stop index, and Train reference.

Block and Platform owner dictionaries are derived indexes over Train mutable state. On restore they are rebuilt from Train snapshots and reject duplicate ownership.

## Tick pipeline

`SimulationWorld.Step()` advances Railway Operations inside the same deterministic fixed-tick loop as other Simulation systems. Trains are processed by stable Train ID order.

For each Train the store performs lifecycle activation, dwell release, Platform look-ahead, braking target calculation, acceleration/deceleration, Block transition/reservation, route-distance integration, Station arrival, delay update, and Depot completion.

The ordering deliberately makes ownership decisions single-threaded and deterministic. No wall clock, asynchronous callback, or Web/Server state participates in Simulation decisions.

## Contention

`_blockOwners` and `_platformOwners` are the exclusive-ownership indexes. Reservation uses `TryAdd` semantics and is idempotent for the current owner. A Train that cannot reserve its next Block or Platform stops before entering the contested resource.

Exact route-step boundaries are normalized through the same Block ownership transition before speed integration. This avoids a Train being geometrically inside a new Track step while still owning only the previous Block.

## Persistence

`SimulationCheckpoint` contains all Railway Operations definitions, mutable states, and next-ID counters. `WorldSaveSerializer` Format 9 maps that checkpoint to a nested `railwayOperations` DTO section.

Restore order is Infrastructure first, then Railway Operations. Routes and Timetable stop distances are revalidated against restored Infrastructure before Train state is accepted. Owner indexes are reconstructed after Train materialization. Format 8 migration supplies empty operations and next IDs of 1.

## Publish model

`SimulationRuntime.CapturePublishSnapshot()` captures Train snapshots and the Railway Operations snapshot while holding the Simulation gate. `SimulationPublishSnapshot` builds a spatial index for Train positions alongside Agent/Pedestrian/Vehicle indexes.

Per-client volume filtering happens after capture. `RailwayOperationsMessageMapper` selects visible Trains and only the Service/Timetable definitions referenced by those Trains. This preserves one authoritative capture tick while avoiding full-world dynamic railway transfer to every client.

Protocol 2.7 message 710 is serialized by `RailwayOperationsProtocolCodec`; earlier negotiated minors never receive it. Static Railway Infrastructure remains message 700 and revision-driven rather than tick-driven.

message 710はsingle-frame contractでchunkingを行わない。`RailwayOperationsProtocolCodec.GetPayloadLength()`はTrain / Service / Timetable stopを含む正確なencoded payload長をallocation前に算出し、`SnapshotPublishService`の`RailwayOperationsSnapshotMessagePlanner`が1 MiB上限をpreflightする。上限超過時はRailway snapshotの代わりにsubscription-localなstructured `InvalidRequest`を送信し、delivery schedulerへunexpected exceptionを残さない。したがって1 Clientの広いsubscriptionや大規模Operations stateがsnapshot publisher全体をfaultさせない。

## Web architecture

`connection.ts` dispatches message 710 to the dedicated Railway Operations decoder. `RailwayOperationsLayer` owns a Three.js group and a stable Train-ID-to-mesh map. Spawn/update/remove behavior is derived from each snapshot without duplicating Simulation state.

The UI consumes Service/Timetable fields only for debug presentation. It never feeds arrival estimates, delay, Platform, or Train state back into Simulation.

## Verification layers

- Simulation tests: route validation, exclusive Block/Platform ownership, lifecycle, deterministic checkpoint continuation.
- Persistence tests: Format 9 round-trip and Format 8 migration.
- Protocol tests: 2.7 binary round-trip, 2.6 rejection, 1 MiB payload preflight boundary.
- Server tests: visible Train to Service/Timetable mapping and oversize snapshot structured-error planning.
- Web tests: decoder validation and Three.js pose update.
- Phase 18 E2E: real Server/WebSocket/headless browser through a complete two-Train operating cycle.
- Benchmark: 100 and 1,000 Train/Service fixed-tick and snapshot scaling.