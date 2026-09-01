# Architecture Overview

MachiVerseWorks は、authoritativeな都市Simulation、read-onlyなView、World / City / Serverを変更するManagementを明確に分離します。

実装計画・Task状態も同じ責務境界に合わせ、Simulation側は[`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)、純粋な観測・描画は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)、管理・編集UIは[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)を正本とします。

## 全体構成

```text
                         ┌───────────────────────┐
                         │    Read-Only View     │
                         │ render / inspector    │
                         └───────────▲───────────┘
                                     │ observation only
                              MachiVerseWorks.Protocol
                                     │
                         ┌───────────┴───────────┐
                         │ MachiVerseWorks.Server│
                         │ Observation Gateway   │
                         │ Command Boundary      │
                         └───────┬───────▲───────┘
                                 │       │ command
                                 ▼       │
                         ┌───────────────────────┐
                         │ MachiVerseWorks.      │
                         │ Simulation            │
                         │ authoritative world   │
                         └───────────▲───────────┘
                                     │ checkpoint mapping
                         ┌───────────┴───────────┐
                         │ MachiVerseWorks.      │
                         │ Persistence           │
                         └───────────────────────┘

Management Client ── command ────────► Server Command Boundary
                 └─ read-only View component / Observation Gatewayを再利用可能
```

`MachiVerseWorks.Persistence` は実行ループを所有せず、Simulation checkpointとversioned Save Dataの変換境界としてSimulationを参照します。Server save/load機能は、この境界を実行ホストから呼び出します。

## Simulation Core

`MachiVerseWorks.Simulation` がauthoritative worldと意味的処理を所有します。

責務:

- World / Agent / Traffic / Transit / Logistics / Power等の状態管理
- simulation tick
- rule / semantic state / schedule / state transition
- authoritative commandの適用
- spatial index
- deterministic / reproducibleな処理が必要な領域の管理
- Current / Recent / Planned / Historical等の観測に必要なread model境界
- save/load用checkpointの作成・復元境界

Activity、ETA、都市分類、予定、semantic event等をView側へ計算委譲しません。

Simulation CoreはHTTP、WebSocket、ASP.NET Core、DOM、Three.jsを知りません。

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

`MachiVerseWorks.Server` は実行ホストと外部境界です。read-only observationとauthoritative commandを別責務として扱います。

責務:

- Simulationのライフサイクル
- tick loop
- Client接続
- Observation Gateway
  - subscription / interest management
  - detached read model取得
  - spatial filtering
  - snapshot / delta配信
  - observation cache / request deduplication
  - reconnect / resync
- command validation / dispatch
- save / load等の外部I/O境界
- Remote Administration / Managementからauthoritative command境界を安全に再利用するhost機能

Network I/OとSimulationのmutable stateを直接共有し続けず、明示的なobservation / command境界を使います。

Observation Gatewayの詳細は[`observation-gateway.md`](observation-gateway.md)を参照してください。

## Protocol

`MachiVerseWorks.Protocol` は外部契約です。

- observation request
- snapshot / delta message
- entity spawn / update / remove
- management / administration command type
- metadata
- protocol version
- binary layout
- compatibility rule

read-only Observation Requestとauthoritative mutation commandは意味的に区別します。Simulation内部のclassやobject graphをそのままnetwork contractにしません。現行versionとbinary layoutの正本は[`protocol.md`](protocol.md)です。

## View

Viewは**完全read-only**な観測Clientです。

- Camera周辺や明示targetのObservation Requestを送る
- snapshot / deltaを受信する
- spawn / update / removeをローカル描画stateへ反映する
- Simulation tick間を補間して描画する
- Objectを選択し、Current / Recent Past / Planned Future等のSimulation提供値をInspectorへ表示する
- Historical projectionを受信して過去Worldを表示する

ViewはSimulation stateを変更するcommandを送信しません。ViewローカルstateはCamera、Selection、Rendering resource、cache、LOD、interpolation等のPresentation用途に限定します。

Viewの存在・非存在、Client数、Camera位置、Selection、描画FPS、Rendering LOD、View cache状態によってSimulation結果が変化してはなりません。

## Management

ManagementはWorld / City / Serverを明示的に変更する操作Clientです。

- build / edit / remove
- naming / override
- simulation pause / resume / step
- Server configuration
- Save / Load
- destructive operation confirmation

Managementはread-only View componentを画面内で再利用してよいですが、mutationは必ずServerのauthoritative command境界から実行します。View module自体へcommand責務を混在させません。

ManagementのTask状態は[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)を正本とします。

## Analytics

人口統計、経済分析、交通分析、trend、heatmap等の分析処理はViewに実装しません。必要になった場合は専用Analytics Listener / data pipeline / analysis clientを別責務として設計します。

ViewがCamera中心の局所Observationを必要とするのに対し、Analyticsは長期・全World・集計向けfeedを必要とするため、両者を同じClient-side処理へ統合しません。

## Tick と Snapshot

Simulation tickと表示frameを分離します。

```text
Simulation: fixed / controlled tick
Observation publish: lower-frequency / revision-driven
Rendering: display refresh rate + interpolation
```

具体的なtick rateやpublish rateはbenchmark後に決定し、固定値を設計上の前提にしすぎません。

Snapshotはnetwork threadがSimulationのmutable storageを直接読む方式ではなく、detached immutable read model、double buffer、copy-on-publish等を使用します。

## Spatial Observation

大規模World全体を全Viewへ送信しません。

ViewはCamera / inspection target等から必要範囲をObservation Gatewayへ通知し、Gatewayはdetached read modelをspatial filteringして配信します。

代表的なdelivery:

- spawn
- update
- remove
- revision-driven static snapshot
- explicit inspect result
- Historical projection

必要に応じてfull snapshotとdeltaを組み合わせます。

Observation範囲はdelivery負荷を決めるだけで、Simulation FidelityやSimulation対象を変えません。

## Performance Principles

旧Machi-Simでの経験から、次を初期原則とします。

- hot pathの不要allocationを避ける
- AgentごとのTaskを作らない
- subsystem / chunk / range単位で並列化する
- 毎tick / frameの全件scanを避ける
- active / sleeping / event-driven stateを検討する
- routing / traffic / pedestrian / publish / renderを個別計測する
- Observation Gatewayのcache / deduplicationで同一read処理を無駄に繰り返さない
- optimizationはbenchmarkとprofilerに基づく

C#側ではまず通常のarray / struct / Span / Parallel.For等で明快に実装し、unsafe / SIMD / native codeは計測後に必要な箇所だけ検討します。

## Legacyとの違い

旧`Machi-Sim_Legacy`はブラウザ内でSimulationとRenderingを完結させていました。

MachiVerseWorksでは以下を変更します。

- Browser-owned world → Server-owned authoritative world
- Worker / SharedArrayBuffer中心 → C# Simulation Core
- runtime patch accumulation → 明示的な責務とcontract
- rendering requirementとsimulation data ownershipを分離
- whole-world client state → spatial / explicit Observation Request
- View mutation UI → 独立したManagement command client

Legacyから引き継ぐ知見は[`../archive/legacy-machi-sim/README.md`](../archive/legacy-machi-sim/README.md)を参照してください。
