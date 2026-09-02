# Simulation Roadmap

このファイルは、MachiVerseWorks の **Simulation側の実装ロードマップ**です。Simulation Core、authoritative World、Simulation rule / semantic state、Save、authoritative observation source、およびserver-authoritative Management command contractを対象とします。

- Observation Request、subscription、cache、deduplication、delivery、Protocol adaptation、reconnect / resyncは[`GATEWAY_ROADMAP.md`](GATEWAY_ROADMAP.md)を正本とする。
- 純粋なread-only View表現・Camera・Selection・Inspector・描画最適化・localizationは[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)を正本とする。
- World / City / Serverを変更するeditor・運転control・Save / Load・configuration等のUIは[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)を正本とする。

SimulationがWorldの唯一の意味的正本です。Activity、Status、分類、予定、ETA、状態遷移、semantic event等の意味的処理はSimulation側で完結させ、Gateway / Viewへ推測・補完・再計算させません。

> **現在:** Phase 31 — Persistent Regional & Settlement Evolution（実装完了・develop統合待ち）
> **次の実装タスク:** Phase 32 `P32-001`

> **Gateway:** Gateway Phase 3 `G3-001` から独立進行可能  
> **Application version:** ルート [`VERSION`](../VERSION) を正本とする  
> **Protocol:** `2.19`
> **Save format:** `11`

## 進行ルール

- Simulationはauthoritative state / rule / Save / semantic observation source / domain payload contractを担当する。
- GatewayはSimulationとread-only consumerの間のrequest / subscription / cache / deduplication / serialization adaptation / deliveryを担当し、Simulationへ配送都合を逆流させない。
- Viewはread-only consumerとし、Simulation内部実装やCamera位置をauthoritative stateの生成条件へ使わない。
- Management mutationは専用command境界を経由し、View / Gatewayへmutation責務を持ち込まない。
- Gateway / View / ManagementのPhase番号はSimulationと同期させず、それぞれのRoadmapの依存関係を正本とする。
- 完了済みPhaseの詳細は `docs/archive/` へ退避し、現行Roadmapは次の判断点を読みやすく保つ。
- 同一seed / config / inputからのdeterminism、stable ID、参照整合性、bounded inputを継続的な共通条件とする。
- **Task実装状態・`develop`統合状態・Phase正式closeoutは別の状態として扱う。**

## 現在のPhase一覧

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0〜24 | Foundation / Mobility / Population / Economy / Utilities | ✅ 完了・履歴化済み |
| 25 | Gas Infrastructure | ✅ 完了 |
| 26 | Optical Communication | ✅ 完了 |
| 27 | Remote MCP Administration | ✅ 完了 |
| 28 | Radio & Spectrum Foundation | ✅ 完了 |
| 29 | World & Physical Environment Generation | ✅ 完了・develop統合済み |
| 30 | Regional & Urban Generation | ✅ 完了・develop統合済み |
| 31 | Persistent Regional & Settlement Evolution | ✅ 実装完了・develop統合待ち |
| 32 | Simulation Scheduling & Workload Optimization | ⬜ 未着手 |
| 33 | Deterministic Parallel Simulation | ⬜ 未着手 |
| 35 | Historical World & Replay | ⬜ 未着手 |
| 36 | World & City Management Commands | ⬜ 未着手 |
| 37 | Distribution & Compatibility | ⬜ 未着手 |
| 38 | Extension Platform | ⬜ 未着手 |

Gatewayは独立した[`GATEWAY_ROADMAP.md`](GATEWAY_ROADMAP.md)でPhase 1から管理する。Viewは[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)、Management UIは[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)で独立管理する。

> Phase 29着手前までの完全な計画履歴は `docs/archive/roadmap-through-phase24-closeout.txt`、Phase 29 closeout直前の全Roadmap snapshotは `docs/archive/roadmap-through-phase29-plan-snapshot.txt` に保存する。

## 依存関係の読み方

- 各Simulation Phaseの`依存`はauthoritative contractの依存を表す。
- Gateway / View / Management側の依存は各Roadmapを正本とし、Simulation Phase番号へ無理に同期させない。
- Simulation側TaskがClient配信を必要とする場合、Simulationはsemantic observation source / domain contractまでを担当し、request / subscription / cache / deliveryをSimulation側Taskへ戻さない。
- Gateway側最適化が未完成でも、正しいbaseline deliveryがある限りSimulation domainのcloseoutを不必要に止めない。ただしGateway側の未完了TaskをSimulation完了として数えない。
- Simulation内部の大規模変更とGatewayの`SimulationRuntime` / source capture境界変更が競合する場合は、同じ境界への同時変更を避ける。

## Gateway / View / Managementへの移管記録

### Gateway Roadmap

旧Simulation RoadmapのObservation Gateway系Taskは[`GATEWAY_ROADMAP.md`](GATEWAY_ROADMAP.md)へ移管する。

- 旧`OBS-001`〜`OBS-003` — Observation boundary / detached source / Server module整理 → Gateway Phase 1
- 旧`OBS-004`〜`OBS-007` — cache / deduplication / encoded payload / invalidation / resync → Gateway Phase 2〜3
- 旧`OBS-008` — Current / Recent / Planned / Relations inspection → Gateway Phase 4
- 旧`OBS-009` / `OBS-010` — invariance E2E / scalability・performance → Gateway Phase 6
- 旧`OBS-011` — docs同期 → 各Gateway Phase / Phase 6 closeout

Phase29ブランチで一時的に置いていた`OBG-*`もGatewayへ統合する。

| 旧Task | Gateway側の扱い |
| --- | --- |
| `OBG-001` WorldSnapshotCoordinator / capture point統一 | Gateway `G1-002`を中心に、共通source capture設計へ統合 |
| `OBG-002` lock内captureとlock外serialization / cache | Gateway `G1-002` / `G2-006` / Phase 6 performance検証へ統合 |
| `OBG-003` passive immutable DTO / observation invariance | Gateway `G1-004` / `G6-001` / `G6-002`へ統合 |
| `OBG-004` spatial filtering / cache / invalidation | Gateway Phase 2 `G2-*` / Phase 3 `G3-*`へ統合 |

