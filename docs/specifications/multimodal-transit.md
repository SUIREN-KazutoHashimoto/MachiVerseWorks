# Multimodal Transit Specification

## Purpose

Phase 19 unifies walking, Bus, Taxi, and Railway travel behind one deterministic Journey model. Road Traffic remains authoritative for road vehicle movement, Railway Operations remains authoritative for train/service operation, and Population remains authoritative for Person daily activity. Multimodal Transit owns only the cross-mode travel definitions and mutable passenger/dispatch state needed to connect those domains.

## Stable IDs and entities

The following entities use monotonically allocated unsigned 64-bit stable IDs; zero is invalid:

- `TransitStopId`
- `TransitLineId`
- `TransitServicePatternId`
- `TransitTripId`
- `TransitVehicleId`
- `TaxiRequestId`
- `JourneyId`
- `PassengerId`

A `TransitStop` is either Bus or Railway. Bus stops reference an existing `LaneId`. Railway stops reference an existing `StationId` and may reference a Platform belonging to that Station.

A `TransitLine` is Bus or Railway. A `TransitServicePattern` contains at least two ordered stops. The first stop has zero travel ticks from previous; subsequent stops require positive travel ticks. Railway patterns reference an existing Railway Service and derive their stop sequence/timing from its Timetable instead of duplicating Railway Operations state.

A scheduled Bus `TransitTrip` binds a service pattern and planned start tick. A Bus `TransitVehicle` binds that Trip. Taxi vehicles are unscheduled and are selected by Taxi Request dispatch.

## Road Traffic reuse

Bus and Taxi do not implement a second road movement engine.

A Bus creates Road Traffic route/vehicle state for each stop-to-stop road leg. On arrival it holds the Road Traffic vehicle for the configured dwell interval before continuing to the next stop. The Transit Vehicle mirrors the authoritative Road Traffic position and exposes its transit-specific state and arrival estimate.

A Taxi Request has pickup/drop-off points and lifecycle `Requested -> Assigned -> PickingUp -> Riding -> Completed`, with `Failed` for unroutable legs. Dispatch selects the nearest idle Taxi by squared 3D distance and breaks exact ties by stable Transit Vehicle ID. Pickup and drop-off movement uses Road Routing and Road Traffic.

## Journey planning

A `Journey` is an ordered list of `JourneyLegSnapshot` values. A leg records mode, endpoint/stop references, optional line/Railway Service, estimated duration, and transfer ticks.

The planner builds a deterministic graph from current Transit Stops and Service Patterns:

- access walking from the origin Road access point to candidate stops;
- Bus/Railway edges from ordered service-pattern stops;
- transfer walking between stops within 300 m;
- egress walking from candidate final stops to the destination Road access point.

Walking duration uses 1.4 m/s and is converted to fixed simulation ticks. Candidate selection uses estimated tick cost, with stable stop IDs providing deterministic tie ordering.

Railway travel is represented by the common Journey leg contract but references the authoritative Railway Service. Timetable/Train physics remain owned by Railway Operations.

## Mode choice and Population

For each Population Trip Request, mode choice compares walking against available public transit, idle Taxi, and private Motor when the Person has a vehicle. The lowest estimated duration wins. A Bus/Railway choice creates a Passenger tied to the selected Journey; Taxi creates a Taxi Request; Motor and walking continue through their existing execution paths.

`PersonTravelState.Transit` and `TravelMode.Transit` represent active public-transit or Taxi travel without exposing a specific transport provider to Population.

## Passenger state machine

Passenger lifecycle is deterministic and fixed-tick driven:

`Waiting -> Boarding -> Riding -> Alighting`, with `Transfer` used for stop-to-stop walking transfer legs and `Arrived` as the terminal state.

Access and egress walking are represented as Journey walking legs. Transfer walking advances for its estimated duration before the next boarding leg. Passenger progress depends only on stored Journey/state/tick values and therefore survives checkpoint restoration.

## Checkpoint and Save Data

Save Format 10 adds `simulation.multimodalTransit`. It stores all Multimodal Transit entities, mutable Bus/Taxi/Passenger state, Journey definitions, and every next-ID counter. It also preserves Road Vehicle references used by active Bus/Taxi movement and Railway Service references used by Railway patterns/Journeys.

Restore rejects missing or mismatched Lane, Station, Platform, Railway Service, Transit pattern/trip/vehicle/Journey, Road Vehicle, or active Population Trip references. Format 9 remains readable and migrates to an empty Multimodal Transit subsystem.

A transfer-in-progress save/load must continue to the same Passenger state and final arrival as uninterrupted execution.

## Protocol and Web

Protocol 2.8 adds message type 720, `MultimodalTransitSnapshot`. Each publish contains the authoritative tick plus:

- Bus/Railway lines;
- Bus/Railway stops and infrastructure attachments;
- service patterns and optional Railway Service reference;
- realtime Bus/Taxi positions and state;
- next-stop Bus arrival estimates.

Protocol 2.7 and earlier never receive message 720. The Web Client decodes 2.8 and exposes route, stop count, Bus/Railway line count, Bus/Taxi vehicle positions, and arrivals in the Transit Debug view.

## Determinism and verification

The deterministic Phase 19 fixture refuses to start unless it can construct a `Walk -> Railway -> Walk` Journey from the seeded Railway Service. It then adds a scheduled Bus and a dispatched Taxi on one Road Traffic lane.

Verification covers journey construction, transfer Passenger state, checkpoint continuation, Save Format 10 migration/round-trip, Protocol 2.8 validation, Server mapping, Web decoding/debug presentation, and real Server-to-browser Bus/Taxi/Railway delivery.
