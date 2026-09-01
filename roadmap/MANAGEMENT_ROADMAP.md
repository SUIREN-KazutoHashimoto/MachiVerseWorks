# Management Roadmap

このファイルは、MachiVerseWorks の **Management側の実装ロードマップ**です。World / City / Serverを人間が明示的に操作・編集・管理するためのUIとcommand clientを対象とします。

- Simulationのauthoritative state / rule / command contract / validationは[`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md)を正本とする。
- 純粋な描画・Camera・Selection・Inspector・Historical viewing・Rendering LOD・View localizationは[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)を正本とする。
- Management UIはread-only View componentを再利用してよいが、View module自体へmutation責務を持ち込まない。

> **現在:** Management Phase 1 — Management Client Foundation  
> **次の実装タスク:** Simulation Phase 36のcommand境界実装に合わせて着手

## 全体の現在地

| Management Phase | 内容 | 主なSimulation依存 | 状態 |
| --- | --- | --- | --- |
| 1 | Management Client Foundation | Simulation Phase 36共通command境界 | ⏳ Simulation依存待ち |
| 2 | World & Infrastructure Editing | Simulation Phase 36 editing command | ⏳ Simulation依存待ち |
| 3 | Runtime & Server Operations | Simulation Phase 36 runtime / config / Save command | ⏳ Simulation依存待ち |
| 4 | Management Safety & Production UX | permission / confirmation / error contract | ⏳ Simulation依存待ち |
| 5 | Addon & Extension Management | Simulation Phase 38 Extension Platform | ⏳ Simulation依存待ち |

## Management Roadmap 運用ルール

- ManagementはSimulationを変更できる一般GUI系Clientとして扱い、Viewとは別責務にする。
- mutationは必ずSimulation Roadmap側で定義されたserver-authoritative command境界を通す。
- Client側のoptimistic mutationをauthoritative stateとして扱わない。
- command成功後のWorld表示はObservation Gatewayから再取得したauthoritative observationを正とする。
- read-only表示には可能な限りView componentを再利用し、同じEntity描画・Selection・Inspectorを二重実装しない。
- View componentへcommand clientを注入してmutation可能にする設計は避け、Management shell側で観測とcommandを組み合わせる。
- Analytics / 統計分析 / trend / heatmap生成はManagementの必須責務に含めない。
- destructive操作、権限、confirmation、trust、auditabilityを通常のView UXとは別に扱う。

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
> **依存:** Simulation Phase 36の共通command request / result / authorization境界、Viewのread-only Selection / Inspector基盤

- ⬜ **M1-001** — Management Clientとread-only Viewの責務境界をarchitectureとして固定する
- ⬜ **M1-002** — server-authoritative command request / resultを扱うManagement command clientを実装する
- ⬜ **M1-003** — command pending / success / failureをViewのauthoritative observation stateと混同しないClient state modelを実装する
- ⬜ **M1-004** — command成功後にObservation Gatewayから更新済みauthoritative stateを再観測する同期方針を実装する
- ⬜ **M1-005** — permission / capability metadataに基づき利用可能なManagement操作だけを表示する基盤を実装する
- ⬜ **M1-006** — Management Client FoundationのBrowser E2Eとarchitecture documentを同期する

### Management Phase 1 完了条件

- View moduleをmutation可能にせず、Management shellからだけcommandを送信できる。
- command結果とWorldのauthoritative observationを別stateとして扱える。
- command failure時にClientだけWorld stateが進んだように見えない。

---

## Management Phase 2 — World & Infrastructure Editing

> **状態: ⏳ Simulation依存待ち**  
> **依存:** Management Phase 1 / Simulation Phase 36の各build / edit / remove command

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
- ⬜ **M2-011** — World & Infrastructure EditingのBrowser E2E / usability / performanceを検証する

### Management Phase 2 完了条件

- 主要World Entity / InfrastructureをManagement UIから編集できる。
- 全mutationがServer側validation / authorizationを通る。
- previewとauthoritative observationを明確に区別できる。

---

## Management Phase 3 — Runtime & Server Operations

> **状態: ⏳ Simulation依存待ち**  
> **依存:** Management Phase 1 / Simulation Phase 36のruntime / configuration / Save command

- ⬜ **M3-001** — Simulation pause / resume / explicit step等の運転control UIを実装する（旧`P36-015`のUI部分）
- ⬜ **M3-002** — runtime変更可能設定とrestart-required設定を区別するServer configuration UIを実装する（旧`P36-017`のUI部分）
- ⬜ **M3-003** — current Save formatのSave / Load操作UIを実装する（旧`P36-018`のUI部分）
- ⬜ **M3-004** — Server / Simulation current statusをread-only observationとして表示し、command操作と分離する
- ⬜ **M3-005** — Runtime & Server OperationsのE2Eを追加する

### Management Phase 3 完了条件

- 運転control、Server設定、Save / LoadをManagement UIから安全に実行できる。
- 操作結果はServerのstructured resultとObservation Gateway上のauthoritative stateで確認できる。

---

## Management Phase 4 — Management Safety & Production UX

> **状態: ⏳ Simulation依存待ち**  
> **依存:** Management Phase 1〜3 / stable error code / confirmation metadata / authorization

- ⬜ **M4-001** — destructive commandのconfirmation UIを実装する（旧`P36-019`のUI部分）
- ⬜ **M4-002** — stable error code / structured parameterをManagement向け表示へ変換する
- ⬜ **M4-003** — command timeout / cancellation / duplicate submit / reconnect時の安全なUXを実装する
- ⬜ **M4-004** — permission不足・conflict・validation failureをWorld state変更なしで表示するnegative E2Eを追加する
- ⬜ **M4-005** — large WorldでManagement overlay / preview / selectionがView renderingを過度に阻害しないperformance testを追加する
- ⬜ **M4-006** — Management architecture / security / UX / Roadmapを同期する

### Management Phase 4 完了条件

- destructive / privileged操作を誤操作しにくいUI境界を持つ。
- failureやreconnectがauthoritative World stateの誤表示へつながらない。
- Management UIを無効化・未接続にしてもSimulationとViewの観測能力は独立して成立する。

---

## Management Phase 5 — Addon & Extension Management

> **状態: ⏳ Simulation依存待ち**  
> **依存:** Management Phase 1 / 4、Simulation Phase 38 Extension Platform

Addonの存在・trust・dependency・conflict・設定を人間が管理するUIを提供する。model / material / rendering layer等のread-only View Addon適用そのものはView Roadmap側で扱う。

- ⬜ **M5-001** — Installed / Official / Community / Updatesを表示するAddon Manager shellを実装する
- ⬜ **M5-002** — `.mvaddon`のinstall / uninstall / update UIを実装する
- ⬜ **M5-003** — enable / disable操作をExtension Platformのauthoritative lifecycle境界へ接続する
- ⬜ **M5-004** — publisher / requested capability / code-vs-data-only / trust informationを表示する
- ⬜ **M5-005** — dependency不足・version incompatibility・conflictをstructured metadataから表示する
- ⬜ **M5-006** — exclusive provider / override conflictの解決候補を表示し、明示選択をExtension Platformへ送るUIを実装する
- ⬜ **M5-007** — Addon固有settings schemaから設定画面を構築し、runtime-changeable / restart-requiredを区別する
- ⬜ **M5-008** — Developer Modeのlocal Addon link / unlink UIを明示的な開発機能として実装する
- ⬜ **M5-009** — Addon install / enable / conflict / settings / failureのBrowser E2Eを追加する
- ⬜ **M5-010** — Addon & Extension Managementのsecurity / trust / UX / Roadmapを同期する

### Management Phase 5 完了条件

- Addonのinstall / uninstall / enable / disable / update /設定変更をManagementから安全に行える。
- trust / capability / dependency / conflictが操作前に確認できる。
- Addon管理操作をView moduleへ持ち込まない。
- Addonの最終状態はExtension Platformのauthoritative resultとObservationから確認する。

---

## 継続Backlog

- Management Addon / custom command tool contribution
- multi-user operation / conflict UX
- role-specific management workspace
- audit log viewer

これらは実装時にSimulation / Security / Extension Platform側の依存を確認してPhase化する。
