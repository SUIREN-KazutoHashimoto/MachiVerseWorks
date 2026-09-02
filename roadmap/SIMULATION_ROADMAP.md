# Simulation Roadmap

Simulation側の実装順序と完了状態の正本です。read-only Viewは `VIEW_ROADMAP.md`、World / City / Server操作UIは `MANAGEMENT_ROADMAP.md` を正本とします。

> 現在: Phase 29 — World & Physical Environment Generation は実装完了、develop統合待ち  
> 次の実装タスク: Phase 30 `P30-001` — Settlement / SettlementOrigin / RegionalRole / historical growth eventの正本契約  
> Application version: `0.45.0`  
> Protocol: `2.17`  
> Save format: `11`

## 進行ルール

- Simulationのauthoritative state / rule / Save / Protocol / Server配信は本Roadmapで管理する。
- Viewはread-only consumerとし、Simulation内部実装やCamera位置をauthoritative stateの生成条件へ使わない。
- Management mutationは専用command境界を経由し、Viewへmutation責務を持ち込まない。
- 完了済みPhaseの詳細は `docs/archive/` へ退避し、現行Roadmapは次の判断点を読みやすく保つ。
- 同一seed / config / inputからのdeterminism、stable ID、参照整合性、bounded inputを継続的な共通条件とする。

## 現在のPhase一覧

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0〜24 | Foundation / Mobility / Population / Economy / Utilities | ✅ 完了・履歴化済み |
| 25 | Gas Infrastructure | ✅ 完了 |
| 26 | Optical Communication | ✅ 完了 |
| 27 | Remote MCP Administration | ✅ 完了 |
| 28 | Radio & Spectrum Foundation | ✅ 完了 |
| 29 | World & Physical Environment Generation | ✅ 実装完了・develop統合待ち |
| 30 | Regional & Urban Generation | ⬜ 次 |
| 31 | Persistent Regional & Settlement Evolution | ⬜ 未着手 |
| 32 | Simulation Scheduling & Workload Optimization | ⬜ 未着手 |
| 33 | Deterministic Parallel Simulation | ⬜ 未着手 |
| 35 | Historical World & Replay | ⬜ 未着手 |
| 36 | World & City Management Commands | ⬜ 未着手 |
| 37 | Distribution & Compatibility | ⬜ 未着手 |
| 38 | Extension Platform | ⬜ 未着手 |

> Phase 29着手前までの完全な計画履歴は `docs/archive/roadmap-through-phase24-closeout.txt`、Phase 29 closeout直前の全Roadmap snapshotは `docs/archive/roadmap-through-phase29-plan-snapshot.txt` に保存する。

---

## Observation Gateway — 横断基盤

Simulationとread-only Viewの境界はPhase進行と並行して整備する。Observationはauthoritative stateを変更してはならない。

- ⬜ **OBG-001** — `WorldSnapshotCoordinator`を導入し、複数domainのsnapshot capture pointを統一する
- ⬜ **OBG-002** — Simulation lock内ではimmutableなWorldSnapshot/read modelのcaptureだけを行い、serialization / client別filter / cache更新をlock外へ出す
- ⬜ **OBG-003** — Observation DTOがpassive / immutableで、View要求やCamera状態からSimulation stateを変更しないことを監査する
- ⬜ **OBG-004** — Server spatial filtering / cache / invalidationをdomain横断で一貫させる

### 並行開発ルール

- Protocol / Save / Observationの局所最適化は、authoritative modelを変更しない範囲で並行可能。
- `SimulationWorld` / `SimulationRuntime` / publisher群へ広範囲変更を入れる作業同士は同時進行を避ける。
- Viewは別workspaceとしてSimulationの完成済みObservation contractを追随する。
- Management commandはread-only Observationと別境界として実装する。

---

## Phase 29 — World & Physical Environment Generation

> **状態: ✅ 実装完了 / develop統合待ち**  
> **依存:** Phase 0〜28  
> Global EnvironmentとDetailed 3D Terrainを分離しつつ、両方をSimulation authoritative boundaryから決定する。View / Cameraは生成正本にしない。

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
- ✅ **P29-025** — World / Terrain / GeographicFeature / ToponymをObservation / Protocol 2.17 / Serverへ配信する
- ✅ **P29-027** — 同一seedから同じglobal environment / detailed terrain / feature / toponymを得るServer再起動reproducibility E2Eを追加する
- ✅ **P29-028** — global field / detailed terrainのgeneration・query・memory benchmarkを追加する
- ✅ **P29-029** — World / Terrain / Geographic Featureのspecification / architecture / ADR / ROADMAPを同期する

