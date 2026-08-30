# MachiVerseWorks Roadmap

MachiVerseWorks の作業を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** Phase 18 — Railway Operations（次）  
> **次の実装タスク:** P18-001 — Train / Formation / Service / Timetableの責務とstable ID契約を仕様化する

## 全体の現在地

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0 | リポジトリ初期セットアップ | ✅ 完了 |
| 1 | 開発プロジェクト骨格 | ✅ 完了 |
| 2 | Simulation Core 最小 PoC | ✅ 完了 |
| 3 | Protocol 最小実装 | ✅ 完了 |
| 4 | Headless Server 最小実装 | ✅ 完了 |
| 5 | Web Client 最小実装 | ✅ 完了 |
| 6 | End-to-End PoC | ✅ 完了 |
| 7 | 性能基盤の拡張 | ✅ 完了 |
| 8 | 保存・復元基盤 | ✅ 完了 |
| 9 | 3D Simulation Foundation | ✅ 完了 |
| 10 | Urban World Foundation | ✅ 完了 |
| 11 | Road Network Foundation | ✅ 完了 |
| 12 | Routing Foundation | ✅ 完了 |
| 13 | Road Traffic Simulation | ✅ 完了 |
| 14 | Intersection & Signal Control | ✅ 完了 |
| 15 | Population & Daily Activity | ✅ 完了 |
| 16 | Pedestrian Simulation | ✅ 完了 |
| 17 | Railway Infrastructure | ✅ 完了 |
| 18 | Railway Operations | ⏭️ 次 |
| 19 | Multimodal Transit | ⏳ 待機 |
| 20 | Industry / Jobs / Economy | ⏳ 待機 |
| 21 | Logistics / Freight | ⏳ 待機 |
| 22 | Power Infrastructure | ⏳ 待機 |
| 23 | Urban Growth & City Generation | ⏳ 待機 |
| 24 | City Management UI | ⏳ 待機 |
| 25 | Distribution & Compatibility | ⏳ 待機 |
| 26 | Extension Platform & Localization | ⏳ 待機 |

Phase 0〜8の詳細TaskとPhase 9着手時点の計画状態は、履歴として [`docs/archive/roadmap-through-phase9-plan.md`](docs/archive/roadmap-through-phase9-plan.md) に保存しています。Phase 13〜16の正式closeout時点のTask状態と検証証跡は [`docs/archive/roadmap-phase13-through-phase16-closeout.md`](docs/archive/roadmap-phase13-through-phase16-closeout.md) に保存しています。

## ROADMAP 運用ルール

- 状態記号を付けるのは、単独で完了判定できる作業だけとする。
- 1タスクは原則として「1つの観測可能な成果」を持つ。
- 1タスク内に独立した成果が複数ある場合は分割する。
- E2E、benchmark、docs同期のように独立して完了可能な成果は、それぞれ別Taskとする。
- コード変更では、必要な build / test / benchmark / 実機確認まで含めて完了とする。
- 仕様や設計を変更した場合は、対応する docs / ADR の更新まで含めて完了とする。
- Protocol version / Save format version は application `VERSION` と独立して、互換性が変わるときだけ更新する。
- 「ほぼ完了」「一部完了」は ✅ にしない。残作業を別Taskへ明示的に切り出した場合のみ元Taskを完了にできる。
- 作業中に新しい依存関係が見つかった場合は、後続PhaseのTaskを更新してから実装を進める。
- Phaseから外した計画済み項目は暗黙に削除せず、対応Phaseまたは継続Backlogへ必ず移す。
- 完了済みPhaseの詳細は必要に応じて `docs/archive/` へ移し、現行ROADMAPを次の判断に使いやすく保つ。
- **Task実装状態・`develop`統合状態・Phase正式closeoutは別の状態として扱う。** 後続Phaseの実装を依存Phase完了前に先行mergeする場合、安定した既存境界だけに依存し、未完了依存を完了扱いにせず、ROADMAPへ「develop統合済み / closeout待ち」と理由を記録する。
- 先行mergeは依存順を無効化しない。依存Phaseが正式完了するまで、後続Phase全体を✅へせず、依存部分のTaskを明示的に未完了で残す。

## Phase 10以降の依存順