Gateway分離は責務・進捗管理の分離であり、現時点で別repository / process / deploy unitを要求しない。

### View Roadmap

- 旧`P29-026` — Terrain / Water / GeographicFeature / 地名の3D描画 → View Phase 3 `V3-001`
- 旧`P30-028`のWeb Client 3D可視化部分 → View Phase 4 `V4-001`
- 旧Phase 34 — World Rendering & Rendering LOD → 主にView Phase 6
- 旧`P35-010` / `P35-015`のtimeline rendering部分 → View Phase 9
- 旧`P36-003` / `P36-004` — read-only Selection / Inspector → View Phase 7
- 旧Phase 38 Localization / View extension関連 → View Phase 10 / 12

### Management Roadmap

- 旧Phase 36のeditor / override / runtime / configuration / Save / Load / confirmation UI → Management Phase 1〜4
- Extension管理UI → Management Phase 5
- read-only Selection / InspectorはManagementへ持たずView componentを再利用する。

## World-scale Simulationの不変条件

- **Simulation FidelityはCamera距離・表示状態・都市/郊外/農村の区分で変更しない。**
- **Viewは完全read-onlyである。** Viewの存在・非存在、接続数、Camera、Selection、FPS、Rendering LOD、View cacheでSimulation結果を変えない。
- **Gatewayはread-only delivery境界である。** subscription / cache / deduplication / reconnect状態でSimulation結果を変えない。
- **CameraやRendering LODはSimulation結果へ影響しない。** 同一seed・初期状態・外部入力・経過時間なら、観測した地域と一度も描画しなかった地域で同一authoritative stateを得る。
- **負荷軽減はSimulationの省略ではなく不要な計算の排除で行う。** Event scheduling、dirty update、dependency tracking、spatial index、時刻からの派生値、deterministic parallelism等を使用する。
- **Global coarse fieldは生成・検索・indexの補助表現であり、詳細Simulationの代替正本にしない。**
- Rendering LOD / culling / View cacheはView Roadmapの責務とし、Simulation stateやworkloadの判定条件に使用しない。
- Gateway cache / delivery stateはGateway Roadmapの責務とし、Simulation stateやworkloadの判定条件に使用しない。
- Management commandは明示的な外部入力としてSimulation結果を変更できるが、必ずserver-authoritative command境界を通し、read-only Observationと混同しない。

## 推奨closeout順と並行開発

Simulation側は次の順序を基本とする。

```text
Phase 29 World / Physical Environment
  -> Phase 30 Regional / Urban Generation
  -> Phase 31 Persistent Regional / Settlement Evolution
  -> Phase 32 Scheduling / Workload Optimization
  -> Phase 33 Deterministic Parallel Simulation
  -> Phase 35 Historical World / Replay
  -> Phase 36 World / City Management Commands
  -> Phase 37 Distribution / Compatibility
  -> Phase 38 Extension Platform
```

Gatewayは[`GATEWAY_ROADMAP.md`](GATEWAY_ROADMAP.md)で独立してPhase 1から進め、現行Server / Protocolで成立する基盤はSimulation Phase 29以降と並行実装できる。View / Managementも各Roadmapの依存に従って独立進行する。

---

## Phase 29 — World & Physical Environment Generation

> **状態: ✅ 完了 / develop統合済み**  
> **依存:** Phase 0〜28  
> Global EnvironmentとDetailed 3D Terrainを分離しつつ、両方をSimulation authoritative boundaryから決定する。View / Camera / Gateway subscription状態は生成正本にしない。

### 29.1 Global World Generation

- ✅ **P29-001** — `WorldEnvironmentConfig` / world seed / geographic north / latitude・hemisphere・sea level等の正本契約を仕様化する
- ✅ **P29-002** — latitude・continentality・maritime influence・temperature・seasonality・precipitation等の連続parameterをauthoritative inputとして定義する
- ✅ **P29-003** — global environment field上にOcean / Continent / Island等をdeterministicに生成する
- ✅ **P29-004** — large-scale elevation・mountain range・plain・basin等の地形形成fieldを生成する
- ✅ **P29-005** — latitude・elevation・maritime influence等から簡易Climate / temperature / precipitation fieldをdeterministicに派生する
- ✅ **P29-006** — watershed / drainage / major river / lake / coastをglobal field上へ生成する
- ✅ **P29-007** — 地域ごとのclimate・hydrology・terrain tendency・buildability入力をquery可能にする
- ✅ **P29-008** — configured coastline distance等のconfigured値と生成後derived値を混同しないprecedence / derivation契約を定義する
- ✅ **P29-009** — 世界全体から複数のSettlement candidate regionを抽出し、自然条件・交通可能性・水アクセス等の基礎scoreを計算する
- ✅ **P29-010** — weighted deterministic selectionによりcoastal / river / basin / mountain / cold / dry / island等の立地多様性を保持する

### 29.2 Detailed Terrain & Geographic Features

