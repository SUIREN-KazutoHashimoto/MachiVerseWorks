# ADR-0001: Simulation を C# のヘッドレスサーバーへ分離する

## Status

Accepted

## Context

旧 Machi-Sim は TypeScript / browser / Worker / SharedArrayBuffer を中心に、Simulation と Rendering を同一アプリケーション内で動作させていました。

この構成ではクライアントだけで完結する利点がある一方、大規模 Agent 処理、複数 subsystem の並列化、hot path のメモリ管理、Simulation と Rendering の独立したスケーリングが難しくなりました。また runtime patch が積み重なるにつれ、状態所有者と実効 code path の追跡コストも増加しました。

MachiVerseWorks では、旧実装を局所最適化し続けるのではなく、Simulation の所有境界を再設計します。

## Decision

都市 Simulation の authoritative state と tick を C# 製の `MachiVerseWorks.Simulation` に置き、`MachiVerseWorks.Server` からヘッドレスで実行します。

Browser Client は network protocol を通じて必要な範囲の snapshot / delta を受信し、描画・入力・補間を担当します。

依存方向は次を基本とします。

```text
Browser Client
      ↕
Protocol
      ↕
Server
      ↓
Simulation
```

Simulation Core は HTTP / WebSocket / ASP.NET Core / Browser / Three.js に依存しません。

## Consequences

### Positive

- Simulation と Rendering の性能問題を分離できる
- C# の並列処理・メモリ管理・profiling ecosystem を利用できる
- Server authoritative な world を明確にできる
- Client には camera 周辺など必要範囲だけを配信できる
- headless benchmark / test が行いやすくなる
- 将来的に複数 client や異なる viewer を追加しやすい

### Negative

- Client / Server protocol の設計と互換性管理が必要になる
- standalone browser application だけでは完全な Simulation を実行できない
- network latency、bandwidth、snapshot consistency を考慮する必要がある
- local development でも Server と Client の起動が必要になる

## Notes

旧 Machi-Sim のドメイン仕様や視覚表現は参考にしますが、Browser-owned state、Worker pool、SharedArrayBuffer、runtime patch を新アーキテクチャの前提として移植しません。

関連資料:

- [`../architecture/overview.md`](../architecture/overview.md)
- [`../archive/legacy-machi-sim/README.md`](../archive/legacy-machi-sim/README.md)