```text
3D Simulation Foundation
  -> Urban World
  -> Road Network
  -> Routing
  -> Road Traffic
  -> Intersection / Signal
  -> Population / Daily Activity
  -> Pedestrian
  -> Railway Infrastructure
  -> Railway Operations
  -> Multimodal Transit
  -> Industry / Jobs / Economy
  -> Logistics / Freight
  -> Power Infrastructure
  -> Urban Growth / City Generation
  -> City Management UI
  -> Distribution / Compatibility
  -> Extension Platform / Localization
```

この順番は、後続機能が前段の正本モデルを再利用できることを優先する。先行mergeを行っても、Phaseの正式closeout順は依存関係に従う。

---

## Phase 9〜17 — 完了済みFoundation / Simulation Domains

Phase 9〜17は正式closeout済み。現行ROADMAPでは完了履歴の詳細Taskを繰り返さず、実装・仕様・benchmarkの正本へ参照を集約する。

| Phase | 主な正本 / 証跡 |
| --- | --- |
| Phase 9 — 3D Simulation Foundation | `docs/specifications/world-coordinate-system.md`、3D対応Protocol / Save / E2E / benchmarkのGit履歴 |
| Phase 10 — Urban World Foundation | [`docs/specifications/building-poi.md`](docs/specifications/building-poi.md) |
| Phase 11 — Road Network Foundation | [`docs/specifications/road-network.md`](docs/specifications/road-network.md)、[`docs/development/road-network-benchmark.md`](docs/development/road-network-benchmark.md)、PR #47 |
| Phase 12 — Routing Foundation | [`docs/specifications/routing.md`](docs/specifications/routing.md)、[`docs/development/routing-benchmark.md`](docs/development/routing-benchmark.md) |
| Phase 13 — Road Traffic Simulation | [`docs/specifications/road-traffic.md`](docs/specifications/road-traffic.md)、[`docs/architecture/road-traffic.md`](docs/architecture/road-traffic.md)、[`docs/development/road-traffic-benchmark.md`](docs/development/road-traffic-benchmark.md) |
| Phase 14 — Intersection & Signal Control | [`docs/specifications/intersection-signal-control.md`](docs/specifications/intersection-signal-control.md)、[`docs/development/phase14-intersection-benchmark.md`](docs/development/phase14-intersection-benchmark.md) |
| Phase 15 — Population & Daily Activity | [`docs/specifications/population-daily-activity.md`](docs/specifications/population-daily-activity.md)、[`docs/architecture/population-daily-activity.md`](docs/architecture/population-daily-activity.md)、[`docs/development/population-benchmark.md`](docs/development/population-benchmark.md) |
| Phase 16 — Pedestrian Simulation | [`docs/specifications/pedestrian-simulation.md`](docs/specifications/pedestrian-simulation.md)、[`docs/architecture/pedestrian-simulation.md`](docs/architecture/pedestrian-simulation.md)、[`docs/development/pedestrian-benchmark.md`](docs/development/pedestrian-benchmark.md) |
| Phase 17 — Railway Infrastructure | [`docs/specifications/railway-infrastructure.md`](docs/specifications/railway-infrastructure.md)、[`docs/architecture/railway-infrastructure.md`](docs/architecture/railway-infrastructure.md)、Phase 17 E2E / Railway benchmark workflow、PR #78 |

### Phase 13〜16 closeout

Phase 13の未完了だったP13-001 / P13-015 / P13-016 / P13-017を完了し、依存順にPhase 14→15→16の正式closeout待ちを解消した。詳細Taskと検証runは [`docs/archive/roadmap-phase13-through-phase16-closeout.md`](docs/archive/roadmap-phase13-through-phase16-closeout.md) を正本とする。

Phase 13 benchmarkでは1,000 / 10,000 / 100,000 Vehicleを同一runnerで計測した。100,000 Vehicleのfull tick平均は155.8082msで30Hz realtime budgetを超えるため、性能保証とはせず、今後の最適化・回帰検知baselineとして明示的に保持する。

Phase 10から後続へ委譲した計画済み項目は現在も次のPhaseを正本とする。

| 項目 | 正本となるPhase / 境界 |
| --- | --- |
| Road上のBuilding / POI access | Phase 11 `RoadAccessPoint` |
| 徒歩networkへのBuilding / POI access | Phase 16 |
| Parcel / zoning / land use / development | Phase 23 |
| Building / Parcel / POIのInspector・編集UI | Phase 24 |
| 建物mesh / floor / room / entrance | 必要になるdomain Phaseで契約追加。Phase 10では`WorldVolume`のみを正本とする |
| PopulationによるPOI選択 | Phase 15 |