> 旧`P29-026`のWeb Client 3D描画はView Roadmap Phase 3 `V3-001`へ移管済み。`P29-025`がObservation contractを提供するため、View側は独立して追随できる。

### Phase 29 完了条件

- ✅ 世界全体の環境fieldと任意Region / Partitionの高解像度Terrainを同一seed・設定から再現できる。
- ✅ Global fieldを詳細Simulationの代替正本にせず、Camera位置に依存しない。
- ✅ Terrainがsurface queryに加え、Water / solid / Void / cavityを扱う3D volume境界を持つ。
- ✅ Road / Railway / Building等がterrain height / slope / 3D volume constraintを共通APIから参照できる。
- ✅ 河川・山・谷・盆地・峠・湾等がstableな`GeographicFeature`となり、自然地名とprovenanceをSave / Protocol境界で扱える。
- ✅ Save inputはfeature / geometry / toponymのbounded pre-scanを持ち、Protocolは未知discriminant / 壊れた参照を拒否する。
- ✅ 同一subscription volumeのenvironment snapshot生成をpublish cycle内で重複させない。
- ✅ 植生・動物・生態系、advanced erosion / flood、cut/fill等はPhase 29完了条件から分離している。

### Phase 29 Closeout evidence

- Specification: `docs/specifications/world-environment-terrain.md`
- Architecture: `docs/architecture/world-environment-terrain.md`
- ADR: `docs/decisions/ADR-0008-authoritative-two-level-world-environment-terrain.md`
- Protocol: 2.17 / `WorldEnvironmentSnapshot` message 800
- Save: format 11 optional extension、旧Save後方互換
- Tests: Simulation / Persistence / Protocol regression、observation Save-byte invariance、3D terrain invariants
- E2E: Server restart reproducibility
- Benchmark: dedicated World Environment benchmark workflow
- Integration: PR #183 のdevelop merge完了時に「develop統合済み」とする

---

## Phase 30 — Regional & Urban Generation

> **状態: ⬜ 未着手**  
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

- ⬜ **P30-001** — Settlement / SettlementOrigin / RegionalRole / historical growth eventの正本契約を仕様化する
- ⬜ **P30-002** — flatness・water access・transport potential・buildability・resource access・flood risk・steep slope・isolation・construction cost等からSettlement Suitabilityを評価する
- ⬜ **P30-003** — Phase 29のcandidate regionからweighted deterministic selectionで複数のSettlement originを決定する
- ⬜ **P30-004** — river plain / estuary / bay / basin / valley / mountain pass / resource access等からSettlementOrigin / RegionalRole / InitialEconomyの基礎傾向を派生する
- ⬜ **P30-005** — City / Town / Village / Hamletを固定テンプレートとして直接配置せず、複数の初期Settlementと人口・機能・周辺関係を生成する
- ⬜ **P30-006** — 地形・河川・峠・海岸・Settlement間需要を考慮してprimary road / regional / intercity corridorを生成する
- ⬜ **P30-007** — Railway等の大規模transport corridorを需要・地形・Settlement成長履歴から形成できるgeneration境界を実装する
- ⬜ **P30-008** — 各Settlementについてpopulation / economy growthに応じたcenter formation・urban expansion・suburbanizationを段階生成する
- ⬜ **P30-009** — congestion / accessibility / land pressure等に応じたredevelopment・new center formation・複数中心化の履歴ruleを実装する
- ⬜ **P30-010** — 自然地名をSettlement / City / District等の人間側名称へ継承・変形するNaming provenance ruleを実装する
- ⬜ **P30-011** — Settlementごとの生成履歴とSettlement間関係をevent / generation stageとして保存する

### 30.2 Detailed Urban Fabric & Signage

