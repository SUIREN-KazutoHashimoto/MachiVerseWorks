# Development

開発者向けの運用ドキュメントを管理します。

現在の主要文書:

- [`getting-started.md`](getting-started.md): 環境準備、build、test、Server + Web Client起動手順
- [`e2e-poc.md`](e2e-poc.md): Phase 6 End-to-End PoC の再現手順、計測点、既知ボトルネック
- [`performance-benchmark.md`](performance-benchmark.md): Phase 7 BenchmarkDotNet基盤、結果保存、Server/Web観測、改善候補
- [`simulation-core-benchmark.md`](simulation-core-benchmark.md): Phase 2 Simulation Core 最小 PoC の初回性能baseline
- [`road-network-benchmark.md`](road-network-benchmark.md): Road Network spatial query / topology snapshot benchmark
- [`routing-benchmark.md`](routing-benchmark.md): Phase 12 small / medium / large Road routing search / cache benchmark
- [`road-traffic-benchmark.md`](road-traffic-benchmark.md): Phase 13 1,000 / 10,000 / 100,000 Vehicle tick / occupancy / snapshot benchmark
- [`phase14-intersection-benchmark.md`](phase14-intersection-benchmark.md): Phase 14 queued intersection tick / controller snapshot benchmark baseline
- [`population-benchmark.md`](population-benchmark.md): Phase 15 1,000 / 10,000 / 100,000 Person planner / tick / managed memory benchmark
- [`pedestrian-benchmark.md`](pedestrian-benchmark.md): Phase 16 1,000 / 10,000 Pedestrian fixed-tick / routing benchmark
- [`git-workflow.md`](git-workflow.md): branch / PR / validation の標準フロー
- [`repository-settings.md`](repository-settings.md): branch protection、merge方式、GitHub Security設定の基準
- [`versioning.md`](versioning.md): `A.B.C` とルート `VERSION` の運用
- [`ci.md`](ci.md): GitHub Actions、CI、CodeQL、Dependency Review の運用
- [`coding-guidelines.md`](coding-guidelines.md): C# / Simulation / Server / Protocol / Web の共通実装ルール
- [`performance.md`](performance.md): benchmark、profiling、性能指標、最適化判断の基準
- [`localization-guidelines.md`](localization-guidelines.md): 将来の多言語対応を壊さない実装ルール

今後ここへ、必要に応じて次の文書を追加します。

- testing.md

共通の開発ルールはルートの `AGENTS.md` を正本とします。

.NET SDK の基準はルートの `global.json` を正本とし、CIも同じ設定を使用します。
Node.js の基準は `src/web/.node-version` を正本とします。