---

## Phase 17 — Railway Infrastructure

> **状態: ✅ 完了**  
> **依存:** Phase 10 / 11 / 16  
> 線路・分岐・block・駅・ホーム・車庫を、列車運行が利用できる3D railway topologyとして確立する。

- ✅ **P17-001** — Railway Infrastructureの軸・接続・track gauge・方向・高度契約を仕様化する
- ✅ **P17-002** — TrackNode / TrackSegmentのstable IDと3D geometryを実装する
- ✅ **P17-003** — Track direction / speed limit / electrification等の最小属性を実装する
- ✅ **P17-004** — switch / junctionと進行可能connectionを表すtopologyを実装する
- ✅ **P17-005** — train separationに使うblock sectionのInfrastructureモデルを実装する
- ✅ **P17-006** — Station / Platformのstable ID・geometry・track connectionを実装する
- ✅ **P17-007** — Platform access pointをUrban World / pedestrian networkへ接続する
- ✅ **P17-008** — Depot / sidingの最小Infrastructureモデルを実装する
- ✅ **P17-009** — Railway topologyの3D spatial queryと接続validationを実装する
- ✅ **P17-010** — Railway Infrastructureをcheckpoint / Save Dataへ含める
- ✅ **P17-011** — Track / Station / PlatformのProtocol配信契約を追加する
- ✅ **P17-012** — Web ClientでTrack / Station / Platformを3D描画する
- ✅ **P17-013** — 高架・地下・複線・分岐・駅を含むdeterministic fixtureを追加する
- ✅ **P17-014** — Railway InfrastructureのSave→Server→Browser E2Eを追加する
- ✅ **P17-015** — 大規模Railway topologyのquery・validation benchmarkを記録する
- ✅ **P17-016** — Railway Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 17 完了条件

- Train operationが利用できる連続したtrack topology・block・station・platformが存在する。
- 道路同様、立体交差と接続を混同しない。
- Infrastructureを保存・配信・描画でき、pedestrian networkからPlatformへ到達できる。

---

## Phase 18 — Railway Operations

> **状態: ⬜ 未着手（次）**  
> **依存:** Phase 17  
> Train・service・timetable・station stop・block separationを実装し、再現可能な鉄道運行を成立させる。

- ⬜ **P18-001** — Train / Formation / Service / Timetableの責務とstable ID契約を仕様化する
- ⬜ **P18-002** — Train formationの長さ・性能・capacityを表す最小モデルを実装する
- ⬜ **P18-003** — Railway routeをTrack sequenceとして構築・検証する処理を実装する
- ⬜ **P18-004** — Serviceとstop sequence / planned arrival / departureを表すTimetableを実装する
- ⬜ **P18-005** — TrainをTrack geometryに沿って3D移動させる固定tick更新を実装する
- ⬜ **P18-006** — block occupancy / reservationを実装し、同一blockへの危険な進入を防ぐ
- ⬜ **P18-007** — station approach / stop position / dwell / departureを実装する
- ⬜ **P18-008** — platform assignmentとoccupied platformの競合処理を実装する
- ⬜ **P18-009** — timetableとの差からdelayを計算し、後続stopへ伝播する
- ⬜ **P18-010** — depotからの出庫・service開始・終端・入庫のlifecycleを実装する
- ⬜ **P18-011** — Train / Service / Timetable状態をcheckpoint / Save Dataへ含める
- ⬜ **P18-012** — Train位置・service・delay・platform stateをProtocol / Serverで配信する
- ⬜ **P18-013** — Web ClientでTrainを描画し、駅の発着情報をdebug表示する
- ⬜ **P18-014** — 複数列車・複数駅・遅延を含む1運行周期のdeterministic E2Eを追加する
- ⬜ **P18-015** — 大規模Train/Service数のtick・routing・block処理benchmarkを記録する
- ⬜ **P18-016** — Railway Operationsのspecification / architecture / ROADMAPを同期する

### Phase 18 完了条件

- TrainがTimetableに基づいて駅間を走行・停車し、block競合を起こさない。
- delay・platform・service stateを保存復元・配信できる。
- 同一seed / timetableで再現可能な運行結果を得られる。

---

## Phase 19 — Multimodal Transit

