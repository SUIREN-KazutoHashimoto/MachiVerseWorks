# Intersection & Signal Control Architecture

## Component boundary

Phase 14 keeps Road Network topology authoritative and inserts one derived control layer between Route/Lane traffic state and Vehicle Lane transitions. The same per-tick controller state is also consumed by the Pedestrian crossing gate so Vehicle and Pedestrian priority are resolved from one traffic-control decision.

```text
RoadNetworkStore
  -> RoadTrafficTopology
  -> IntersectionControlStore
       ^ entry intents from VehicleStore
       v per-tick entry grants
     VehicleStore
       |
       +-> SimulationWorld crossing control gate -> PedestrianStore
  -> SimulationPublishSnapshot
  -> Protocol / Server
  -> Web
```

`IntersectionControlStore` is internal Simulation infrastructure. Callers consume immutable public snapshots rather than mutating controller state directly.

## Topology rebuild

`SimulationWorld.InvalidateRouting()` invalidates both routing and Road Traffic topology. The next traffic use rebuilds `RoadTrafficTopology` from the current `RoadNetworkSnapshot` and immediately rebuilds `IntersectionControlStore` from the same snapshot.

The controller store therefore cannot retain movements that refer to removed or rewritten Lane connections. Pedestrian walking topology is rebuilt independently from RoadNode / RoadSegment / RoadAccessPoint state; crossing IDs cache their originating Intersection RoadNode so the controller can be matched without coupling Pedestrian edges to Lane IDs.

## Movement runtime

For every Lane connection through an intersection, the runtime stores:

- stable movement ID;
- the source `LaneConnectionSnapshot`;
- stop-line position;
- near-intersection chord endpoints used for conflict testing;
- incoming RoadSegment priority;
- turn priority;
- conflict movement IDs;
- assigned signal phase;
- current-tick queue / grant observations.

Current-tick queue and grant fields are ephemeral and reset by `PrepareTick`.

## Entry-intent batch

`VehicleStore.Step` first calls `CollectIntersectionIntents`. An intent consists of:

- `VehicleId`
- `LaneConnectionId`
- `DownstreamAvailable`

The controller resolves the complete list before any Vehicle is advanced. Grants are represented by `(VehicleId, LaneConnectionId)` keys and are valid only for that prepared tick.

A Vehicle transition whose Route step exits through an intersection asks `IntersectionControlStore.IsEntryGranted`. Missing grant means the Vehicle stays on the incoming step and enters `WaitingForTraffic`.

## Signal phase construction

Conflict-safe phases are produced with deterministic greedy coloring over movement priority order. The algorithm favors a small, reproducible phase set without introducing an optimization solver into the fixed-tick path.

A controller is signalized only when:

- it has at least four distinct incoming RoadSegments; and
- more than one conflict-safe phase is required.

Signal indication is computed from `TickCount % cycleDuration`. Phase timing has no mutable wall-clock timer and no asynchronous callback. Current fixed-signal timing is 20 seconds Green, 3 seconds Yellow, 1 second all-red for each phase.

## Pedestrian crossing gate

Pedestrian crossing topology and manual permission remain owned by the Pedestrian subsystem. Intersection control contributes only a derived automatic gate. Effective crossing permission is:

```text
manual crossing permission && automatic intersection-control permission
```

For a `FixedSignal` controller the automatic gate is open only when every Vehicle movement is Red and none has `EntryGrantedThisTick`; in other words Pedestrians use the deterministic all-red window. Green and Yellow never overlap an open Pedestrian gate.

For an `Unsignalized` controller, a Vehicle entry grant has priority for that prepared tick. Any granted movement closes the crossing gate; when no movement is granted the automatic gate is open and the manual Pedestrian permission decides the result.

A crossing whose RoadNode has no controller receives no automatic restriction. `SetPedestrianCrossingOpen(false)` can always force close; `true` cannot override a closed automatic gate.

The control decision is made before `PedestrianStore.Step` for the same Simulation tick. A Pedestrian reaching the intersection node observes that gate before switching to the next incident walking edge, enters `WaitingForCrossing` while closed, and resumes on the next open tick. This is a point-transition safety model, not a continuous crossing-polygon/body-envelope collision model.

## Persistence boundary

The controller topology and phase are derived state:

- topology derives from RoadNode/Lane/LaneConnection state;
- phase derives from topology + Simulation `TickCount` + `TickRate`;
- Pedestrian automatic crossing permission derives from controller state and current-tick grants.

Those authoritative inputs are already in `SimulationCheckpoint`. Persisting a second controller topology, phase offset, or automatic crossing permission would create two sources of truth, so they are deliberately not added to Save. Only the Pedestrian manual crossing permission is persisted.

If a future adaptive controller gains detector history, manual offsets, preemption state, or another independently mutable value, that value must become explicit checkpoint/Save state and requires a Save format version change.

## Publish read model

`SimulationRuntime.CapturePublishSnapshot` holds the Simulation lock only while capturing Agent, Pedestrian, Vehicle, intersection controller, and Road Network read-model state.

`SimulationPublishSnapshot` builds spatial indexes for Vehicle and intersection controllers in addition to existing entity indexes. Intersection controllers are spatially keyed by their RoadNode position.

For a subscription, the Server sends Vehicle spawn/update/remove and one intersection-controller snapshot per visible controller. Protocol-version checks prevent newer controller messages from being sent to incompatible negotiated minors.

## Protocol encoding

The base Protocol codec continues to encode established message families. The intersection snapshot uses `IntersectionControlProtocolCodec`; `ClientConnection.SendAsync` selects that codec for `IntersectionControlSnapshotMessage` and the base codec for other message families.

This isolates the variable-length movement payload and keeps older Protocol codec paths stable.

## Web read models

`traffic-protocol.ts` adds Vehicle / intersection decoding without forcing the older core protocol module to understand renderer-specific traffic state.

`VehicleStore` interpolates XYZ position and forward direction. `IntersectionControlStore` stores the latest controller message and expires stale controllers from debug rendering.

`WorldView` owns separate renderers:

- `VehicleRenderer`: instanced boxes scaled to Vehicle dimensions and yawed from the forward vector;
- `IntersectionControlRenderer`: stop-line point, signal-indication point, and queue line geometry.

The canvas exposes diagnostic Vehicle / intersection-control counts for browser E2E assertions.

## Failure and safety properties

- A missing movement for a referenced Lane connection is treated as an invariant failure.
- Conflicting Vehicle grants in one tick are prevented before Vehicle updates begin.
- Red/yellow indication blocks new Vehicle entry.
- Downstream occupancy blocks Vehicle entry even on green.
- FixedSignal Pedestrian crossing is closed whenever any Vehicle movement is Green or Yellow.
- Unsignalized Vehicle entry grant closes the Pedestrian crossing for that tick.
- Manual Pedestrian open cannot bypass an automatic closed gate.
- Stable ID sorting removes dictionary-order dependence.
- Protocol payload validation rejects invalid IDs, enum values, non-finite coordinates, or inconsistent lengths.

## Verification automation

- `.github/workflows/ci.yml`: solution全体のbuild/testでVehicle/Pedestrian mixed crossingを含むdeterministic Simulation regressionを検証する。
- `.github/workflows/e2e.yml`: `signal-traffic-server-browser` jobで実Server/WebSocket/Browser/Three.js経路を検証する。
- `.github/workflows/benchmarks.yml`: `queued-intersections` jobでqueued-intersection tickとcontroller-snapshotを計測する。

Phase専用workflowは置かず、共有境界のcorrectnessは`CI`、統合動作は`End-to-end`、性能回帰は`Benchmarks`へ集約する。
