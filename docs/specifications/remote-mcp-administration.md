# Remote MCP Administration Specification

## Scope

Phase 27 provides authenticated remote inspection and bounded administration of a running MachiVerseWorks Server through Model Context Protocol (MCP) Streamable HTTP.

The public endpoint is `/mcp`. MCP is disabled by default and has no anonymous mode when enabled.

## Configuration

Configuration keys are under `Server:Mcp` and use normal ASP.NET Core configuration mapping, so production credentials should normally be supplied as environment variables such as `Server__Mcp__ReadToken`.

| Key | Default | Contract |
| --- | --- | --- |
| `Enabled` | `false` | Explicit opt-in to register and map MCP |
| `ReadToken` | unset | read credential, minimum 32 characters |
| `WriteToken` | unset | read + write credential, minimum 32 characters |
| `DestructiveToken` | unset | read + write + destructive credential, minimum 32 characters |
| `AllowedOrigins` | empty | semicolon-separated exact HTTP(S) origins for requests that send `Origin` |
| `MaxRequestBytes` | `262144` | MCP request-body ceiling |
| `MaxConcurrentRequests` | `8` | global MCP in-flight request ceiling |
| `RequestsPerMinute` | `120` | fixed-window ceiling per configured credential |
| `RequestTimeoutMilliseconds` | `30000` | request cancellation timeout |
| `MaxResultBytes` | `65536` | bounded tool result target |
| `MaxLogEntries` | `512` | bounded in-memory log tail capacity |
| `MaxQueryItems` | `200` | maximum entity/log items requested by one tool call |
| `SaveDirectory` | `data/mcp-saves` | server-controlled root for MCP saves |

If MCP is enabled, at least one credential is required. Configured credentials must be distinct.

## Authorization

MCP requests use `Authorization: Bearer <token>`.

A read credential may discover and invoke only read tools. A write credential also receives write tools. A destructive credential receives all tools. Authorization is checked both during MCP discovery and invocation.

## Read tools

### `server_status`

Returns the existing administration `status` result, including authoritative tick/pause and summary counts.

### `server_version`

Returns the running application version through the existing `version` command.

### `simulation_status`

Returns authoritative tick, pause state, and tick rate through `simulation status`.

### `diagnostics_metrics`

Returns the existing bounded E2E metrics snapshot.

### `logs_query`

Returns a bounded in-memory tail. Parameters:

- `limit`: 1 through `MaxQueryItems`
- `contains`: optional case-insensitive category/message filter

It cannot read arbitrary log files.

### `entity_query`

Accepts an allowlisted entity type and optional ID. Without ID it maps to `list`; with ID it maps to `show`.

The allowlist includes current Phase 20 administration entities for agents, buildings, POIs, road infrastructure, railway infrastructure/operations, vehicles for inspection, formations, rail routes, timetables, services, and trains.

## Write tools

### `simulation_pause`

Maps to `simulation pause`.

### `simulation_step`

Maps to `simulation step <count>`, with count restricted to 1-10000.

### `simulation_resume`

Maps to `simulation resume`.

### `simulation_save`

Accepts a safe slot name and maps it to `world save <SaveDirectory>/<slot>.mvw`. Arbitrary paths and `world load` are not exposed.

### `entity_write`

Accepts an allowlisted entity, operation `add` or `update`, and at most 32 bounded arguments. Each argument is encoded as exactly one quoted administration command token before parsing.

Vehicle spawning and connection controls are not part of the generic writable allowlist.

## Destructive tool

### `entity_remove`

Requires the destructive credential and `confirm=true`. The entity must be in the writable allowlist, and arguments receive the same token count/length controls as `entity_write`.

## Stable MCP result

Administration-backed tools return:

```json
{
  "success": true,
  "code": "ok",
  "message": "..."
}
```

Stable codes include `ok`, `invalid_syntax`, `unknown_command`, `invalid_argument`, `not_found`, `conflict`, `invalid_state`, `queue_full`, `io_error`, `internal_error`, and MCP-specific `confirmation_required`.

## Security requirements

The following capabilities must remain unavailable through MCP:

- arbitrary shell or process execution
- arbitrary administration command execution
- server shutdown
- arbitrary filesystem reads/writes
- authoritative world load/replacement
- mutation that bypasses `AdminCommandQueue` / `AdminCommandExecutorV2`

Raw bearer token values must not appear in server logs, MCP result payloads, or source-controlled default configuration.

## Deployment requirements

The client-facing MCP URL must use HTTPS. If TLS terminates at Cloudflare or another reverse proxy, the Kestrel origin must be private or otherwise inaccessible to untrusted networks. The reverse proxy must forward authorization and MCP protocol headers and must not cache `/mcp`.