> **状態: ⬜ 未着手**  
> **依存:** Phase 14 / 16 / 18  
> 徒歩・自動車・Bus・Taxi・Railwayを共通Tripとして組み合わせ、公共交通を含む移動を成立させる。

- ⬜ **P19-001** — Transit Stop / Line / Service pattern / Trip legの共通契約を仕様化する
- ⬜ **P19-002** — Bus stopとRoad Laneの接続モデルを実装する
- ⬜ **P19-003** — Bus service / stop sequence / timetableの最小モデルを実装する
- ⬜ **P19-004** — Bus VehicleをRoad Trafficへ接続し、停留所停車・dwellを実装する
- ⬜ **P19-005** — Taxi Vehicle / request / pickup / drop-offの状態モデルを実装する
- ⬜ **P19-006** — Taxi requestをVehicleへ割り当てる最小dispatch policyを実装する
- ⬜ **P19-007** — 徒歩・Bus・Railwayを組み合わせるmultimodal journey graphを構築する
- ⬜ **P19-008** — transfer timeとaccess/egress walkingを含むjourney planningを実装する
- ⬜ **P19-009** — Population Trip Requestから利用可能modeを選ぶ最小mode-choice policyを実装する
- ⬜ **P19-010** — waiting / boarding / riding / transfer / alightingのPassenger state machineを実装する
- ⬜ **P19-011** — Multimodal transit stateをcheckpoint / Save Dataへ含める
- ⬜ **P19-012** — Transit line / realtime vehicle / arrival estimateをProtocol / Serverで配信する
- ⬜ **P19-013** — Web Clientでroute・stop・vehicle・arrival情報をdebug表示する
- ⬜ **P19-014** — 徒歩→鉄道→徒歩、Bus、Taxiを含むTripを実Server→Browserで検証するE2Eを追加する
- ⬜ **P19-015** — journey planning / transfer / dispatchのbenchmarkを記録する
- ⬜ **P19-016** — Multimodal Transitのspecification / architecture / ROADMAPを同期する

### Phase 19 完了条件

- Personが単一交通modeへ固定されず、徒歩・道路・公共交通を組み合わせて目的地へ移動できる。
- BusとTaxiが既存Road Trafficを再利用し、鉄道も共通Journeyへ統合される。
- transferを含むTripを保存復元して継続できる。

---

## Phase 20 — Industry / Jobs / Economy

> **状態: ⬜ 未着手**  
> **依存:** Phase 15 / 19  
> 企業・職場・雇用・所得・生産・消費の最小循環を作り、都市活動へ経済的な理由を与える。

- ⬜ **P20-001** — Company / Establishment / Job / Economic Actorの責務とstable IDを仕様化する
- ⬜ **P20-002** — Company / EstablishmentをBuilding / POIへ配置できるモデルを実装する
- ⬜ **P20-003** — Job position・必要worker数・wageの最小モデルを実装する
- ⬜ **P20-004** — PersonとJobを結ぶemployment stateを実装する
- ⬜ **P20-005** — residenceとworkplaceから通勤activity / Trip需要を生成する
- ⬜ **P20-006** — Household income / cash balanceの最小stateを実装する
- ⬜ **P20-007** — Company cash balance / revenue / expenseの最小stateを実装する
- ⬜ **P20-008** — Industry sectorと簡易production capacityを実装する
- ⬜ **P20-009** — Householdの基本消費需要とCommercial POIでの支出を実装する
- ⬜ **P20-010** — wage支払と消費による最小economic cycleを固定tick上で実装する
- ⬜ **P20-011** — Economy stateをcheckpoint / Save Dataへ含める
- ⬜ **P20-012** — employment / income / company / productionの集計statisticsをServer配信可能にする
- ⬜ **P20-013** — Web Clientで選択Company / Householdと経済統計をdebug表示する
- ⬜ **P20-014** — 複数日economic cycleのdeterministic integration testを追加する
- ⬜ **P20-015** — 大規模Economic Actorのtick・planner・memory benchmarkを記録する
- ⬜ **P20-016** — Economyのspecification / architecture / ROADMAPを同期する

### Phase 20 完了条件

- HouseholdとCompanyの間に雇用・賃金・消費による最小循環が存在する。
- 通勤需要がPopulation / Transitへ自然に接続される。
- 経済状態がstable IDとraw valueで保存され、locale依存文言を持たない。

