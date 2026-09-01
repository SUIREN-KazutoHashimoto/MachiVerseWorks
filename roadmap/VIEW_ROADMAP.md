# View Roadmap

このファイルは、MachiVerseWorks の **View 側の実装ロードマップ**です。Web Client の描画、カメラ、表示表現、UI / UX、可視化、描画最適化、localizationなど、Simulation の正本状態を利用してユーザーへ提示・操作する機能を対象とします。

Simulation Core、authoritative World、Simulation rule、determinism、Simulation workload 最適化、Server-authoritative command / Protocol / Save Dataなどは [`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md) で管理します。

MachiVerseWorks の View 開発を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** View Phase 1 — World & Regional Visualization Foundation  
> **次の実装タスク:** Simulation側の依存Phase進行に合わせて着手

## 全体の現在地

| View Phase | 内容 | 状態 |
| --- | --- | --- |
| 1 | World & Regional Visualization Foundation | ⏳ Simulation依存待ち |
| 2 | World Rendering & Rendering LOD | ⏳ Simulation依存待ち |
| 3 | Historical World View | ⏳ Simulation依存待ち |
| 4 | World & City Management UI | ⏳ Simulation依存待ち |
| 5 | Localization | ⏳ 待機 |

## View Roadmap 運用ルール

- 状態記号を付けるのは、単独で完了判定できる作業だけとする。
- 未完了Taskは `⬜`、必要な検証まで済んだ完了Taskは `✅` で表す。
- 1タスクは原則として「1つの観測可能な成果」を持つ。
- 1タスク内に独立した成果が複数ある場合は分割する。
- 描画、UI、UX、操作性、performance、accessibility、localization等を必要に応じて独立Taskへ分割する。
- Simulation の正本状態やruleをView都合で変更しない。
- View cache / LOD / culling / Camera / selection状態をSimulationのauthoritative stateへフィードバックしない。
- Simulation側の仕様変更が必要になった場合は、[`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md) と関連仕様・設計へ切り分ける。
- 実装だけでなく、必要なBrowser確認、test、performance計測、docs同期まで含めて完了判定する。
- 未実装の大テーマはTaskへ詰め込まず、PhaseまたはBacklogとして整理してから分解する。

## Simulation Roadmapからの移管対応

既存のSimulation Roadmapに混在していた未着手のView項目を以下へ移管する。旧Task IDは履歴・Issue・PRから追跡できるよう移管元として記録する。

| 移管元 | 移管先 |
| --- | --- |
| 旧 `P29-026` | `V1-001` |
| 旧 `P30-028` のWeb Client 3D可視化部分 | `V1-002` |
| 旧 Phase 34 `P34-001`〜`P34-015` | View Phase 2 `V2-001`〜`V2-015` |
| 旧 `P35-010` / `P35-015` のtimeline rendering部分 | View Phase 3 |
| 旧 Phase 36 のselection / Inspector / editor UI / dashboard / management UI / Browser E2E / rendering performance | View Phase 4 |
| 旧 Phase 38 Localization `P38-010`〜`P38-015` とlocalization関連closeout | View Phase 5 |

完了済みの `P25-014`、`P26-013`、`P28-016` 等のdebug可視化は、各Simulation PhaseのE2E・closeout証跡として既に完了履歴へ組み込まれているため移動しない。

---

## View Phase 1 — World & Regional Visualization Foundation

> **状態: ⏳ Simulation依存待ち**  
> **依存:** Simulation Phase 29 / 30 のProtocol / Server read model  
> 物理世界・地形・地理Feature・Settlement・都市生成結果をWeb Client上で観測するための基礎描画を整える。

