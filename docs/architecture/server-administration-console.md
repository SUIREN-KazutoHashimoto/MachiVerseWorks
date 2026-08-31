# Server Administration Console Architecture

Phase 20 introduces a server-authoritative administration boundary for local stdin today and future Remote Admin / City Management UI callers.

## Responsibility split

- `ServerConsoleService` owns stdin only. It reads lines, parses them, submits requests to the bounded queue, and prints structured results. EOF or cancellation stops the console service without crashing the server.
- `AdminCommandParser` owns grammar only. It tokenizes quoted arguments, parses `--option=value`, and preserves invariant-culture values as strings until command-specific validation.
- `AdminCommandQueue` is bounded and single-reader. Producers never mutate the world directly; queue saturation returns a stable `QueueFull` result instead of blocking the server indefinitely.
- `AdminCommandExecutorV2` is the reusable execution boundary. It maps parsed commands to server and Simulation operations and converts validation, I/O, state, and reference failures into stable result codes.
- `SimulationRuntime` is the authoritative synchronization boundary. Automatic ticks, paused manual steps, reads, mutations, checkpoint capture, and world replacement all serialize through the same runtime gate.
- `SimulationWorld` owns domain validation. Console code must call official APIs rather than editing stores or checkpoint collections directly.

## Ordering and tick boundary

The runtime gate prevents an administration mutation from observing or producing a half-updated simulation tick. Automatic tick, admin read/mutation, checkpoint capture, world replacement, and paused `step` are mutually serialized.

`pause` disables automatic advancement. `step N` is valid only while paused and advances exactly `N` complete fixed ticks while holding the runtime gate. `resume` re-enables automatic advancement after all earlier queued commands have completed. Because the admin queue has one reader, commands execute FIFO.

## Read-model invalidation

Road and Railway topology are published as revisioned read models. Mutations that can change topology tell `SimulationRuntime` which revision to increment. `world load` replaces the authoritative `SimulationWorld`, invalidates both topology revisions, and causes connected clients to receive fresh snapshots even when their subscription did not change.

The revision is monotonic within a server process. A client therefore treats a larger revision as newer and never relies on stable object identity from a previous world instance.

## Persistence boundary

`world save` captures a checkpoint while holding the runtime gate, then serializes and writes the detached world after releasing that gate. Long file I/O does not stall simulation synchronization.

`world load` performs file reading and deserialization outside the runtime gate. Only the final validated world replacement is synchronized.

## Domain mutation policy

Agent, Building, POI, Road, Vehicle, Railway Infrastructure, and Railway Operations commands reuse Simulation APIs. Referential constraints remain domain rules:

- Building updates cannot exclude linked POIs.
- POIs linked to a Building must remain inside its bounds.
- Road topology mutation respects Vehicle-derived-route constraints.
- Vehicle spawn requires a successful `FindRoadRoute` result.
- Railway Infrastructure update/remove rebuilds from a candidate checkpoint and runs the same validation used by persistence before replacing the infrastructure store.
- Railway Operations expose only create operations already represented safely by Simulation APIs.

## Future Remote Admin reuse

Remote administration must not invoke stdin-specific code. A future transport authenticates and authorizes a caller, builds the same `AdminCommand` contract, submits it to `AdminCommandQueue`, and returns `AdminCommandResult`. Authentication, rate limits, audit policy, and remote transport framing remain outside the execution boundary.

See also:

- [`../specifications/server-administration-console.md`](../specifications/server-administration-console.md)
- [`../decisions/ADR-0005-server-administration-boundary.md`](../decisions/ADR-0005-server-administration-boundary.md)
- [`../../src/MachiVerseWorks.Server/README.md`](../../src/MachiVerseWorks.Server/README.md)
