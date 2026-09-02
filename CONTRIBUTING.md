# Contributing to MachiVerseWorks

MachiVerseWorks への貢献に関心を持っていただきありがとうございます。

## 開発の基本方針

- 通常開発は最新の `develop` を基準にします。
- `main` はリリース系統として扱い、通常開発では直接更新しません。
- リポジトリ初期セットアップ期間だけは、明示された作業に限り `main` を直接整備する場合があります。
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
- **View**: Gatewayから受け取ったauthoritative observationの描画、Camera、Selection、Inspector、Rendering LOD等を担当する完全read-only clientです。
- **Management**: World / City / Serverを明示的に変更するcommand client / UIです。mutationはSimulationのserver-authoritative command境界を必ず通します。
- `MachiVerseWorks.Persistence`: Simulation checkpointとversioned Save Dataの変換・検証を担当します。
- `MachiVerseWorks.Protocol`: 共有wire componentです。domain semantic payloadはSimulation、Observation control / deliveryはGateway、mutation command semanticsはSimulationという責務分離を維持します。

責務を跨ぐ近道を作るより、必要なsemantic source、Observation contract、command、presentationを各Roadmapへ明示的に追加してください。

Roadmapの正本:

- [`roadmap/SIMULATION_ROADMAP.md`](roadmap/SIMULATION_ROADMAP.md)
- [`roadmap/GATEWAY_ROADMAP.md`](roadmap/GATEWAY_ROADMAP.md)
- [`roadmap/VIEW_ROADMAP.md`](roadmap/VIEW_ROADMAP.md)
- [`roadmap/MANAGEMENT_ROADMAP.md`](roadmap/MANAGEMENT_ROADMAP.md)

## Pull Request

PR には可能な範囲で次を含めてください。

- 変更の目的
- 主な変更点
- 不具合修正の場合は原因
- 実施した build / test / benchmark
- UI や描画変更がある場合は必要に応じて画像・動画
- 未確認事項・既知の制約
- 仕様・設計ドキュメントの更新範囲

CI 成功と、実際の Simulation / Server / Browser の動作確認は同一ではありません。必要な実機確認を行っていない場合は、その旨を明記してください。

PR の標準マージ方式は **merge commit** です。version履歴と個々のコミットを保持するため、通常は squash / rebase merge を使用しません。マージ済みの短命ブランチは原則として削除します。

## Issue

不具合報告では、可能な範囲で次を記載してください。

- バージョンまたはコミット
- 発生箇所（Simulation / Gateway / View / Management / Server host / Protocol など）
- 再現条件
- 期待する挙動
- 実際の挙動
- seed、設定、OS、ブラウザなどの環境情報
- ログ、画像、動画

新機能提案では、実装方法だけでなく、解決したい問題と期待する効果も記載してください。

## バージョン

通常開発へ移行後は `AGENTS.md` と [`docs/development/versioning.md`](docs/development/versioning.md) の `A.B.C` ルールに従います。

- `A`: `main` 向け PR 作成時に +1。`B` と `C` を 0 にリセット
- `B`: `develop` 向け PR 作成時に +1。`C` を 0 にリセット
- `C`: 通常コミット作成時に +1

通常開発開始後はルート `VERSION` をアプリケーションバージョンの正本とします。Protocol version と Save format version は独立して管理します。

初期セットアップ期間は、明示的に終了するまでバージョンを更新せず、`VERSION` も作成しません。

## ライセンス

貢献として提出されたコードや文書は、リポジトリの [`LICENSE`](LICENSE) に記載された Apache License 2.0 の下で配布されることに同意したものとして扱います。

第三者コードや素材を追加する場合は、そのライセンス条件を確認し、必要に応じて `NOTICE` や `THIRD_PARTY_NOTICES.txt` を更新してください。

行動規範は [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)、脆弱性報告は [`SECURITY.md`](SECURITY.md) を参照してください。