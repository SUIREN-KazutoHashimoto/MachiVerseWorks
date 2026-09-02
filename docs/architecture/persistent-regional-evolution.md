# Persistent Regional Evolution Architecture

## Data flow

```text
Phase 30 RegionalGenerationSnapshot
        |
        v
PersistentRegionalEvolutionEngine
        |
        +--> SettlementEvolutionState
        +--> ParcelEvolutionState
        +--> BuildingLifecycleState
        +--> ServiceCatchment / InfrastructureDemandSignal
        +--> RegionalRelation / RegionalEvolutionEvent
        |
        v
SimulationWorld.PersistentRegionalMaterialization
        |
        +--> Building / POI / RoadAccessPoint
        +--> Household / Person relocation or creation
        +--> Company / Establishment / Job
        |
        v
Existing domain systems (Population / Economy / Logistics / Transport)
        |
        v
RegionalInteractionSnapshot (commuting / freight)
        |
        v
IObservationSource -> Protocol 2.19 -> read-only Gateway clients
```

## Ownership

`SimulationWorld` is the only semantic authority. Phase 31 does not introduce a Gateway-side regional model and does not use camera/rendering LOD to decide which settlements receive simulation rules.

The persistent regional engine owns slow regional transitions and derived classification/influence. Existing domain stores continue to own concrete people, jobs, buildings, POIs, roads and shipments. The materialization layer is the bridge between those boundaries.

## Scheduling

`SimulationWorld.Step()` computes the target regional year from tick count. Missing years are processed one at a time. This preserves event order and avoids a large time jump bypassing construction, relocation or relation transitions. Phase 32 may later optimize scheduling, but must preserve this semantic result.

## Persistence

`EconomyCheckpoint` carries an optional `PersistentRegionalEvolutionCheckpoint`, keeping compatibility with checkpoints created before Phase 31. Save validation bounds all major collections. Restore validates ids, references, time and unit-range values before reactivating state.

## Observation

`CreatePersistentRegionalEvolutionSnapshot()` and `CreateRegionalInteractionSnapshot()` return detached data. `SimulationObservationSource` captures both under the Simulation runtime lock. Protocol 2.19 gates publication, so clients negotiated to 2.18 or older never receive the new message.

## Deterministic ordering

Settlement, Parcel, Building, relation, flow and event projections are sorted by stable identifiers. Regional event ids are monotonic. Multi-year advancement is decomposed into deterministic one-year transitions. No wall-clock time or random source is used by the evolution engine.

## Scaling boundary

All regions use the same authoritative model. A remote settlement is not replaced by an aggregate-only alternative. Performance work belongs to Phase 32 and may skip work only when dependencies/next-event state prove that no semantic change can occur.
