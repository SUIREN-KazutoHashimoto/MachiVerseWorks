# ADR-0006: Route Remote MCP Through the Administration Boundary

## Status

Accepted

## Context

Phase 20 introduced an in-process administration command boundary with parsing, validation, a bounded command queue, and a single executor that owns authoritative mutations against `SimulationRuntime`.

Phase 27 needs remote MCP access for AI assistants and operational tooling. A direct MCP-to-Simulation adapter would be shorter initially, but it would create a second mutation path, duplicate validation, weaken deterministic ordering, and make future administration rules diverge between local and remote callers.

Remote access also introduces a larger security surface: authentication, least privilege, destructive-operation confirmation, request bounding, proxy deployment, and prompt-injection-driven tool calls must be considered explicitly.

## Decision

MCP is a transport and tool-adaptation layer only.

All authoritative MCP reads and mutations that have an administration equivalent are converted to fixed, allowlisted administration commands and sent through `AdminCommandParser`, `AdminCommandQueue`, and `AdminCommandExecutorV2`.

MCP tools do not depend on `SimulationRuntime` and do not expose a generic command executor.

The MCP surface uses three bearer scopes: read, write, and destructive. Tool authorization metadata is enforced during both discovery and invocation. Destructive entity removal also requires an explicit confirmation parameter.

MCP is disabled by default. The public transport is Streamable HTTP at `/mcp`, using the official C# MCP ASP.NET Core SDK. Remote deployments terminate HTTPS at the application or a trusted reverse proxy/tunnel while keeping any plaintext origin private.

The remote surface intentionally excludes server shutdown, world load, arbitrary shell/process execution, arbitrary filesystem access, and arbitrary administration commands.

## Consequences

### Positive

- local console and remote MCP share one authoritative command/validation path
- simulation mutation ordering remains serialized by the existing bounded queue
- MCP does not introduce direct dependencies into Simulation domain projects
- security review can reason about a small explicit remote allowlist
- read/write/destructive capabilities are discoverable according to caller scope
- future administration validation improvements automatically apply to MCP

### Negative

- MCP result shape is constrained by the text-oriented Phase 20 administration results
- some operations require quoting/translation between structured MCP arguments and administration command tokens
- high-volume data export is intentionally unsuitable for this interface
- pre-shared bearer credentials require external rotation and secret management

## Follow-up

If a future phase requires richer structured administration responses, evolve the shared Administration boundary itself rather than letting MCP bypass it. If OAuth/OIDC is later required, replace the credential authentication layer while preserving the same authorization policies and tool-to-Administration mapping.