---

## Phase 21 — Logistics / Freight

> **状態: ⬜ 未着手**  
> **依存:** Phase 13 / 20  
> 生産・在庫・注文・Shipment・Freight Vehicleを接続し、都市内物流をSimulationする。

- ⬜ **P21-001** — Commodity / Inventory / Order / Shipmentの正本契約を仕様化する
- ⬜ **P21-002** — Establishmentごとのinventoryとcapacityを実装する
- ⬜ **P21-003** — production / consumptionから補充Orderを生成する最小ruleを実装する
- ⬜ **P21-004** — OrderをShipmentへまとめるallocation policyを実装する
- ⬜ **P21-005** — Warehouse / loading point / delivery pointをUrban Worldへ接続する
- ⬜ **P21-006** — Freight VehicleをRoad Trafficへ接続する
- ⬜ **P21-007** — pickup / loading / transit / unloading / deliveredのShipment state machineを実装する
- ⬜ **P21-008** — Freight routeと配送順序をRoutingへ接続する
- ⬜ **P21-009** — 渋滞・配送遅延がinventoryへ影響する最低限の連携を実装する
- ⬜ **P21-010** — Logistics stateをcheckpoint / Save Dataへ含める
- ⬜ **P21-011** — Shipment / inventory / freight statisticsをProtocol / Serverで配信する
- ⬜ **P21-012** — Web ClientでFreight Vehicle / Shipment / inventoryをdebug表示する
- ⬜ **P21-013** — 生産→配送→在庫補充を実Server→Browserで検証するE2Eを追加する
- ⬜ **P21-014** — 大規模Shipment / Inventoryのtick・routing・memory benchmarkを記録する
- ⬜ **P21-015** — Logistics / Freightのspecification / architecture / ROADMAPを同期する

### Phase 21 完了条件

- 生産側の物資がShipmentとして道路網を移動し、需要側inventoryへ到着する。
- FreightがRoad Trafficの渋滞を共有し、配送遅延が観測できる。
- Logistics stateを保存復元して継続できる。

---

## Phase 22 — Power Infrastructure

> **状態: ⬜ 未着手**  
> **依存:** Phase 10 / 20  
> 発電・送配電・需要を都市Entityと接続し、電力供給状態をSimulationへ導入する。

- ⬜ **P22-001** — Generator / Substation / PowerLine / Loadの正本契約を仕様化する
- ⬜ **P22-002** — PowerNode / PowerLine topologyとstable IDを実装する
- ⬜ **P22-003** — Generator capacity / output / operating stateの最小モデルを実装する
- ⬜ **P22-004** — Building / EstablishmentをPower Loadへ関連付ける契約を実装する
- ⬜ **P22-005** — 時刻・用途・activityからload demandを計算する最小ruleを実装する
- ⬜ **P22-006** — network接続とcapacityを考慮する簡易power balance / dispatchを実装する
- ⬜ **P22-007** — insufficient supply時のunserved demand / outage stateを実装する
- ⬜ **P22-008** — outageをBuilding / Industryの稼働状態へ反映する最小連携を実装する
- ⬜ **P22-009** — Power stateをcheckpoint / Save Dataへ含める
- ⬜ **P22-010** — Power topology / supply / demand / outageをProtocol / Serverで配信する
- ⬜ **P22-011** — Web ClientでPower networkと供給状態をdebug可視化する
- ⬜ **P22-012** — 需要変動・generator停止・outage復旧を検証するdeterministic E2Eを追加する
- ⬜ **P22-013** — 大規模Power node/loadのtick・topology benchmarkを記録する
- ⬜ **P22-014** — Power Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 22 完了条件

- 都市のBuilding / Industryに電力需要があり、発電・network capacityに応じて供給状態が変化する。
- outageを保存・配信・可視化できる。
- Power Simulationが他domainと疎結合な明確な境界を持つ。

---

## Phase 23 — Urban Growth & City Generation

> **状態: ⬜ 未着手**  
> **依存:** Phase 10〜22の主要都市モデル  
> Parcel / Zoning / Land Useとdeterministic city generationを導入し、都市を手作業fixtureだけでなく生成・成長させられるようにする。Phase 10から委譲されたParcel / land-useの正本はこのPhaseで導入する。

