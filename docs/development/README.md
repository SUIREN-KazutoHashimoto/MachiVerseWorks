# Development

開発者向けの運用ドキュメントを管理します。

現在の主要文書:

- [`getting-started.md`](getting-started.md): 初回の環境準備、build、test、Web Client 起動手順
- [`git-workflow.md`](git-workflow.md): branch / PR / validation の標準フロー
- [`repository-settings.md`](repository-settings.md): branch protection、merge方式、GitHub Security設定の基準
- [`versioning.md`](versioning.md): `A.B.C` とルート `VERSION` の運用
- [`ci.md`](ci.md): GitHub Actions、CI、CodeQL、Dependency Review の運用
- [`coding-guidelines.md`](coding-guidelines.md): C# / Simulation / Server / Protocol / Web の共通実装ルール
- [`performance.md`](performance.md): benchmark、profiling、性能指標、最適化判断の基準
- [`simulation-core-benchmark.md`](simulation-core-benchmark.md): Phase 2 Simulation Core 最小 PoC の初回性能 baseline
- [`localization-guidelines.md`](localization-guidelines.md): 将来の多言語対応を壊さない実装ルール

今後ここへ、必要に応じて次の文書を追加します。

- testing.md

共通の開発ルールはルートの `AGENTS.md` を正本とします。

.NET SDK の基準はルートの `global.json` を正本とし、CI も同じ設定を使用します。
Node.js の基準は `src/web/.node-version` を正本とします。
