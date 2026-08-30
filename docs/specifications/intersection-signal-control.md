# Intersection & Signal Control Specification

## Scope

Phase 14 adds deterministic intersection-entry arbitration on top of the Road Network and Road Traffic foundations. The authoritative infrastructure input remains `RoadNode` / `RoadSegment` / `Lane` / `LaneConnection`; Phase 14 does not introduce a second editable intersection graph.

This specification covers:

- intersection movements derived from `LaneConnection`
- conflict relations between movements
- stop-line waiting and queue observation
- unsignalized priority / yield
- fixed-cycle signal control
- downstream blocking
- checkpoint / Save restoration semantics
- Protocol 2.4 delivery and Web debug visualization

Adaptive control, protected/permissive arrow policies, pedestrian signal phases, detector-actuated timing, emergency priority, and traffic-light editing UI are outside Phase 14.

## Authoritative movement model

Each `LaneConnection` whose `ViaNodeId` references a `RoadNodeKind.Intersection` produces one `IntersectionMovement`.

- `IntersectionMovementId` is stable and currently uses the corresponding `LaneConnectionId` value.
- `FromLaneId`, `ToLaneId`, `ViaNodeId`, and `TurnMovement` remain sourced from the `LaneConnection`.
- A movement exposes a stop-line position at the incoming Lane exit.
- A movement exposes the IDs of movements with which it conflicts.

A topology rebuild is required after Road / Lane / LaneConnection mutation. The movement graph is derived again in stable ID order so no separately persisted editable graph can diverge from the Road Network.

## Conflict relation

Two movements conflict when at least one of the following is true:

1. they use the same incoming Lane;
2. they enter the same outgoing Lane;
3. their near-intersection movement chords intersect in the simulation XY plane.

Conflict relations are symmetric. The controller must never grant two conflicting movements in the same tick.

The geometric test deliberately uses short probe points on the incoming and outgoing Lane instead of two copies of the intersection center. Grade-separated roads are not represented as the same `RoadNodeKind.Intersection`, so they do not enter the same controller.

## Stop line and queue semantics

A Vehicle that reaches a Route step whose `ExitConnectionId` enters an intersection creates an entry intent for that tick.

If entry is not granted, the Vehicle remains at the end of the incoming Route step, speed becomes zero, and state becomes `WaitingForTraffic`.

`IntersectionMovementStateSnapshot.QueueLength` is the number of Vehicles presenting an entry intent to that movement in the current tick. It is a stop-line/controller queue observation, not a count of every slow Vehicle upstream on the Lane. Broader traffic queue metrics remain available through `TrafficMetrics.QueueLength`.

## Unsignalized priority and yield

Unsignalized controllers arbitrate eligible entry intents deterministically. Candidate ordering is:

1. intersection node stable ID;
2. incoming Road class priority: Highway, Arterial, Collector, Local, Service;
3. turn priority: Straight, Right, Left, U-turn;
4. movement stable ID;
5. Vehicle stable ID.

Candidates are accepted in this order only when they do not conflict with a movement already selected for the same tick. This is intentionally a minimal deterministic yield rule, not a jurisdiction-specific right-of-way model.

## Fixed-cycle signal controller

An intersection uses `FixedSignal` mode when it has at least four distinct incoming RoadSegments and its movement conflict graph requires more than one non-conflicting phase. Other intersections use `Unsignalized` mode.

Signal phases are built deterministically from the conflict graph in stable movement priority order. A movement belongs to exactly one phase.

For each fixed phase:

- green: 20 seconds
- yellow: 3 seconds
- all-red clearance: 1 second
- total phase duration: 24 seconds

Timing is converted to ticks using the Simulation `TickRate`. The current phase is calculated from the authoritative Simulation `TickCount`; wall-clock time is not used.

During yellow and red, new intersection entry is not granted. A Vehicle already past the transition boundary is not rolled back.

## Downstream blocking

An entry intent is eligible only when the first position on the next Lane has enough occupancy space for the Vehicle dimensions and configured minimum gap. A green indication never overrides downstream occupancy.

This prevents a Vehicle from entering an intersection when it cannot clear into its next Lane.

## Fixed-tick ordering and determinism

For every Simulation step:

1. the next `SimulationTime` tick is calculated;
2. Vehicle entry intents for that next tick are collected from the current traffic state;
3. the intersection controller resolves all intents as a batch;
4. Vehicle states advance using the resulting grants;
5. the Simulation commits the next tick.

Batch arbitration prevents Vehicle iteration order from being the sole source of right-of-way.

Controller phase is a pure function of `TickCount`, `TickRate`, and the derived movement topology. Therefore no independent mutable phase offset is persisted. Checkpoint and Save Data already preserve the authoritative tick and Road/LaneConnection topology; restoration rebuilds the controller and reproduces the same phase. Regression and Save round-trip tests verify this contract. Save format 6 remains valid because Phase 14 adds no independent authoritative persistence field.

## Protocol and Server delivery

Protocol 2.4 adds `IntersectionControlSnapshot` (`MessageType` 500).

Each controller message contains:

- Simulation tick
- intersection node ID
- controller mode
- phase index and phase tick
- movement ID and Lane connection identity
- turn movement
- stop-line XYZ position
- signal indication
- queue length
- whether entry was granted in that tick

Vehicle messages remain Protocol 2.3-compatible. A 2.4 client receives Road Network, Vehicle, and intersection-control state from the same atomic Server publish snapshot. Clients negotiating an older compatible minor version do not receive the 2.4 intersection message.

## Web debug visualization

The Web Client maintains independent `VehicleStore` and `IntersectionControlStore` read models. `WorldView` renders:

- Vehicle instances as `vehicles`
- stop-line points as `traffic-stop-lines`
- red/yellow/green indications as `traffic-signal-red`, `traffic-signal-yellow`, `traffic-signal-green`
- queue bars as `traffic-queues`

The visualization is diagnostic; Simulation remains authoritative.

## Verification

Phase 14 is covered by:

- movement/conflict and fixed-cycle unit tests;
- red wait / green resume and downstream blocking tests;
- multiple-intersection, turn-movement, and high-load queue deterministic regression tests;
- checkpoint and Save round-trip signal-phase tests;
- Protocol 2.4 codec tests;
- real Server -> WebSocket -> Browser -> Three.js E2E checks;
- `IntersectionControlBenchmarks` for queued intersection tick processing and controller snapshot generation.

Phase 14 may be merged ahead of formal Phase 13 closeout only under the ROADMAP rule for forward implementation: it depends exclusively on stable Road Traffic boundaries already present on `develop`, while Phase 14 itself remains marked as awaiting Phase 13 closeout until that dependency is formally completed.
