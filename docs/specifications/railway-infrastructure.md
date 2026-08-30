# Railway Infrastructure Specification

## Purpose

Phase 17 establishes the authoritative static railway infrastructure consumed by later train-operation phases. The scope is track topology, block sections, stations, platforms, pedestrian access, and depots. Train, timetable, occupancy, signalling state, and service operation remain Phase 18 concerns.

## Coordinate and topology contract

Railway geometry uses the same world coordinate system as the rest of Simulation: X/Y are horizontal coordinates in metres and Z is altitude in metres. A `TrackSegment` is a straight 3D segment between two `TrackNode` positions.

Geometric intersection never implies railway connectivity. Two tracks are connected only when they share a `TrackNode` and an explicit `TrackConnection` permits traversal. Therefore an elevated, ground-level, or underground segment may cross another segment at the same X/Y without creating a route between them.

## Stable IDs

The following entities have monotonic unsigned 64-bit stable IDs:

- `TrackNodeId`
- `TrackSegmentId`
- `TrackConnectionId`
- `BlockSectionId`
- `StationId`
- `PlatformId`
- `PlatformAccessPointId`
- `DepotId`

ID value zero is invalid. Checkpoint and Save Data preserve both entity IDs and each next-ID counter so restored worlds continue the same identity sequence.

## Track nodes and segments

`TrackNode` contains a 3D `WorldPoint` and a node kind:

- `Endpoint`: may have one incident segment.
- `Junction`: may join multiple segments.
- `Switch`: may join multiple segments and represents switch topology intended for operations.

`TrackSegment` contains:

- start/end `TrackNodeId`
- direction: bidirectional, start-to-end, or end-to-start
- gauge in metres
- speed limit in metres/second
- electrification: none, overhead, or third rail
- usage: mainline, siding, or depot

Gauge and speed limit must be finite and greater than zero. A segment cannot connect a node to itself.

## Traversable connections

`TrackConnection` is the authoritative transition from one segment to another through a declared Junction or Switch node. Both segments must be incident to the via node and their direction contracts must allow arrival from the source and departure onto the destination.

Connections are directed. Bidirectional junction traversal is represented by two explicit connections when both directions are intended.

## Block sections

A `BlockSection` contains one or more TrackSegment IDs. A TrackSegment belongs to at most one block section. Phase 17 defines only the static separation boundary; occupancy and reservation are Phase 18 state.

1つの`BlockSection`が保持できるTrackSegment membershipは最大100,000件とする。この上限はSimulation mutation、Checkpoint / Save復元の双方で適用し、Protocol 2.6の単一BlockSection itemが1 MiB payload上限内へ必ず収まることを保証する。

## Stations and platforms

A `Station` owns a 3D `WorldVolume` and stable ID. A `Platform` references exactly one Station and one TrackSegment, has a normalized `[startSegmentOffset, endSegmentOffset]` interval on that segment, and owns its own 3D bounds.

Platform offsets satisfy `0 <= start < end <= 1`.

## Pedestrian access

`PlatformAccessPoint` joins a Platform to an existing `RoadAccessPoint`. The referenced RoadAccessPoint must permit `RoadAccessMode.Foot`. `FindWalkingRouteToPlatform` evaluates the Platform's pedestrian access points through the existing pedestrian network and returns the shortest reachable walking route.

This keeps railway infrastructure independent from pedestrian pathfinding while giving multimodal phases a stable interchange contract.

## Depot and siding

A `Depot` owns a 3D volume and one or more TrackSegments. Depot membership accepts siding/depot usage segments and rejects mainline-only segments. Phase 17 models infrastructure only; train storage and entry/exit lifecycle belong to Phase 18.

1つの`Depot`が保持できるTrackSegment membershipも最大100,000件とする。BlockSectionと同様に、CreateとCheckpoint / Save復元の両境界で検証する。

## Spatial query and validation

`CreateRailwayInfrastructureSnapshot(WorldVolume)` returns the railway entities relevant to the requested 3D volume. Segment selection is based on 3D segment bounds, and dependent Station/Platform/Depot entities are included when their geometry or referenced track is selected.

`ValidateRailwayInfrastructure()` reports connected-component count and traversable-connection count using explicit TrackConnections, not geometry intersections. This rule is intentionally identical for same-Z crossings and grade-separated crossings: without shared nodes and declared connections, they are disconnected.

## Persistence

Save Format 8 adds all railway entities and next-ID counters. Formats 3 through 7 remain readable and migrate to an empty railway infrastructure with all railway next IDs initialized to 1.

All railway collections remain subject to bounded deserialization and materialization limits. Save Data stores raw numeric values and enum codes, never localized presentation strings. BlockSection / Depot membershipの100,000件上限はrestore validationにも適用し、Protocolでは送信不能な正当stateをSaveから導入しない。

## Protocol and server distribution

Protocol 2.6 adds message type `700`, `RailwayInfrastructureSnapshot`. It carries revision, full-snapshot flag, TrackNodes, TrackSegments, TrackConnections, BlockSections, Stations, Platforms, PlatformAccessPoints, and Depots.

The Server treats railway topology as static infrastructure with a revision. It sends a filtered snapshot when a client subscribes, when the subscription volume changes, or when the railway revision changes; it does not resend identical static topology every simulation tick.

Railway Infrastructure snapshot全体が1 MiBを超える場合は複数frameへchunkするが、1つのBlockSection / Depot item自体は分割しない。Simulation側membership上限により各itemは必ず1 frameへ収まり、正当なauthoritative stateが`RailwayInfrastructureProtocolChunker`のsingle-item overflowでpublisherをfaultさせない。

## Web rendering

The Web Client negotiates Protocol 2.6 and decodes RailwayInfrastructureSnapshot independently of older dynamic message codecs. TrackSegments render as Three.js line segments using world altitude. Station and Platform volumes render as 3D wireframes. Static geometry is rebuilt only when the railway revision changes.

## Deterministic validation fixture

The Phase 17 deterministic fixture includes:

- parallel/double-track infrastructure
- an explicit branch/junction
- Station and Platform
- pedestrian PlatformAccessPoint
- siding/depot track
- elevated and underground tracks
- same-XY crossings that remain disconnected unless an explicit shared topology exists

The Phase 17 Save→Server→Browser E2E loads a Format 8 fixture, negotiates Protocol 2.6, verifies railway entity counts and no implicit grade-separated connection, then checks Track/Station/Platform geometry in a real headless browser.