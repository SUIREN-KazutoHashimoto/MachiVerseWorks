# MachiVerseWorks Roadmap

MachiVerseWorks の作業を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** Phase 0 — 初期セットアップ最終確認  
> **手動設定待ち:** `SET-019` / `SET-021`  
> **次の実装タスク:** `SKL-001` — ルートに `MachiVerseWorks.slnx` を作成する

## 全体の現在地

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0 | リポジトリ初期セットアップ | ⚠️ **手動設定待ち** |
| 1 | 開発プロジェクト骨格 | ⏭️ 次 |
| 2 | Simulation Core 最小 PoC | ⏳ 待機 |
| 3 | Protocol 最小実装 | ⏳ 待機 |
| 4 | Headless Server 最小実装 | ⏳ 待機 |
| 5 | Web Client 最小実装 | ⏳ 待機 |
| 6 | End-to-End PoC | ⏳ 待機 |
| 7 | 性能基盤の拡張 | ⏳ 待機 |
| 8 | 保存・復元基盤 | ⏳ 待機 |

## 状態の見方

- ⬜ **未完了** — 実装・検証のどちらかが残っている
- ✅ **完了** — 必要な実装・build・test・benchmark・実機確認まで済んでいる
- ⚠️ **手動設定待ち** — GitHub SettingsなどRepositoryファイルから変更できない作業が残っている

Task ID は参照用の固定IDです。並び替えても変更しません。

## 初期セットアップ残タスク

以下の2項目だけは GitHub Settings 側の操作が必要です。設定値の正本は [`docs/development/repository-settings.md`](docs/development/repository-settings.md) です。

- ⬜ **SET-019** — Private vulnerability reporting / Dependabot alerts / Secret scanning 等のSecurity設定を確認・有効化する
- ⬜ **SET-021** — Repositoryのmerge方式をmerge commitのみにし、merge後の短命branch自動削除を有効化する

<details>
<summary><strong>ROADMAP 運用ルール</strong></summary>

- 状態記号を付けるのは、単独で完了判定できる作業だけとする。
- 1タスクは原則として「1つの観測可能な成果」を持つ。
- 1タスク内に独立した成果が複数ある場合は、着手前または判明した時点で分割する。
- 「交通を完成させる」「経済を実装する」のような大テーマはTask化しない。
- 大テーマは見出しまたは将来 Backlog として管理し、着手時に小タスクへ分解する。
- 完了条件が曖昧なタスクは、そのまま実装を始めず完了条件を具体化する。
- コード変更では、必要な build / test / benchmark / 実機確認まで含めて完了とする。
- 仕様や設計を変更した場合は、対応する docs / ADR の更新まで含めて完了とする。
- 作業中にタスクが膨らんだ場合は、無理に1項目で抱えず未完了へ戻して分割する。
- 「ほぼ完了」「一部完了」は ✅ にしない。残作業を別 Task ID へ明示的に切り出した場合のみ元タスクを完了にできる。
- 全項目が完了した大きなセクションは、必要に応じて `docs/archive/` へ履歴を移し、ROADMAP を読みやすく保つ。

</details>

---

<details>
<summary><strong>Phase 0 — リポジトリ初期セットアップ（手動設定待ち）</strong></summary>

- ✅ **SET-001** — 基本ディレクトリ構成を作成する
- ✅ **SET-002** — ルート `README.md` / `AGENTS.md` を整備する
- ✅ **SET-003** — `.gitignore` / `.gitattributes` / `.editorconfig` を整備する
- ✅ **SET-004** — Apache-2.0 の `LICENSE` / `NOTICE` / 第三者通知の基盤を整備する
- ✅ **SET-005** — CONTRIBUTING / SECURITY / Code of Conduct / Issue・PRテンプレートを整備する
- ✅ **SET-006** — Legacy から再利用する開発知見と移行方針を整理する
- ✅ **SET-007** — Server-authoritative 構成の ADR とアーキテクチャ概要を整備する
- ✅ **SET-008** — 将来の多言語対応を壊さない localization 境界を整備する
- ✅ **SET-009** — GitHub Actions の CI / CodeQL / Dependency Review / Dependabot を整備する
- ✅ **SET-010** — `.NET` SDK を `global.json` で固定する
- ✅ **SET-011** — Coding Guidelines と Performance Guidelines を整備する
- ✅ **SET-012** — 細粒度タスク管理用の `ROADMAP.md` を導入する
- ✅ **SET-013** — `develop` ブランチを作成する
- ✅ **SET-014** — `main` / `develop` のRulesetまたはbranch protectionを設定する
- ✅ **SET-015** — CI に実装有無に依存しない固定 `ci-gate` jobを追加する
- ✅ **SET-016** — 新機能branch名を `feature/*` に統一する
- ✅ **SET-017** — PRの標準merge方式とbranch削除方針を確定・文書化する
- ✅ **SET-018** — 通常開発用application versionの正本設計を確定する
- ⬜ **SET-019** — GitHub Security設定を確認・有効化する
- ✅ **SET-020** — MarkdownのRepository内リンク検証をCIへ追加する
- ⬜ **SET-021** — Repositoryのmerge設定とmerge後branch自動削除をGitHub Settingsへ反映する

