# Persistent Regional & Settlement Evolution Specification

## Scope

Phase 31 keeps the regional world created by Phase 30 authoritative after initialization. Settlement, Parcel, Building, Population, Economy, accessibility and regional relationships continue to change as Simulation time advances. Rendering distance does not change the Simulation rules.

## Time model

- The default regional evolution cadence is one Simulation year per `EconomyDefaults.TicksPerEconomicDay * 365` ticks.
- Tests and controlled scenarios may configure a different `TicksPerYear` without changing the rule set.
- Long jumps are processed year-by-year in stable order so intermediate construction, mobility and history are not skipped.
- Every derived observation carries the Simulation tick and every historical event carries a stable event id and Simulation year.

## Authoritative boundaries

- `RegionalGenerationSnapshot` is the Phase 30 initial condition.
- `PersistentRegionalEvolutionSnapshot` is the authoritative long-lived regional state for Settlement classification/influence, Parcel demand, Building lifecycle, service catchments, infrastructure demand and regional relations.
- Existing `SimulationWorld` Building/POI, Household/Person, Economy/Job and Logistics entities remain authoritative for their own domains. Phase 31 materializes regional decisions into those existing entities rather than replacing them with a second simplified simulation.
- `RegionalInteractionSnapshot` derives commuting and freight dependencies from actual Employment and Logistics state.
- Gateway and View receive detached read-only Protocol payloads and must not infer semantic regional state.

## Settlement model

Settlement scale is derived from population, jobs, service index, density and accessibility. The stable scale sequence is Hamlet, Village, Town, City and Metropolis. Scale is never an immutable creation-time type.

Center, influence radius, service catchments and regional relations are recalculated from current state. Settlements may grow, stabilize, decline, recover or become dormant. A dormant settlement keeps its stable id and history.

A new settlement may emerge around real Building/Population/Job concentration outside the strong influence of existing settlements when population, jobs and connectivity pass the common emergence rule.

## Population and economy feedback

Regional drivers use current employment utilization, establishment capacity, local population/jobs, accessibility and regional quality. Growth regions can materialize new households and persons in newly developed residential capacity. Households in declining/dormant settlements may relocate to a materially more attractive active settlement while retaining stable Household/Person ids.

Actual employment assignments are used to derive cross-settlement commuting flows. Actual Logistics shipments are used to derive cross-settlement freight flows by commodity, shipment count, quantity and delivered quantity.

## Parcel and Building lifecycle

Parcel development demand is recomputed from current Settlement population/jobs/accessibility/services and land value. High-demand vacant parcels may materialize a real Building and optional POI, road access and economic capacity.

Building lifecycle tracks built year, condition, occupancy, use, capacity and status. Aging can produce vacancy, abandonment, renovation and demolition state transitions. Demolished lifecycle entries release their Parcel back to a vacant state so later demand can produce redevelopment, while high-demand low-occupancy buildings may be repurposed. Major transitions are recorded as stable regional evolution events.

## Services, infrastructure and polycentric relations

Commerce, education and medical service catchments are derived per active Settlement. Road, transit and utility demand signals are derived from population, jobs, density, services and accessibility.

Settlement relations are derived from proximity, functional complementarity and accessibility. Commuting, trade, service and metro relations may form or disappear over time. In addition, `RegionalPolycentricInteractionRules` evaluates every active Settlement pair using the same deterministic rule and exposes competition, complementarity and specialization strengths plus a dominant interaction mode. Pair ordering is stable by Settlement id and no privileged global center is selected, so urban, suburban, rural and remote settlements can remain distinct centers with different functions.

## Persistence and observation

Persistent regional state and event history are included in Simulation checkpoint/Save Data limits. Protocol 2.19 adds `PersistentRegionalEvolutionSnapshot`, including regional state plus authoritative commuting/freight flows. Older negotiated protocol versions do not receive this payload.

## Determinism and limits

All collections with semantic ordering use stable ids/order. Annual transitions are deterministic for identical initial state and drivers. Checkpoints preserve the current year, evolution state and history. Bounded collection limits protect Save Data and Protocol payloads.