- ⬜ **P30-012** — Parcel境界・Zone種別・土地利用・占有/development stateの正本契約を仕様化する
- ⬜ **P30-013** — Historical Road Networkからterrain-awareな詳細Road / Lane networkを生成する
- ⬜ **P30-014** — Road NetworkからBlock / Parcelをdeterministicに生成するsubdivisionを実装する
- ⬜ **P30-015** — Road access・parcel size・slope・flood risk・land value・land use等からdevelopment suitabilityを評価する
- ⬜ **P30-016** — Zone / Land Useに応じたBuilding用途・規模・density・height候補を生成する
- ⬜ **P30-017** — 初期生成履歴として空ParcelへのBuilding / POI developmentを段階生成する
- ⬜ **P30-018** — demand変化に応じたredevelopment / vacancyの最小ruleを実装する
- ⬜ **P30-019** — station district / CBD / industrial area / suburb / old town等を都市履歴とaccessibilityから形成する
- ⬜ **P30-020** — 初期Population / Household / Jobを複数Settlementへ配置するseeding処理を実装する
- ⬜ **P30-021** — Railway / Power / Water / Sewer / Gas / Optical / Radio等を壊さず地形とSettlement networkへ適応するgeneration constraintを定義する
- ⬜ **P30-022** — 自然地名・Settlement履歴・District hierarchyからRoad / Bridge / Tunnel / Station / District等の名称をdeterministicに生成する
- ⬜ **P30-023** — Road geometry・hierarchy・destination・Geographic Featureを解析するRoad Context Analysisを実装する
- ⬜ **P30-024** — steep grade / sharp curve / rock slope / floodplain / river crossing / mountain pass / tunnel / coastal lowland等から必要な標識を決定する
- ⬜ **P30-025** — destination name・distance・direction・route contextを使う案内標識と地名標識をdeterministicに生成する
- ⬜ **P30-026** — Road Signをstable ID付き都市Entityとして配置し、Road Segment / Lane / GeographicFeature / named destinationへの参照を保持する
- ⬜ **P30-027** — Parcel / Zone / generation history / human toponym / Road Signをcheckpoint / Save Dataへ統合する
- ⬜ **P30-028** — Settlement network / Parcel / Zone / development / urban naming / Road SignをObservation Gateway / Protocol / Serverへ配信する

> Web Client 3D可視化はView Roadmap Phase 4 `V4-001`へ分離する。

### Generation Quality / Validation

- ⬜ **P30-029** — `RegionalQualityReport`を実装し、TerrainAdaptation / RoadConnectivity / AverageSlopeCost / Accessibility / CongestionRisk / LandUseConsistency / FloodExposure / UrbanCompactness / PolycentricBalance等を独立評価する
- ⬜ **P30-030** — 弱いquality dimensionに応じて道路・土地利用・Settlement中心配置等を改善するGenerate → Evaluate → Improve loopを実装する
- ⬜ **P30-031** — 同一seed・設定で同一Settlement network・都市形状・名称・標識・quality reportを生成するreproducibility E2Eを追加する
- ⬜ **P30-032** — river / port / basin / valley / mountain / cold / dry inland / island region等のdeterministic fixtureを追加する
- ⬜ **P30-033** — Draft / Standard / High Quality等のgeneration quality presetとiteration budgetを定義する
- ⬜ **P30-034** — 小/中/大規模Settlement networkのgeneration時間・memory・quality metrics・初期Simulation負荷benchmarkを記録する
- ⬜ **P30-035** — World→Terrain→Settlement Network→Historical Growth→Urban Fabric→Validationのspecification / architecture / ADR / ROADMAPを同期する

### Phase 30 完了条件

- Settlement立地が自然環境とregional contextから説明可能で、単一の完成都市を単純noiseから直接生成しない。
- 都市・町・村・集落が複数存在し、regional networkで関係しながら異なる規模・役割・成長履歴を持てる。
- Road / Parcel / Land Use / Building / POIが地形・水系・歴史的成長へ適応している。
- 自然地名からSettlement名・地区名・道路名・橋・トンネル・駅名等へ由来を追跡できる。
- 道路標識を地形・道路形状・destination・named Geographic Featureから導出する。
- 生成品質を独立評価し、同じseed・quality presetから同じpolycentricな地域を再現できる。

---

## Phase 31 — Persistent Regional & Settlement Evolution

> **状態: ⬜ 未着手** / **依存:** Phase 30  
> 初期Worldを完成品として固定せず、Population / Economy / Parcel / Building / Transport / Settlement関係を時間経過で継続変化させる。

- ⬜ Settlement分類・territory・influenceを実World stateから再評価する
- ⬜ 転居・雇用・service catchment・物流・development demandを既存domainへ接続する
- ⬜ Building建設・aging・renovation・用途変更・vacancy・demolitionを履歴付きで扱う
- ⬜ Settlementの成立・成長・縮小・廃村・Metro / Urban Region関係を動的に扱う
- ⬜ Persistent Regional stateをSave / Observation / Protocolへ統合する
- ⬜ 100年以上のlong-run deterministic E2EとWorld-scale benchmarkを追加する

## Phase 32 — Simulation Scheduling & Workload Optimization

> **状態: ⬜ 未着手** / **依存:** Phase 31  
> Fidelityを落とさず、event scheduling / dirty update / spatial invalidationで結果に影響しない仕事を除去する。Camera依存Simulation LODは禁止する。