- ✅ **P29-011** — 任意Region / PartitionをSimulation解像度へ展開する`TerrainSurface`正本モデルを仕様化する
- ✅ **P29-012** — arbitrary `(X,Y)` のsurface height・normal・slope・roughness・surface material queryを実装する
- ✅ **P29-013** — mountain / hill / ridge / valley / basin / plateau / cliff / plain等を含む高解像度Terrain generatorを実装する
- ✅ **P29-014** — river / tributary / lake / coast / floodplain等のregional hydrologyをDetailed Terrain / TerrainVolumeへ接続する
- ✅ **P29-015** — Air / Water / Soil / Rock / Voidを扱える`TerrainVolume` / chunk境界を定義する
- ✅ **P29-016** — 同一XYに複数surfaceを持てるcavity / cave / overhang対応の3D terrain query境界を実装する
- ✅ **P29-017** — terrain solid / cavityをWorldVolume・3D spatial queryと共存させ、地下構造を同一World座標へ配置できるようにする
- ✅ **P29-018** — terrain collision / ground snapping / surface tracking基盤を実装し、平面gridへの暗黙依存を除く
- ✅ **P29-019** — Road / Railway / Building placementがslope・surface・solid volumeを参照できるterrain constraint APIを実装する
- ✅ **P29-020** — `GeographicFeature`のstable ID・type・geometry / area・parent relation・elevation range契約を仕様化する
- ✅ **P29-021** — Mountain / Mountain Range / River / Tributary / Lake / Valley / Basin / Plain / Plateau / Pass / Cape / Bay / Coast / Island / Peninsula / Cave等をTerrainからdeterministicに識別する
- ✅ **P29-022** — Geographic Featureへseed-deterministicな自然地名を付与する
- ✅ **P29-023** — 自然地名のToponym provenance契約を実装する
- ✅ **P29-024** — WorldEnvironment / Terrain / GeographicFeature / Toponymをcheckpoint / Save Dataへ統合し、read-only observationがSave状態を変更しない境界を固定する
- ✅ **P29-025** — World / Terrain / GeographicFeature / Toponymのauthoritative observation sourceとProtocol 2.17 domain payloadを実装し、Gatewayがread-only配信できるcontractを提供する
- ✅ **P29-027** — 同一seedから同じglobal environment / detailed terrain / feature / toponymを得るServer再起動reproducibility E2Eを追加する
- ✅ **P29-028** — global field / detailed terrainのgeneration・query・memory benchmarkを追加する
- ✅ **P29-029** — World / Terrain / Geographic Featureのspecification / architecture / ADR / ROADMAPを同期する

> 旧`P29-026`のWeb Client 3D描画はView Roadmap Phase 3 `V3-001`へ移管済み。Phase29で実装したServer publisher / 同一volume publish-cycle dedupはGatewayのbaseline実装として引き継ぎ、今後の共通cache / dedup / resyncはGateway Roadmap Phase 1〜3を正本とする。

### Phase 29 完了条件

- ✅ 世界全体の環境fieldと任意Region / Partitionの高解像度Terrainを同一seed・設定から再現できる。
- ✅ Global fieldを詳細Simulationの代替正本にせず、Camera / Gateway subscription状態に依存しない。
- ✅ Terrainがsurface queryに加え、Water / solid / Void / cavityを扱う3D volume境界を持つ。
- ✅ Road / Railway / Building等がterrain height / slope / 3D volume constraintを共通APIから参照できる。
- ✅ 河川・山・谷・盆地・峠・湾等がstableな`GeographicFeature`となり、自然地名とprovenanceをSave / domain payload境界で扱える。
- ✅ Save inputはfeature / geometry / toponymのbounded pre-scanを持ち、Protocol domain payloadは未知discriminant / 壊れた参照を拒否する。
- ✅ read-only Observationを取得してもauthoritative Save状態が変化しない。
- ✅ 植生・動物・生態系、advanced erosion / flood、cut/fill等はPhase 29完了条件から分離している。

### Phase 29 Closeout evidence

- Specification: `docs/specifications/world-environment-terrain.md`
- Architecture: `docs/architecture/world-environment-terrain.md`
- ADR: `docs/decisions/ADR-0008-authoritative-two-level-world-environment-terrain.md`
- Protocol domain payload: 2.17 / `WorldEnvironmentSnapshot` message 800
- Save: format 11 optional extension、旧Save後方互換
- Tests: Simulation / Persistence / Protocol regression、observation Save-byte invariance、3D terrain invariants
- Integrated baseline delivery E2E: Server restart reproducibility
- Benchmark: dedicated World Environment benchmark workflow
- Gateway follow-up: request / subscription / shared cache / generalized dedup / delivery / resyncは`GATEWAY_ROADMAP.md`
- Integration: PR #183 で `develop` へ統合済み

---

## Phase 30 — Regional & Urban Generation

> **状態: ✅ 完了 / develop統合済み**  
> **依存:** Phase 10〜19 / 21〜29  
> Phase 29の自然環境から複数Settlementの成立理由と歴史を生成し、道路・街区・Parcel・Land Use・Building・POI・人間由来の地名・道路標識を形成する。単一中心の完成都市を一度に生成せず、environment-driven / history-driven / iterative / polycentricな地域生成を正本方針とする。

### 都市・地域生成の原則

- **Deterministic** — 同じseed・設定・input worldから同じ地域とSettlement群を再生成できる。
- **Environment-driven** — 地形・水系・気候・災害risk・建設costがSettlement立地と形状へ影響する。
- **History-driven** — 小集落→交通→中心形成→拡張→郊外化→再開発の履歴を蓄積して現在の地域を作る。
- **Polycentric** — 都市・町・村・集落が複数の中心と役割を形成できる。
- **Iterative** — Generate → Evaluate → Improveを反復する。
- **Multi-objective** — accessibility・terrain adaptation・cost・risk・compactness・regional balance等を同時評価する。
- **Quality-first** — 初期地域生成はrealtime完了を要求せず、再現可能なbudgetを品質へ使える。

### 30.1 Settlement Network & Historical Urban Growth

- ✅ **P30-001** — Settlement / SettlementOrigin / RegionalRole / historical growth eventの正本契約を仕様化する
- ✅ **P30-002** — flatness・water access・transport potential・buildability・resource access・flood risk・steep slope・isolation・construction cost等からSettlement Suitabilityを評価する
- ✅ **P30-003** — Phase 29のcandidate regionからweighted deterministic selectionで複数のSettlement originを決定する
- ✅ **P30-004** — river plain / estuary / bay / basin / valley / mountain pass / resource access等からSettlementOrigin / RegionalRole / InitialEconomyの基礎傾向を派生する
- ✅ **P30-005** — City / Town / Village / Hamletを固定テンプレートとして直接配置せず、複数の初期Settlementと人口・機能・周辺関係を生成する
- ✅ **P30-006** — 地形・河川・峠・海岸・Settlement間需要を考慮してprimary road / regional / intercity corridorを生成する
- ✅ **P30-007** — Railway等の大規模transport corridorを需要・地形・Settlement成長履歴から形成できるgeneration境界を実装する
- ✅ **P30-008** — 各Settlementについてpopulation / economy growthに応じたcenter formation・urban expansion・suburbanizationを段階生成する
- ✅ **P30-009** — congestion / accessibility / land pressure等に応じたredevelopment・new center formation・複数中心化の履歴ruleを実装する
- ✅ **P30-010** — 自然地名をSettlement / City / District等の人間側名称へ継承・変形するNaming provenance ruleを実装する
- ✅ **P30-011** — Settlementごとの生成履歴とSettlement間関係をevent / generation stageとして保存する

