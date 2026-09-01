# Radio & Spectrum Foundation Specification

## Purpose

Phase 28 adds a technology-neutral Radio / Spectrum foundation to the authoritative Simulation. LTE, 5G, Wi-Fi, broadcast, satellite, public safety, and future radio systems may reuse the same foundation, but none of those application technologies is part of the authoritative Phase 28 model.

The standard observable behavior is spectrum occupancy, transmitter/receiver compatibility, antenna geometry, deterministic received power, obstruction loss, interference, SINR, service availability, infrastructure dependency, persistence, and bounded debug distribution. Full electromagnetic field simulation, reflection, diffraction, multipath, terrain/material ray tracing, protocol scheduling, modulation, coding, and technology-specific cell procedures remain outside the standard completion criteria.

## Units and coordinate contract

- Position and distance use the existing World coordinate system and meters.
- Frequency and bandwidth use MHz.
- Transmit/received/noise/interference power use dBm.
- Antenna gain and losses use dB.
- Antenna orientation is a normalized 3D `WorldVector`.
- Beamwidth uses degrees in `(0, 360]`.
- Utilization is a dimensionless value in `[0, 1]`.
- SINR uses dB.

Radio calculations never introduce a second coordinate system. Building obstruction uses the authoritative Building `WorldVolume` geometry.

## Stable entities

- `SpectrumBand`: stable ID, name, minimum frequency, and maximum frequency.
- `RadioChannel`: stable channel ID backed by the common frequency-block contract, center frequency, bandwidth, and parent spectrum band.
- `RadioSite`: stable ID, site kind, 3D position, service state, and optional infrastructure binding.
- `RadioAntenna`: stable ID, site reference, 3D offset/orientation, gain, radiation-pattern kind, beamwidth, front-to-back ratio, and service state.
- `RadioTransmitter`: stable ID, site/antenna references, maximum transmit power, service state, and derived operational state.
- `RadioReceiver`: stable ID, site/antenna references, supported receive range, sensitivity, service state, and derived operational state.
- `RadioEmission`: stable ID, transmitter/channel references, transmit power, utilization, service state, and derived operational state.
- `RadioLink`: stable result relationship between an emission and compatible receiver, including path loss, received power, interference, SINR, utilization, and link state.

All IDs and references survive checkpoint/save/restore. Missing references, invalid frequency ranges, non-finite engineering values, or emission power above transmitter capability are invalid state.

## Spectrum and compatibility

A channel must fit inside its parent `SpectrumBand`. Two channels/emissions overlap when their occupied frequency intervals overlap. A receiver is a candidate only when the channel interval fits its supported receive range.

Spectrum overlap alone does not imply a technology-specific collision rule. Phase 28 exposes deterministic overlap/conflict candidates and aggregate interference so later application layers can apply their own MAC, scheduler, reuse, or regulatory rules.

## Antennas and transmission

Omnidirectional antennas use a 360-degree pattern. Directional antennas expose normalized 3D orientation, beamwidth, and a bounded front-to-back penalty. The standard solver converts the simple pattern into a deterministic directional gain adjustment. Detailed antenna arrays, polarization, MIMO, beamforming, sidelobes, and measured pattern files are extension concerns.

An `Emission` carries the actual operating center frequency, bandwidth, transmit power, utilization, and service state through its channel/transmitter references. Transmit power is bounded by the transmitter maximum.

## Propagation, obstruction, and interference

`IRadioPropagationSolver` is the replaceable propagation boundary. The standard `DeterministicRadioPropagationSolver` uses a lightweight free-space path-loss calculation plus a small deterministic high-frequency attenuation term, antenna gain, externally supplied path correction, Building obstruction penalty, noise, and interference.

Building obstruction is derived from intersection between the transmitter-receiver segment and authoritative Building `WorldVolume`s. The standard model distinguishes clear and obstructed paths through deterministic NLoS / penetration penalties. It does not attempt reflection, diffraction, multipath, terrain/material RF modeling, or ray tracing.

Interference is accumulated from operational emissions whose occupied frequencies overlap the target channel. Candidate discovery must use the Radio 3D spatial index rather than scanning every transmitter/emission in the world. Aggregate received interference is converted from dBm to linear power before summation and then converted back to dBm.

The common link result exposes received power, noise/interference contribution, SINR, reachability, and a technology-neutral state (`Healthy`, `Marginal`, `Interfered`, `Unreachable`, or `OutOfService`).

## Infrastructure dependency

A Radio Site may bind to an existing Building power boundary and an `OpticalBackhaul`. A site configured to require power is operational only while the linked Building has Power service. A linked Optical backhaul must also be operational. Radio does not directly mutate Power or Optical state and does not duplicate their topology.

Loss and recovery of either dependency must deterministically propagate to transmitter/receiver/emission/link operational state on the Simulation boundary.

## Persistence and distribution

Radio/Spectrum state is included in checkpoint/Save Data with stable next-ID values and entity references. Vector orientation is serialized as explicit XYZ state so restore produces identical radio geometry and deterministic continuation.

Protocol 2.16 introduces bounded `RadioSnapshot` (790) and `SpectrumSnapshot` (791) messages. The Radio snapshot distributes Radio Site, Antenna, Transmitter, Receiver, Emission, link result, and service-area debug state. The Spectrum snapshot distributes bands, frequency blocks/channels, and conflict state. The Server uses the existing serialized WebSocket send boundary; the browser decoder rejects Radio/Spectrum frames below Protocol 2.16.

The Web Client debug overlay visualizes site positions, simple coverage/service areas, directional antenna orientation, link health/utilization, channels, interference/SINR-related state, and spectrum conflicts. It is a diagnostic view rather than authoritative simulation state.

## Determinism and solver boundary

For identical checkpoint/configuration/input sequence, Radio state and link results must be reproducible. Stable-ID ordering is used where iteration order affects results. No wall-clock time, random sampling, camera distance, renderer visibility, or client subscription may change authoritative Radio calculations.

Higher-fidelity solvers may replace `IRadioPropagationSolver`, but they must preserve the technology-neutral request/result boundary and must not move authoritative Radio ownership into the renderer or Web Client.

## Verification criteria

Phase 28 verification covers multiple frequencies, overlapping emissions, Building obstruction, deterministic interference/SINR, receiver compatibility, 3D candidate filtering, Power outage/recovery, Optical backhaul outage/recovery, checkpoint/save continuity, Protocol 2.16 encode/decode, Server-to-Browser Radio/Spectrum distribution and debug rendering, and large candidate-query/propagation benchmarks.
