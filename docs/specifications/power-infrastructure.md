# Power Infrastructure Specification

## Scope

Phase 23 introduces an authoritative, deterministic power domain that connects generation, transmission/distribution capacity, Building / Establishment demand, and outage state to the existing city simulation.

The standard simulation intentionally models connectivity and capacity rather than detailed electrical physics. Voltage, reactive power, frequency stability, protection coordination, AC/DC load flow, and other high-fidelity calculations remain outside the standard domain and can be supplied through the solver boundary.

## Authoritative entities

### PowerNode

`PowerNodeId` is a stable unsigned 64-bit identifier. A node has a `PowerNodeKind` and a finite world position.

Kinds:

- `GeneratorBus`
- `Substation`
- `Distribution`
- `Load`

### PowerLine

`PowerLineId` is stable. A line connects two distinct existing `PowerNode` values and has:

- positive finite transfer capacity in MW
- `IsInService`

The standard solver treats an in-service line as a bidirectional transfer edge with the configured capacity in each direction. This is a simplified network-capacity contract, not a physical AC/DC line-flow model.

### Generator

`GeneratorId` is stable. A Generator references an existing `PowerNode` and has:

- positive finite capacity in MW
- dispatched output in MW
- `Online` / `Offline` operating state

Offline generators expose zero available capacity to the standard dispatch solver.

### PowerLoad

`PowerLoadId` is stable. A Load references an existing `PowerNode` and at least one of:

- `BuildingId`
- `EstablishmentId`

When an Establishment has a Building, the relation is validated so conflicting Building references cannot be created.

A Load stores:

- base demand in MW
- current calculated demand in MW
- served demand in MW
- unserved demand in MW
- `Supplied`, `Constrained`, or `Outage` state

## Demand rule

For each simulation tick, current demand is recalculated before Economy processing.

The standard rule is:

`Demand = BaseDemand × TimeFactor × BuildingUseFactor × IndustryFactor × ActivityFactor`

### Time factor

The factor uses elapsed simulation time modulo 24 hours:

| Time | Factor |
| --- | ---: |
| 00:00–05:59 | 0.55 |
| 06:00–08:59 | 0.80 |
| 09:00–16:59 | 1.00 |
| 17:00–21:59 | 0.90 |
| 22:00–23:59 | 0.65 |

### Building use factor

| Building kind | Factor |
| --- | ---: |
| Residential | 0.80 |
| Commercial | 1.10 |
| Industrial | 1.25 |
| Civic | 1.05 |
| MixedUse | 1.00 |
| Other | 0.90 |

### Industry factor

When a Load references an Establishment, its Company sector contributes another factor:

| Sector | Factor |
| --- | ---: |
| Manufacturing | 1.35 |
| Retail | 1.10 |
| Transport | 1.15 |
| Public | 1.05 |
| Other | 1.00 |

### Activity factor

For an Establishment with Jobs, staffing utilization is calculated from filled workers divided by required workers and clamped to `[0, 1]`.

`ActivityFactor = 0.60 + 0.40 × StaffingUtilization`

An Establishment with no Jobs uses `0.75`. A Building-only Load uses `1.00` for the activity factor.

## Dispatch and solver boundary

`IPowerDispatchSolver` is the replacement boundary between authoritative Power state and supply calculation.

Input contains only raw Power domain values:

- nodes
- lines and capacities / service state
- generators and available capacity
- loads and current demand

Output contains:

- Generator dispatched output
- Load served demand

The default `CapacityPowerDispatchSolver` performs deterministic capacity-constrained maximum flow. Inputs are processed in stable ID order so the same state produces the same dispatch result.

Custom solvers can be injected when constructing `SimulationWorld`. They must return finite, non-negative values that do not exceed Generator capacity or Load demand.

## Outage state

After dispatch:

- `Supplied`: unserved demand is effectively zero.
- `Constrained`: some demand is served but some remains unserved.
- `Outage`: served demand is effectively zero while demand is positive.

`PowerStatistics` exposes generation capacity/output, demand, served demand, unserved demand, and outage Load count.

## Building and Industry effects

`IsBuildingPowered` and `IsEstablishmentPowered` expose whether an associated demand set receives any supply.

Economy production is calculated normally first, then the production delta for the current economic cycle is scaled by the weighted Power availability of the Company's Establishments. Establishments with no configured Power Load retain availability `1.0`, preserving behavior for worlds and Save Data created before Phase 23.

The initial Phase 23 operational effect is intentionally limited to Industry production. Future phases can consume the same availability contract for additional Building services without coupling those systems to the standard Power solver implementation.

## Persistence

Power state is stored as optional `economy.power` checkpoint data in Save Format 11. The stored state includes:

- next stable IDs
- nodes
- lines and service state
- generators, output, and operating state
- loads, calculated demand, served/unserved demand, and supply state

Because the new property is optional, existing Save Format 11 data without Power state restores as an empty Power domain. Collection counts are validated before materialization and again through simulation checkpoint validation.

## Protocol and Server

Protocol 2.12 adds `PowerSnapshot` (`MessageType 750`).

The snapshot contains aggregate statistics plus bounded debug entries for:

- PowerNode
- PowerLine
- Generator
- PowerLoad

The Server sends Power snapshots only to connections that negotiated Protocol 2.12 or newer within the supported 2.x line. Protocol 2.11 and older connections continue without receiving Power frames.

## Web debug visualization

The Web Client decodes Protocol 2.12 Power snapshots and displays:

- generation output / capacity
- demand / served / unserved MW
- outage Load count
- a simple SVG network view of nodes and lines
- offline lines and generators
- constrained and outage Loads

This is a diagnostic visualization, not a city-management editing UI.

## Determinism and compatibility

- IDs are stable and monotonically allocated.
- default dispatch is ordered by stable IDs.
- Power tick runs before Economy for the same next simulation time.
- missing Power configuration means full operational availability for legacy domains.
- Save Format remains 11 because the nested Power property is optional and backward compatible.
- Protocol advances independently to 2.12 because a new wire message is introduced.
