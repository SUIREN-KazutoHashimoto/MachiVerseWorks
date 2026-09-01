# Git / 開発ワークフロー

この文書は、旧 Machi-Sim の開発・不具合修正手順から、MachiVerseWorks でも有効な原則を引き継ぎ、新アーキテクチャ向けに整理したものです。

共通ルールの正本はルートの[`AGENTS.md`](../../AGENTS.md)です。

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

リリース時は`develop`から`main`へPRを作成します。

### ブランチ名

- `feature/<topic>`: 新機能
- `fix/<topic>`: 不具合修正
- `perf/<topic>`: 性能改善
- `refactor/<topic>`: 振る舞いを変えない構造改善
- `docs/<topic>`: 文書・公開設定
- `experiment/<topic>`: 採用未確定の実験

新機能ブランチは`feature/*`を正規名とし、`feat/*`は使用しません。

### マージ方式

PRの標準マージ方式は **merge commit** とします。

- 個々の開発コミットとversion推移を残すため、通常はsquash mergeを使用しない。
- commit SHAを書き換えないため、通常はrebase mergeを使用しない。
- GitHubがPRマージ時に生成するmerge commitは管理上のコミットとして扱い、version `C`を別途加算しない。
- マージ済みの短命branchは原則削除する。
- `main`と`develop`は長期branchとして削除しない。

GitHub Repository側で設定する値は[`repository-settings.md`](repository-settings.md)を参照してください。

## 2. 作業開始前

変更前に最低限、次を確認します。

1. 対象の状態・責務を所有するproject / class / module
2. 呼び出し元と出力先
3. 関連するtest
4. 関連する仕様 (`docs/specifications/`)
5. 関連する設計 (`docs/architecture/`)
6. 重要な採用理由がある場合はADR (`docs/decisions/`)
7. 対応する`roadmap/SIMULATION_ROADMAP.md`、`roadmap/VIEW_ROADMAP.md`、`roadmap/MANAGEMENT_ROADMAP.md`のTask ID

ファイル名や古い資料だけで現在の挙動を決めつけず、実効コードとtestを確認します。

## 3. 新機能

要求を次の観点に分解します。

- authoritative state / semantic processing
- Observation read model / Server delivery
- Management command / validation
- Protocol
- View rendering / Camera / Selection / Inspector
- Management UI / editor / operation
- config / persistence
- performance / scalability
- documentation
- test / benchmark

責務分類:

- Simulationのstate / rule / meaning / schedule / Observation contract / authoritative command → Simulation Roadmap
- read-only描画 / Camera / Selection / Inspector / Historical viewing / Rendering LOD → View Roadmap
- build / edit / runtime control / Server config / Save UI → Management Roadmap
- 統計分析 / trend / heatmap等 → View / Managementへ入れず将来Analytics系として別設計

Simulation / View / Managementにまたがる機能では、1 Taskへ混在させず責務ごとに分割します。

### ViewのSimulation追従

View RoadmapはSimulation RoadmapとPhase番号を合わせません。View固有基盤をPhase 1から積み上げます。

Simulationから移管されたView Taskは、次の両方を満たした時点で実装します。

1. View側の前提Taskが完了している。
2. 依存するSimulation Phase / Observation read modelが実装済みである。

依存Simulationが未完成の場合、Viewが仮のsemantic stateを生成して先行実装しません。

### 状態の所有者

同じ情報を複数subsystemで推定し直さず、正規のstate ownerから取得します。

特にSimulationの意味的stateをServer / View / Managementが独自に再構築して別の正本を作らないようにします。

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

1. 実効code pathを追う
2. 原因を一文で説明できる状態にする
3. 原因を持つ最小の責務で修正する
4. 元の再現条件で確認する
5. 近接する正常ケースが壊れていないか確認する
6. 必要なら仕様・設計・ADRを同期する

Timeout、強制リセット、fallbackは安全網として有効な場合がありますが、原因が別にある場合は回避策だけを最終修正にしません。

## 5. アーキテクチャ境界の確認

変更時は依存方向を確認します。

```text
                         read-only
View ───── Observation Request ────┐
View ◄──── Observation Result ─────┤
                                   ▼
                            Observation Gateway
                                   │
                                   ▼ read model
                              Simulation
                                   ▲
                                   │ authoritative command
                            Command Boundary
                                   ▲
                                   │
                              Management
```

- SimulationはHTTP / WebSocket / browser APIを知りません。
- Observation GatewayはSimulation内部mutable stateをnetwork処理から直接共有し続けません。
- Viewは完全read-onlyで、意味的stateを推測・再計算しません。
- Management mutationはserver-authoritative command境界を必ず通します。
- Protocolは内部object graphのdumpではなく、Observation / Commandの外部契約として設計します。
- View接続数、Camera、Selection、LOD、FPS、cacheがSimulation結果へ影響してはいけません。

詳細は[`../architecture/observation-gateway.md`](../architecture/observation-gateway.md)を参照してください。

## 6. 性能変更

性能改善は計測結果を起点にします。

- 変更前の基準値を残す
- tick time、p50 / p95、allocation、snapshot size、network send time、render timeなど対象に合った指標を選ぶ
- AgentごとのTask、hot pathのLINQ、不要なobject allocation、全件scanを安易に追加しない
- Observation Gatewayはrevision cache / request deduplication等で同じread処理を無駄に繰り返さない
- 最適化によって仕様やdeterminismが変わる場合は、単なる`perf`として扱わず明示する

## 7. 検証

変更内容に応じて次を組み合わせます。

- build
- unit test
- integration test
- protocol compatibility test
- observation invariance test
- management command validation test
- benchmark
- Server / Client結合確認
- Browser実機確認
- 複数seed / configの確認

View / Observation変更では、View未接続 / 接続中、Camera / Selection / LOD / cache差でSimulation state digestが一致することを必要に応じて確認します。

CIが成功していても、runtime behaviorを確認していない場合は「確認済み」と表現しません。

## 8. Pull Request

PR本文には最低限、可能な範囲で次を含めます。

- 目的
- 原因（bugfixの場合）
- 実装内容
- 重要なsemantic / protocol / performance変更
- Simulation / View / Managementの責務変更
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
- Phase補足設計・検討 → `docs/roadmap/`（Task状態の正本にはしない）
- 廃止済み・Legacy・実験履歴 → `docs/archive/`
- Simulation側の実装計画 → `roadmap/SIMULATION_ROADMAP.md`
- read-only View側の実装計画 → `roadmap/VIEW_ROADMAP.md`
- Management側の実装計画 → `roadmap/MANAGEMENT_ROADMAP.md`

過去資料を現行仕様の根拠へ戻さないようにします。文書を移動・改名した場合は参照元を更新し、`python scripts/check-markdown-links.py`またはCIのMarkdown link validationでローカルリンクとheading anchorを検証します。

## 10. 完了条件

作業完了時は次を確認します。

- 実装と文書が同じ意味を説明している
- 必要なtest / build / benchmarkが成功している
- runtime確認が必要な項目は確認済み、または未確認と明記している
- 不要なdebug code / experiment flagが残っていない
- 対応する3 RoadmapのTask状態が同期されている
- Markdownを追加・移動・改名した場合はlocal link / heading anchor validationが成功している
- 通常開発期間では`AGENTS.md`と`versioning.md`のversion規則へ従っている
