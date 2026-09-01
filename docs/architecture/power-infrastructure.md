# Power Infrastructure Architecture

## Responsibility boundary

Phase 23 keeps authoritative city state in `MachiVerseWorks.Simulation` and treats electrical calculation as a replaceable policy.

```text
Building / Establishment
        |
        v
     PowerLoad ---- demand rule
        |               |
        v               v
PowerNode / PowerLine / Generator
        |
        v
IPowerDispatchSolver
        |
        v
served / unserved demand
        |
        +----> Building / Establishment availability
        |
        +----> Industry production scaling
```

Simulation owns IDs, references, lifecycle, checkpoint validation, and the resulting operational state. A solver does not own entities and cannot mutate the World directly.

## Tick order

The relevant `SimulationWorld.Step` sequence is:

1. advance the next simulation time
2. move generic Agents
3. capture Company production baselines
4. calculate Power demand and run dispatch
5. run Economy
6. scale newly produced Industry output by Power availability
7. run Logistics
8. plan trips and execute transport domains
9. commit the next SimulationTime

Running Power before Economy means production for a tick observes a single completed Power state. The production baseline isolates the power effect to newly produced units instead of repeatedly rescaling historical cumulative production.

## Authoritative storage

`SimulationWorld.Power.cs` owns four ordered stores plus ID indexes:

- PowerNode
- PowerLine
- Generator
- PowerLoad

Each creation API validates referenced authoritative entities immediately. Checkpoint restore repeats structural validation before materializing state so malformed Save Data cannot create dangling Power references.

## Solver boundary

`IPowerDispatchSolver.Solve(PowerDispatchRequest)` receives immutable request data and returns Generator and Load dispatch values.

The default implementation, `CapacityPowerDispatchSolver`, builds a deterministic flow graph:

```text
super source
  -> Generator node edges (available generator capacity)
  -> in-service PowerLine edges (transfer capacity)
  -> Load node edges (demand)
  -> super sink
```

PowerLine edges are represented in both directions. Dinic-style maximum flow is used as a small, deterministic capacity solver. Node, line, Generator, and Load processing order is stable-ID order.

The standard solver deliberately does not model voltage, impedance, reactive power, losses, phase angle, frequency, protection, or contingency analysis. A future high-fidelity implementation can replace the solver while reusing authoritative IDs, Save Data, Protocol snapshots, and domain integration.

## Cross-domain coupling

Power does not call Logistics, Routing, Traffic, or Web code.

The Economy integration consumes only an availability factor derived from PowerLoad served/demand values. When no Load is configured for a Building or Establishment, the availability factor is `1.0`. This allows existing worlds and tests to continue unchanged while making Power an opt-in operational constraint.

The first consumer is Industry production. Additional domains should depend on a similarly small availability contract rather than on `CapacityPowerDispatchSolver` or its graph internals.

## Persistence architecture

Power checkpoint data is nested under the existing optional Economy checkpoint:

```text
WorldSaveData
  simulation
    economy
      logistics?  // Phase 22
      power?      // Phase 23
```

Save Format 11 is retained because adding the optional `power` property is backward compatible under the existing JSON contract. Old Format 11 payloads restore an empty Power domain.

Two validation layers remain in effect:

1. streaming nested collection-count scan before DTO materialization
2. semantic checkpoint validation before `SimulationWorld` restore

## Protocol distribution

```text
SimulationWorld.CreatePowerSnapshot
  -> PowerMessageMapper
  -> PowerPublishService
  -> ClientConnection.SendAsync
  -> PowerProtocolCodec (Protocol 2.12)
  -> WebSocket
  -> power-protocol.ts
  -> PowerDebugOverlay
```

Power publication is capability-gated by the negotiated Protocol version. This prevents a Protocol 2.11 connection from receiving an unknown frame.

The Server limits each debug entity category to 512 entries. Aggregate statistics remain authoritative for the complete Power domain even when detailed visualization is bounded.

## Web visualization

`PowerDebugOverlay` is a diagnostic layer independent of the main world rendering model. It converts authoritative world coordinates into a small SVG view and distinguishes:

- active / inactive PowerLine state
- online / offline Generator state
- supplied / constrained / outage Load state
- node roles

The overlay is intentionally read-only. Editing and city-management workflows belong to later management phases.

## Determinism

Determinism relies on:

- monotonic stable IDs
- stable-ID ordering before solver graph construction
- fixed-tick demand calculation
- explicit Generator / PowerLine operating state
- no wall-clock input in Simulation calculation
- checkpointing current demand, dispatch, outage state, and next IDs

The deterministic Phase 23 fixture uses simulation TickCount to create repeatable demand changes and Generator outages for Server-to-Browser E2E validation.
