# Simulation Core Architecture

## Overview

Phase 2 の Simulation Core は、外部 API と mutable store を分離した小さな server-authoritative core とする。

```text
SimulationWorld
  ├─ SimulationConfig
  ├─ SimulationTime
  ├─ DeterministicRandom
  ├─ AgentStore
  └─ SpatialIndex

AgentStore + SpatialIndex
  └─ AgentSnapshot[] へコピー
```

## Public boundary

`SimulationWorld` が command / step / snapshot の入口となる。

- Agent 生成・削除
- `Step()`
- 単体 Agent snapshot
- 範囲 snapshot

通信・I/O は持ち込まない。

## Mutable state ownership

`AgentStore` が Agent state の正本を所有する。

state は内部 mutable struct とし、外部には `AgentSnapshot` の値コピーだけを返す。これにより Server や将来の Protocol 実装が tick 中の state を直接保持し続けることを防ぐ。

## Stable ID

`AgentId` は `ulong` を包む strongly typed value とする。

Agent 削除後も ID は再利用・再採番しない。内部 storage index と公開 identity を分離する。

## Spatial index

`SpatialIndex` は次の2方向の mapping を持つ。

- cell -> Agent ID set
- Agent ID -> current cell

Agent が cell 境界を跨いだ場合だけ membership を更新する。範囲 snapshot では対象 cell の Agent ID だけを候補として取り出し、最後に `WorldRect.Contains` で正確な範囲判定を行う。

## Tick hot path

`AgentStore.Step()` は既存 storage を `Span<T>` として走査し、各 active Agent の position を in-place 更新する。

Phase 2 では単純な全 active Agent 更新が目的なので、tick 内で Agent ごとの object、LINQ chain、Task、temporary collection を作らない。

Spatial membership は cell が変わった Agent のみ更新する。

## Determinism

Agent 自動生成用乱数は内部の SplitMix64 系 PRNG を使用する。

同じ runtime 内の `System.Random` 実装詳細へ依存せず、seed と呼び出し順から明示的に同じ sequence を生成する。

## Future evolution

PoC benchmark で scale limit を確認してから、必要に応じて次を検討する。

- SoA storage
- sparse slot reuse
- chunked spatial index
- range job による parallel step
- snapshot buffer reuse

Phase 2 では benchmark 根拠なしにこれらを先行導入しない。
