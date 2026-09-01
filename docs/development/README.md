# Development

開発環境、実装ルール、CI、検証、性能計測などの**開発者向け運用ドキュメント**を管理します。

## 開発フロー / 共通ルール

- [`getting-started.md`](getting-started.md): 環境準備、build、test、Server + Web Client起動手順
- [`coding-guidelines.md`](coding-guidelines.md): C# / Simulation / Server / Protocol / Webの共通実装ルール
- [`git-workflow.md`](git-workflow.md): branch / PR / validationの標準フロー
- [`repository-settings.md`](repository-settings.md): branch protection、merge方式、GitHub Security設定の基準
- [`versioning.md`](versioning.md): `A.B.C` とルート `VERSION` の運用
- [`ci.md`](ci.md): GitHub Actions、CI、CodeQL、Dependency Reviewの運用
- [`performance.md`](performance.md): benchmark、profiling、性能指標、最適化判断の基準
- [`localization-guidelines.md`](localization-guidelines.md): 将来の多言語対応を壊さない実装ルール

共通の開発ルールはルートの [`../../AGENTS.md`](../../AGENTS.md) を正本とします。.NET SDKの基準はルートの `global.json`、Node.jsの基準は `src/web/.node-version` を正本とします。

実装計画・Task状態は、Simulation側を[`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)、View側を[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)で管理します。`docs/roadmap/` にあるPhase補足資料は詳細設計・検討用であり、Task状態の正本ではありません。

## E2E / Benchmark基盤

- [`e2e-poc.md`](e2e-poc.md): 初期End-to-End PoCの再現手順、計測点、既知ボトルネック
- [`performance-benchmark.md`](performance-benchmark.md): BenchmarkDotNet基盤、結果保存、Server / Web観測、改善候補

これらは検証手順・計測方法の基盤です。個別domainの性能baselineは次のbenchmark evidenceを参照します。

## Benchmark evidence

### Core / Read model

- [`simulation-core-benchmark.md`](simulation-core-benchmark.md): Simulation Core最小PoCの性能baseline
- [`snapshot-read-model-benchmark.md`](snapshot-read-model-benchmark.md): Snapshot / read-model生成の性能baseline

### Mobility / Population / Transit

- [`road-network-benchmark.md`](road-network-benchmark.md): Road Network spatial query / topology snapshot
- [`routing-benchmark.md`](routing-benchmark.md): Road routing search / cache
- [`road-traffic-benchmark.md`](road-traffic-benchmark.md): Vehicle tick / occupancy / snapshot
- [`phase14-intersection-benchmark.md`](phase14-intersection-benchmark.md): Intersection queued tick / controller snapshotのPhase-scoped baseline
- [`population-benchmark.md`](population-benchmark.md): Person planner / tick / managed memory
- [`pedestrian-benchmark.md`](pedestrian-benchmark.md): Pedestrian fixed-tick / routing
- [`railway-infrastructure-benchmark.md`](railway-infrastructure-benchmark.md): Railway topology / query / snapshot
- [`railway-operations-benchmark.md`](railway-operations-benchmark.md): Train / Service fixed-tick / snapshot
- [`multimodal-transit-benchmark.md`](multimodal-transit-benchmark.md): Multimodal Journey / dispatch / snapshot
- [`logistics-freight-benchmark.md`](logistics-freight-benchmark.md): Freight demand / shipment / dispatch

### Urban Infrastructure

- [`power-infrastructure-benchmark.md`](power-infrastructure-benchmark.md): Power network solver / state publish
- [`water-sewer-infrastructure-benchmark.md`](water-sewer-infrastructure-benchmark.md): Water / Sewer infrastructure solver / state publish
- [`gas-infrastructure-benchmark.md`](gas-infrastructure-benchmark.md): Gas infrastructure solver / state publish

Phase番号を含むbenchmark文書は、その時点のbaseline evidenceを識別するための名称です。現在のSimulation / View Roadmap上の進行Phaseを示す索引としては扱いません。新しいbenchmark evidenceを追加した場合は、このREADMEにも追記します。