### 30.2 Detailed Urban Fabric & Signage

- ✅ **P30-012** — Parcel境界・Zone種別・土地利用・占有/development stateの正本契約を仕様化する
- ✅ **P30-013** — Historical Road Networkからterrain-awareな詳細Road / Lane networkを生成する
- ✅ **P30-014** — Road NetworkからBlock / Parcelをdeterministicに生成するsubdivisionを実装する
- ✅ **P30-015** — Road access・parcel size・slope・flood risk・land value・land use等からdevelopment suitabilityを評価する
- ✅ **P30-016** — Zone / Land Useに応じたBuilding用途・規模・density・height候補を生成する
- ✅ **P30-017** — 初期生成履歴として空ParcelへのBuilding / POI developmentを段階生成する
- ✅ **P30-018** — demand変化に応じたredevelopment / vacancyの最小ruleを実装する
- ✅ **P30-019** — station district / CBD / industrial area / suburb / old town等を都市履歴とaccessibilityから形成する
- ✅ **P30-020** — 初期Population / Household / Jobを複数Settlementへ配置するseeding処理を実装する
- ✅ **P30-021** — Railway / Power / Water / Sewer / Gas / Optical / Radio等を壊さず地形とSettlement networkへ適応するgeneration constraintを定義する
- ✅ **P30-022** — 自然地名・Settlement履歴・District hierarchyからRoad / Bridge / Tunnel / Station / District等の名称をdeterministicに生成する
- ✅ **P30-023** — Road geometry・hierarchy・destination・Geographic Featureを解析するRoad Context Analysisを実装する
- ✅ **P30-024** — steep grade / sharp curve / rock slope / floodplain / river crossing / mountain pass / tunnel / coastal lowland等から必要な標識を決定する
- ✅ **P30-025** — destination name・distance・direction・route contextを使う案内標識と地名標識をdeterministicに生成する
- ✅ **P30-026** — Road Signをstable ID付き都市Entityとして配置し、Road Segment / Lane / GeographicFeature / named destinationへの参照を保持する
- ✅ **P30-027** — Parcel / Zone / generation history / human toponym / Road Signをcheckpoint / Save Dataへ統合する
- ✅ **P30-028** — Settlement network / Parcel / Zone / development / urban naming / Road Signのauthoritative observation source / domain payload contractを実装する。subscription / cache / deliveryはGateway Roadmapへ切り分ける

> Web Client 3D可視化はView Roadmap Phase 4 `V4-001`へ分離する。

### Generation Quality / Validation

- ✅ **P30-029** — `RegionalQualityReport`を実装し、TerrainAdaptation / RoadConnectivity / AverageSlopeCost / Accessibility / CongestionRisk / LandUseConsistency / FloodExposure / UrbanCompactness / PolycentricBalance等を独立評価する
- ✅ **P30-030** — 弱いquality dimensionに応じて道路・土地利用・Settlement中心配置等を改善するGenerate → Evaluate → Improve loopを実装する
- ✅ **P30-031** — 同一seed・設定で同一Settlement network・都市形状・名称・標識・quality reportを生成するreproducibility E2Eを追加する
- ✅ **P30-032** — river / port / basin / valley / mountain / cold / dry inland / island region等のdeterministic fixtureを追加する
- ✅ **P30-033** — Draft / Standard / High Quality等のgeneration quality presetとiteration budgetを定義する
- ✅ **P30-034** — 小/中/大規模Settlement networkのgeneration時間・memory・quality metrics・初期Simulation負荷benchmarkを記録する
- ✅ **P30-035** — World→Terrain→Settlement Network→Historical Growth→Urban Fabric→Validationのspecification / architecture / ADR / ROADMAPを同期する

### Phase 30 完了条件

- Settlement立地が自然環境とregional contextから説明可能で、単一の完成都市を単純noiseから直接生成しない。
- 都市・町・村・集落が複数存在し、regional networkで関係しながら異なる規模・役割・成長履歴を持てる。
- Road / Parcel / Land Use / Building / POIが地形・水系・歴史的成長へ適応している。
- 自然地名からSettlement名・地区名・道路名・橋・トンネル・駅名等へ由来を追跡できる。
- 道路標識を地形・道路形状・destination・named Geographic Featureから導出する。
- 生成品質を独立評価し、同じseed・quality presetから同じpolycentricな地域を再現できる。

### Phase 30 Closeout evidence

- Specification: `docs/specifications/regional-urban-generation.md`
- Architecture: `docs/architecture/regional-urban-generation.md`
- Protocol domain payload: 2.18 / `RegionalGenerationSnapshot`
- Save: format 11 extension
- Validation: Phase30 deterministic / materialization / Protocol / Persistence / Server regression
- Benchmark: Regional Generation benchmark workflow
- Integration: PR #225 で `develop` へ統合済み

---

## Phase 31 — Persistent Regional & Settlement Evolution

> **状態: ✅ 実装完了 / develop統合待ち**  
> **依存:** Phase 15 / 19 / 21 / 22 / 24〜30  
> Phase 30が生成した初期Worldを固定された完成品として扱わず、Simulation時間の進行に応じて都市・町・村・集落・Parcel・Building・交通・地域間関係が継続的に変化するauthoritativeな地域Simulationを確立する。Settlementの規模分類は固定typeではなく実際の人口・機能・サービス・接続性から派生させ、一極集中を強制しない。