- ⬜ **P23-001** — Parcel境界・Zone種別・土地利用・占有/development stateの正本契約を仕様化する
- ⬜ **P23-002** — Parcel store / stable ID lifecycleとZone designationを設定するSimulation commandを実装する
- ⬜ **P23-003** — Road access・parcel size・land useからdevelopment suitabilityを評価する
- ⬜ **P23-004** — Zoneに応じたBuilding用途・規模候補を選ぶdevelopment ruleを実装する
- ⬜ **P23-005** — 空ParcelへのBuilding development lifecycleを実装する
- ⬜ **P23-006** — demand変化に応じたredevelopment / vacancyの最小ruleを実装する
- ⬜ **P23-007** — seedからRoad Networkを生成するdeterministic generatorを実装する
- ⬜ **P23-008** — Road NetworkからParcelを生成するdeterministic subdivisionを実装する
- ⬜ **P23-009** — Parcel / ZoneからBuilding / POIを生成するdeterministic generatorを実装する
- ⬜ **P23-010** — 初期Population / Household / Jobを生成都市へ配置するseeding処理を実装する
- ⬜ **P23-011** — Railway / Power等の既存Infrastructureを壊さないgeneration constraintを定義する
- ⬜ **P23-012** — Parcel / Zone / city generation設定・seed・生成結果をSave / checkpoint契約へ統合する
- ⬜ **P23-013** — Parcel / Zone / development stateをProtocol / Serverで配信し、Web Clientで可視化する
- ⬜ **P23-014** — 同一seedで同一都市を生成するreproducibility E2Eを追加する
- ⬜ **P23-015** — 小/中/大規模都市generation時間・memory・初期Simulation負荷benchmarkを記録する
- ⬜ **P23-016** — Urban Growth / City Generationのspecification / architecture / ROADMAPを同期する

### Phase 23 完了条件

- Parcel / Zone / land-useがSimulation正本として存在し、Zone指定からBuilding developmentへ状態が遷移できる。
- 同一seed・設定から同一のRoad / Parcel / Buildingを再生成できる。
- Parcel / Zone状態を保存・配信・可視化できる。

---

## Phase 24 — City Management UI

> **状態: ⬜ 未着手**  
> **依存:** Phase 23  
> Browserから都市状態を調査・編集・管理するためのserver-authoritative UIとcommand境界を整える。

- ⬜ **P24-001** — Build / Edit commandの認可・validation・ack/error契約を仕様化する
- ⬜ **P24-002** — Protocolへserver-authoritative command request / resultの共通枠組みを追加する
- ⬜ **P24-003** — Web Clientで3D Entityを選択するpicking / selection基盤を実装する
- ⬜ **P24-004** — Building / Parcel / POI / Person / Vehicle等をServer read modelから表示するInspector基盤を実装する
- ⬜ **P24-005** — Road / Laneのbuild / edit / remove commandとUIを実装する
- ⬜ **P24-006** — Building / POI / Parcel / Zoneのbuild / edit commandとUIを実装する
- ⬜ **P24-007** — Railway track / station / platformのbuild / edit commandとUIを実装する
- ⬜ **P24-008** — Power Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P24-009** — command失敗時にClient側だけ状態が進まないoptimistic-state禁止またはrollback方針を実装する
- ⬜ **P24-010** — Simulation speed / pause / resume等の運転controlをServer commandとして実装する
- ⬜ **P24-011** — Population / Traffic / Transit / Economy / Logistics / PowerのDashboard統計を実装する
- ⬜ **P24-012** — Server configurationの変更可能項目・restart必要項目を分離してUI化する
- ⬜ **P24-013** — current Save formatのsave / load操作をServer経由で実行する管理UIを追加する
- ⬜ **P24-014** — destructive commandのconfirmationとstable error localizationを実装する
- ⬜ **P24-015** — Inspector / build / edit / config / save操作のBrowser E2Eを追加する
- ⬜ **P24-016** — 大規模都市でselection・overlay・dashboardが描画hot pathを阻害しないperformance testを追加する
- ⬜ **P24-017** — City Management UIのarchitecture / UX contract / ROADMAPを同期する

### Phase 24 完了条件

- 都市の主要EntityをBrowserから選択・調査できる。
- build/edit操作は必ずServer-authoritative commandを経由し、Clientだけで正本状態を変更しない。
- 主要statisticsと運転設定を管理UIから確認できる。

---

## Phase 25 — Distribution & Compatibility

