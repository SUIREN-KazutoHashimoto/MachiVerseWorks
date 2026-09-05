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

- 個々の開発コミットとPR境界を残すため、通常はsquash mergeを使用しない。
- commit SHAを書き換えないため、通常はrebase mergeを使用しない。
- GitHubがPRマージ時に生成するmerge commitは管理上の統合コミットとして扱う。
- Application `VERSION`はmerge commitやPR数に連動させず、Release時だけ更新する。
- マージ済みの短命branchは原則削除する。
- `main`と`develop`は長期branchとして削除しない。

GitHub Repository側で設定する値は[`repository-settings.md`](repository-settings.md)を参照してください。

## 2. 作業開始前

変更前に最低限、次を確認します。

1. 今回の**対象コンポーネントを1つ**決める。
2. 対象の状態・責務を所有するproject / class / moduleを確認する。
3. 呼び出し元と出力先を確認する。
4. 関連するtestを確認する。
5. 関連する仕様 (`docs/specifications/`) を確認する。
6. 関連する設計 (`docs/architecture/`) を確認する。
7. 重要な採用理由がある場合はADR (`docs/decisions/`) を確認する。
8. 対応する`roadmap/SIMULATION_ROADMAP.md`、`roadmap/GATEWAY_ROADMAP.md`、`roadmap/VIEW_ROADMAP.md`、`roadmap/MANAGEMENT_ROADMAP.md`のTask IDを確認する。

ファイル名や古い資料だけで現在の挙動を決めつけず、実効コードとtestを確認します。

### AGENTのコンポーネント境界

1つの開発AGENTは、原則として1つの作業で1コンポーネントだけを実装します。

対象候補はSimulation / Gateway / Server / Protocol / Persistence / View / Managementです。Gatewayは物理的には`src/server/`内でhostされますが、責務上は独立した対象コンポーネントとして扱います。

作業中に別コンポーネントの変更が必要だと判明した場合は、同じAGENTがそのまま実装しません。

1. 必要な変更内容・理由・依存元をIssueへ記録する。
2. 元の作業との依存関係を明記する。
3. 別AGENTへ引き継ぐ。
4. 共有Protocol変更も、Protocol側の実装として別作業へ分割する。

Repository全体のCI、運用ルール、共通ドキュメントだけを変更する作業は`Repository-wide tooling/docs`として扱えます。この例外を機能実装の跨ぎ変更に使用しません。

## 3. 新機能

要求を次の観点に分解します。

- authoritative state / semantic processing
- semantic observation source
- Observation Request / subscription / Gateway delivery
- Gateway cache / deduplication / reconnect
- Management command / validation
- Protocol
- View rendering / Camera / Selection / Inspector
- Management UI / editor / operation
- config / persistence
- performance / scalability
- documentation
- test / benchmark

責務分類:

- Simulationのstate / rule / meaning / schedule / history / semantic observation source / authoritative command → Simulation Roadmap
- Observation Request / subscription / filtering / cache / deduplication / Protocol adaptation / delivery / reconnect / resync → Gateway Roadmap
- read-only描画 / Camera / Selection / Inspector / Historical viewing / Rendering LOD → View Roadmap
- build / edit / runtime control / Server config / Save UI → Management Roadmap
- 統計分析 / trend / heatmap等 → View / Managementへ入れず将来Analytics系として別設計

Simulation / Gateway / Server / Protocol / Persistence / View / Managementにまたがる機能では、1 Task / 1 AGENTへ混在させず責務ごとのIssueへ分割します。

### ViewのSimulation / Gateway追従

View RoadmapはSimulation / Gateway RoadmapとPhase番号を合わせません。View固有基盤をPhase 1から積み上げます。

Simulationから移管されたView Taskは、原則として次を満たした時点で実装します。

1. View側の前提Taskが完了している。
2. 依存するSimulation Phase / semantic observation sourceが実装済みである。
3. 対象をViewへ届けるGateway contractが必要なら、そのbaseline delivery contractが実装済みである。

依存Simulationが未完成の場合、Viewが仮のsemantic stateを生成して先行実装しません。Gatewayのcache等の最適化だけが未完成なら、正しいbaseline delivery contractがある範囲でViewを先行できます。

### 状態の所有者

同じ情報を複数subsystemで推定し直さず、正規のstate ownerから取得します。

特にSimulationの意味的stateをGateway / View / Managementが独自に再構築して別の正本を作らないようにします。Gatewayは配送・cacheの正本であってWorld意味の正本ではありません。

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
4. 別コンポーネントの追従が必要ならIssueへ切り出す
5. 元の再現条件で確認する
6. 近接する正常ケースが壊れていないか確認する
7. 必要なら仕様・設計・ADRを同期する