- ✅ **P31-001** — Persistent Regional Simulationの責務、時間粒度、Settlement / Parcel / Buildingのauthoritative境界を仕様化する
- ✅ **P31-002** — Settlement population・jobs・services・density・accessibility等からHamlet / Village / Town / City等を派生分類するstable ruleを実装する
- ✅ **P31-003** — Settlement center / territory / influenceを固定境界ではなく実World stateから再評価できる契約を実装する
- ✅ **P31-004** — 既存Population / Householdの転居・転入・転出を住宅・雇用・生活利便性・交通accessibilityへ接続する
- ✅ **P31-005** — 既存Industry / Jobs / EconomyとPopulationを接続し、Settlement内外の雇用・通勤需要を継続更新する
- ✅ **P31-006** — 商業・教育・医療等のserviceごとに到達可能性とservice catchment / influenceを計算する最小モデルを実装する
- ✅ **P31-007** — Settlement間の物流・商流を既存Logistics / Freightへ接続し、地域間依存をauthoritative stateとして観測できるようにする
- ✅ **P31-008** — Population / Economy / Accessibility / Land ValueからParcel単位の住宅・商業・工業等のdevelopment demandを計算する
- ✅ **P31-009** — development demandとParcel suitabilityから空地への新規Building / POI建設を時間経過イベントとして実装する
- ✅ **P31-010** — BuildingのbuiltAt / condition / use / capacity等を用いるaging・renovation・用途変更・redevelopment lifecycleを実装する
- ✅ **P31-011** — demand低下・事業停止・人口減少等からvacancy・closure・abandonment・demolition・空地化を実装する
- ✅ **P31-012** — 交通量・人口・産業・service需要からRoad / Transit / Utilityへの整備・増強需要signalを生成する共通境界を実装する
- ✅ **P31-013** — 既存Road / Transit networkの接続性変化がSettlement成長・土地利用・通勤・物流へフィードバックする最小ruleを実装する
- ✅ **P31-014** — 既存Settlement外で人口・雇用・交通nodeが集積した場合に新しいSettlementが成立できるemergence ruleを実装する
- ✅ **P31-015** — 人口・service・建物が減少したSettlementの縮小・分類降格・廃村化を履歴を失わず表現する
- ✅ **P31-016** — 通勤・物流・service依存・連続市街地等から複数SettlementのMetro / Urban Region関係を動的に派生する
- ✅ **P31-017** — 単一中心への固定吸収を避け、複数中心が競合・補完・専門化できるregional interaction ruleを実装する
- ✅ **P31-018** — Settlement growth / decline / Building lifecycle / regional relationの主要変化をstable historical eventとして記録する
- ✅ **P31-019** — Persistent Regional stateと必要な履歴をcheckpoint / Save Data / authoritative observation source / Protocol domain payloadへ統合し、Gatewayからread-only配信できるようにする
- ✅ **P31-020** — 複数都市・町・村・集落が100年以上成長・停滞・衰退・再成長するlong-run deterministic E2Eを追加する
- ✅ **P31-021** — 大都市・郊外・農村・遠隔集落を同一ruleで進めるWorld-scale Simulation benchmarkを記録する
- ✅ **P31-022** — Persistent Regional & Settlement Evolutionのspecification / architecture / ADR / ROADMAPを同期する

### Phase 31 完了条件

- ✅ 初期生成後もSettlement / Parcel / Building / Population / Economy / Transportの状態が時間経過で継続的に変化する。
- ✅ 都市・町・村・集落の分類と影響圏が実Simulation状態から派生し、固定テンプレートや単一中心への強制収束に依存しない。
- ✅ 遠隔地・郊外・農村を集計値だけの別Simulationへ置換せず、都市部と同じauthoritative model・ruleで成長・衰退を再現できる。
- ✅ 建設・老朽化・用途変更・再開発・閉鎖・解体・Settlement成立/消滅等が履歴として追跡できる。

### Phase 31 Closeout evidence

- Specification: `docs/specifications/persistent-regional-evolution.md`
- Architecture: `docs/architecture/persistent-regional-evolution.md`
- ADR: `docs/decisions/ADR-0010-persistent-regional-evolution.md`
- Protocol domain payload: 2.19 / `PersistentRegionalEvolutionSnapshot`
- Save: format 11内へPersistent Regional state / event historyを統合し、既存Save format番号は変更しない
- Determinism: 120年同一seed再現、60年衰退→60年再成長、materialized World 12年 + checkpoint round-trip
- Regional interaction: actual Employment / Logistics / service catchment / continuous urban areaからCommuting / Trade / Service / Metroを年次再評価し、competition / complementarity / specialization profileを提供する
- Territory: current center / influence / neighboring Settlement距離からderived territoryを再評価する
- Benchmark: `PersistentRegionalEvolutionBenchmarks`を共通BenchmarkDotNet matrixへ登録
- Gateway boundary: detached read-only source、Protocol capability gate、2.18以前への2.19 payload非配信
- Integration: 最新`develop`を同期済み。Phase31本体は`develop`統合待ち

---

## Phase 32 — Simulation Scheduling & Workload Optimization

> **状態: ⬜ 未着手**  
> **依存:** Phase 31  
> World-scale Simulationの精度・rule・Entity解像度を落とさず、状態が変化しないEntityや影響を受けない領域への不要な仕事を除去する。Camera距離ではなく、次回event時刻・dependency・dirty state・spatial relationによって実行workloadを決定する。

