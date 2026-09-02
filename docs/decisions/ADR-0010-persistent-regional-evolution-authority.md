# ADR-0010: Persistent Regional Evolution remains Simulation-authoritative

- Status: Accepted
- Date: 2026-09-02

## Context

Phase 30 creates a deterministic polycentric initial regional world. Treating that output as immutable would prevent population movement, development, decline, redevelopment and changing regional dependencies. A second aggregate simulation for rural or remote areas would also create inconsistent semantics and make observation dependent on location or rendering detail.

## Decision

1. `SimulationWorld` owns all semantic regional evolution.
2. Phase 30 generation is an initial condition, not a permanent final state.
3. Settlement scale and influence are derived from current population, jobs, services, density and accessibility instead of immutable settlement types.
4. Urban, suburban, rural and remote settlements use the same evolution rules. Rendering/camera distance cannot change Simulation fidelity.
5. Slow regional decisions are materialized into existing authoritative Building/POI, Population, Economy and Logistics domains instead of maintaining disconnected duplicate entities.
6. Regional commuting/freight observations are derived from actual Employment and Logistics state.
7. Regional history uses stable ids and deterministic year ordering and is persisted in checkpoints/Save Data.
8. Gateway receives detached read-only state through `IObservationSource`. Protocol 2.19 capability-gates persistent regional evolution messages.
9. Workload optimization is deferred to Phase 32 and must preserve identical semantic outcomes.

## Consequences

### Positive

- Regional state cannot disagree with the concrete Simulation domains by design intent.
- Long-running worlds can grow, decline, recover and develop without regeneration.
- Remote settlements retain full semantic fidelity.
- Gateway/View do not need to infer settlement class, lifecycle or regional relations.
- Checkpoint restore and deterministic long-run tests can verify the full regional state transition sequence.

### Costs

- Cross-domain materialization requires careful reference validation.
- Long-run event history and world-scale simulation need explicit limits and later scheduling optimization.
- Protocol evolution is required when new authoritative regional fields are exposed.
