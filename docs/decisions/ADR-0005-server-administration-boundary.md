# ADR-0005: Reusable Server Administration Command Boundary

- Status: Accepted
- Date: 2026-08-31

## Context

The Headless Server needs a local stdin administration console in Phase 20, while later phases may expose equivalent operations through authenticated Remote Admin or City Management UI surfaces. Letting each input adapter mutate `SimulationWorld` directly would duplicate validation and make tick ordering dependent on the transport.

## Decision

Use a transport-independent `AdminCommand` / `AdminCommandResult` contract, a bounded FIFO `AdminCommandQueue`, and one sequential `AdminCommandExecutor`.

`ServerConsoleService` is only an stdin adapter. The executor enters `SimulationRuntime` for authoritative reads and mutations. Runtime locking serializes command mutations against simulation steps, so no mutation occurs mid-tick. Pause/manual-step state also belongs to the runtime boundary.

World save captures a checkpoint under the authoritative lock and performs serialization/file I/O after releasing it. World load performs I/O/deserialization before atomically replacing the world. Topology and world replacement invalidate cached publish read models using monotonic revisions.

## Consequences

Future admin transports can reuse command execution without depending on stdin. They must add their own authentication, authorization, auditing, and rate limits before enqueueing commands.

The bounded queue provides explicit backpressure (`QueueFull`) rather than unbounded memory growth. Sequential execution prioritizes deterministic behavior over parallel administrative throughput, which is appropriate because administration is control-plane traffic rather than simulation data-plane traffic.