- ⬜ **P32-001** — Simulation Fidelity / Workload / Rendering LODを別概念として仕様化し、Camera依存Simulation LODを禁止するADRを追加する
- ⬜ **P32-002** — stable time orderingを持つWorld-level Event Scheduler / priority queueを実装する
- ⬜ **P32-003** — Entity / systemが次に状態変化し得る時刻を登録するnext-event scheduling契約を実装する
- ⬜ **P32-004** — age / building age / contract duration等、current timeから厳密に派生可能なstateを毎tick mutationしないtime-derived state境界を実装する
- ⬜ **P32-005** — dependency change時のみ再評価対象をdirty化するDependency / Dirty Update基盤を実装する
- ⬜ **P32-006** — Road / Parcel / Utility / Settlement等の変更影響を空間範囲へ限定するspatial invalidationを実装する
- ⬜ **P32-007** — 同一時刻・同一ruleの大量処理を結果を変えずbatch化できるdeterministic batch execution境界を実装する
- ⬜ **P32-008** — activityのないEntityを次回eventまでwork queueから外すscheduled dormancyを実装し、Cameraや描画状態を判定条件に使用しない
- ⬜ **P32-009** — dependency変化・予定event・外部commandによる正確なwake-up / rescheduleを実装する
- ⬜ **P32-010** — event同時刻競合・stable ID ordering・random stream消費順を含むdeterministic execution policyを実装する
- ⬜ **P32-011** — Scheduler stateをSaveへ直接保持する場合とauthoritative stateから再構築する場合の互換契約を定義する
- ⬜ **P32-012** — system別event count / dirty count / skipped work / queue depth / execution costを観測するSimulation workload metricsを追加する
- ⬜ **P32-013** — 同じWorldを常時表示した場合と一度も表示しなかった場合でauthoritative state digestが一致するE2Eを追加する
- ⬜ **P32-014** — 都市・郊外・農村・遠隔集落についてScheduler最適化前後でstate digestが一致するequivalence testを追加する
- ⬜ **P32-015** — large Worldでnaive tick scanとoptimized schedulingのCPU / allocation / queue / throughput benchmarkを記録する
- ⬜ **P32-016** — Simulation Scheduling & Workload Optimizationのspecification / architecture / performance guideline / ROADMAPを同期する

### Phase 32 完了条件

- 遠い・見えないという理由だけでSimulation modelやEntity解像度を簡略化しない。
- Event scheduling / dirty update / time-derived state / spatial invalidationによって、結果に影響しない処理を実行しない。
- 同一seed・inputでは最適化前後および観測有無でauthoritative stateが一致する。
- World規模拡大時のCPU・allocation・event queue負荷を継続benchmarkできる。

---

## Phase 33 — Deterministic Parallel Simulation

> **状態: ⬜ 未着手**  
> **依存:** Phase 32  
> Spatial Partitionやdomain単位でSimulation workloadを並列化しながら、worker数・partition配置・実行順の違いをWorld結果へ漏らさないdeterministic executionを確立する。Partitionは計算・memory localityの単位であり、Simulation Fidelityの境界にはしない。

- ⬜ **P33-001** — deterministic parallelismのordering / ownership / synchronization / RNG policyを仕様化する
- ⬜ **P33-002** — Spatial Partitionのownershipと跨境Entity / referenceの参照契約を実装する
- ⬜ **P33-003** — Partition間event / dirty propagationをstable orderで受け渡すboundary queueを実装する
- ⬜ **P33-004** — Entity / system / purposeごとに独立したdeterministic random streamを割り当て、worker schedulingからRNG結果を分離する
- ⬜ **P33-005** — Phase 32 Schedulerから安全にparallel work batchを抽出するworker schedulingを実装する
- ⬜ **P33-006** — parallel aggregation / reductionで浮動小数点順序等が結果を不安定化しないdeterministic reduction方針を実装する
- ⬜ **P33-007** — Road / Population / Economy / Logistics / Utility / Regional evolutionのうち依存関係を満たすworkloadを段階的にparallel化する
- ⬜ **P33-008** — Partition migration / load rebalanceを実装する場合もstable IDとauthoritative stateを保持する契約を定義する
- ⬜ **P33-009** — 1 / 2 / 4 / 8 / 16 workerで同一World state digestを得るdeterminism E2Eを追加する
- ⬜ **P33-010** — 異なるSpatial Partition分割・worker割当でも同一World state digestを得るdistribution-invariance testを追加する
- ⬜ **P33-011** — partition boundary上のRoad / Utility / migration / logistics eventを跨ぐlong-run E2Eを追加する
- ⬜ **P33-012** — worker scaling・CPU utilization・memory locality・sync cost・throughput benchmarkを記録する
- ⬜ **P33-013** — Deterministic Parallel Simulationのspecification / architecture / ADR / performance guideline / ROADMAPを同期する

### Phase 33 完了条件

- worker数やPartition配置を変更しても同一seed・inputから同一authoritative World stateを得られる。
- Partition境界は計算配置のためだけに存在し、郊外・遠隔地・別PartitionのSimulation Fidelityを変更しない。
- parallelismによる性能向上とsync overheadを継続benchmarkできる。

---

## Phase 35 — Historical World & Replay

> **状態: ⬜ 未着手**  
> **依存:** Phase 31〜33  
> Worldの現在値だけでなく、Settlementの成立・成長・衰退、Buildingの建設・改修・用途変更・解体、交通網や地域関係の変化を時間軸で追跡・再構築できる履歴基盤を整える。View側のtimeline / time sliderはView Roadmap Phase 9で扱い、Simulation側はread-only Historical projectionまでを責務とする。

