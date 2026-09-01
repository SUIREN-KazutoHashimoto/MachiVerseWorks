# Remote MCP Administration Architecture

## Purpose

Phase 27 adds a remote Model Context Protocol (MCP) boundary to the existing headless server without creating a second authoritative administration path.

The data flow is intentionally one-way:

```text
MCP client
  -> HTTPS reverse proxy / tunnel
  -> Streamable HTTP /mcp
  -> RemoteMcpSecurityMiddleware
  -> MCP tool adapter
  -> RemoteMcpAdminGateway
  -> AdminCommandParser
  -> AdminCommandQueue
  -> AdminCommandExecutorV2
  -> SimulationRuntime
```

MCP tools never receive `SimulationRuntime` and never reach simulation stores directly. Read operations and mutations therefore keep the same validation, deterministic scheduling, and state ownership used by the Phase 20 administration console.

## Host boundary

The MCP server is hosted inside `MachiVerseWorks.Server` with the official `ModelContextProtocol.AspNetCore` package and Streamable HTTP at `/mcp`.

`Server:Mcp:Enabled` defaults to `false`. When disabled, MCP services are not registered and `/mcp` is not mapped. Enabling MCP therefore requires an explicit deployment decision.

The transport is configured as stateless because the Phase 27 tool set does not use server-to-client sampling, elicitation, or other session-owned features.

## Authentication and authorization

Phase 27 uses pre-shared bearer credentials with three monotonically increasing scopes:

| Credential | Claims | Intended tools |
| --- | --- | --- |
| read | `read` | status, version, diagnostics, logs, entity inspection |
| write | `read`, `write` | read tools plus pause, step, resume, save, entity add/update |
| destructive | `read`, `write`, `destructive` | all above plus entity removal |

Configured tokens must be at least 32 characters and distinct. Only SHA-256 token hashes are retained by the runtime. The raw configured token value is never written to MCP logs.

ASP.NET Core authorization policies are also attached to MCP tools through the MCP SDK authorization filters. Consequently, unauthorized tools are filtered from `tools/list`, and a client cannot bypass the scope check by directly issuing `tools/call` with a hidden tool name.

## Tool surface

The MCP surface is deliberately smaller than the local administration console.

Exposed read tools:

- `server_status`
- `server_version`
- `simulation_status`
- `diagnostics_metrics`
- `logs_query`
- `entity_query`

Exposed write tools:

- `simulation_pause`
- `simulation_step`
- `simulation_resume`
- `simulation_save`
- `entity_write` (`add` / `update` only)

Exposed destructive tool:

- `entity_remove` with destructive scope and `confirm=true`

Not exposed:

- server shutdown / `stop` / `exit`
- `world load`
- arbitrary administration command execution
- arbitrary shell/process execution
- arbitrary file read/write paths
- client disconnect controls

Dynamic MCP arguments are converted into exactly one quoted administration token each. Entity types are mapped through a fixed allowlist before command parsing.

## Save boundary

`simulation_save` accepts a slot name rather than a path. A slot is restricted to 1-64 ASCII letters, digits, `.`, `_`, and `-`, excluding `.` and `..`. The resulting path is always placed under `Server:Mcp:SaveDirectory` and then handed to the existing `world save` administration command.

MCP does not expose `world load` because remote replacement of the authoritative world has a wider failure and confirmation surface than Phase 27 requires.

## Bounded diagnostics and logs

A bounded in-memory `ILoggerProvider` is registered only when MCP is enabled. `logs_query` can search this tail but cannot access files. Query count and result size are bounded by configuration.

`diagnostics_metrics` reuses the existing `E2eMetrics` snapshot and caps the serialized result.

## Request isolation

The `/mcp` boundary applies:

- maximum request body size
- global concurrent-request limit
- per-credential fixed-window requests-per-minute limit
- request timeout via `RequestAborted`
- exact Origin allowlist when an `Origin` header is present
- bounded tool query/result sizes

Requests without an `Origin` header are supported for non-browser MCP clients. Browser-originated requests must match `Server:Mcp:AllowedOrigins` exactly.

## Reverse proxy / Cloudflare contract

Remote clients must connect to an HTTPS URL such as `https://server.example/mcp`. Kestrel can remain on a private HTTP origin when TLS terminates at a trusted reverse proxy or Cloudflare Tunnel, provided that the origin is not directly reachable from untrusted networks.

Deployment requirements:

1. Publish only the proxy/tunnel HTTPS endpoint.
2. Keep the Kestrel origin private or firewall-restricted.
3. Inject bearer tokens through environment/secrets management, never source control.
4. Configure proxy request/body/time limits at least as strict as the application limits.
5. Do not cache `/mcp` responses.
6. Preserve `Authorization`, `Content-Type`, `Accept`, `MCP-Protocol-Version`, and MCP response headers.
7. If browser MCP clients are allowed, configure only the required trusted origins.

Cloudflare Access may be added as an outer authentication layer, but it does not replace the MCP bearer scope checks in this phase.

## Failure isolation

A slow or malformed MCP client can consume only its bounded request slot and timeout. Administration work is still serialized through the existing bounded `AdminCommandQueue`, so MCP cannot introduce a second concurrent mutation path into the simulation.
