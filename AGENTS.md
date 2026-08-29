# MachiVerseWorks 開発ルール

このファイルは、ChatGPT / Codex を含む開発エージェントと人間の開発者が、MachiVerseWorks で作業するときの共通ルールです。

## 1. 基本方針

- ドキュメント、Issue、PR の説明は原則として日本語で記述する。
- クラス名、メソッド名、API 名などのコード識別子は英語を基本とする。
- 仕様を推測で変更しない。仕様変更が必要な場合は、関連ドキュメントも同時に更新する。
- 既存の責務分離を崩して短期的に動かすだけの実装を避ける。
- 大規模シミュレーションを前提とし、ホットパスでの不要な allocation、LINQ、全件走査、Agent ごとの Task 作成を安易に導入しない。
- 最適化は必ず計測可能な根拠を持って行う。

## 2. アーキテクチャ境界

### MachiVerseWorks.Simulation

都市シミュレーションの正本です。

- HTTP、WebSocket、ASP.NET Core、ブラウザ固有 API に依存しない。
- World、Agent、Traffic、Transit、Logistics、Power などの状態とルールを保持する。
- 外部からは明確な command / step / snapshot 境界を通して操作する。

### MachiVerseWorks.Server

実行ホストと通信境界です。

- Simulation Core のライフサイクルと tick を管理する。
- クライアント command を Simulation へ渡す。
- Snapshot / delta / statistics をクライアントへ配信する。
- Simulation 内部の可変データをネットワーク処理から直接参照し続けない。

### MachiVerseWorks.Protocol

クライアント・サーバー間契約です。

- message type、version、binary layout、control message を管理する。
- Simulation の内部データ構造をそのまま公開 API にしない。
- 後方互換性が必要になった場合に protocol version を独立して管理できる構成にする。

### Web Client

表示と入力を担当します。

- Server から受け取った状態を描画する。
- Simulation の正本にならない。
- 表示 FPS と Simulation tick を分離し、必要に応じて補間する。

## 3. ディレクトリルール

- `src/`: 実行コード
- `tests/`: 自動テスト
- `benchmarks/`: 性能評価コード
- `docs/product/`: 目的・概念・用語
- `docs/architecture/`: 実装アーキテクチャ（How）
- `docs/specifications/`: シミュレーション仕様（What / Why）
- `docs/development/`: 開発・テスト・Git 運用
- `docs/decisions/`: ADR
- `docs/archive/`: 廃止済み資料・Legacy 資料・実験記録
- `scripts/`: 開発・CI 補助スクリプト
- `tools/`: 独立した開発支援ツール

ドキュメントをルートへ無秩序に追加しない。ルートへ置くのは、`README.md`、`ROADMAP.md`、ライセンス・貢献・開発ルールなど、リポジトリ全体の入口として必要なファイルに限定する。

## 4. ドキュメントルール

- 仕様（What / Why）と設計（How）を同じ文書へ混在させすぎない。
- 現行仕様は `docs/specifications/` を正とする。
- 技術構成や責務分離は `docs/architecture/` を正とする。
- 採用理由を将来説明する必要がある設計判断は `docs/decisions/` に ADR を作成する。
- 廃止した資料は削除ではなく、参照価値がある場合のみ `docs/archive/` へ移す。
- `archive` を未整理ファイルの一時置き場として使わない。
- 将来の予定や作業状態は仕様書へ混ぜず、`ROADMAP.md` で管理する。

## 5. Git 運用

通常開発では次の流れを基本とする。

```text
main
  └─ develop
       └─ feature/* / fix/* / docs/* / refactor/* / experiment/*
```

- 通常の実装は短命な作業ブランチで行う。
- `develop` への統合は PR を使用する。
- リリースは `develop` から `main` への PR を使用する。
- 実験コードは実験ブランチに閉じ込め、採用しない場合は本流へ混ぜない。
- PR をマージする前に、必要な build / test / benchmark / static analysis を確認する。

## 6. バージョン運用

バージョンは `A.B.C` 形式で管理する。

