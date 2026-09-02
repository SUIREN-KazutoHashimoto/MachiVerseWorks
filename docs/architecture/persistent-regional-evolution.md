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
        +--> actual Building distribution / Road / Railway connectivity
        +--> Employment-based commuting / Logistics-based freight
        |
        v
Spatial feedback + territory + regional relation recalculation
        |
        +--> dynamic Settlement center / accessibility / scale
        +--> derived SettlementTerritorySnapshot
        +--> Commuting / Trade / Service / Metro relation
        +--> Competition / Complementarity / Specialization profile
        |
        v
IObservationSource -> Protocol 2.19 -> read-only Gateway clients
```

## Ownership

`SimulationWorld` is the only semantic authority. Phase 31 does not introduce a Gateway-side regional model and does not use camera/rendering LOD to decide which settlements receive simulation rules.

The persistent regional engine owns slow regional transitions and derived classification/influence. Existing domain stores continue to own concrete people, jobs, buildings, POIs, roads and shipments. The materialization layer is the bridge between those boundaries.

Settlement territory is a derived Simulation-side projection, not a second mutable boundary. It is recalculated from the current Settlement center, current influence radius and neighboring Settlement distances. Regional interaction profiles are likewise read-only derived semantics over the current authoritative Settlement state.

## Annual transition order

Each annual transition uses a stable semantic order:

1. Advance the persistent regional engine by one year.
2. Apply lifecycle redevelopment and release demolished parcels for later development.
3. Materialize new Building / POI / Population / Economy capacity and apply Household mobility / Settlement emergence.
4. Recalculate Settlement centers and accessibility from actual Building, Road and Railway state.
5. Recalculate regional relations from actual Employment commuting, Logistics freight, service catchments and continuous urban proximity while preserving active relation IDs / `SinceYear` where the relation kind remains stable.
6. Record formed/ended regional relation events after the authoritative relation set is known.

This order prevents relation history from being recorded against an intermediate pre-materialization state.

## Scheduling

`SimulationWorld.Step()` computes the target regional year from tick count. Missing years are processed one at a time. This preserves event order and avoids a large time jump bypassing construction, relocation or relation transitions. Phase 32 may later optimize scheduling, but must preserve this semantic result.

## Persistence

`EconomyCheckpoint` carries an optional `PersistentRegionalEvolutionCheckpoint`, keeping compatibility with checkpoints created before Phase 31. Save validation bounds all major collections. Restore validates ids, references, time and unit-range values before reactivating state.

## Observation

`CreatePersistentRegionalEvolutionSnapshot()` and `CreateRegionalInteractionSnapshot()` return detached data. `SimulationObservationSource` captures both under the Simulation runtime lock. Protocol 2.19 gates publication, so clients negotiated to 2.18 or older never receive the new message.

`CreateSettlementTerritorySnapshot()` and `CreateRegionalInteractionProfileSnapshot()` expose additional deterministic Simulation-side derived projections without giving Gateway or View responsibility for semantic inference.

## Deterministic ordering

Settlement, Parcel, Building, relation, territory, interaction profile, flow and event projections are sorted by stable identifiers. Regional event ids are monotonic. Active regional relation IDs and `SinceYear` are retained when the pair and relation kind remain unchanged. Multi-year advancement is decomposed into deterministic one-year transitions. No wall-clock time or random source is used by the evolution engine.

## Scaling boundary

All regions use the same authoritative model. A remote settlement is not replaced by an aggregate-only alternative. Performance work belongs to Phase 32 and may skip work only when dependencies/next-event state prove that no semantic change can occur.