- ⬜ **P35-001** — Historical Event / Snapshot / Replayの責務・保持範囲・determinism・Save境界を仕様化する
- ⬜ **P35-002** — Entity created / changed / destroyedと主要domain eventをstable ID・SimulationTime付きで記録するhistory contractを実装する
- ⬜ **P35-003** — 長期間Replayを全event先頭から再実行しなくて済むperiodic historical snapshotを実装する
- ⬜ **P35-004** — Snapshot + Eventから指定SimulationTimeのread-only World stateをdeterministicに再構築する
- ⬜ **P35-005** — Entity lifetime / provenanceをqueryし、現存しないBuilding / Settlement / Infrastructureも履歴から参照できるようにする
- ⬜ **P35-006** — Buildingの建設・改修・用途変更・vacancy・解体履歴をquery可能にする
- ⬜ **P35-007** — Settlementの人口・分類・中心・territory・role・Urban Region関係の時系列をquery可能にする
- ⬜ **P35-008** — Road / Railway / Utility等のnetwork変更履歴をquery可能にする
- ⬜ **P35-009** — Historical query / snapshot metadata / timelineのauthoritative historical observation source / Protocol domain payloadを実装し、Gateway Phase 5からread-only配信できるようにする
- ⬜ **P35-011** — live Simulationを停止・巻き戻しせずHistorical Viewへ提供できるread-only projectionを実装する
- ⬜ **P35-012** — retention / snapshot interval / event compactionを設定可能にし、保持対象期間の再構築可能性を損なわないpolicyを実装する
- ⬜ **P35-013** — Historical stateをSave Dataへ統合し、load後もtimelineを継続できるようにする
- ⬜ **P35-014** — 100年以上のSettlement / Building / Network変化を指定時点へ再構築するdeterministic Replay E2Eを追加する
- ⬜ **P35-015** — history storage size / snapshot creation / reconstruction time benchmarkを記録する
- ⬜ **P35-016** — Historical World & Replayのspecification / architecture / ADR / ROADMAPを同期する

> 旧`P35-010`と`P35-015`のtimeline rendering benchmark部分はView Roadmap Phase 9へ移管した。

### Phase 35 完了条件

- 「この場所・建物・Settlementが昔どうだったか」をstable IDと時間から追跡できる。
- 指定時点のWorldをdeterministicに再構築し、authoritative historical projectionとしてGatewayへ提供できる。
- Historical projectionの参照・Gateway配信がlive Simulationのauthoritative stateへ影響しない。

---

## Phase 36 — World & City Management Commands

> **状態: ⬜ 未着手**  
> **依存:** Phase 20 / 30 / 31 / 35  
> World・地域・都市・Serverを明示的に編集・管理するためのserver-authoritative command / validation / authorization境界を整える。Phase 35を必須依存にするのは、Management mutationによるBuilding / Settlement / Network変更もHistorical Event / Replayの正本契約へ記録し、履歴を迂回する第二のmutation経路を作らないためである。Browserのread-only Selection / InspectorはView Roadmap、mutation / administration UIはManagement Roadmapで扱う。

- ⬜ **P36-001** — Build / Edit commandの認可・validation・ack / error契約を仕様化する
- ⬜ **P36-002** — Protocolへserver-authoritative command request / resultの共通枠組みを追加する
- ⬜ **P36-005** — Road / Laneのbuild / edit / remove commandをserver-authoritative境界として実装する
- ⬜ **P36-006** — Building / POI / Parcel / Zoneのbuild / edit commandをserver-authoritative境界として実装する
- ⬜ **P36-007** — Railway track / station / platformのbuild / edit commandをserver-authoritative境界として実装する
- ⬜ **P36-008** — Power Infrastructureのbuild / edit commandをserver-authoritative境界として実装する
- ⬜ **P36-009** — Water / Sewer Infrastructureのbuild / edit commandをserver-authoritative境界として実装する
- ⬜ **P36-010** — Gas Infrastructureのbuild / edit commandをserver-authoritative境界として実装する
- ⬜ **P36-011** — Optical Communication Infrastructureのbuild / edit commandをserver-authoritative境界として実装する
- ⬜ **P36-012** — Radio Site / Antenna / Spectrum設定のbuild / edit commandをserver-authoritative境界として実装する
- ⬜ **P36-013** — Geographic Feature名・Settlement / 地区 / 道路名・Road Signのserver-authoritative edit / override境界を実装する
- ⬜ **P36-015** — Simulation speed / pause / resume / explicit step等の運転controlをServer commandとして実装する
- ⬜ **P36-017** — Server configurationの変更可能項目・restart必要項目を区別するmetadataとserver-authoritative変更境界を実装する
- ⬜ **P36-018** — current Save formatのsave / load操作をServer commandとして実装する
- ⬜ **P36-019** — destructive commandのconfirmation metadataとstable error code / structured parameter契約を実装する
- ⬜ **P36-022** — World & City Management Commandsのspecification / architecture / command contract / ROADMAPを同期する

> 旧`P36-003` / `P36-004`のSelection / InspectorはView Roadmap Phase 7へ移管した。旧`P36-005`〜`P36-019`に含まれていたmutation / administration UI部分と`P36-014` / `P36-020` / `P36-021`のManagement側作業はManagement Roadmapへ移管した。旧`P36-016`のDashboard / statistics分析系は将来Analytics系へ分離し、本Phase完了条件には含めない。

### Phase 36 完了条件

- build / edit / remove / naming / signage / runtime control / configuration / Save操作のserver-authoritative command境界が存在する。
- build / edit操作は必ずServer-authoritative commandを経由し、Clientが正本状態を直接変更できない。
- 自動生成された名称・標識を由来情報を保持したまま明示的にoverrideできるcommand境界を持つ。
- Management Clientがstable command result / error / permission / confirmation metadataを利用できる。
- World mutationがPhase 35のHistorical Event / Replay契約を迂回しない。
- read-only View / Gatewayへmutation command責務を持ち込まない。

---

## Phase 37 — Distribution & Compatibility

> **状態: ⬜ 未着手**  
> **依存:** Phase 36  
> Save migrationと配布物を整備し、開発環境外でもversion付き成果物として起動・更新・復元できる状態にする。artifact packaging等の一部Taskは安定した既存境界だけで先行できるが、Phase closeoutはManagement commandを含む対象Client / Server境界が揃った後とする。

### Save互換性

- ⬜ **P37-001** — Save migrationのsupport範囲・失敗契約・version policyを仕様化する
- ⬜ **P37-002** — Save formatごとのmigration stepを登録できるframeworkを実装する
- ⬜ **P37-003** — repositoryに旧Save format fixtureを保持し、自動migration testを追加する
- ⬜ **P37-004** — migration中断・unsupported version・破損dataを安全に拒否する
- ⬜ **P37-005** — migration前後でstable IDと継続可能stateを保持するintegration testを追加する