</details>

---

## Phase 1 — 開発プロジェクト骨格

> **状態: ⏭️ 次**  
> 初期セットアップのGitHub手動設定を終えた後、C# Solution と Web Client の最小骨格を作り、既存CIを実際のbuildへ接続する。

### .NET Solution

- ⬜ **SKL-001** — ルートに `MachiVerseWorks.slnx` を作成する
- ⬜ **SKL-002** — `MachiVerseWorks.Simulation` の `.csproj` を作成する
- ⬜ **SKL-003** — `MachiVerseWorks.Protocol` の `.csproj` を作成する
- ⬜ **SKL-004** — `MachiVerseWorks.Server` の `.csproj` を作成する
- ⬜ **SKL-005** — `MachiVerseWorks.Benchmarks` の `.csproj` を作成する
- ⬜ **SKL-006** — `MachiVerseWorks.Simulation.Tests` の `.csproj` を作成する
- ⬜ **SKL-007** — `MachiVerseWorks.Protocol.Tests` の `.csproj` を作成する
- ⬜ **SKL-008** — `MachiVerseWorks.Server.Tests` の `.csproj` を作成する
- ⬜ **SKL-009** — Solution に全 C# project を登録する
- ⬜ **SKL-010** — ProjectReference の依存方向を設定する
- ⬜ **SKL-011** — 空の状態で `dotnet restore` が成功することを確認する
- ⬜ **SKL-012** — 空の状態で Release build が成功することを確認する
- ⬜ **SKL-013** — 空の状態で全 test project が成功することを確認する

### Web Client

- ⬜ **SKL-014** — Web Client の採用パッケージとversion方針を決める
- ⬜ **SKL-015** — `src/web/package.json` を作成する
- ⬜ **SKL-016** — npm lockfile を作成する
- ⬜ **SKL-017** — Node.js version固定ファイルを追加する
- ⬜ **SKL-018** — TypeScript 設定を追加する
- ⬜ **SKL-019** — Vite の最小構成を追加する
- ⬜ **SKL-020** — Three.js の最小依存を追加する
- ⬜ **SKL-021** — ブラウザに空の MachiVerseWorks 画面を表示できるようにする
- ⬜ **SKL-022** — Web Client の lint / typecheck script を用意する
- ⬜ **SKL-023** — Web Client の最小 build を成功させる

### CI 連携

- ⬜ **SKL-024** — CI の `dotnet` job が実行され成功することを確認する
- ⬜ **SKL-025** — CI の `web` job が実行され成功することを確認する
- ⬜ **SKL-026** — Dependabot に NuGet 更新設定を追加する
- ⬜ **SKL-027** — Dependabot に npm 更新設定を追加する
- ⬜ **SKL-028** — 初回実装向けのローカル開発手順を `getting-started.md` に記載する

---

<details>
<summary><strong>Phase 2 — Simulation Core 最小 PoC（待機）</strong></summary>

目標は「都市機能」ではなく、まず多数 Agent を安定して step できる最小コアを作ること。

### 基本時間・設定

- ⬜ **SIM-001** — Simulation の tick rate を保持する設定型を作る
- ⬜ **SIM-002** — Simulation seed を保持する設定を追加する
- ⬜ **SIM-003** — Simulation time / tick counter を表す型を作る
- ⬜ **SIM-004** — `Step()` で tick を1回進める最小 API を作る
- ⬜ **SIM-005** — 同一seed・同一入力で同じ結果になるテストを作る

### Agent Store

- ⬜ **SIM-006** — `AgentId` の安定したID型を作る
- ⬜ **SIM-007** — Agent の最小状態を格納する `AgentStore` を作る
- ⬜ **SIM-008** — Agent を1体生成できる API を作る
- ⬜ **SIM-009** — 指定数の Agent を一括生成できるようにする
- ⬜ **SIM-010** — Agent ID を途中で詰め直さないことをテストする
- ⬜ **SIM-011** — 1 tick で Agent の最小状態を更新する処理を作る

### Spatial Index

- ⬜ **SIM-012** — World 座標を cell / chunk に変換する型を作る
- ⬜ **SIM-013** — Agent を spatial cell に登録できるようにする
- ⬜ **SIM-014** — Agent 移動時に cell 所属を更新できるようにする
- ⬜ **SIM-015** — 矩形範囲の Agent ID を取得する query を作る
- ⬜ **SIM-016** — spatial query の境界条件テストを追加する

### Snapshot

