# MachiVerseWorks Roadmaps

`roadmap/` はMachiVerseWorksの実装計画を責務別に管理します。Task状態の正本は次の3ファイルです。

- [`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md) — authoritative World / semantics / Server・Protocol・Persistence境界 / shared platform contract
- [`VIEW_ROADMAP.md`](VIEW_ROADMAP.md) — 完全read-onlyな観測・描画・Inspector・presentation
- [`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md) — World / City / Serverを明示的に変更するcommand client / management UI

このREADME自体はTask状態の正本ではありません。各Taskの状態・依存・完了条件は対応Roadmapを参照します。

## 責務の最上位ルール

| 関心事 | 正本 |
| --- | --- |
| World state / rule / semantic state / schedule / history | Simulation |
| Observation read model / Protocol / Save / server-authoritative command contract | Simulation |
| Observation Gateway / cache / subscription / read-only delivery | Simulation Roadmapのcross-cutting Server基盤 |
| Camera / rendering / LOD / Selection / Inspector / Historical presentation | View |
| model / material / audio / read-only View Addon | View |
| build / edit / remove / runtime control / Server config / Save操作UI | Management |
| Addon install / enable / trust / conflict / settings UI | Management |
| Population / Economy / Traffic等の分析・trend・heatmap | 将来Analytics Listener / analysis clientとして別設計 |

Simulationが唯一の意味的正本です。Viewは意味を生成せず、ManagementはViewをmutation可能にせず、authoritative command境界からだけWorldを変更します。

## 横断Platformの扱い

3 Roadmapへ分離しても、一部のplatform contractは全領域から利用されます。これらは4つ目のTask Roadmapを作らず、contractの正本を持つ領域で調整します。

### Observation Gateway

Simulation Roadmapの`OBS-*`を正本とします。

- Viewはread-only Observation contractだけへ依存する。
- Managementもcommand成功後のauthoritative result確認にObservationを再利用する。
- cache / deduplication / encoding最適化はServer側責務であり、View側へ意味処理を移さない。

### Distribution & Compatibility

Simulation Roadmap Phase 37を**project-wide release / compatibility integrationの調整正本**とします。

Phase 37にWeb / Server / container等のartifact統合Taskが存在しても、ViewやManagementの機能責務がSimulationへ移ることを意味しません。Client固有のpresentation / command実装は各Roadmapを正本とし、Phase 37は完成した成果物のversioning、packaging、migration、release smoke、integrity metadataを統合します。

### Extension Platform

- public Extension API / package / lifecycle / dependency / compatibility / Save / Protocol contract: Simulation Roadmap Phase 38
- read-only model / material / rendering / Inspector extension: View Roadmap Phase 12
- install / update / enable / disable / trust / settings / conflict操作: Management Roadmap Phase 5

### Localization

- View固有UIと共通Web presentation i18n基盤: View Roadmap Phase 10
- Management固有command / confirmation / permission / failure文言: Management Roadmap Phase 4
- Simulation / Protocol / Saveは翻訳済み文言を正本として持たない

## 依存関係の原則

各Roadmapの`依存:` / `必須依存:`をhard gateの正本とします。

- **必須依存** — 完了に必要なauthoritative contractまたは前提Task
- **並行可能依存** — 実装を並行できるがintegration / closeoutまでに必要
- **統合依存** — component再利用や最終統合に必要だが、基礎実装開始を止めない

Roadmap間でPhase番号を同期しません。例えばView Phase 3はSimulation Phase 29のTerrain observationが完成した時点で実装可能になり、View Phase 6はSimulation Phase 32 / 33のScheduler / parallelismそのものを待ちません。Viewは公開Observation contractへ依存し、Simulation内部の実行方式へ依存しないためです。

Simulation Phaseがcloseoutした場合は、対応するView / Management Taskの依存が解除されたかを確認します。逆にView / Management側で新しいsemantic stateやmutation contractが必要になった場合は、Client側で仮実装せずSimulation RoadmapへTaskを切り出します。

## 補足資料

`../docs/roadmap/` はPhaseの詳細設計・検討資料です。進捗やTask状態はこのディレクトリの3 Roadmapへ同期し、`docs/roadmap/`だけで状態を管理しません。

責務境界のarchitectureは[`../docs/architecture/overview.md`](../docs/architecture/overview.md)、read-only Observation境界は[`../docs/architecture/observation-gateway.md`](../docs/architecture/observation-gateway.md)、重要な判断は[`../docs/decisions/ADR-0007-read-only-view-observation-management-boundary.md`](../docs/decisions/ADR-0007-read-only-view-observation-management-boundary.md)を参照してください。
