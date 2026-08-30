# Railway Infrastructure Architecture

## Boundary

Railway infrastructure is authoritative Simulation state. It is intentionally separated from train-operation state so Phase 18 can consume a stable topology without owning or duplicating track geometry.

The data path is:

`SimulationWorld / RailwayInfrastructureStore` → `SimulationCheckpoint / Save Format 8` → `SimulationRuntime / RailwayInfrastructureReadModel` → `Protocol 2.6` → `Web RailwayInfrastructureLayer`.

## Simulation ownership

`SimulationWorld` exposes railway commands and snapshots while `Internal/RailwayInfrastructureStore` owns dictionaries, stable-ID counters, invariants, spatial filtering, checkpoint projection, and connectivity validation.

The store does not infer connectivity from geometry. `TrackConnection` is the only traversable segment-to-segment edge. This makes bridges, tunnels, stacked tracks, and same-level crossings representable without topology ambiguity.

Road/pedestrian integration occurs at `PlatformAccessPoint`: Simulation validates that the referenced `RoadAccessPoint` exists and permits foot access, then delegates walking route calculation to the existing pedestrian network.

`BlockSection`と`Depot`の可変長TrackSegment membershipはそれぞれ100,000件をhard limitとする。public mutationとCheckpoint validationで同じ上限を適用し、Protocol transportの制約より大きいauthoritative aggregateを作成・復元できないようにする。

## Persistence boundary

`SimulationCheckpoint` is the in-memory persistence contract. Format 8 projects railway records to JSON DTOs in `MachiVerseWorks.Persistence`, including every stable ID and next-ID counter.

Migration is one-way at load time: formats 3–7 produce empty railway collections and next IDs of 1. No older format is rewritten in place. Existing bounded-input checks are reused for railway arrays so hostile collection counts are rejected before unbounded materialization.

BlockSection / Depotの100,000件membership上限はCheckpoint restore boundaryでも検証されるため、Save DataからProtocol配信不能な単一aggregateをauthoritative stateへ導入しない。

## Server read model

`SimulationRuntime.CapturePublishSnapshot()` captures railway state under the same world lock as other authoritative publish data. A `RailwayInfrastructureReadModel` pairs the static snapshot with a revision number.

`SnapshotPublishService` applies each client's 3D subscription to the read model. The client connection retains the last sent railway revision/subscription state, so an unchanged topology is not serialized every tick. A new subscription or changed revision causes a fresh filtered snapshot.

This keeps the hot path proportional to client interest rather than world-wide railway size.

## Protocol boundary

Railway distribution uses a dedicated Protocol 2.6 codec rather than expanding the original generic message codec. Message type 700 contains all static Phase 17 railway entity kinds.

The decoder performs fixed-size/count checks before collection materialization and validates IDs, enum ranges, finite dimensions, normalized platform offsets, and volume ordering. Referential/topological correctness remains Simulation responsibility; the wire codec guarantees structurally valid transport data.

Snapshot全体が1 MiBを超える場合、`RailwayInfrastructureProtocolChunker`はentity境界で複数frameへ分割する。一方、BlockSection / Depot 1件のmembership自体は分割しない。Simulation側の100,000件上限によりBlockSectionは約0.8 MiB、Depotも約0.8 MiB以内へ収まり、single-item overflowを正当stateから発生させない。

## Web boundary

`connection.ts` negotiates Protocol 2.6 and dispatches message type 700 through `railway-infrastructure.ts`. `RailwayInfrastructureLayer` owns Three.js static geometry separate from the dynamic agent, pedestrian, and vehicle render paths.

Rendering maps Simulation `(x, y, z)` to Three.js `(x, z, y)` consistently with existing WorldView geometry. Tracks are `LineSegments`; Station and Platform `WorldVolume`s are 12-edge wireframes. The layer keys updates by railway revision and supports clear/dispose on reconnect/application shutdown.

## Tests and performance gates

Phase 17 validation is split by responsibility:

- Simulation tests: explicit topology, spatial crossing isolation, pedestrian Platform access, checkpoint restoration.
- Persistence tests: Format 8 roundtrip and Format 7 migration.
- Protocol tests: 2.6 roundtrip, minimum-version enforcement, malformed payload rejection.
- Web tests: binary decoding and actual Three.js geometry construction.
- E2E: a Format 8 Save is loaded by the Server and verified in a real browser through Protocol 2.6.
- Benchmark: 10k/100k TrackSegment spatial snapshot, full snapshot, and connectivity validation.

性能回帰は`.github/workflows/benchmarks.yml`の`railway-10k-100k` jobへ集約し、`benchmark-railway-infrastructure` artifactとして保存する。E2Eは`.github/workflows/e2e.yml`の`save-server-browser-railway` jobで継続検証する。

## Phase 18 extension points

Phase 18 should consume, not mutate the meaning of, these contracts:

- Train routes reference ordered TrackSegments/TrackConnections.
- Block occupancy references `BlockSectionId`.
- station stops reference `StationId`/`PlatformId`.
- depot lifecycle references `DepotId` and its member tracks.

Operational occupancy, switch position, signalling aspect, train position, service, timetable, and delay are deliberately excluded from Phase 17 infrastructure snapshots.