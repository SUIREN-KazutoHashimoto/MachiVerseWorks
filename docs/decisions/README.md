# Architecture Decision Records

長期間影響する技術判断と、その理由を ADR として記録します。

現在の ADR:

- [`ADR-0001-csharp-headless-simulation-server.md`](ADR-0001-csharp-headless-simulation-server.md): C# 製 Headless Simulation Server を採用する
- [`ADR-0002-localization-boundary.md`](ADR-0002-localization-boundary.md): localization を Client 境界へ置き、Protocol / Save Data を言語非依存にする
- [`ADR-0003-versioned-save-data-boundary.md`](ADR-0003-versioned-save-data-boundary.md): Simulation checkpoint と versioned Save Data serialization を分離する
- [`ADR-0004-3d-world-coordinate-system.md`](ADR-0004-3d-world-coordinate-system.md): Simulation World の正本座標を3D化する
- [`ADR-0005-server-administration-boundary.md`](ADR-0005-server-administration-boundary.md): Server Administrationを単一のauthoritative command境界へ集約する
- [`ADR-0006-remote-mcp-through-administration-boundary.md`](ADR-0006-remote-mcp-through-administration-boundary.md): Remote MCPをPhase 20 Administration境界経由に限定する
- [`ADR-0007-read-only-view-observation-management-boundary.md`](ADR-0007-read-only-view-observation-management-boundary.md): Viewを完全read-onlyとし、Observation GatewayとManagement command境界を分離する
- [`ADR-0008-authoritative-two-level-world-environment-terrain.md`](ADR-0008-authoritative-two-level-world-environment-terrain.md): Global EnvironmentとDetailed 3D Terrainを二層化し、両方をSimulation authoritative stateから決定する
- [`ADR-0009-deterministic-regional-generation-authority.md`](ADR-0009-deterministic-regional-generation-authority.md): Regional GenerationをSimulation authoritative stateとして決定論的に生成し、generated planとlive-world materializationを分離する

命名例:

```text
ADR-0001-csharp-simulation-server.md
ADR-0002-server-authoritative-world.md
ADR-0003-binary-snapshot-protocol.md
```

各 ADR は最低限、`Status`、`Context`、`Decision`、`Consequences` を持つものとします。