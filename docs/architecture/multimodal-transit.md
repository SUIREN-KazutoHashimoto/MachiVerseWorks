# Multimodal Transit Architecture

## Boundary and ownership

Multimodal Transit is an integration subsystem inside `SimulationWorld`; it does not replace the domain engines it connects.

- Road Network/Routing owns Lane topology and road paths.
- Road Traffic owns Bus/Taxi physical road movement.
- Railway Operations owns Train, Service, Timetable, block/platform, delay, and depot lifecycle.
- Population owns Person schedule and Trip demand.
- Multimodal Transit owns Transit Stop/Line/Pattern/Trip, Taxi Request, Journey, Passenger, and transit-specific vehicle association/state.

`SimulationWorld.MultimodalTransit` is the façade and `MultimodalTransitStore` is the state owner. Cross-domain references are stable IDs and are validated on creation/restore.

## Tick pipeline

The fixed tick remains the sole simulation clock. Population planning runs before road/rail movement, then Road Traffic and Railway Operations advance, Pedestrians advance, and Multimodal Transit mirrors/advances Bus/Taxi/Passenger state before Population completes arrived trips.

Bus and Taxi road positions are never integrated independently. Their Road Vehicle ID is resolved after Road Traffic has stepped. This preserves one authoritative collision/car-following/intersection model.

## Journey graph

The planner is derived from current Transit Stop/Pattern definitions for each Trip Request. Pattern edges keep only cross-domain identifiers and estimated fixed-tick cost. Railway pattern construction projects a Railway Service Timetable into the common stop contract, retaining `RailwayServiceId` so the original Railway definition remains authoritative.

Access/egress and transfer walking use direct 3D distance in the Phase 19 minimum model. This is intentionally separate from Phase 16 pedestrian route execution; future route-aware access refinement can replace the cost derivation without changing Journey leg identity.

## Population integration

`PlanPopulationTrips` allocates one Trip Request ID, runs mode choice, then launches exactly one execution path. Transit Persons carry `TravelMode.Transit`; completion resolves through Passenger or Taxi Request state. Private Motor and walking continue to use existing Vehicle/Pedestrian references.

Mode choice is deterministic and does not use wall-clock or client state. Stable entity ordering breaks equal-cost dispatch/graph choices.

## Persistence

`SimulationCheckpoint.MultimodalTransit` is captured after all dependent Road/Rail/Population state is materialized. Restore order reconstructs Railway Infrastructure/Operations and Road Traffic before restoring Multimodal Transit, then performs cross-reference validation.

Save Format 10 serializes the Multimodal checkpoint as raw numeric/domain data. Format 9 migration supplies an empty subsystem. No UI/localized strings enter the save contract.

## Publish and Protocol

`SimulationRuntime.CapturePublishSnapshot()` captures Multimodal Transit under the same Simulation gate as Road/Rail snapshots. `MultimodalTransitMessageMapper` converts immutable simulation snapshots to Protocol 2.8 definitions and realtime Bus/Taxi state. Bus arrival estimates are derived from the current Transit Vehicle next stop/tick estimate.

`HostedServices` only sends message 720 when the negotiated version supports 2.8. `ClientConnections` uses the dedicated Multimodal codec, keeping the core protocol codec small.

## Web boundary

`connection.ts` negotiates 2.8 and routes type 720 to `multimodal-transit.ts`. The decoder validates frame sizes, IDs, enum ranges, pattern stop references, and arrival references before returning data to the application.

The Phase 19 Web scope is debug presentation rather than a full transit map layer. `ClientUi` renders route stop sequences, counts, realtime vehicle coordinates, and arrival ticks. It is read-only and does not influence planning or dispatch.

## Verification layers

- Simulation: Bus dwell/Road Traffic reuse, Taxi nearest dispatch, multimodal graph, Railway integration, transfer state, deterministic checkpoint continuation.
- Persistence: Format 10 round-trip during transfer and Format 9 migration.
- Protocol: 2.8 binary round-trip/malformed-reference rejection and 2.7 rejection.
- Server: line/stop/pattern/vehicle/arrival mapping.
- Web: type-720 decoder and Transit Debug update.
- Phase 19 E2E: deterministic fixture startup validates Walk→Railway→Walk, then a real Server/WebSocket/headless browser observes Railway/Bus/Taxi definitions, Road-backed vehicle movement, and arrival estimates.
- Benchmark: journey graph planning, nearest Taxi dispatch, and transfer checkpoint continuation at two scales.
