# Contributing to MachiVerseWorks

MachiVerseWorks への貢献に関心を持っていただきありがとうございます。

## 開発の基本方針

- 通常開発は最新の `develop` を基準にします。
- `main` はリリース系統として扱い、通常開発では直接更新しません。
- 変更は目的ごとに小さくまとめ、維持する挙動と意図的に変更する挙動を分けて説明してください。
- 不具合修正では症状を隠す回避策より、原因を持つ責務での修正を優先してください。
- 仕様変更時は `docs/specifications/`、設計変更時は `docs/architecture/` を同期してください。
- 長期間影響する重要な設計判断は `docs/decisions/` に ADR を残してください。

共通ルールの正本は [`AGENTS.md`](AGENTS.md) です。実装計画の責務と依存関係は [`roadmap/README.md`](roadmap/README.md) を入口とします。

## ブランチ名

標準として次の形式を使用します。

- `feature/<topic>`: 新機能
- `fix/<topic>`: 不具合修正
- `perf/<topic>`: 性能改善
- `refactor/<topic>`: 振る舞いを変えない構造改善
- `docs/<topic>`: 文書・公開設定
- `experiment/<topic>`: 採用未確定の実験

通常の作業ブランチは `develop` から作成し、PR で `develop` へ戻します。リリースは `develop` から `main` への PR を使用します。

## アーキテクチャ境界

- **Simulation**: authoritative World、rule、意味的state、schedule / history、semantic observation source、authoritative command contractを所有します。
- **Gateway**: read-only Observation Request、subscription、filtering、cache、deduplication、delivery、Protocol adaptation、reconnect / resyncを担当します。現在は主に`MachiVerseWorks.Server`内へ実装します。
- **Server**: headless host、transport integration、Administration / Management command boundaryを担当します。
- **Protocol**: Client / Server間の共有wire contractです。
- **Persistence**: Simulation checkpointとversioned Save Dataの変換・検証を担当します。
- **View**: Gatewayから受け取ったauthoritative observationの描画、Camera、Selection、Inspector、Rendering LOD等を担当する完全read-only clientです。
- **Management**: World / City / Serverを明示的に変更するcommand client / UIです。mutationはSimulationのserver-authoritative command境界を必ず通します。

責務を跨ぐ近道を作るより、必要なsemantic source、Observation contract、command、presentationを各Roadmapへ明示的に追加してください。

### 並行AGENT開発

開発AGENTは原則として1つの作業で1コンポーネントだけを実装します。

別コンポーネントの修正が必要になった場合は、その場で跨いで実装せず、必要な変更をIssueへ切り出し、別AGENTへ引き継ぎます。Protocolなどの共有contract変更も、実装責務ごとに分割します。

Repository全体のCI、運用ルール、共通ドキュメントだけを変更する作業は`Repository-wide tooling/docs`として扱えます。この例外を機能実装の跨ぎ変更に使用しません。

Roadmapの正本:

- [`roadmap/SIMULATION_ROADMAP.md`](roadmap/SIMULATION_ROADMAP.md)
- [`roadmap/GATEWAY_ROADMAP.md`](roadmap/GATEWAY_ROADMAP.md)
- [`roadmap/VIEW_ROADMAP.md`](roadmap/VIEW_ROADMAP.md)
- [`roadmap/MANAGEMENT_ROADMAP.md`](roadmap/MANAGEMENT_ROADMAP.md)

## Pull Request

PR には可能な範囲で次を含めてください。

- 変更の目的
- 対象コンポーネント
- 主な変更点
- 不具合修正の場合は原因
- 実施した build / test / benchmark
- UI や描画変更がある場合は必要に応じて画像・動画
- 未確認事項・既知の制約
- 仕様・設計ドキュメントの更新範囲
- 他コンポーネントへ必要なfollow-up Issue

CI 成功と、実際の Simulation / Server / Browser の動作確認は同一ではありません。必要な実機確認を行っていない場合は、その旨を明記してください。

PR の標準マージ方式は **merge commit** です。個々の開発コミットとPR境界を保持するため、通常は squash / rebase merge を使用しません。マージ済みの短命ブランチは原則として削除します。

## Issue

不具合報告では、可能な範囲で次を記載してください。

- バージョンまたはコミット
- 発生箇所（Simulation / Gateway / Server / Protocol / Persistence / View / Management など）
- 再現条件
- 期待する挙動
- 実際の挙動
- seed、設定、OS、ブラウザなどの環境情報
- ログ、画像、動画

新機能提案では、実装方法だけでなく、解決したい問題と期待する効果も記載してください。

1つの要求が複数コンポーネントへまたがる場合、要求全体を1 Issueで説明しても構いませんが、実装着手時にはコンポーネントごとの作業Issueへ分割し、依存関係を明記します。

## バージョン

ルート `VERSION` はGit運用の番号ではなく、公開成果物のRelease versionです。

- 通常のfeature / fix / refactor / docs PRでは原則として変更しません。
- `develop`向けPRだからversionを上げる、`main`向けPRだからmajorを上げる、といったbranch依存の規則はありません。
- Releaseするversionを決めたときだけ`VERSION`を明示的に変更します。
- `develop -> main`のRelease PRには、公開したいversionが設定済みの状態で含めます。

詳細は [`docs/development/versioning.md`](docs/development/versioning.md) を参照してください。Protocol version と Save format version は独立して管理します。

## ライセンス

貢献として提出されたコードや文書は、リポジトリの [`LICENSE`](LICENSE) に記載された Apache License 2.0 の下で配布されることに同意したものとして扱います。

第三者コードや素材を追加する場合は、そのライセンス条件を確認し、必要に応じて `NOTICE` や `THIRD_PARTY_NOTICES.txt` を更新してください。

行動規範は [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)、脆弱性報告は [`SECURITY.md`](SECURITY.md) を参照してください。
