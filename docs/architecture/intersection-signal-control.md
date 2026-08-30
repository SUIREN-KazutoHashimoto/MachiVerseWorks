# Intersection & Signal Control Architecture

## Component boundary

Phase 14 keeps Road Network topology authoritative and inserts one derived control layer between Route/Lane traffic state and Vehicle Lane transitions.

```text
RoadNetworkStore
  -> RoadTrafficTopology
  -> IntersectionControlStore
       ^ entry intents from VehicleStore
       v per-tick entry grants
     VehicleStore
  -> SimulationPublishSnapshot
  -> Protocol 2.4 / Server
  -> VehicleStore + IntersectionControlStore (Web)
  -> Three.js debug renderer
```

`IntersectionControlStore` is internal Simulation infrastructure. Callers consume immutable public snapshots rather than mutating controller state directly.

## Topology rebuild

`SimulationWorld.InvalidateRouting()` invalidates both routing and Road Traffic topology. The next traffic use rebuilds `RoadTrafficTopology` from the current `RoadNetworkSnapshot` and immediately rebuilds `IntersectionControlStore` from the same snapshot.

The controller store therefore cannot retain movements that refer to removed or rewritten Lane connections.

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

Signal indication is computed from `TickCount % cycleDuration`. Phase timing has no mutable wall-clock timer and no asynchronous callback.

## Persistence boundary

The controller topology and phase are derived state:

- topology derives from RoadNode/Lane/LaneConnection state;
- phase derives from topology + Simulation `TickCount` + `TickRate`.

Those authoritative inputs are already in `SimulationCheckpoint` and Save format 6. Persisting a second controller topology or phase offset would create two sources of truth, so Phase 14 deliberately does not add a Save-format field. Checkpoint and Save round-trip tests compare reconstructed controller state at the same tick.

If a future adaptive controller gains detector history, manual offsets, preemption state, or another independently mutable value, that value must become explicit checkpoint/Save state and requires a Save format version change.

## Publish read model

`SimulationRuntime.CapturePublishSnapshot` holds the Simulation lock only while capturing:

- Agent snapshots;
- Pedestrian snapshots;
- Vehicle snapshots;
- intersection controller snapshots;
- Road Network read model.

`SimulationPublishSnapshot` builds spatial indexes for Vehicle and intersection controllers in addition to existing entity indexes. Intersection controllers are spatially keyed by their RoadNode position.

For a subscription, the Server sends Vehicle spawn/update/remove and one intersection-controller snapshot per visible controller. Protocol-version checks prevent 2.4-only controller messages from being sent to older negotiated minors.

## Protocol encoding

The base Protocol codec continues to encode established message families. The 2.4 intersection snapshot uses `IntersectionControlProtocolCodec`; `ClientConnection.SendAsync` selects that codec for `IntersectionControlSnapshotMessage` and the base codec for all other messages.

This isolates the variable-length movement payload and keeps older Protocol codec paths stable.

## Web read models

`traffic-protocol.ts` adds Vehicle / intersection decoding without forcing the older core protocol module to understand renderer-specific traffic state.

`VehicleStore` interpolates XYZ position and forward direction. `IntersectionControlStore` stores the latest controller message and expires stale controllers from debug rendering.

`WorldView` owns separate renderers:

- `VehicleRenderer`: instanced boxes scaled to Vehicle dimensions and yawed from the forward vector;
- `IntersectionControlRenderer`: stop-line point, signal-indication point, and queue line geometry.

The canvas exposes diagnostic `data-vehicle-count` and `data-intersection-control-count` values for browser E2E assertions.

## Failure and safety properties

- A missing movement for a referenced Lane connection is treated as an invariant failure.
- Conflicting grants in one tick are prevented before Vehicle updates begin.
- Red/yellow indication blocks new entry.
- Downstream occupancy blocks entry even on green.
- Stable ID sorting removes dictionary-order dependence.
- Protocol 2.4 payload validation rejects invalid IDs, enum values, non-finite coordinates, or inconsistent lengths.

## Verification automation

- `.github/workflows/ci.yml`: solution全体のbuild/testでdeterministic Simulation regressionを含めて検証する。
- `.github/workflows/e2e.yml`: `signal-traffic-server-browser` jobで実Server/WebSocket/Browser/Three.js経路を検証し、`e2e-signal-traffic` artifactを保存する。
- `.github/workflows/benchmarks.yml`: `queued-intersections` jobでqueued-intersection tickとcontroller-snapshotを計測し、`benchmark-intersection-control` artifactを保存する。

Phase専用workflowは置かず、共有境界のcorrectnessは`CI`、統合動作は`End-to-end`、性能回帰は`Benchmarks`へ集約する。
