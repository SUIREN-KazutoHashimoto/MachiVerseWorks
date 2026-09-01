# Optical Communication Specification

## Purpose

Phase 26 adds fixed optical communication to the authoritative Simulation. The required observable behavior is connectivity, requested and allocated bandwidth, congestion, degraded service, outage, and recovery across city entities. Optical loss, wavelength assignment, chromatic dispersion, modulation, and other physical-layer calculations are not part of the standard completion criteria.

## Stable entities

- `OpticalNode`: stable ID, kind, and 3D position.
- `FiberCable`: stable ID, two node references, Gbit/s capacity, current load, service state, utilization, and congestion state.
- `OpticalEquipment`: OLT, ONU, splitter, switch, or router attached to a node; may reference Building/Establishment and may require Power.
- `OpticalBackhaul`: stable external-backbone boundary with Gbit/s capacity and allocation.
- `OpticalDemand`: Building, Office, DataCenter, or RadioBackhaul demand with stable ID, requested/allocated bandwidth, quality state, selected backhaul, and route cable IDs.

All IDs remain stable through checkpoint/save/restore. References to missing nodes, buildings, establishments, cables, or backhauls are invalid.

## Demand and capacity

Bandwidth is measured in Gbit/s. Base demand is multiplied by deterministic city activity rules derived from Building use and Establishment/Industry state. Radio backhaul is represented as a generic demand kind so Phase 28 can reference Phase 26 without coupling Phase 26 to a specific radio technology.

The default solver reserves capacity at the endpoint, each traversed FiberCable, and the selected external backhaul. Demands are processed deterministically by priority then stable ID. A route with no positive residual bottleneck is unavailable.

## Quality

- `Healthy`: requested bandwidth is available without congestion.
- `Congested`: service is allocated but at least one relevant capacity is at or above the standard 85% utilization threshold.
- `Degraded`: some requested bandwidth is allocated but the full demand cannot be served.
- `Unavailable`: no usable bandwidth is allocated.

Fiber cuts and stopped equipment affect the next tick's route calculation. A redundant path is selected automatically when available. Power-dependent equipment is non-operational while its linked city entity lacks Power service and recovers automatically after power restoration.

## Persistence and distribution

Optical checkpoint state is optional within Save Format 11 so pre-Phase26 saves remain compatible. Protocol 2.15 message type 780 distributes bounded debug snapshots through the existing serialized WebSocket send path. The Web client exposes topology, cable utilization/service state, equipment state, backhaul allocation, demand quality, congestion, and outage.

## Verification criteria

The implementation must provide deterministic unit coverage for routing, capacity, fiber failure/reroute, power outage, 3D query, and checkpoint restore; Protocol 2.15 encode/decode coverage; Server-to-Browser E2E covering congestion, reroute, outage, and recovery; and 1,000/5,000 demand benchmark coverage.
