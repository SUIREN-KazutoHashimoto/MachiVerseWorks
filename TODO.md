# TODO

このファイルは、将来対応を忘れないための**メモと正本Roadmapへの入口**です。Taskの実装状態・完了状態はここでは管理しません。

Task IDと状態記号の正本は、責務に応じて以下の4 Roadmapとします。

- `roadmap/SIMULATION_ROADMAP.md`
- `roadmap/GATEWAY_ROADMAP.md`
- `roadmap/VIEW_ROADMAP.md`
- `roadmap/MANAGEMENT_ROADMAP.md`

## Parallel Development Working Memo

Simulation / Gateway / Viewを並行して進めるときの**作業順を考えるためだけの仮メモ**です。

ここで使う「開発段階」はRoadmap上の正式なPhaseではなく、Task状態・完了判定・依存関係の正本にも使用しません。実際に着手できるかどうかは、各Roadmapに記載された必須依存を優先して判断します。

また、同じ行にある3領域が同時に完了するまで次の行へ進めない、という意味ではありません。ある領域が先行しても、その先のPhaseの必須依存を満たしているなら先へ進めて構いません。`None` は、その段階では無理に並行作業を作らず待機してよいことを示します。

| 仮の開発段階 | Simulation | Gateway | View |
| --- | --- | --- | --- |
| 1 | Phase 29 | Phase 1 | Phase 1 |
| 2 | Phase 29 | Phase 2 | Phase 2 |
| 3 | Phase 29 | Phase 3 | Phase 3 |
| 4 | Phase 30 | Phase 4 | Phase 4 |
| 5 | Phase 31 | Phase 4 | Phase 5 |
| 6 | Phase 31 | Phase 4 | Phase 6 |
| 7 | Phase 32 | Phase 4 | Phase 6 |
| 8 | Phase 33 | Phase 4 | Phase 7 |
| 9 | Phase 33 | Phase 4 | Phase 8 |
| 10 | Phase 35 | Phase 5 | Phase 9 |
| 11 | Phase 36 | Phase 6 | Phase 10 |
| 12 | Phase 37 | None | Phase 11 |
| 13 | Phase 38 | None | Phase 12 |
| 14 | None | None | Phase 13 |

### Memo usage

- Simulation / Gateway / ViewのPhase番号を同期させる意図はない。
- GatewayがSimulation / Viewより先行しても、Gateway Roadmap上の必須依存を満たす限りそのまま進めてよい。
- Viewのdomain描画は、対象Simulationのauthoritative semantic sourceと必要なGateway delivery contractが揃うまで待つ。
- Gateway Phase 4以降のようにSimulation semantic observationへ依存する箇所は、必要なsourceが未完成なら先行しない。
- Gateway Phase 5はSimulation Phase 35のhistory / replay sourceを待つ。
- `None` の領域に、並行化のためだけの仮実装や別正本を作らない。
- この表とRoadmapが食い違う場合はRoadmapを優先し、必要に応じてこのメモだけを気軽に更新する。

## First Alpha Release

最初のalphaリリース準備を開始する目安は次の状態とする。

- Simulation: Phase 31 完了
- Gateway: Phase 3 完了
- View: Phase 5 完了
- Management: 初回alphaでは必須としない

Gateway Phase 3まででbaseline Observation boundary、subscription / delivery、reconnect / resyncを揃え、最初の観測可能なvertical sliceを成立させる。Generic Inspector / Temporal ObservationやHistorical Replayは初回alphaの必須条件にはせず、必要になった機能だけ各Roadmapの依存に従って追加する。

これはrelease milestoneのメモであり、個別作業の状態管理はRoadmap側で行う。

### Canonical Roadmap tasks

初回alphaに必要なrelease作業は、Simulation Roadmap Phase 37の既存Taskをproject-wide release / compatibility integrationの調整正本として追跡する。Phase 37全体のcloseoutはPhase 36依存を維持するが、Phase 37本文の規則どおり、安定した既存境界だけで実行できる配布Taskは初回alpha向けに先行してよい。

- supported artifact / platform: `P37-006`〜`P37-010`
- release metadata / traceability: `P37-012`〜`P37-013`
- release smoke test: `P37-014`
- install / release procedure documentation: `P37-015`
- develop→mainのversion / artifact / release note手順: `P37-016`
- architecture / development docs / Roadmap同期: `P37-017`

初回alphaで追加の独立成果が必要になった場合は、このファイルへchecklistを追加せず、責務に応じたRoadmapへ小さなTask IDとして追加する。

### Versioning note

現在のルート `VERSION` は既存のApplication / development versionとして履歴を維持し、**一度使用した値より後退させない**。

外部公開alphaを `0.1.0-alpha.1` のような製品向けRelease Versionから開始したい場合は、既存 `VERSION` をその値へ巻き戻さず、Application / development versionとRelease Versionを別の識別子として扱う。Release Versionの正本ファイル名・生成方法・`VERSION`との関係は、初回alpha準備時に `P37-016` の一部として確定し、`docs/development/versioning.md`、`AGENTS.md`、`CONTRIBUTING.md`、CIを同期してから使用する。

想定する識別の役割は次のとおり。

- Application / development version: 現在の `VERSION` 系列を継続し、既存artifact・diagnostics・bug reportとの連続性を保つ
- Release Version: `0.1.0-alpha.1` 等の外部公開用SemVer prerelease系列
- Build identity: commit SHA / CI build identifier
- Protocol Version: wire互換性。Release Versionとは独立
- Save Format Version: Save互換性。Release Versionとは独立

Git tag / GitHub Pre-release / release notes / 配布artifactの外向け識別にはRelease Versionを使用し、diagnosticsではRelease Version、Application / development version、commit SHAを相互に追跡できる形を目標とする。

### Release boundary

初回alphaはPhase 37全体の完了を意味しない。Simulation Phase 31 + Gateway Phase 3 + View Phase 5で成立する最初の観測可能なvertical sliceを公開するため、Phase 37のうち初回配布に必要なTaskだけを先行して利用する。

本格的なSave migration、distribution compatibility、release automation、artifact traceabilityのcloseoutは、引き続きSimulation Phase 37の完了条件で管理する。
