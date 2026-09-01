# Radio & Spectrum Foundation Architecture

Phase 28 owns technology-neutral RF state inside `MachiVerseWorks.Simulation` and exposes bounded diagnostic snapshots through Protocol / Server / Web. Application technologies consume this foundation rather than placing LTE/5G/Wi-Fi-specific state into the Radio core.

## State ownership

`SimulationWorld` owns the mutable Radio stores and stable-ID indexes for sites, antennas, transmitters, receivers, emissions, links, peers, infrastructure bindings, spectrum bands/channels, and derived propagation state. The Web Client and Protocol contain snapshots only; they never become authoritative stores.

The implementation retains the earlier site-to-site `RadioLink` creation overload as a compatibility path, while the Phase 28 canonical path is:

`SpectrumBand -> RadioChannel -> RadioSite -> RadioAntenna -> RadioTransmitter -> RadioEmission -> RadioReceiver -> RadioLink`

Explicit link bindings retain the `EmissionId` and `ReceiverId` so recalculation can reconstruct current antenna gain, channel, operational state, obstruction, and interference rather than freezing those values at creation time.

## Solver boundary

`IRadioPropagationSolver.Solve(RadioPropagationRequest)` is the propagation-provider boundary. The request carries technology-neutral geometry, frequency block, link budget, interference/noise, and obstruction loss. `RadioPropagationResult` returns distance, path loss, received power, interference, SINR, and reachability.

`DeterministicRadioPropagationSolver` is the standard lightweight implementation. It calculates free-space path loss, deterministic frequency-dependent attenuation, optional `IRadioPathCorrection`, externally determined obstruction loss, received power, combined noise/interference, SINR, and sensitivity/fade-margin reachability.

Building lookup, Radio entity ownership, candidate selection, and infrastructure availability intentionally remain outside the solver. A future ray-tracing or terrain/material solver can therefore replace propagation without taking ownership of Simulation stores or technology-specific scheduling.

## Antenna geometry

A Radio Site supplies the base 3D World position. Each antenna stores a local `WorldVector` offset and normalized orientation. `SimulationWorld.Radio.Entities` derives the absolute antenna point and a simple deterministic pattern gain toward the other endpoint.

Omnidirectional and directional pattern contracts are deliberately small. Detailed arrays, polarization, MIMO, beam codebooks, and measured pattern assets belong behind future extension/provider boundaries.

## Spatial candidate index

`RadioEmissionSpatialIndex` is a 3D grid keyed by `SpatialCell`. Operational emission IDs are registered using the current transmitter antenna position. A receiver query computes the cells intersecting its bounded candidate radius, merges the IDs deterministically, then filters by exact distance, operational state, and receive-frequency compatibility.

This index is the standard boundary for interference/candidate discovery; the hot path must not enumerate all transmitters or emissions. The index itself is derived state and is rebuilt from checkpoint-owned entities on restore.

## Propagation and interference flow

For an explicit Radio link, recalculation performs the following deterministic flow:

1. Resolve the bound emission, transmitter, transmitter antenna, receiver, and receiver antenna.
2. Check Site/Antenna/Tx/Rx/Emission service state plus Power/Optical infrastructure availability.
3. Derive 3D endpoint positions and antenna directional gains.
4. Query overlapping operational emissions through the spatial candidate index.
5. Solve each interfering path using the same propagation boundary without recursively adding interference.
6. Sum interfering powers in the linear domain and convert the total back to dBm.
7. Intersect the desired path with Building `WorldVolume`s to derive obstruction / NLoS loss.
8. Solve the desired path and classify the resulting SINR/reachability into the technology-neutral `RadioLinkState`.

Stable-ID ordering is used when collection order could otherwise affect reproducibility.

## Building obstruction

The Simulation World owns Building geometry, so Radio does not maintain duplicate RF obstacles. The standard obstruction calculation tests the transmitter-receiver segment against Building axis-aligned `WorldVolume` bounds. Each intersecting obstruction contributes the defined penetration loss and the obstructed path receives the standard NLoS penalty.

This is intentionally an inexpensive deterministic approximation. Reflection, diffraction, terrain, material databases, and multipath are not added to the shared Radio store.

## Power and Optical integration

`RadioSiteInfrastructureBinding` references existing `BuildingId` and/or `OpticalBackhaulId`. `IsRadioSiteOperational` reads the existing Power availability boundary for a power-dependent Building and the current Optical backhaul operational snapshot. It does not write to either infrastructure domain.

Radio step ordering follows the Simulation infrastructure lifecycle so dependency changes become visible to Radio calculation deterministically. An unavailable dependency makes bound Tx/Rx/Emission and resulting links non-operational; restoration requires no Radio ID replacement.

## Persistence

Radio checkpoint state is stored in the existing infrastructure/economy checkpoint extension chain. The checkpoint includes next-ID counters, spectrum/channel state, explicit Radio entities, legacy links/peers, entity-link bindings, and site infrastructure bindings. Derived propagation results and the spatial grid can be recalculated/rebuilt from authoritative checkpoint state.

Validation runs before restore and checks stable-ID uniqueness, next-ID monotonicity, finite engineering values, references, channel/band relationships, antenna geometry, transmitter limits, receiver ranges, link bindings, and infrastructure references.

`WorldVector` uses explicit JSON construction so antenna offset/orientation XYZ survives Save Data serialization rather than depending on default struct construction behavior.

## Protocol and Server publish

Protocol 2.16 owns message types 790 (`RadioSnapshot`) and 791 (`SpectrumSnapshot`). `RadioMessageMapper` converts Simulation snapshots to bounded protocol records and limits debug entries before serialization. `RadioPublishService` uses the same connection registry / serialized send path as other Server snapshots and only sends Radio frames to negotiated versions supporting Radio.

The wire contract exposes diagnostic state only. It does not expose Simulation dictionaries, spatial-index internals, solver objects, or writable Radio state.

## Web boundary

`radio-protocol.ts` detects and validates 2.16 Radio/Spectrum frames before generic decoding. `connection.ts` dispatches Radio frames as a specialized protocol family. `RadioDebugOverlay` renders a bounded 2D diagnostic projection: sites, service areas, links, link health/utilization, directional antenna vectors, channel summary, and spectrum conflict counts.

The overlay is disposable derived UI. Closing, hiding, zooming, or not subscribing a Web Client never changes authoritative propagation or Radio tick behavior.

## Verification and performance

Simulation tests cover obstruction, interference, candidate filtering, Power/Optical failure, checkpoint restoration, and stable IDs. Persistence tests cover Save Data continuation, Protocol tests cover the 2.16 binary contract, and the Phase 28 Server-to-Browser E2E observes full Radio/Spectrum snapshots plus outage/recovery and debug rendering.

`RadioBenchmarks` separately records large spatial candidate queries and propagation solver evaluation. Benchmark code is diagnostic and is not linked into the authoritative tick path.