- ⬜ **V1-001** — Web Clientのflat `GridHelper`依存を置換し、terrain mesh・water・主要Geographic Feature・地名を3D描画する（旧 `P29-026`）
- ⬜ **V1-002** — Settlement network / Parcel / Zone / development / urban naming / Road SignをServer read modelから3D可視化する（旧 `P30-028` のView部分）
- ⬜ **V1-003** — World / Regional visualizationのBrowser E2Eを追加し、Terrain・Feature・Settlement・Parcel・Road Signの表示を検証する
- ⬜ **V1-004** — World / Regional visualizationの基本frame time・draw call・memory基準を記録する
- ⬜ **V1-005** — World & Regional Visualization Foundationのarchitecture / UX / performance guideline / View Roadmapを同期する

### View Phase 1 完了条件

- flat gridを物理世界の表示正本として扱わず、Simulationから配信されたTerrain / Water / GeographicFeatureを3D表示できる。
- Settlement / Parcel / Zone / Building / naming / Road Sign等の地域生成結果をSimulationの正本状態から可視化できる。
- View側の状態がSimulationのauthoritative stateへ影響しない。

---

## View Phase 2 — World Rendering & Rendering LOD

> **状態: ⏳ Simulation依存待ち**  
> **依存:** View Phase 1 / Simulation Phase 29〜33  
> 同一のauthoritative World stateを、広域WorldからSettlement・街区・建物・Agentまで連続的に観測できるViewへ展開する。LOD / culling / streamingは描画にのみ適用し、Simulation workloadや結果へ一切フィードバックしない。

- ⬜ **V2-001** — Simulation read model / Rendering state / Camera stateの一方向境界とRendering LOD契約を仕様化する（旧 `P34-001`）
- ⬜ **V2-002** — World / Region / Settlement / District / Streetの複数scaleを連続ズームできるCamera / coordinate strategyを実装する（旧 `P34-002`）
- ⬜ **V2-003** — Terrain / Water / GeographicFeatureのdistance-based mesh LODとcullingを実装する（旧 `P34-003`）
- ⬜ **V2-004** — 遠距離Settlementを市街地shape・population / role indicator等のView aggregateとして描画し、Simulation Entity自体は集約しない（旧 `P34-004`）
- ⬜ **V2-005** — Road / Railway / Utility corridorをscaleに応じたgeometry / line representationへ切り替えるRendering LODを実装する（旧 `P34-005`）
- ⬜ **V2-006** — Buildingをindividual model / block mass / urban footprintへ切り替えるRendering LODを実装する（旧 `P34-006`）
- ⬜ **V2-007** — Person / Vehicle等の大量Agentについてfrustum / distance culling・instancing・virtualizationを実装する（旧 `P34-007`）
- ⬜ **V2-008** — View data streaming / cache / evictionを実装し、evictionがSimulation Entity lifecycleへ影響しないようにする（旧 `P34-008`）
- ⬜ **V2-009** — Settlement / District / GeographicFeature / Road等のlabel hierarchyとcollision回避を実装する（旧 `P34-009`）
- ⬜ **V2-010** — Population / land use / traffic / economy / utility等のregional overlay表示基盤を実装する（旧 `P34-010`）
- ⬜ **V2-011** — Dynamic Urban Region / Settlement influence / service catchmentを広域map overlayとして可視化する（旧 `P34-011`）
- ⬜ **V2-012** — World scaleから一軒のBuildingまで移動しても座標精度・selection精度を維持するfloating-origin等の必要なprecision対策を実装する（旧 `P34-012`）
- ⬜ **V2-013** — Rendering有無・Camera位置・LOD levelを変更してもSimulation state digestが一致するE2Eを追加する（旧 `P34-013`）
- ⬜ **V2-014** — 都市中心・郊外・農村・World overviewそれぞれのframe time / draw call / memory benchmarkを記録する（旧 `P34-014`）
- ⬜ **V2-015** — World Rendering & Rendering LODのarchitecture / UX / performance guideline / View Roadmapを同期する（旧 `P34-015`）

### View Phase 2 完了条件