> **状態: ⬜ 未着手**  
> **依存:** Phase 24  
> Save migrationと配布物を整備し、開発環境外でもversion付き成果物として起動・更新・復元できる状態にする。

### Save互換性

- ⬜ **P25-001** — Save migrationのsupport範囲・失敗契約・version policyを仕様化する
- ⬜ **P25-002** — Save formatごとのmigration stepを登録できるframeworkを実装する
- ⬜ **P25-003** — repositoryに旧Save format fixtureを保持し、自動migration testを追加する
- ⬜ **P25-004** — migration中断・unsupported version・破損dataを安全に拒否する
- ⬜ **P25-005** — migration前後でstable IDと継続可能stateを保持するintegration testを追加する

### 配布・Deployment

- ⬜ **P25-006** — Server standalone binaryのsupported OS / architecture matrixを定義する
- ⬜ **P25-007** — Windows / Linux向けServer publish artifactをCIで生成する
- ⬜ **P25-008** — 必要性を検証した上で追加architecture / OS向けartifactを生成する
- ⬜ **P25-009** — Web Client production buildのbase path / Server endpoint設定をdeployment向けに整理する
- ⬜ **P25-010** — static hosting向けWeb Client artifactをCIで生成する
- ⬜ **P25-011** — Server用container imageとruntime configuration契約を実装する
- ⬜ **P25-012** — release artifactへVERSION・commit SHA・license / third-party noticeを同梱する
- ⬜ **P25-013** — release artifactのchecksum / SBOM等、配布時に必要なintegrity metadataを生成する
- ⬜ **P25-014** — package / binary / Web / containerを起動するrelease smoke testをCIへ追加する
- ⬜ **P25-015** — install / upgrade / rollback / backup / restore手順をdocument化する
- ⬜ **P25-016** — develop→main release時のversion / artifact / release note手順を自動化可能な形へ整理する
- ⬜ **P25-017** — Distribution / Compatibilityのarchitecture / development docs / ROADMAPを同期する

### Phase 25 完了条件

- 開発toolchainを手作業構築しなくても、配布artifactからServerとWeb Clientを起動できる。
- 対応対象の旧Save Dataを明示的なmigration経路で読み込める。
- release artifactのversion・commit・license・integrity情報を追跡できる。

---

## Phase 26 — Extension Platform & Localization

> **状態: ⬜ 未着手**  
> **依存:** Phase 25  
> 正本Simulationと互換性境界を壊さず、外部拡張と追加localeを導入できる公開拡張基盤を作る。

### Extension Platform

- ⬜ **P26-001** — Extension / Modで公開する範囲と非公開内部APIの境界を仕様化する
- ⬜ **P26-002** — Extension manifest・stable ID・version・dependency契約を定義する
- ⬜ **P26-003** — data-only extensionとcode extensionを分離したloading modelを設計する
- ⬜ **P26-004** — code extensionの信頼境界・権限・非sandbox性を明示し、安全なdefault policyを実装する
- ⬜ **P26-005** — Simulationへextension contentを登録するversioned public APIを実装する
- ⬜ **P26-006** — Extension固有Save Dataをnamespace付きで保存し、missing extension時の挙動を定義する
- ⬜ **P26-007** — Protocolへextension固有wire typeを直接衝突させない拡張契約を設計する
- ⬜ **P26-008** — Extensionのload order / dependency cycle / incompatible versionをvalidationする
- ⬜ **P26-009** — Extension packageの開発・test用templateとsample extensionを追加する

### Localization

- ⬜ **P26-010** — `ja-JP`をdefaultにしたlocale discovery / fallback policyを再確認・固定する
- ⬜ **P26-011** — 追加locale resource packを導入できるWeb Client loading境界を実装する
- ⬜ **P26-012** — 数値・日時・単位・plural等のlocale formattingを共通化する
- ⬜ **P26-013** — stable error code / structured parameterから各localeの表示文を生成するcoverageを拡張する
- ⬜ **P26-014** — translation key欠落・未使用key・parameter不一致をCIで検出する
- ⬜ **P26-015** — 少なくとも1つの追加localeで主要UI / Inspector / Dashboard / error表示をE2E確認する

### Closeout