- `A`: `main` 向け PR を作成するときに `+1` し、`B = 0`, `C = 0` にリセットする。
- `B`: `develop` 向け PR を作成するときに `+1` し、`C = 0` にリセットする。
- `C`: 通常のコミットを作成するときに `+1` する。

例: `1.4.12`

```text
通常コミット       -> 1.4.13
develop 向け PR   -> 1.5.0
その後のコミット   -> 1.5.1
main 向け PR      -> 2.0.0
```

PR に伴う A / B の更新コミットは、PR 作成のためのバージョン更新として扱い、同じ操作で C を別途加算しない。

### 初期セットアップ例外

リポジトリ初期セットアップ期間中は、明示的に通常開発へ移行するまでバージョンをカウントアップしない。初期セットアップのためだけにバージョンファイルを作成・更新しない。

## 7. 完了条件

作業を Done とするには、対象に応じて次を満たすこと。

- 実装またはドキュメント変更が完了している。
- 必要な build / test / benchmark が成功している。
- 仕様を変更した場合は関連ドキュメントが更新されている。
- 新しい設計判断が重要な場合は ADR が追加または更新されている。
- 一時的なデバッグコード、不要なログ、実験用フラグが本流に残っていない。
- `ROADMAP.md` の対象タスクがある場合は、実際の完了状態とチェック状態が一致している。

## 8. エージェント向け注意

- 依頼されていない破壊的変更、ブランチ削除、Release 削除、Repository 設定変更を勝手に行わない。
- PR のマージは、ユーザーが明示的に依頼した場合、または現在の作業指示から明確にマージまで求められている場合のみ行う。
- 既に会話やリポジトリから判明している情報を再質問しない。
- 大きな変更では、実装前に既存構造と関連コードを調査する。
- 変更は可能な限り論理的にまとまった単位で行う。

## 9. 多言語対応の前提

現時点では日本語を主言語として開発するが、将来の localization を壊さないため次を守る。

- default locale は `ja-JP` とし、locale tag は BCP 47 形式で扱う。
- Simulation Core の状態へ翻訳済み UI 文言を持ち込まない。
- Protocol の正式契約へ日本語や英語などの翻訳済みエラーメッセージを埋め込まず、stable code と structured parameter を使用する。
- Save Data には翻訳済みラベルではなく stable ID / enum / code / raw value を保存する。
- ユーザー向け表示文言の localization と数値・日時・単位 formatting は Web Client の責務とする。
- Web Client の locale resource は `src/web/locales/` を正規入口とする。
- Web Client の本実装開始後は、固定 UI 文言を可能な限り locale resource key 経由で参照する。
- 翻訳文を単語単位でコード上で連結せず、named parameter を持つ message として扱う。
- ユーザー入力文字列、固有名詞、外部コンテンツはシステム UI 文言と区別する。
- i18n library は Web Client 実装開始時に選定し、初期セットアップ段階では固定しない。

詳細は `docs/architecture/localization.md`、`docs/development/localization-guidelines.md`、`docs/decisions/ADR-0002-localization-boundary.md` を参照する。

## 10. ROADMAP 運用

`ROADMAP.md` は、現在および将来の実装を小さな完了可能タスクとして追跡する正本とする。

- 作業開始前に、依頼内容に対応する既存 Task ID があるか確認する。
- 対応タスクが存在しない計画済み作業は、必要に応じて小さなタスクへ分解して `ROADMAP.md` に追加する。
- 1つのチェック項目へ複数の独立した成果を詰め込まない。
- 「交通を完成」「UIを完成」など長期間閉じられない粒度のチェック項目を作らない。
- 作業中に想定より大きいことが分かった場合は、元タスクを無理に完了させず残作業を新しい Task ID へ分割する。
- 実装だけ終わって検証が残っている項目は `[x]` にしない。
- 完了報告をする前に、対象 Task ID のチェックを `[x]` へ同期する。
- 未実装の大テーマはチェック項目ではなく Backlog として置き、着手時に分解する。
- ROADMAP は仕様書ではない。仕様の正本は `docs/specifications/`、設計の正本は `docs/architecture/` とする。
