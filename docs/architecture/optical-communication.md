# Optical Communication Infrastructure

Phase 26 models fixed optical communication as a deterministic, capacity-constrained city infrastructure domain. The standard simulation intentionally models topology, routing, bandwidth allocation, congestion, equipment availability, power dependency, and failures rather than physical-layer optical loss or dispersion.

## Scope and units

`OpticalNode` represents stable 3D network points. `FiberCable` is an undirected capacity edge measured in Gbit/s. `OpticalEquipment` represents OLT/ONU, splitter, switch, and router capacity at nodes. `OpticalBackhaul` is the boundary between the simulated city network and external backbone capacity. `OpticalDemand` represents Building, Office, DataCenter, or RadioBackhaul traffic, also measured in Gbit/s.

The standard solver uses deterministic topology routing and bottleneck capacity. Detailed propagation loss, wavelength planning, dispersion, modulation, FEC, and other transmission physics are intentionally outside the standard solver and can be introduced behind `IOpticalRoutingSolver` or a future higher-fidelity extension.

## Routing and quality

`CapacityOpticalRoutingSolver` processes demands by stable priority and ID order. It finds deterministic shortest available paths, reserves endpoint, cable, and external backhaul capacity, and records the selected backhaul and cable route. Equal candidates are resolved using stable IDs so checkpoint replay remains deterministic.

Fiber utilization at or above `0.85` is considered congested. Demand quality is exposed as `Healthy`, `Congested`, `Degraded`, or `Unavailable`. A fiber cut removes that edge from the next routing pass; redundant topology can therefore reroute without replacing IDs. If no usable route remains, allocated bandwidth becomes zero and the demand becomes unavailable.

## Power and city entities

Active optical equipment can reference a Building or Establishment and can require power. The optical step reads the existing Power Infrastructure availability boundary; an unpowered OLT/ONU/router/switch becomes non-operational and therefore removes its node capacity from routing. Passive splitters can be configured without a power requirement.

Building and Establishment references reuse existing city entity IDs. Building use and establishment activity provide the deterministic demand multiplier used by the default demand rule. `RadioBackhaul` demand intentionally has no Phase 27 radio-type dependency, providing a stable boundary for future radio sites and base stations.

## Persistence and protocol

Optical state is included as the optional `OpticalCheckpoint` within the economy/infrastructure checkpoint tree so older saves without optical state remain readable. Stable next-ID values, topology, equipment/backhaul state, demand allocation, and selected routes are restored.

Protocol 2.15 adds `OpticalSnapshot` message type 780. Server snapshots expose topology, capacity/load/utilization, equipment power/operating state, external backhaul allocation, demand allocation, and quality state. The browser decoder rejects older protocol versions and the debug overlay visualizes fiber service/congestion plus demand quality.

## Verification

Simulation tests cover deterministic route selection, redundant fiber rerouting, congestion, power-dependent outage, 3D query, and checkpoint/stable-ID restore. Protocol tests cover 2.15 round-trip and 2.14 rejection. The Phase 26 browser E2E observes connected service, congestion, primary fiber failure with alternate-route traffic, complete outage, and recovery. `OpticalBenchmarks` records 1,000 and 5,000 load routing/tick/snapshot costs.
