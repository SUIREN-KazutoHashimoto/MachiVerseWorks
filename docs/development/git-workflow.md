# Git / 開発ワークフロー

この文書は、旧 Machi-Sim の開発・不具合修正手順から、MachiVerseWorks でも有効な原則を引き継ぎ、新アーキテクチャ向けに整理したものです。

共通ルールの正本はルートの [`AGENTS.md`](../../AGENTS.md) です。

## 1. 基本フロー

通常開発は次を基本とします。

```text
develop latest
  ↓
feature / fix / perf / refactor / docs / experiment branch
  ↓
implementation + validation
  ↓
PR → develop
  ↓
required checks
  ↓
merge commit
```

リリース時は `develop` から `main` へ PR を作成します。

リポジトリ初期セットアップ期間は例外として、明示された基盤整備を `main` に直接反映する場合があります。通常開発開始後はこの例外を使用しません。

### ブランチ名

- `feature/<topic>`: 新機能
- `fix/<topic>`: 不具合修正
- `perf/<topic>`: 性能改善
- `refactor/<topic>`: 振る舞いを変えない構造改善
- `docs/<topic>`: 文書・公開設定
- `experiment/<topic>`: 採用未確定の実験

新機能ブランチは `feature/*` を正規名とし、`feat/*` は使用しません。

### マージ方式

PR の標準マージ方式は **merge commit** とします。

- 個々の開発コミットと version 推移を残すため、通常は squash merge を使用しない。
- commit SHA を書き換えないため、通常は rebase merge を使用しない。
- GitHub が PR マージ時に生成する merge commit は管理上のコミットとして扱い、version `C` を別途加算しない。
- マージ済みの `feature/*` / `fix/*` / `perf/*` / `refactor/*` / `docs/*` / `experiment/*` は原則削除する。
- `main` と `develop` は長期ブランチとして削除しない。

GitHub Repository 側で設定する値は [`repository-settings.md`](repository-settings.md) を参照してください。

## 2. 作業開始前

変更前に最低限、次を確認します。

1. 対象の状態・責務を所有する project / class / module
2. 呼び出し元と出力先
3. 関連する test
4. 関連する仕様 (`docs/specifications/`)
5. 関連する設計 (`docs/architecture/`)
6. 重要な採用理由がある場合は ADR (`docs/decisions/`)
7. 対応する `ROADMAP.md` の Task ID

ファイル名や古い資料だけで現在の挙動を決めつけず、実効コードと test を確認します。

## 3. 新機能

要求を次の観点に分解します。

- 外から見える挙動
- state / data owner
- simulation update
- Server command / API
- Protocol message / snapshot
- Web Client / rendering / UI
- config / persistence
- performance / scalability
- documentation
- test / benchmark

### 状態の所有者

同じ情報を複数の subsystem で推定し直さず、正規の state owner から取得します。

特に、Simulation の状態を Server や Web Client が独自に再構築して別の正本を作らないようにします。

## 4. 不具合修正

修正前に可能な範囲で整理します。

```text
症状:
期待する挙動:
実際の挙動:
再現条件:
再現頻度:
影響範囲:
version / commit / branch:
```

その後、次の順で進めます。

1. 実効 code path を追う
2. 原因を一文で説明できる状態にする
3. 原因を持つ最小の責務で修正する
4. 元の再現条件で確認する
5. 近接する正常ケースが壊れていないか確認する
6. 必要なら仕様・設計・ADRを同期する

Timeout、強制リセット、fallback は安全網として有効な場合がありますが、原因が別にある場合は回避策だけを最終修正にしません。

## 5. アーキテクチャ境界の確認

変更時は依存方向を確認します。

```text
Browser Client
      ↕
Protocol
      ↕
Server
      ↓
Simulation
```

- Simulation は HTTP / WebSocket / browser API を知りません。
- Server は Simulation の内部可変配列を network thread から直接共有し続けません。
- Protocol は内部 object graph のダンプではなく、外部契約として設計します。
- Web Client は snapshot / delta を表示する側で、authoritative world を持ちません。

## 6. 性能変更

性能改善は計測結果を起点にします。

- 変更前の基準値を残す
- tick time、p50 / p95、allocation、snapshot size、network send time、render time など対象に合った指標を選ぶ
- Agent ごとの Task、hot path の LINQ、不要な object allocation、全件 scan を安易に追加しない
- 最適化によって仕様や determinism が変わる場合は、単なる `perf` として扱わず明示する

## 7. 検証

変更内容に応じて次を組み合わせます。

- build
- unit test
- integration test
- protocol compatibility test
- benchmark
- Server / Client 結合確認
- Browser 実機確認
- 複数 seed / config の確認

CI が成功していても、runtime behavior を確認していない場合は「確認済み」と表現しません。

## 8. Pull Request

PR本文には最低限、可能な範囲で次を含めます。

- 目的
- 原因（bugfix の場合）
- 実装内容
- 重要な semantic / protocol / performance 変更
- 検証結果
- 未確認事項
- 既知制約
- ドキュメント更新範囲

## 9. ドキュメント同期

役割は次の通りです。

- external / simulation behavior → `docs/specifications/`
- architecture / responsibility → `docs/architecture/`
- 開発・テスト・運用 → `docs/development/`
- 長期的な設計判断と理由 → `docs/decisions/`
- 廃止済み・Legacy・実験履歴 → `docs/archive/`

過去資料を現行仕様の根拠へ戻さないようにします。

## 10. 完了条件

作業完了時は次を確認します。

- 実装と文書が同じ意味を説明している
- 必要な test / build / benchmark が成功している
- runtime 確認が必要な項目は確認済み、または未確認と明記している
- 不要な debug code / experiment flag が残っていない
- `ROADMAP.md` の Task 状態が同期されている
- 通常開発期間では `AGENTS.md` と `versioning.md` の version 規則へ従っている