- ⬜ **SIM-017** — Client配信用の最小 Agent snapshot 型を定義する
- ⬜ **SIM-018** — Simulation内部の可変Storeとsnapshotを分離する
- ⬜ **SIM-019** — 指定範囲だけ snapshot を生成できるようにする
- ⬜ **SIM-020** — snapshot生成中にSimulation内部状態を外部へ公開しないことをテストする

### 最小性能計測

- ⬜ **SIM-021** — 1,000 Agent の tick benchmark を追加する
- ⬜ **SIM-022** — 10,000 Agent の tick benchmark を追加する
- ⬜ **SIM-023** — 100,000 Agent の tick benchmark を追加する
- ⬜ **SIM-024** — tick時間の p50 / p95 / p99 を記録できるようにする
- ⬜ **SIM-025** — tickあたり allocation を記録できるようにする
- ⬜ **SIM-026** — PoCの初回性能結果を文書へ記録する

</details>

---

<details>
<summary><strong>Phase 3 — Protocol 最小実装（待機）</strong></summary>

- ⬜ **PRT-001** — Protocol version の表現方法を決める
- ⬜ **PRT-002** — message type ID の管理方法を決める
- ⬜ **PRT-003** — binary frame のheader layoutを定義する
- ⬜ **PRT-004** — Client → Server `Hello` message を定義する
- ⬜ **PRT-005** — Server → Client `HelloAck` message を定義する
- ⬜ **PRT-006** — Client → Server `SubscribeArea` message を定義する
- ⬜ **PRT-007** — Server → Client Agent spawn message を定義する
- ⬜ **PRT-008** — Server → Client Agent update message を定義する
- ⬜ **PRT-009** — Server → Client Agent remove message を定義する
- ⬜ **PRT-010** — user-facing error用の stable code + parameter contract を定義する
- ⬜ **PRT-011** — 最小 serializer を実装する
- ⬜ **PRT-012** — 最小 deserializer を実装する
- ⬜ **PRT-013** — 各messageのround-trip testを追加する
- ⬜ **PRT-014** — 不正なframe長を拒否するテストを追加する
- ⬜ **PRT-015** — 未知message typeを安全に拒否するテストを追加する
- ⬜ **PRT-016** — binary layout を architecture docs に記録する

</details>

---

<details>
<summary><strong>Phase 4 — Headless Server 最小実装（待機）</strong></summary>

- ⬜ **SRV-001** — Server project を単独起動できるようにする
- ⬜ **SRV-002** — 設定ファイルからlisten address / portを読めるようにする
- ⬜ **SRV-003** — health endpoint を追加する
- ⬜ **SRV-004** — Simulation Core の lifecycle をServerから開始できるようにする
- ⬜ **SRV-005** — Simulation tick loop を専用の実行境界で動かす
- ⬜ **SRV-006** — graceful shutdown でtick loopを停止できるようにする
- ⬜ **SRV-007** — WebSocket endpoint を追加する
- ⬜ **SRV-008** — Client接続を登録・解除する仕組みを作る
- ⬜ **SRV-009** — `Hello` / `HelloAck` の接続ハンドシェイクを実装する
- ⬜ **SRV-010** — Client command をSimulation側へ渡すqueue/channelを作る
- ⬜ **SRV-011** — `SubscribeArea` を接続単位で保持する
- ⬜ **SRV-012** — subscription範囲のsnapshotだけを取得する
- ⬜ **SRV-013** — snapshot publish周期をSimulation tickから分離する
- ⬜ **SRV-014** — Agent spawn/update/remove を送信する
- ⬜ **SRV-015** — 切断時にsubscription stateを破棄する
- ⬜ **SRV-016** — Server起動・停止のintegration testを追加する
- ⬜ **SRV-017** — WebSocket handshakeのintegration testを追加する
- ⬜ **SRV-018** — snapshot送信のintegration testを追加する

</details>

---

<details>
<summary><strong>Phase 5 — Web Client 最小実装（待機）</strong></summary>

- ⬜ **WEB-001** — Web Client のapplication entry pointを作る
- ⬜ **WEB-002** — locale manifestからdefault localeを初期化する
- ⬜ **WEB-003** — `ja-JP` の最小UI resourceを作る
- ⬜ **WEB-004** — Server URL設定を読み込めるようにする
- ⬜ **WEB-005** — WebSocket接続クラスを作る
- ⬜ **WEB-006** — `Hello` / `HelloAck` を実装する
- ⬜ **WEB-007** — binary frame decoderを作る
- ⬜ **WEB-008** — Client側entity storeを作る
- ⬜ **WEB-009** — Agent spawnをentity storeへ反映する
- ⬜ **WEB-010** — Agent updateをentity storeへ反映する
- ⬜ **WEB-011** — Agent removeをentity storeへ反映する
- ⬜ **WEB-012** — Three.js scene / camera / renderer の最小構成を作る
- ⬜ **WEB-013** — camera位置からsubscription範囲を計算する
- ⬜ **WEB-014** — `SubscribeArea` をServerへ送る
- ⬜ **WEB-015** — Agentを最小形状で描画する
- ⬜ **WEB-016** — snapshot間の位置補間を実装する
- ⬜ **WEB-017** — 接続状態をUIへ表示する
- ⬜ **WEB-018** — Protocol error codeをlocale resource経由で表示する
- ⬜ **WEB-019** — 接続切断を検知する
- ⬜ **WEB-020** — 最小の再接続処理を実装する