Timeout、強制リセット、fallbackは安全網として有効な場合がありますが、原因が別にある場合は回避策だけを最終修正にしません。

## 5. アーキテクチャ境界の確認

変更時は依存方向を確認します。

```text
                         read-only
View ───── Observation Request ────┐
View ◄──── Observation Result ─────┤
                                   ▼
                                Gateway
                                   │
                                   ▼ detached semantic source
                              Simulation
                                   ▲
                                   │ authoritative command
                            Command Boundary
                                   ▲
                                   │
                              Management
```

- SimulationはHTTP / WebSocket / browser APIを知りません。
- GatewayはSimulation内部mutable stateをnetwork処理から直接共有し続けません。
- Gatewayは意味的stateを生成せず、authoritative mutation routeを持ちません。
- Viewは完全read-onlyで、意味的stateを推測・再計算しません。
- Management mutationはserver-authoritative command境界を必ず通します。
- Protocolは内部object graphのdumpではなく、Observation / Commandの外部契約として設計します。
- Gateway / View接続数、Camera、Selection、LOD、FPS、cacheがSimulation結果へ影響してはいけません。

詳細は[`../architecture/observation-gateway.md`](../architecture/observation-gateway.md)を参照してください。

## 6. 性能変更

性能改善は計測結果を起点にします。

- 変更前の基準値を残す
- tick time、p50 / p95、allocation、snapshot size、network send time、render timeなど対象に合った指標を選ぶ
- AgentごとのTask、hot pathのLINQ、不要なobject allocation、全件scanを安易に追加しない
- Gatewayはrevision cache / request deduplication等で同じread処理を無駄に繰り返さない
- 最適化によって仕様やdeterminismが変わる場合は、単なる`perf`として扱わず明示する

Benchmark workflowは現時点ではadvisory / non-blockingです。ただし性能へ影響するPRで赤になった場合は原因を調査し、未解決ならPRに明記します。

## 7. 検証

変更内容に応じて次を組み合わせます。

- build
- unit test
- integration test
- protocol compatibility test
- observation invariance test
- gateway cache / delivery equivalence test
- management command validation test
- benchmark
- Server / Client結合確認
- Browser実機確認
- 複数seed / configの確認

Gateway / View変更では、未接続 / 接続中、Camera / Selection / LOD / cache差でSimulation state digestが一致することを必要に応じて確認します。

CIが成功していても、runtime behaviorを確認していない場合は「確認済み」と表現しません。

## 8. Pull Request

PR本文には最低限、可能な範囲で次を含めます。

- 目的
- 対象コンポーネント
- 原因（bugfixの場合）
- 実装内容
- 重要なsemantic / protocol / performance変更
- Simulation / Gateway / View / Managementの責務変更
- 検証結果
- 未確認事項
- 既知制約
- ドキュメント更新範囲
- 他コンポーネントへのfollow-up Issue

自動reviewでinline threadが作られた場合は、修正後にthreadをResolveし、最新pushで追加指摘がないか確認します。

## 9. ドキュメント同期

役割は次の通りです。

- external / simulation behavior → `docs/specifications/`
- architecture / responsibility → `docs/architecture/`
- 開発・テスト・運用 → `docs/development/`
- 長期的な設計判断と理由 → `docs/decisions/`
- Phase補足設計・検討 → `docs/roadmap/`（Task状態の正本にはしない）
- 廃止済み・Legacy・実験履歴 → `docs/archive/`
- Simulation側の実装計画 → `roadmap/SIMULATION_ROADMAP.md`
- Gateway側の実装計画 → `roadmap/GATEWAY_ROADMAP.md`
- read-only View側の実装計画 → `roadmap/VIEW_ROADMAP.md`
- Management側の実装計画 → `roadmap/MANAGEMENT_ROADMAP.md`

過去資料を現行仕様の根拠へ戻さないようにします。文書を移動・改名した場合は参照元を更新し、`python scripts/check-markdown-links.py`またはCIのMarkdown link validationでローカルリンクとheading anchorを検証します。

## 10. 完了条件

作業完了時は次を確認します。

- 実装と文書が同じ意味を説明している
- 実装変更が対象コンポーネント1つに限定され、必要な他コンポーネント作業はIssueへ切り出されている
- 必要なtest / build / benchmarkが成功している、またはnon-blocking benchmarkの未解決事項が明記されている
- runtime確認が必要な項目は確認済み、または未確認と明記している
- 不要なdebug code / experiment flagが残っていない
- 対応する4 RoadmapのTask状態が同期されている
- Markdownを追加・移動・改名した場合はlocal link / heading anchor validationが成功している
- 通常開発では`VERSION`をRelease番号として扱い、PRやmergeの回数に応じて機械的に更新していない
