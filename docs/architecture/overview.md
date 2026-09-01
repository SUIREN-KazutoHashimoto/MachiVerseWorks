# Architecture Overview

MachiVerseWorks は、都市シミュレーション本体と表示クライアントを明確に分離します。

実装計画・Task状態も同じ責務境界に合わせ、Server-authoritativeなSimulation側は[`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)、描画・Camera・UI / UX・View performance・localizationは[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)を正本とします。

## 全体構成

```text
Browser 3D Client
        ↕
MachiVerseWorks.Protocol
        ↕
MachiVerseWorks.Server
        ↓
MachiVerseWorks.Simulation
        ↑
MachiVerseWorks.Persistence
```

`MachiVerseWorks.Persistence` は実行ループを所有せず、Simulation checkpointとversioned Save Dataの変換境界としてSimulationを参照します。Server save/load機能は、この境界を実行ホストから呼び出します。

## Simulation Core

`MachiVerseWorks.Simulation` が authoritative world を所有します。

責務:

- World / Agent / Traffic / Transit / Logistics / Power 等の状態管理
- simulation tick
- command の適用
- spatial index
- deterministic / reproducible な処理が必要な領域の管理
- snapshot 作成に必要な読み取り境界の提供
- save/load用checkpointの作成・復元境界

Simulation Core は HTTP、WebSocket、ASP.NET Core、DOM、Three.js を知りません。

## Persistence

`MachiVerseWorks.Persistence` は保存形式とSimulation状態の間の境界です。

責務:

- Save format version
- versioned Save Data schema
- JSON serialization / deserialization
- 外部Save Dataのvalidation
- Simulation checkpointとのmapping

PersistenceはSimulation内部Storeを正本として所有せず、file path、save slot、Web UIも所有しません。

## Server

`MachiVerseWorks.Server` は実行ホストと外部境界です。

責務:

- Simulation のライフサイクル
- tick loop
- Client 接続
- command validation / dispatch
- subscription / interest management
- snapshot / delta / statistics の配信
- save / load 等の外部 I/O 境界
- Remote Administration等からauthoritative command境界を安全に再利用するhost機能

Network I/O と Simulation の可変 state を直接共有し続けず、明示的な command / snapshot 境界を使います。

## Protocol

`MachiVerseWorks.Protocol` は外部契約です。

- command type
- snapshot / delta message
- entity spawn / update / remove
- metadata / statistics
- protocol version
- binary layout
- compatibility rule

Simulation 内部の class や object graph を、そのまま network contract にしません。現行versionとbinary layoutの正本は[`protocol.md`](protocol.md)です。

## Web Client

Web Client は表示と入力を担当します。

- Camera 周辺など必要範囲を subscribe
- snapshot / delta を受信
- spawn / update / remove をローカル描画 state へ反映
- Simulation tick 間を補間して描画
- UI / Inspector から server-authoritative command 境界へ入力する

Client のローカル state は描画・UX用のキャッシュであり、都市世界の正本ではありません。Camera / Rendering LOD / View cacheをSimulation Fidelityやworkloadの判定条件に使用しません。

## Tick と Snapshot

Simulation tick と表示 frame を分離します。

例:

```text
Simulation: fixed / controlled tick
Snapshot:   lower-frequency publish
Rendering:  display refresh rate + interpolation
```

具体的な tick rate や publish rate は benchmark 後に決定し、固定値を設計上の前提にしすぎません。

Snapshot は network thread が Simulation の mutable storage を直接読む方式ではなく、immutable view、double buffer、copy-on-publish 等の方式を比較して決定します。

## Spatial Interest Management

大規模都市全体を全 Client へ送信しません。

Client は camera / inspection target 等から必要範囲を Server へ通知し、Server は空間 index を用いて対象 entity を選択します。domainによってaggregate / world-wide read modelが必要な場合は、各Protocol contractで明示します。

基本イベント:

- spawn
- update
- remove

必要に応じて full snapshot と delta を組み合わせます。

## Performance Principles

旧 Machi-Sim での経験から、次を初期原則とします。

- hot path の不要 allocation を避ける
- Agent ごとの Task を作らない
- subsystem / chunk / range 単位で並列化する
- 毎 tick / frame の全件 scan を避ける
- active / sleeping / event-driven state を検討する
- routing / traffic / pedestrian / publish / render を個別計測する
- optimization は benchmark と profiler に基づく

C# 側ではまず通常の array / struct / Span / Parallel.For 等で明快に実装し、unsafe / SIMD / native code は計測後に必要な箇所だけ検討します。

## Legacyとの違い

旧 `Machi-Sim_Legacy` はブラウザ内で Simulation と Rendering を完結させていました。

MachiVerseWorks では以下を変更します。

- Browser-owned world → Server-owned world
- Worker / SharedArrayBuffer 中心 → C# Simulation Core
- runtime patch accumulation → 明示的な責務と contract
- rendering requirement と simulation data ownership を分離
- whole-world client state → spatial / explicit read-model subscription

Legacyから引き継ぐ知見は [`../archive/legacy-machi-sim/README.md`](../archive/legacy-machi-sim/README.md) を参照してください。
