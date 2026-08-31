# Server Administration Console Specification

## Purpose and trust boundary

The administration console is a trusted local operator interface for a running Headless Server. It is not an authentication boundary and must not be exposed as a remote protocol. `Server:Console:Enabled=false` disables stdin ingestion entirely.

stdin is only an input adapter. Parsed commands are submitted to the same reusable `AdminCommandQueue` / `AdminCommandExecutor` boundary that future Remote Admin and City Management UI integrations can call after supplying their own authentication and authorization.

## Execution contract

- command ingestion never mutates `SimulationWorld` directly;
- the bounded queue has one logical reader and preserves FIFO execution;
- mutations execute while holding the `SimulationRuntime` authoritative lock, so they cannot occur in the middle of a simulation tick;
- malformed input, missing entities, reference conflicts, invalid state, queue-full and I/O failures return structured `AdminCommandResultCode` values instead of terminating the server;
- human-readable display strings are separate from the result code;
- stdin EOF ends the console service only; normal host cancellation ends it without treating cancellation as an error.

## Grammar

A line is tokenized using whitespace. Double quotes form one token and support backslash escaping inside quoted text. The first token is the command name. Tokens beginning with `--` are options; `--name=value` supplies a value and `--flag` supplies a valueless option. Duplicate option names are rejected.

Numbers are parsed with invariant culture. Stable entity IDs use positive unsigned 64-bit decimal values. Enum values are case-insensitive names and must be defined enum members.

Core commands:

```text
help
status
version
exit
simulation status
simulation pause
simulation resume
simulation step [count]
agent list|show|add|remove ...
building list|show|add|remove ...
poi list|show|add|remove ...
road node|segment|lane|connection|access list|show|add|remove ...
connection list|show|disconnect ...
world save <path>
world load <path>
```

`simulation step` is valid only while paused. Automatic scheduler ticks become no-ops while paused. Manual steps and resume therefore have deterministic ordering relative to queued mutations.

## World persistence

`world save` captures a `SimulationCheckpoint` while holding the runtime lock, then restores a detached world and performs serialization and file I/O outside the runtime lock. `world load` performs file I/O and deserialization first, then replaces the authoritative world atomically under the runtime lock.

World replacement increments both Road and Railway published revisions and invalidates cached read models, causing subscribed clients to receive the replacement topology on subsequent publishing.

## Result codes

`Ok`, `InvalidSyntax`, `UnknownCommand`, `InvalidArgument`, `NotFound`, `Conflict`, `InvalidState`, `QueueFull`, `IoError`, and `InternalError` are stable result categories. Callers should branch on the code rather than parse the display message.
