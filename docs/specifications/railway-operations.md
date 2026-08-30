# Railway Operations Specification

## Purpose

Phase 18 adds deterministic train operation on top of the Phase 17 railway infrastructure. The authoritative scope is Train Formation, Route, Timetable, Service, Train movement, Block ownership, Platform assignment, delay, and Depot lifecycle.

Static Track / Station / Platform / Depot topology remains owned by Railway Infrastructure. Railway Operations references those entities by stable ID and never infers connectivity from geometry.

## Stable IDs and definitions

The following operation entities use monotonically allocated unsigned 64-bit stable IDs. Zero is invalid.

- `TrainFormationId`
- `RailwayRouteId`
- `TimetableId`
- `RailwayServiceId`
- `TrainId`

Checkpoint and Save Data preserve both assigned IDs and every next-ID counter.

A `TrainFormation` defines length, maximum speed, maximum acceleration, service deceleration, and passenger capacity. All physical quantities must be finite and positive; capacity must be greater than zero.

A `RailwayRoute` is an ordered TrackSegment sequence. Consecutive segments must be connected by an explicit `TrackConnection`, respect Track direction, and form one continuous traversal. The route stores its derived 3D length.

A `Timetable` is an ordered stop sequence. Each stop defines Station ID, planned arrival tick, planned departure tick, minimum dwell ticks, and an optional preferred Platform. Departure may not precede arrival, and successive stops must be nondecreasing in time.

A `RailwayService` binds one Formation, Route, Timetable, origin Depot, destination Depot, and planned start tick. The route must begin on an origin Depot track and end on a destination Depot track. Timetable stations must appear in route order.

A `Train` is the mutable physical execution state of one Service. A Service owns at most one Train.

## Fixed-tick movement

Train movement is advanced only by `SimulationWorld.Step()`. Each tick uses the configured fixed tick duration; wall-clock time is not authoritative.

A running Train stores route distance, 3D world position, forward vector, speed, movement state, Block ownership, Platform assignment/occupancy, Depot state, and dwell departure tick.

Target speed is bounded by both Formation maximum speed and the current TrackSegment speed limit. Acceleration and service braking are applied deterministically from the prior tick state. Position and forward vector are derived from Route Track geometry at the resulting route distance.

Train processing order is stable Train ID order. Therefore contention resolution is deterministic for the same infrastructure, seed, timetable, and prior state.

## Block separation

Each TrackSegment may belong to a Phase 17 `BlockSection`. Railway Operations maintains a single Train owner per Block.

Before a Train enters a different Block it must reserve that Block. If another Train owns it, the Train stops at the boundary and enters `WaitingForBlock`. The previous Block is released only after the next Block has been acquired. Exact Track-step boundary positions use the same ownership transition rule, preventing boundary stalls and double ownership.

At no time may two Trains own the same Block.

## Station approach and Platform assignment

As a Train approaches the next Timetable stop, the operation store selects a Platform belonging to that Station and lying on the Route. A preferred Platform is attempted first when valid; otherwise eligible platforms are considered by stable Platform ID order.

A Platform has at most one Train owner. If no eligible Platform is available, the approaching Train brakes to a deterministic wait point before the stop rather than entering an occupied Platform.

After assignment, the Train brakes to the selected Platform stop distance, transitions to `Dwelling`, marks the Platform occupied, and remains stopped until both planned departure constraints and minimum dwell are satisfied. On departure the Platform is released before proceeding to the next stop.

## Delay

Delay is stored on the Service as nonnegative ticks. At station arrival/departure, actual tick is compared with the corresponding planned Timetable tick. Positive lateness increases Service delay; the same accumulated delay is applied to later stop expectations and is delivered to clients.

Contention for Block or Platform can therefore produce deterministic propagated delay without changing the original Timetable definition.

## Depot lifecycle

A Service begins in `Planned` state with its Train in the origin Depot. At or after the planned start tick, the Train may depart only after acquiring its first Block. The Service then becomes `Active`.

After all Timetable stops are completed, the Train continues to the Route endpoint. At the destination Depot it releases remaining Block/Platform ownership, stops, records the destination Depot, and transitions with the Service to `Completed`.

## Checkpoint and Save Data

Simulation checkpoint stores Formation, Route, Timetable, Service, Train and all next-ID counters. Mutable Train state includes route distance, 3D pose, speed, movement state, Block/Platform/Depot references, dwell departure tick, and snapshot tick.

Save Format 9 adds `simulation.railwayOperations` containing those definitions and mutable states. Format 8 remains readable and migrates to empty Railway Operations while preserving Phase 17 infrastructure.

After save/load, a world stepped with the same subsequent ticks must produce the same Service and Train snapshots as uninterrupted execution.

## Protocol and Server delivery

Protocol 2.7 adds message type `710`, `RailwayOperationsSnapshot`. It carries:

- authoritative simulation tick
- visible Train state and 3D pose
- related Service state and delay
- related Timetable stop definitions
- current/assigned Platform, Block, Depot, and dwell state

The Server filters Trains by the client's 3D subscription volume from an immutable publish snapshot. Only Services and Timetables referenced by those visible Trains are included. Protocol 2.6 clients continue to receive static Railway Infrastructure but never receive message 710.

## Web rendering and debug view

The Web Client negotiates Protocol 2.7, decodes Railway Operations separately from static Railway Infrastructure, and renders each Train as a reusable Three.js mesh. Simulation `(X,Y,Z)` is mapped to Three.js `(X,Z,Y)` so altitude remains the rendering Y axis.

The Railway Debug view reports Train count, delayed/completed Service counts, and the next Station arrival tick calculated from planned arrival plus current Service delay.

## Determinism and validation

The deterministic Phase 18 fixture contains two Trains sharing a single-track sequence, two Stations/Platforms, two Timetables, origin/destination Depots, and intentional contention. Tests verify:

- no two Trains own the same Block or Platform
- contention produces delay
- both Services complete and return to a Depot
- checkpoint and Save Data continuation match uninterrupted execution
- Protocol 2.7 preserves Train/Service/Timetable state
- Server-to-browser E2E observes movement, Platform use, dwell, delay, and completion
