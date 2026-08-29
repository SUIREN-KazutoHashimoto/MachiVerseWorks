# Simulation Core Architecture

## Overview

Simulation Core は server-authoritative なネイティブ3D worldを正本として持つ。外部APIとmutable storeを分離し、通信・I/OはCoreへ持ち込まない。

```text
SimulationWorld
  ├─ SimulationConfig
  ├─ SimulationTime
  ├─ DeterministicRandom
  ├─ AgentStore (XYZ position / velocity)
  └─ SpatialIndex (XYZ cells)

AgentStore + SpatialIndex
  └─ AgentSnapshot[]
```

## Public boundary

`SimulationWorld` が command / step / snapshot の入口となる。

- Agent生成・削除
- `Step()`
- 単体Agent snapshot
- `WorldVolume`による3D範囲snapshot
- checkpoint作成・復元

公開Geometry契約は`WorldPoint(X,Y,Z)`、`WorldVector(X,Y,Z)`、`SpatialCell(X,Y,Z)`、`WorldVolume(...)`のみとする。2D専用型、2引数constructor、`WorldRect`互換経路は持たない。

## Mutable state ownership

`AgentStore` がAgent stateの正本を所有する。positionとvelocityは常にXYZ全成分を保持する。

stateは内部mutable structとし、外部には`AgentSnapshot`の値コピーだけを返す。tick失敗時はXYZすべてをrollbackし、SimulationTimeを含めてatomicityを維持する。

## Stable ID

`AgentId`は`ulong`を包むstrongly typed valueとする。Agent削除後もIDは再利用・再採番しない。

## Spatial index

`SpatialIndex`は次の2方向のmappingを持つ。

- `(cellX, cellY, cellZ)` -> Agent ID set
- Agent ID -> current 3D cell

Agentがいずれかの軸でcell境界を跨いだ場合だけmembershipを更新する。3D snapshot/queryでは`WorldVolume`が覆うXYZ cellを列挙し、最後に`WorldVolume.Contains`で正確な範囲判定を行う。

## Tick hot path

`AgentStore.Step()`は既存storageを`Span<T>`として走査し、各active AgentのpositionをXYZ各軸でin-place更新する。tick内でAgentごとのobject、LINQ chain、Task、temporary collectionを生成しない。

自動生成Agentの`VelocityZ = 0`はPhase 9の生成ポリシーであり、Coreのtick/state自体はZ方向移動を正式に処理する。

## Determinism

Agent自動生成用乱数は内部のSplitMix64系PRNGを使用する。同じseedと呼び出し順から同じ3D state sequenceを生成する。