</details>

---

<details>
<summary><strong>Phase 6 — End-to-End PoC（待機）</strong></summary>

- ⬜ **E2E-001** — Server + Web Client のローカル起動手順を確定する
- ⬜ **E2E-002** — BrowserからServerへ接続できることを確認する
- ⬜ **E2E-003** — 1,000 AgentをServerで生成しBrowserに表示する
- ⬜ **E2E-004** — camera移動時にsubscription範囲が更新されることを確認する
- ⬜ **E2E-005** — 範囲外AgentがClientからremoveされることを確認する
- ⬜ **E2E-006** — 再接続後にClient stateを復元できることを確認する
- ⬜ **E2E-007** — 10,000 AgentのServer simulationで近傍だけ描画できることを確認する
- ⬜ **E2E-008** — 100,000 AgentのServer simulationで近傍だけ配信できることを確認する
- ⬜ **E2E-009** — snapshot bytes / encode time / send timeを記録する
- ⬜ **E2E-010** — Client decode time / frame timeを記録する
- ⬜ **E2E-011** — PoC結果と既知のボトルネックを文書化する

</details>

---

<details>
<summary><strong>Phase 7 — 性能基盤の拡張（待機）</strong></summary>

PoCを動かした後、計測結果に基づいて必要な項目だけ進める。

- ⬜ **PER-001** — BenchmarkDotNetの共通設定を作る
- ⬜ **PER-002** — Simulation benchmark結果を保存する形式を決める
- ⬜ **PER-003** — snapshot生成時間のbenchmarkを追加する
- ⬜ **PER-004** — Protocol encode/decode benchmarkを追加する
- ⬜ **PER-005** — spatial query benchmarkを追加する
- ⬜ **PER-006** — GC collection回数を計測結果へ含める
- ⬜ **PER-007** — Serverのsnapshot配信統計をログへ出せるようにする
- ⬜ **PER-008** — Web Clientのdecode時間をdevelopment overlayで確認できるようにする
- ⬜ **PER-009** — Web Clientのrender frame timeをdevelopment overlayで確認できるようにする
- ⬜ **PER-010** — 最初の性能改善候補をprofile結果から選定する

</details>

---

<details>
<summary><strong>Phase 8 — 保存・復元基盤（待機）</strong></summary>

このセクションは End-to-End PoC 後に着手する。

- ⬜ **SAV-001** — Save Data が保持する最小情報を定義する
- ⬜ **SAV-002** — Save format versionを定義する
- ⬜ **SAV-003** — locale依存表示文字列をSave Dataへ保存しないテストを追加する
- ⬜ **SAV-004** — 最小Worldを保存できるようにする
- ⬜ **SAV-005** — 保存した最小Worldを読み込めるようにする
- ⬜ **SAV-006** — save → load で同じSimulation状態を復元するテストを追加する
- ⬜ **SAV-007** — 不正Save Dataを安全に拒否するテストを追加する

</details>

---

<details>
<summary><strong>将来 Backlog</strong></summary>

以下は**テーマ**であり、完了状態記号の対象ではありません。着手するときに、その時点の設計に合わせて上記と同程度の粒度へ分解します。

- Building / POI データモデル
- Agent needs / schedule / household
- Road graph / lane model
- Pathfinding / route cache
- Road traffic simulation
- Intersection / signal control
- Pedestrian simulation
- Railway infrastructure
- Railway operation / timetable
- Bus / taxi / multimodal transit
- Logistics / freight
- Industry / jobs / economy
- Power generation / grid / demand
- City generation
- Zoning / land use
- Inspector / dashboard / statistics UI
- Build / edit commands
- Server configuration UI
- Save migration
- Release packaging
- Server binary distribution
- Web Client deployment
- Container image
- Mod / extension architecture
- Additional locales

### Backlog を着手可能にする条件

1. 現在の仕様・依存関係を確認する。
2. What / Why が必要なら `docs/specifications/` を作成・更新する。
3. How の重要判断が必要なら `docs/architecture/` / ADR を作成・更新する。
4. 1つずつ完了判定できるTaskへ分割する。
5. 最初の数項目だけを優先順に並べ、巨大な一括実装を始めない。

</details>