- ⬜ **P26-016** — Extension有無・追加locale有無でSave / Protocol / Simulation determinismが壊れないintegration testを追加する
- ⬜ **P26-017** — Extension loadingとlocalizationのstartup / memory costをbenchmarkする
- ⬜ **P26-018** — Extension author guide / localization guide / compatibility policyを整備する
- ⬜ **P26-019** — architecture / ADR / ROADMAPを同期し、Phase 10〜26で計画した旧Backlogのcloseoutを確認する

### Phase 26 完了条件

- 既存Simulation内部実装へ直接依存せず、versionedな公開境界からExtensionを追加できる。
- Extension固有stateがSave Dataと衝突せず、missing/incompatible extensionを安全に扱える。
- `ja-JP`以外のlocaleを主要UIへ追加でき、Protocol / Save / Simulationへ翻訳済み文言を持ち込まない。

---

## 旧「将来 Backlog」のPhase移行

Phase 9終了時点で列挙していた将来Backlogは、以下の通りPhase 10〜26へ移行した。今後は各Phase内Taskを正本として追跡する。

| 旧Backlogテーマ | 移行先 |
| --- | --- |
| Building / POI データモデル | Phase 10 |
| Road graph / lane model | Phase 11 |
| Pathfinding / route cache | Phase 12 |
| Road traffic simulation | Phase 13 |
| Intersection / signal control | Phase 14 |
| Agent needs / schedule / household | Phase 15 |
| Pedestrian simulation | Phase 16 |
| Railway infrastructure | Phase 17 |
| Railway operation / timetable | Phase 18 |
| Bus / taxi / multimodal transit | Phase 19 |
| Industry / jobs / economy | Phase 20 |
| Logistics / freight | Phase 21 |
| Power generation / grid / demand | Phase 22 |
| Parcel / zoning / land use | Phase 23 |
| City generation | Phase 23 |
| Inspector / dashboard / statistics UI | Phase 24 |
| Build / edit commands | Phase 24 |
| Server configuration UI | Phase 24 |
| Save migration | Phase 25 |
| Release packaging | Phase 25 |
| Server binary distribution | Phase 25 |
| Web Client deployment | Phase 25 |
| Container image | Phase 25 |
| Mod / extension architecture | Phase 26 |
| Additional locales | Phase 26 |

## Phase 9から継続する計画済み項目

Phase 9では「3D座標を正本として扱える基盤」までを完了とし、具体的な物理・地形ルールは後続へ分離していた。Phase 10〜26へ直接割り当てられない項目も消さず、現行Backlogとして保持する。

| Phase 9で非対象とした項目 | 現在の扱い |
| --- | --- |
| 道路・線路・建物ごとの高度制約 | Phase 10 / 11 / 17の3D geometry・topology・validationで扱う |
| 地下・高架を考慮したpathfinding | Phase 12で扱う |
| 旧Save formatから新formatへのmigration | Phase 25で扱う |
| 重力・落下・ジャンプ等の垂直物理 | 継続Backlog（Phase未割当） |
| 飛行・空中移動等のairborne movement | 継続Backlog（Phase未割当） |
| terrain model / terrain collision | 継続Backlog（Phase未割当） |
| ground snapping / surface追従 | 継続Backlog（Phase未割当） |

### 継続Backlog（Phase未割当）

以下は計画済みだが、Phase 10〜26の完了に必須とはしない。着手時に独立Phaseまたは既存Phaseへの追加Taskとして分解する。

- Physics Foundation — 重力、落下、ジャンプ、垂直速度・加速度、物理stateのSave / Protocol / E2E
- Airborne Movement — 飛行可能Entity、空中経路、飛行高度ルール、3D空間交通との競合境界
- Terrain Foundation — terrain height / surface / slopeの正本モデル、3D spatial query、Save / Protocol / Web描画
- Terrain Interaction — terrain collision、ground snapping、surface追従、道路・建物・Pedestrianとの接続

## 新規Backlogの扱い

Phase 10以降の実装中に新しい大テーマが見つかった場合は、既存Phaseへ無理に詰め込まない。

1. 既存Phaseの完了に必須なら、そのPhaseへ独立Taskとして追加する。
2. 完了に必須でない大テーマなら、このROADMAP末尾へBacklogとして記録する。
3. 着手時にWhat / Whyを`docs/specifications/`、Howを`docs/architecture/`またはADRへ切り分ける。
4. 実装・保存・配信・描画・検証のどこまでをPhase完了条件とするか明示する。
5. Phase完了時に、残件が暗黙に持ち越されていないことを確認する。