### 配布・Deployment

- ⬜ **P37-006** — Server standalone binaryのsupported OS / architecture matrixを定義する
- ⬜ **P37-007** — Windows / Linux向けServer publish artifactをCIで生成する
- ⬜ **P37-008** — 必要性を検証した上で追加architecture / OS向けartifactを生成する
- ⬜ **P37-009** — Web Client production buildのbase path / Server endpoint設定をdeployment向けに整理する
- ⬜ **P37-010** — static hosting向けWeb Client artifactをCIで生成する
- ⬜ **P37-011** — Server用container imageとruntime configuration契約を実装する
- ⬜ **P37-012** — release artifactへVERSION・commit SHA・license / third-party noticeを同梱する
- ⬜ **P37-013** — release artifactのchecksum / SBOM等、配布時に必要なintegrity metadataを生成する
- ⬜ **P37-014** — package / binary / Web / containerを起動するrelease smoke testをCIへ追加する
- ⬜ **P37-015** — install / upgrade / rollback / backup / restore手順をdocument化する
- ⬜ **P37-016** — develop→main release時のversion / artifact / release note手順を自動化可能な形へ整理する
- ⬜ **P37-017** — Distribution / Compatibilityのarchitecture / development docs / ROADMAPを同期する

### Phase 37 完了条件

- 開発toolchainを手作業構築しなくても、配布artifactからServerとWeb Clientを起動できる。
- 対応対象の旧Save Dataを明示的なmigration経路で読み込める。
- release artifactのversion・commit・license・integrity情報を追跡できる。

---

## Phase 38 — Extension Platform

> **状態: ⬜ 未着手**  
> **依存:** Phase 37  
> 正本Simulationと互換性境界を壊さず、外部拡張・高精度solverを導入できる公開拡張基盤を作る。package / distribution / compatibility policyをPhase 37へ依存し、read-only View extensionはView Roadmap Phase 12、Addon管理UIはManagement Roadmap Phase 5、read-only View localizationはView Roadmap Phase 10で管理する。

### Extension Platform

- ⬜ **P38-001** — Extension / Modで公開する範囲と非公開内部APIの境界を仕様化する
- ⬜ **P38-002** — Extension manifest・stable ID・version・dependency契約を定義する
- ⬜ **P38-003** — data-only extensionとcode extensionを分離したloading modelを設計する
- ⬜ **P38-004** — code extensionの信頼境界・権限・非sandbox性を明示し、安全なdefault policyを実装する
- ⬜ **P38-005** — Simulationへextension contentとPower / Water / Sewer / Gas / Optical / Radio / Terrain / Regional evolution等のsolver / rule providerを登録するversioned public APIを実装する
- ⬜ **P38-006** — Extension固有Save Dataをnamespace付きで保存し、missing extension時の挙動を定義する
- ⬜ **P38-007** — Protocolへextension固有wire typeを直接衝突させない拡張契約を設計する
- ⬜ **P38-008** — Extensionのload order / dependency cycle / incompatible versionをvalidationする
- ⬜ **P38-009** — Extension packageの開発・test用templateとsample extensionを追加する

### Closeout

- ⬜ **P38-016** — Extension有無・solver / rule差し替え有無でSave / Protocol / Simulation determinismが壊れないintegration testを追加する
- ⬜ **P38-017** — Extension loading・solver / rule providerのstartup / memory costをbenchmarkする
- ⬜ **P38-018** — Extension author guide / solver provider guide / compatibility policyを整備する
- ⬜ **P38-019** — architecture / ADR / ROADMAPを同期し、Phase 10〜38で計画した旧Backlogのcloseoutを確認する

> 旧Localization `P38-010`〜`P38-015`と`P38-017` / `P38-018`のlocalization部分はView Roadmap Phase 10へ移管した。

### Phase 38 完了条件

- 既存Simulation内部実装へ直接依存せず、versionedな公開境界からExtensionを追加できる。
- 標準の軽量Infrastructure / Terrain solverを維持したまま、Extensionが高精度な物理solverや追加Regional ruleを安全に差し替えられる。
- Extension固有stateがSave Dataと衝突せず、missing / incompatible extensionを安全に扱える。
- View Addon / Management AddonがSimulation内部APIではなく同じExtension Platform contractへ依存できる。

---

## 継続Backlog

- Physics Foundation — 重力、落下、ジャンプ、垂直速度・加速度、物理state Save / domain payload / E2E
- Airborne Movement — 飛行Entity、空中経路、高度ルール、3D空間交通
- Advanced Terrain Modification — cut / fill、grading、reclamation、quarry、dam
- Advanced Natural Dynamics — erosion、landslide、real-time river flow、flood simulation
- Advanced Cave Generation — cave network、natural tunnel、arch、underground water
- Natural Environment Simulation — vegetation、biome、habitat、wildlife、ecosystem
- Analytics Platform — 長期統計、trend、heatmap、analysis storage / query / client

## 新規Backlogの扱い

1. 既存Simulation Phaseの完了に必須なら、そのPhaseへ独立Taskとして追加する。
2. Observation Request / subscription / cache / dedup / delivery / reconnectなら `GATEWAY_ROADMAP.md` へ移す。
3. 純read-only Viewなら `VIEW_ROADMAP.md` へ移す。
4. Management UI / command clientなら `MANAGEMENT_ROADMAP.md` へ移す。
5. 分析・統計・trend等はAnalytics系Backlogとして分離する。
6. Simulation側で完了に必須でない大テーマは本Roadmap末尾へBacklogとして記録する。
7. 着手時にWhat / Whyを`docs/specifications/`、Howを`docs/architecture/`またはADRへ切り分ける。
8. 実装・保存・source contract・配送・検証のどこまでを各RoadmapのPhase完了条件とするか明示する。
9. Phase完了時に残件が暗黙に持ち越されていないことを確認する。