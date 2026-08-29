# Simulation Core Architecture

## Overview

Simulation Core は server-authoritative なネイティブ3D worldを正本として持つ。外部APIとmutable storeを分離し、通信・I/OはCoreへ持ち込まない。

```text
SimulationWorld
  ├─ SimulationConfig
  ├─ SimulationTime
  ├─ DeterministicRandom
  ├─ AgentStore (XYZ position / velocity)
  ├─ BuildingStore (stable ID / kind / 3D bounds)
  ├─ PoiStore (stable ID / kind / 3D position / optional Building ref)
  └─ SpatialIndex (XYZ Agent cells)

AgentStore + SpatialIndex
  └─ AgentSnapshot[]

BuildingStore
  └─ BuildingSnapshot[]

PoiStore
  └─ PoiSnapshot[]
```

## Public boundary

`SimulationWorld` が command / step / snapshot の入口となる。

- Agent生成・削除
- Building / POI生成・削除
- `Step()`
- 単体Agent / Building / POI snapshot
- `WorldVolume`による3D Agent範囲snapshot
- Building / POI全件snapshot
- checkpoint作成・復元

公開Geometry契約は`WorldPoint(X,Y,Z)`、`WorldVector(X,Y,Z)`、`SpatialCell(X,Y,Z)`、`WorldVolume(...)`のみとする。2D専用型、2引数constructor、`WorldRect`互換経路は持たない。

## Mutable state ownership

`AgentStore` がAgent stateの正本を所有する。positionとvelocityは常にXYZ全成分を保持する。

`BuildingStore`はBuildingのstable ID、kind、3D AABBを所有する。`PoiStore`はPOIのstable ID、kind、3D position、任意のBuilding参照を所有する。Building / POIはPhase 10ではtick hot pathへ参加しない静的な都市オブジェクトとして扱う。

stateは内部mutable storeに閉じ、外部には`AgentSnapshot` / `BuildingSnapshot` / `PoiSnapshot`の値コピーだけを返す。Agent tick失敗時はXYZすべてをrollbackし、SimulationTimeを含めてatomicityを維持する。

## Stable ID と生成failure atomicity

`AgentId`、`BuildingId`、`PoiId`はそれぞれ`ulong`を包むstrongly typed valueとし、ID namespaceを混同しない。削除後もIDは再利用・再採番しない。

Agent生成はID capacityについてもfailure-atomicに扱う。`AgentStore`は現在の`nextId`から要求数を事前検証し、ID空間が不足する場合はstate mutation前に`OverflowException`で拒否する。

- random velocityを使う単体`CreateAgent`は、PRNGを進める前に1 ID分のcapacityを確認する。
- `CreateAgents(count, ...)`は位置乱数・速度乱数・Agent追加を始める前に`count`全体分のcapacityを確認する。
- capacity不足時はAgent count、`nextId`、SpatialIndex、PRNG stateを呼出前から変更しない。

Building / POI生成も、次IDを表現可能な状態でのみstoreへ追加し、ID枯渇時は副作用なしで`OverflowException`を返す。crafted checkpointから`Next*Id = ulong.MaxValue`へ到達した状態はrestore可能だが、その後の新規生成は拒否される。

## Building / POI reference integrity

Building / POIはAgentの`SpatialIndex`へ混在させない。Phase 10では静的データであり、Agentの高頻度cell更新と別のstore責務にするためである。

POIがBuildingを参照する場合、commandとcheckpoint restoreの両境界で次を検証する。

1. Building IDが存在する。
2. POIの3D positionがBuildingの`WorldVolume`内にある。
3. 参照POIが存在するBuildingの削除を拒否する。

checkpoint restoreではBuildingを先に検証してID→boundsの一時mapを構築し、そのmapに対して全POI参照を検証してからstoreを復元する。部分的に不正なworld stateを公開しない。

Building / POIの全件snapshotとcheckpointはID昇順へ正規化する。これにより同じstateから安定した保存順序とテスト比較結果を得る。

## Spatial index

`SpatialIndex`は次の2方向のmappingを持つ。

- `(cellX, cellY, cellZ)` -> Agent ID set
- Agent ID -> current 3D cell

Agentがいずれかの軸でcell境界を跨いだ場合だけmembershipを更新する。3D snapshot/queryでは`WorldVolume`が覆うXYZ cellを列挙し、最後に`WorldVolume.Contains`で正確な範囲判定を行う。

Building boundsとPOI positionは生成・restore時に`SpatialGrid.ToCell`相当のrange validationを受けるが、Phase 10では専用のBuilding / POI spatial query indexは導入しない。必要性が生じた後続Phaseで計測結果を基に追加する。

## Tick hot path

`AgentStore.Step()`は既存storageを`Span<T>`として走査し、各active AgentのpositionをXYZ各軸でin-place更新する。tick内でAgentごとのobject、LINQ chain、Task、temporary collectionを生成しない。

Building / POIは`Step()`で走査しないため、Phase 10のデータモデル追加はAgent tick hot pathへ全件走査を追加しない。

自動生成Agentの`VelocityZ = 0`はPhase 9の生成ポリシーであり、Coreのtick/state自体はZ方向移動を正式に処理する。

## Determinism

Agent自動生成用乱数は内部のSplitMix64系PRNGを使用する。同じseedと呼び出し順から同じ3D state sequenceを生成する。

失敗した生成commandはdeterministic stateを変更しない。特にID枯渇による失敗ではPRNG stateを消費しないため、失敗commandの有無が後続の有効操作の乱数系列へ影響しない。

Building / POI生成はPRNGを使用せず、stable IDと入力値だけでstateが決まる。checkpoint / Save Dataは各next IDを保持し、restore後もID系列を継続する。
