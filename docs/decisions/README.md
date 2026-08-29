# Architecture Decision Records

長期間影響する技術判断と、その理由を ADR として記録します。

現在の ADR:

- [`ADR-0001-csharp-headless-simulation-server.md`](ADR-0001-csharp-headless-simulation-server.md): C# 製 Headless Simulation Server を採用する
- [`ADR-0002-localization-boundary.md`](ADR-0002-localization-boundary.md): localization を Client 境界へ置き、Protocol / Save Data を言語非依存にする

命名例:

```text
ADR-0001-csharp-simulation-server.md
ADR-0002-server-authoritative-world.md
ADR-0003-binary-snapshot-protocol.md
```

各 ADR は最低限、`Status`、`Context`、`Decision`、`Consequences` を持つものとします。
