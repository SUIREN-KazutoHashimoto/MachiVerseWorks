# Gas Infrastructure

Phase 25 introduces a gas domain that represents piped gas and delivered gas with one observable demand/service model.

## Domain model

- `GasNodeId`, `GasPipelineId`, `GasSourceId`, `GasImportTerminalId`, `GasStorageId`, and `GasServicePointId` are stable IDs.
- Piped supply flows from a source, import terminal, or storage through directed pipelines. Pipeline capacity and outage state constrain the maximum flow.
- A service point references a building, an establishment, or both and exposes demand, served demand, unserved demand, and `GasServiceState`.
- `GasServiceState` is `Supplied`, `Constrained`, or `Unavailable` for both delivery modes.

## Delivered gas and logistics

Delivered gas reuses the Phase 22 logistics model instead of introducing a second freight system. A delivered-gas service point references an establishment consumer inventory whose commodity kind is `Gas`. The available inventory is converted to the same gas served/unserved state used by piped gas. Existing logistics replenishment orders and freight vehicles therefore determine whether delivered gas remains available.

## Economy integration

For an establishment with utility service points, production availability is the minimum of power, water/sewer, and gas availability. Gas shortages therefore reduce industrial production using the same operational-constraint boundary as the existing utility domains.

## Storage

Storage has a finite stock and release-rate limit. Piped dispatch can draw no faster than the release rate and no faster than the stock can sustain for the economic-day horizon. Dispatched storage volume is deducted gradually over `EconomyDefaults.TicksPerEconomicDay` ticks.

## Save/restore boundary

Gas state is embedded in `EconomyCheckpoint` after Logistics, Power, and Water/Sewer. The checkpoint preserves stable-ID counters, topology, facility state, storage stock, and the last observable service state.