- ⬜ deterministic World event scheduler / next-event scheduling
- ⬜ time-derived state / dependency dirty update / spatial invalidation
- ⬜ deterministic batch execution / dormancy / wake-up
- ⬜ workload metrics、観測有無invariance、最適化前後equivalence test
- ⬜ large World benchmarkとdocs同期

## Phase 33 — Deterministic Parallel Simulation

> **状態: ⬜ 未着手** / **依存:** Phase 32  
> worker数・partition配置・実行順の違いをauthoritative World結果へ漏らさず並列化する。

- ⬜ partition ownership / boundary queue / deterministic RNG stream
- ⬜ deterministic worker scheduling / reduction
- ⬜ domain workloadの段階的parallel化
- ⬜ 1/2/4/8/16 workerおよび異なるpartition分割でstate digest一致E2E
- ⬜ scaling / sync / locality benchmarkとdocs同期

## Phase 35 — Historical World & Replay

> **状態: ⬜ 未着手** / **依存:** Phase 31〜33  
> Settlement / Building / Network等の変化を時間軸で追跡し、指定時点のread-only World projectionを再構築する。

- ⬜ stable Historical Event / periodic snapshot / replay contract
- ⬜ Entity lifetime / Building / Settlement / Network履歴query
- ⬜ Observation / Protocol / Save統合
- ⬜ live Simulationを巻き戻さないread-only historical projection
- ⬜ 100年以上のreplay E2E、storage / reconstruction benchmark、docs同期

## Phase 36 — World & City Management Commands

> **状態: ⬜ 未着手** / **依存:** Phase 20 / 30 / 31 / 35  
> World / City / Serverを編集するserver-authoritative command境界を整備する。UIはManagement Roadmapで扱う。

- ⬜ common command authorization / validation / ack / structured error
- ⬜ Road / Building / Parcel / Zone / Railway / Utility / Radio / namingのbuild / edit / remove
- ⬜ Simulation runtime control / configuration / Save load-save
- ⬜ destructive confirmation metadata
- ⬜ Historical Event契約を迂回しないmutation boundaryとdocs同期

## Phase 37 — Distribution & Compatibility

> **状態: ⬜ 未着手** / **依存:** Phase 36  
> Save migrationと配布物を整備し、開発環境外でもversion付き成果物として起動・更新・復元できるようにする。

- ⬜ Save migration framework / old fixture / failure contract
- ⬜ Windows / Linux Server publish artifact、Web production artifact、container image
- ⬜ VERSION / commit SHA / license / notices / checksum / SBOM
- ⬜ release smoke、install / upgrade / rollback / backup / restore、release automation

## Phase 38 — Extension Platform

> **状態: ⬜ 未着手** / **依存:** Phase 37  
> Simulation正本と互換性境界を壊さず、外部拡張や高精度solverを導入できるversioned public extension基盤を作る。

- ⬜ Extension manifest / stable ID / version / dependency / loading model
- ⬜ trust / permission policy、data-only / code extension分離
- ⬜ Simulation solver / rule provider public API
- ⬜ namespaced extension Save、wire拡張契約、dependency validation
- ⬜ template / sample extension、determinism integration test、benchmark、author guide、docs closeout

---

## 継続Backlog

- Physics Foundation — 重力、落下、ジャンプ、垂直速度・加速度、物理state Save / Protocol / E2E
- Airborne Movement — 飛行Entity、空中経路、高度ルール、3D空間交通
- Advanced Terrain Modification — cut / fill、grading、reclamation、quarry、dam
- Advanced Natural Dynamics — erosion、landslide、real-time river flow、flood simulation
- Advanced Cave Generation — cave network、natural tunnel、arch、underground water
- Natural Environment Simulation — vegetation、biome、habitat、wildlife、ecosystem
- Analytics Platform — 長期統計、trend、heatmap、analysis storage / query / client

## 新規Backlogの扱い

1. 既存Phaseの完了に必須なら、そのPhaseへ独立Taskとして追加する。
2. 純read-only Viewなら `VIEW_ROADMAP.md` へ移す。
3. Management UI / command clientなら `MANAGEMENT_ROADMAP.md` へ移す。
4. 分析・統計・trend等はAnalytics系Backlogとして分離する。
5. Simulation側で完了に必須でない大テーマは本Roadmap末尾へBacklogとして記録する。
6. 着手時にWhat / Whyを`docs/specifications/`、Howを`docs/architecture/`またはADRへ切り分ける。
7. 実装・保存・配信・検証のどこまでをPhase完了条件とするか明示する。
8. Phase完了時に残件が暗黙に持ち越されていないことを確認する。
