# Management Roadmap

このファイルは、MachiVerseWorks の **Management側の実装ロードマップ**です。World / City / Serverを人間が明示的に操作・編集・管理するためのUIとcommand clientを対象とします。

- Simulationのauthoritative state / rule / command contract / validationは[`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md)を正本とする。
- 純粋な描画・Camera・Selection・Inspector・Historical viewing・Rendering LOD・View localizationは[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)を正本とする。
- Management UIはread-only View componentを再利用してよいが、View module自体へmutation責務を持ち込まない。
- Population / Economy / Traffic等の分析・統計・trend・heatmapはManagementへ持ち込まず、将来のAnalytics Listener / analysis clientとして別責務にする。

> **現在:** Management Phase 1 — Management Client Foundation  
> **次の実装タスク:** Simulation Phase 36の共通command境界実装に合わせて着手

## 全体の現在地

| Management Phase | 内容 | 主な必須Simulation依存 | 状態 |
| --- | --- | --- | --- |
| 1 | Management Client Foundation | Simulation Phase 36 common command contract | ⏳ Simulation依存待ち |
| 2 | World & Infrastructure Editing | Simulation Phase 36 editing commands | ⏳ Simulation依存待ち |
| 3 | Runtime & Server Operations | Simulation Phase 36 runtime / config / Save commands | ⏳ Simulation依存待ち |
| 4 | Management Safety & Production UX | permission / confirmation / stable error contract | ⏳ Simulation依存待ち |
| 5 | Addon & Extension Management | Simulation Phase 38 Extension Platform | ⏳ Simulation依存待ち |

## 依存関係の読み方

Management Roadmapでは、依存を次の3種類に分ける。

- **必須依存** — そのcontract / Taskが成立しない限り対象Taskを完了できない実装ゲート。
- **並行可能依存** — interfaceやshell等は先行できるが、integration / closeoutまでに必要な依存。
- **統合依存** — 他Roadmapのcomponentを再利用すると有用だが、Management側の基礎責務そのものを開始するための必須条件ではない依存。

ViewのSelection / Inspector / rendererをManagement UIで再利用することは**統合依存**であり、Management command clientそのものの必須依存にはしない。逆に、mutationに必要なserver-authoritative command contractは必須依存とする。

## Management Roadmap 運用ルール

- ManagementはSimulationを変更できる一般GUI系Clientとして扱い、Viewとは別責務にする。
- mutationは必ずSimulation Roadmap側で定義されたserver-authoritative command境界を通す。
- Client側のoptimistic mutationをauthoritative stateとして扱わない。
- command pending / resultとauthoritative World observationは別stateとして管理する。
- command成功後のWorld表示はObservation Gatewayから再取得したauthoritative observationを正とする。
- read-only表示には可能な限りView componentを再利用し、同じEntity描画・Selection・Inspectorを二重実装しない。
- View componentへcommand clientを注入してmutation可能にする設計は避け、Management shell側で観測とcommandを組み合わせる。
- Analytics / 統計分析 / trend / heatmap生成はManagementの必須責務に含めない。
- destructive操作、権限、confirmation、trust、auditabilityを通常のView UXとは別に扱う。
- Management固有UI文言はManagement側でlocalizeし、Simulation / Protocol / Saveへ翻訳済み文言を持ち込まない。

## Simulation Roadmapからの移管対応

旧Simulation Phase 36に混在していたBrowser操作系のうち、mutation / administration UIをManagementへ移管する。

| 移管元 | Management側の扱い |
| --- | --- |
| 旧 `P36-005`〜`P36-013` のeditor / override UI部分 | Management Phase 2 |
| 旧 `P36-014` のClient command state管理 | Management Phase 1 / 4 |
| 旧 `P36-015` の運転control UI | Management Phase 3 |
| 旧 `P36-017` のServer configuration UI | Management Phase 3 |
| 旧 `P36-018` のSave / Load UI | Management Phase 3 |
| 旧 `P36-019` のconfirmation / error UI | Management Phase 4 |
| 旧 `P36-020` / `P36-021` のManagement UI検証部分 | Management Phase 4 |

旧`P36-003` / `P36-004`のSelection / Inspectorはread-only観測機能としてView Roadmapへ置く。旧`P36-016`のDashboard / statistics分析系はView / Managementの必須責務へ移さず、将来のAnalytics Listener / analysis clientとして別途設計する。

---

## Management Phase 1 — Management Client Foundation

> **状態: ⏳ Simulation依存待ち**  
> **必須依存:** Simulation Phase 36の共通command request / result / authorization境界、authoritative resultを再観測できるObservation Gateway  
> **統合依存:** View Phase 7のread-only Selection / Inspector（Management shellへ埋め込む場合）

- ⬜ **M1-001** — Management Clientとread-only Viewの責務境界をarchitecture / module dependencyとして固定する
- ⬜ **M1-002** — server-authoritative command request / resultを扱うManagement command clientを実装する
- ⬜ **M1-003** — command pending / success / failureをViewのauthoritative observation stateと混同しないClient state modelを実装する
- ⬜ **M1-004** — command成功後にObservation Gatewayから更新済みauthoritative stateを再観測する同期方針を実装する
- ⬜ **M1-005** — permission / capability metadataに基づき利用可能なManagement操作だけを表示する基盤を実装する
- ⬜ **M1-006** — View component未実装 / 未接続でもManagement command clientのcontract testが成立するよう依存を分離する
- ⬜ **M1-007** — Management Client FoundationのBrowser E2Eとarchitecture documentを同期する

### Management Phase 1 完了条件

- View moduleをmutation可能にせず、Management shellからだけcommandを送信できる。
- command結果とWorldのauthoritative observationを別stateとして扱える。
- command failure時にClientだけWorld stateが進んだように見えない。
- View Phase 7が未完了でもcommand client / result stateの基礎実装を独立して検証できる。

---

## Management Phase 2 — World & Infrastructure Editing

> **状態: ⏳ Simulation依存待ち**  
> **必須依存:** Management Phase 1 / Simulation Phase 36の対象build / edit / remove command  
> **統合依存:** View Phase 3〜7の対象描画・Selection / Inspector（selection / preview / result確認へ再利用）

- ⬜ **M2-001** — Road / Lane build / edit / remove UIを実装する（旧`P36-005`のUI部分）
- ⬜ **M2-002** — Building / POI / Parcel / Zone build / edit UIを実装する（旧`P36-006`のUI部分）
- ⬜ **M2-003** — Railway track / station / platform build / edit UIを実装する（旧`P36-007`のUI部分）
- ⬜ **M2-004** — Power Infrastructure build / edit UIを実装する（旧`P36-008`のUI部分）
- ⬜ **M2-005** — Water / Sewer Infrastructure build / edit UIを実装する（旧`P36-009`のUI部分）
- ⬜ **M2-006** — Gas Infrastructure build / edit UIを実装する（旧`P36-010`のUI部分）
- ⬜ **M2-007** — Optical Communication Infrastructure build / edit UIを実装する（旧`P36-011`のUI部分）
- ⬜ **M2-008** — Radio Site / Antenna / Spectrum設定UIを実装する（旧`P36-012`のUI部分）
- ⬜ **M2-009** — Geographic Feature名・Settlement / District / Road名・Road Sign override UIを実装する（旧`P36-013`のUI部分）
- ⬜ **M2-010** — edit previewをView-local presentationとして表示し、確定前にauthoritative Worldへ混入させない
- ⬜ **M2-011** — command成功後のauthoritative observationでpreviewを置換し、失敗時はpreviewだけを破棄する同期を実装する
- ⬜ **M2-012** — World & Infrastructure EditingのBrowser E2E / usability / performanceを検証する

### Management Phase 2 完了条件

- 主要World Entity / InfrastructureをManagement UIから編集できる。
- 全mutationがServer側validation / authorizationを通る。
- previewとauthoritative observationを明確に区別できる。
- Selection / renderingはViewのread-only componentを再利用でき、Management側で第二のWorld表示正本を作らない。

---

## Management Phase 3 — Runtime & Server Operations

> **状態: ⏳ Simulation依存待ち**  
> **必須依存:** Management Phase 1 / Simulation Phase 36のruntime / configuration / Save command  
> **統合依存:** Observation Gateway / View Inspectorによるcurrent status表示

- ⬜ **M3-001** — Simulation pause / resume / explicit step等の運転control UIを実装する（旧`P36-015`のUI部分）
- ⬜ **M3-002** — runtime変更可能設定とrestart-required設定を区別するServer configuration UIを実装する（旧`P36-017`のUI部分）
- ⬜ **M3-003** — current Save formatのSave / Load操作UIを実装する（旧`P36-018`のUI部分）
- ⬜ **M3-004** — Server / Simulation current statusをread-only observationとして表示し、command操作と分離する
- ⬜ **M3-005** — Save / LoadやWorld replacement後にObservation Gatewayのrevision / reconnect契約へ従いView / Management表示を再同期する
- ⬜ **M3-006** — Runtime & Server OperationsのE2Eを追加する

### Management Phase 3 完了条件

- 運転control、Server設定、Save / LoadをManagement UIから安全に実行できる。
- 操作結果はServerのstructured resultとObservation Gateway上のauthoritative stateで確認できる。
- World replacement後にClient-local cacheを正本として残さない。

---

## Management Phase 4 — Management Safety & Production UX

> **状態: ⏳ Simulation依存待ち**  
> **必須依存:** Management Phase 1〜3 / stable error code / confirmation metadata / authorization  
> **並行・統合依存:** 共通Client localization / formatting基盤（View Phase 10とresource基盤を共有可能）

- ⬜ **M4-001** — destructive commandのconfirmation UIを実装する（旧`P36-019`のUI部分）
- ⬜ **M4-002** — stable error code / structured parameterをManagement向け表示へ変換する
- ⬜ **M4-003** — command timeout / cancellation / duplicate submit / reconnect時の安全なUXを実装する
- ⬜ **M4-004** — permission不足・conflict・validation failureをWorld state変更なしで表示するnegative E2Eを追加する
- ⬜ **M4-005** — large WorldでManagement overlay / preview / selectionがView renderingを過度に阻害しないperformance testを追加する
- ⬜ **M4-006** — Management固有のcommand / confirmation / permission / error文言をlocale resource化し、数値・日時・単位formattingを共通Client基盤から再利用する
- ⬜ **M4-007** — 追加localeで主要Management operation / confirmation / failure表示をE2E確認する
- ⬜ **M4-008** — Management architecture / security / localization / UX / Roadmapを同期する

### Management Phase 4 完了条件

- destructive / privileged操作を誤操作しにくいUI境界を持つ。
- failureやreconnectがauthoritative World stateの誤表示へつながらない。
- Management UIを無効化・未接続にしてもSimulationとViewの観測能力は独立して成立する。
- Management固有UIを複数localeへ追加してもSimulation / Protocol / Saveの言語非依存契約を壊さない。

---

## Management Phase 5 — Addon & Extension Management

> **状態: ⏳ Simulation依存待ち**  
> **必須依存:** Management Phase 1 / 4、Simulation Phase 38 Extension Platform  
> **統合依存:** View Phase 12（View Addonの適用結果・read-only previewをManagementから確認する場合）

Addonの存在・trust・dependency・conflict・設定を人間が管理するUIを提供する。model / material / rendering layer等のread-only View Addon適用そのものはView Roadmap Phase 12の責務とする。

- ⬜ **M5-001** — Installed / Official / Community / Updatesを表示するAddon Manager shellを実装する
- ⬜ **M5-002** — `.mvaddon`のinstall / uninstall / update UIを実装する
- ⬜ **M5-003** — enable / disable操作をExtension Platformのauthoritative lifecycle境界へ接続する
- ⬜ **M5-004** — publisher / requested capability / code-vs-data-only / trust informationを表示する
- ⬜ **M5-005** — dependency不足・version incompatibility・conflictをstructured metadataから表示する
- ⬜ **M5-006** — exclusive provider / override conflictの解決候補を表示し、明示選択をExtension Platformへ送るUIを実装する
- ⬜ **M5-007** — Addon固有settings schemaから設定画面を構築し、runtime-changeable / restart-requiredを区別する
- ⬜ **M5-008** — Developer Modeのlocal Addon link / unlink UIを明示的な開発機能として実装する
- ⬜ **M5-009** — View AddonについてはView Phase 12が公開するread-only適用状態を表示し、Management側でrenderer extension contractを再実装しない
- ⬜ **M5-010** — Addon install / enable / conflict / settings / failureのBrowser E2Eを追加する
- ⬜ **M5-011** — Addon & Extension Managementのsecurity / trust / UX / Roadmapを同期する

### Management Phase 5 完了条件

- Addonのinstall / uninstall / enable / disable / update /設定変更をManagementから安全に行える。
- trust / capability / dependency / conflictが操作前に確認できる。
- Addon管理操作をView moduleへ持ち込まない。
- Addonの最終状態はExtension Platformのauthoritative resultとObservationから確認する。
- View Addonのrender / Inspector extension責務とAddon Managerのlifecycle操作責務を混在させない。

---

## 継続Backlog

- Management Addon / custom command tool contribution
- multi-user operation / conflict UX
- role-specific management workspace
- audit log viewer

これらは実装時にSimulation / Security / Extension Platform側の依存を確認してPhase化する。