- 巨大World上の複数Settlementを広域mapとして確認し、任意Settlementへズームして建物・道路・Agentまで観測できる。
- 遠距離描画を大胆に簡略化してもauthoritative Simulation stateは変化しない。
- Camera位置・LOD・View cache状態がSimulation結果へ影響しないことをE2Eで保証する。

---

## View Phase 3 — Historical World View

> **状態: ⏳ Simulation依存待ち**  
> **依存:** View Phase 2 / Simulation Phase 35  
> Simulation側が提供するread-only Historical projectionを使い、現在と過去のWorldを安全に閲覧できる時間軸UIを実装する。

- ⬜ **V3-001** — Web ClientへWorld timeline / time sliderを実装し、現在と過去のmap / 3D Viewを切り替えられるようにする（旧 `P35-010`）
- ⬜ **V3-002** — timeline操作中もlive Simulationを変更せず、Historical read-only projectionだけをViewへ適用する
- ⬜ **V3-003** — 100年以上の履歴を対象にtimeline移動・Entity lifetime表示・現在復帰を検証するBrowser E2Eを追加する
- ⬜ **V3-004** — historical timeline renderingのframe time / memory / cache benchmarkを記録する（旧 `P35-015` のView部分）
- ⬜ **V3-005** — Historical World ViewのUX / architecture / View Roadmapを同期する

### View Phase 3 完了条件

- 指定時点のHistorical Worldをmap / 3D Viewで閲覧できる。
- 過去閲覧中もlive Simulationを停止・巻き戻し・変更しない。
- 長期間timelineの表示性能を継続計測できる。

---

## View Phase 4 — World & City Management UI

> **状態: ⏳ Simulation依存待ち**  
> **依存:** View Phase 2 / 3 / Simulation Phase 36 のserver-authoritative command境界  
> BrowserからWorld・地域・都市状態を選択・調査し、Simulation側が提供するcommand境界を通して安全に編集・管理するUIを整える。

- ⬜ **V4-001** — Web ClientでMap / 3D Entityを選択するpicking / selection基盤を実装する（旧 `P36-003`）
- ⬜ **V4-002** — Region / Settlement / Building / Parcel / POI / Person / Vehicle / GeographicFeature / RoadSign等をServer read modelから表示するInspector基盤を実装する（旧 `P36-004`）
- ⬜ **V4-003** — Road / Laneのbuild / edit / remove commandを操作するeditor UIを実装する（旧 `P36-005` のView部分）
- ⬜ **V4-004** — Building / POI / Parcel / Zoneのbuild / edit commandを操作するeditor UIを実装する（旧 `P36-006` のView部分）
- ⬜ **V4-005** — Railway track / station / platformのbuild / edit commandを操作するeditor UIを実装する（旧 `P36-007` のView部分）
- ⬜ **V4-006** — Power Infrastructureのbuild / edit commandを操作するeditor UIを実装する（旧 `P36-008` のView部分）
- ⬜ **V4-007** — Water / Sewer Infrastructureのbuild / edit commandを操作するeditor UIを実装する（旧 `P36-009` のView部分）
- ⬜ **V4-008** — Gas Infrastructureのbuild / edit commandを操作するeditor UIを実装する（旧 `P36-010` のView部分）
- ⬜ **V4-009** — Optical Communication Infrastructureのbuild / edit commandを操作するeditor UIを実装する（旧 `P36-011` のView部分）
- ⬜ **V4-010** — Radio Site / Antenna / Spectrum設定のbuild / edit commandを操作するeditor UIを実装する（旧 `P36-012` のView部分）
- ⬜ **V4-011** — Geographic Feature名・Settlement / 地区 / 道路名・Road Signのoverride UIを実装する
- ⬜ **V4-012** — command失敗時にClient側だけ状態が進まないoptimistic-state禁止またはrollback方針を実装する（旧 `P36-014`）
- ⬜ **V4-013** — Simulation speed / pause / resume等のServer commandを操作する運転control UIを実装する
- ⬜ **V4-014** — Population / Traffic / Transit / Economy / Logistics / Power / Utility / Communication / Radio / Regional dynamicsのDashboardを実装する（旧 `P36-016` のView部分）
- ⬜ **V4-015** — Server configurationの変更可能項目・restart必要項目を区別して操作できる設定UIを実装する（旧 `P36-017` のView部分）
- ⬜ **V4-016** — current Save formatのsave / load commandをServer経由で実行する管理UIを追加する（旧 `P36-018` のView部分）
- ⬜ **V4-017** — destructive commandのconfirmation UIとstable error codeのlocalized表示を実装する（旧 `P36-019` のView部分）
- ⬜ **V4-018** — Inspector / build / edit / naming / signage / config / save操作のBrowser E2Eを追加する（旧 `P36-020`）
- ⬜ **V4-019** — 大規模Worldでselection・terrain・overlay・dashboardが描画hot pathを阻害しないperformance testを追加する（旧 `P36-021`）
- ⬜ **V4-020** — World & City Management UIのarchitecture / UX contract / View Roadmapを同期する（旧 `P36-022` のView部分）

