# Development

開発者向けの運用ドキュメントを管理します。

現在の主要文書:

- [`git-workflow.md`](git-workflow.md): branch / PR / validation の標準フロー
- [`repository-settings.md`](repository-settings.md): branch protection、merge方式、GitHub Security設定の基準
- [`versioning.md`](versioning.md): `A.B.C` と将来のルート `VERSION` の運用
- [`ci.md`](ci.md): GitHub Actions、CI、CodeQL、Dependency Review の運用
- [`coding-guidelines.md`](coding-guidelines.md): C# / Simulation / Server / Protocol / Web の共通実装ルール
- [`performance.md`](performance.md): benchmark、profiling、性能指標、最適化判断の基準
- [`localization-guidelines.md`](localization-guidelines.md): 将来の多言語対応を壊さない実装ルール

今後ここへ、必要に応じて次の文書を追加します。

- getting-started.md
- testing.md

共通の開発ルールはルートの `AGENTS.md` を正本とします。

.NET SDK の基準はルートの `global.json` を正本とし、CI も同じ設定を使用します。
