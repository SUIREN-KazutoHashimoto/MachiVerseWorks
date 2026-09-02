# MachiVerseWorks Roadmaps

`roadmap/` はMachiVerseWorksの実装計画を責務別に管理します。Task状態の正本は次の4ファイルです。

- [`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md) — authoritative World / rule / semantics / semantic observation source / command contract
- [`GATEWAY_ROADMAP.md`](GATEWAY_ROADMAP.md) — read-only Observation Request / subscription / cache / delivery / Protocol adaptation
- [`VIEW_ROADMAP.md`](VIEW_ROADMAP.md) — 完全read-onlyな観測・描画・Inspector・presentation
- [`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md) — World / City / Serverを明示的に変更するcommand client / management UI

このREADME自体はTask状態の正本ではありません。各Taskの状態・依存・完了条件は対応Roadmapを参照します。

## 責務の最上位ルール

| 関心事 | 正本 |
| --- | --- |
| World state / rule / semantic state / schedule / history | Simulation |
| domainごとのauthoritative observation source / semantic payload | Simulation |
| Observation Request / subscription / filtering / delivery / reconnect | Gateway |
| Entity / Spatial / Static cache / request deduplication / encoded payload cache | Gateway |
| Observation control message / delivery envelope / Protocol adaptation・negotiation | Gateway |
| Camera / rendering / LOD / Selection / Inspector / Historical presentation | View |
| model / material / audio / read-only View Addon | View |
| server-authoritative mutation command semantics / validation | Simulation |
| build / edit / remove / runtime control / Server config / Save操作UI | Management |
| Addon install / enable / trust / conflict / settings UI | Management |
| Population / Economy / Traffic等の分析・trend・heatmap | 将来Analytics Listener / analysis clientとして別設計 |

Simulationが唯一の意味的正本です。Gatewayは意味を生成せず配送・最適化だけを担当し、Viewは意味を表示するだけです。ManagementはView / Gatewayをmutation可能にせず、Simulationのauthoritative command境界からだけWorldを変更します。

## 4 Roadmapの基本Data Flow

```text
                         read-only
Simulation ─ semantic source ─→ Gateway ─→ View
     ▲                           │
     │ authoritative command     └─→ Management read side
     │
Management ──────────────────────┘

Analytics: 将来は専用Listener / data pipelineとして別設計
```

Gateway Roadmapの分離は責務・進捗管理を独立させるためのものです。現時点でGatewayを別process / repository / deploy unitへ分離することを必須とせず、`MachiVerseWorks.Server`内でmodule boundaryを確立してから必要に応じて独立可能な構成へ育てます。

## 横断Platformの扱い

4 Roadmapへ分離しても、一部のplatform contractは複数領域から利用されます。新しいTask Roadmapを増やすのではなく、contractの意味と所有者に応じて正本を決めます。

### Observation / Protocol

- domainの意味・field / unit・authoritative source: Simulation Roadmap
- Observation Request / subscription / delivery / cache / reconnect: Gateway Roadmap
- read-only rendering / Inspector利用: View Roadmap
- mutation commandの意味・validation: Simulation Roadmap
- mutation UI / command client: Management Roadmap

Protocol project自体は共有componentだが、Task ownershipは「wireに何を載せるか」ではなく「その意味や配送責務を誰が所有するか」で決めます。

### Distribution & Compatibility

Simulation Roadmap Phase 37を**project-wide release / compatibility integrationの調整正本**とします。

Phase 37にServer / Gateway / Web artifact統合Taskが存在しても、Gateway / View / Managementの機能責務がSimulationへ移ることを意味しません。各Roadmapで完成した成果物のversioning、packaging、migration、release smoke、integrity metadataをPhase 37で統合します。

### Extension Platform

- public Extension API / package / lifecycle / dependency / compatibility / Save / semantic contract: Simulation Roadmap Phase 38
- extension observation transportが必要な場合のdelivery contract: Gateway Roadmap
- read-only model / material / rendering / Inspector extension: View Roadmap Phase 12
- install / update / enable / disable / trust / settings / conflict操作: Management Roadmap Phase 5

### Localization

- View固有UIと共通Web presentation i18n基盤: View Roadmap Phase 10
- Management固有command / confirmation / permission / failure文言: Management Roadmap Phase 4
- Simulation / Gateway / Protocol / Saveは翻訳済み表示文言を正本として持たない

## 依存関係の原則

各Roadmapの`依存:` / `必須依存:`をhard gateの正本とします。

- **必須依存** — 完了に必要なauthoritative contractまたは前提Task
- **並行可能依存** — 実装を並行できるがintegration / closeoutまでに必要
- **統合依存** — component再利用や最終統合に必要だが、基礎実装開始を止めない

Roadmap間でPhase番号を同期しません。例えばSimulation Phase 29とGateway Phase 1とView Phase 1は並行して進められます。View Phase 3はSimulation Phase 29のsemantic sourceと、それを届けるGateway contractが揃った時点で実装可能になります。

GatewayもSimulation内部実装へ依存しすぎないことを原則とします。detached authoritative sourceが同じなら、Simulation Phase 32 Scheduler / Phase 33 parallelism等の内部execution方式変更をGatewayやViewのhard dependencyにしません。

Simulation Phaseがcloseoutした場合は、対応するGateway / View / Management Taskの依存が解除されたかを確認します。Gateway Phaseがcloseoutした場合も、対応するView / Management Taskの依存を確認します。逆にClient側で新しいsemantic stateが必要になった場合はSimulationへ、新しいObservation delivery能力が必要になった場合はGatewayへTaskを切り出します。

## 並行開発の読み方

各領域は独自のPhase番号を持ちます。開発状況は例えば次のように表せます。

```text
Simulation: Phase 29
Gateway:    Phase 01
View:       Phase 01
Management: None
```

`None`は、その時点で依存が未解決などの理由により着手すべきPhaseがないことを示します。Phase番号が異なっていても問題ありません。

## 補足資料

`../docs/roadmap/` はPhaseの詳細設計・検討資料です。進捗やTask状態はこのディレクトリの4 Roadmapへ同期し、`docs/roadmap/`だけで状態を管理しません。

責務境界のarchitectureは[`../docs/architecture/overview.md`](../docs/architecture/overview.md)、read-only Observation境界は[`../docs/architecture/observation-gateway.md`](../docs/architecture/observation-gateway.md)、重要な判断は[`../docs/decisions/ADR-0007-read-only-view-observation-management-boundary.md`](../docs/decisions/ADR-0007-read-only-view-observation-management-boundary.md)を参照してください。
