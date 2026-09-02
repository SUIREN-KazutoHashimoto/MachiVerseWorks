# TODO

このファイルは、将来対応を忘れないための**メモと正本Roadmapへの入口**です。Taskの実装状態・完了状態はここでは管理しません。

Task IDと状態記号の正本は、責務に応じて以下の4 Roadmapとします。

- `roadmap/SIMULATION_ROADMAP.md`
- `roadmap/GATEWAY_ROADMAP.md`
- `roadmap/VIEW_ROADMAP.md`
- `roadmap/MANAGEMENT_ROADMAP.md`

## First Alpha Release

最初のalphaリリース準備を開始する目安は次の状態とする。

- Simulation: Phase 31 完了
- Gateway: Phase 6 完了（旧 Observation Gateway Foundation closeout 相当）
- View: Phase 5 完了
- Management: 初回alphaでは必須としない

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

初回alphaはPhase 37全体の完了を意味しない。Simulation Phase 31 + Gateway Phase 6 + View Phase 5で成立する最初の観測可能なvertical sliceを公開するため、Phase 37のうち初回配布に必要なTaskだけを先行して利用する。

本格的なSave migration、distribution compatibility、release automation、artifact traceabilityのcloseoutは、引き続きSimulation Phase 37の完了条件で管理する。