### View Phase 4 完了条件

- World / Region / Settlementの主要Entity・Terrain・Geographic Feature・Road SignをBrowserから選択・調査できる。
- build / edit操作は必ずSimulation側のServer-authoritative commandを経由し、Clientだけで正本状態を変更しない。
- 自動生成された名称・標識を由来情報を保持したまま明示的にoverrideできる。
- 主要statistics・運転設定・Server設定・Save操作を管理UIから扱える。

---

## View Phase 5 — Localization

> **状態: ⬜ 未着手**  
> **依存:** View Phase 4 / stable error code・structured parameter契約  
> Protocol / Save / Simulationへ翻訳済み文言を持ち込まず、Web Clientの表示層でlocale resource・formatting・fallback・localized errorを管理する。

- ⬜ **V5-001** — `ja-JP`をdefaultにしたlocale discovery / fallback policyを再確認・固定する（旧 `P38-010`）
- ⬜ **V5-002** — 追加locale resource packを導入できるWeb Client loading境界を実装する（旧 `P38-011`）
- ⬜ **V5-003** — 数値・日時・単位・plural等のlocale formattingを共通化する（旧 `P38-012`）
- ⬜ **V5-004** — stable error code / structured parameterから各localeの表示文を生成するcoverageを拡張する（旧 `P38-013`）
- ⬜ **V5-005** — translation key欠落・未使用key・parameter不一致をCIで検出する（旧 `P38-014`）
- ⬜ **V5-006** — 少なくとも1つの追加localeで主要UI / Inspector / Dashboard / error表示をE2E確認する（旧 `P38-015`）
- ⬜ **V5-007** — localization resource loading / formattingのstartup / memory costをbenchmarkする（旧 `P38-017` のView部分）
- ⬜ **V5-008** — localization guide / compatibility policy / View Roadmapを同期する（旧 `P38-018` のView部分）

### View Phase 5 完了条件

- `ja-JP`以外のlocaleを主要UIへ追加できる。
- Protocol / Save / Simulationへ翻訳済み文言を持ち込まない。
- locale resource欠落・parameter不一致をCIで検出できる。
- 追加localeでも主要UI・Inspector・Dashboard・error表示をE2E検証できる。

---

## 継続Backlog

現時点では未定。

## 新規Backlogの扱い

View 開発中に新しい大テーマが見つかった場合は、既存Phaseへ無理に詰め込まない。

1. 既存Phaseの完了に必須なら、そのPhaseへ独立Taskとして追加する。
2. 完了に必須でない大テーマなら、このView Roadmap末尾へBacklogとして記録する。
3. 着手時に必要な仕様・設計・UX方針を関連文書へ切り分ける。
4. 実装・描画・操作・検証・performanceのどこまでをPhase完了条件とするか明示する。
5. Phase完了時に、残件が暗黙に持ち越されていないことを確認する。
