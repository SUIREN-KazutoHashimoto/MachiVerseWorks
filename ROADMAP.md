# MachiVerseWorks Roadmap

MachiVerseWorks の作業を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** Phase 13 — Road Traffic Simulation（closeout作業中）  
> **次の実装タスク:** P13-001 — Vehicle entity・stable ID・寸法・性能値・状態遷移を仕様化する

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
| 11 | Road Network Foundation | ✅ 完了・develop統合済み |
| 12 | Routing Foundation | ✅ 完了 |
| 13 | Road Traffic Simulation | 🟦 develop統合済み・closeout作業中 |
| 14 | Intersection & Signal Control | 🟦 develop統合済み・Phase 13 closeout待ち |
| 15 | Population & Daily Activity | 🟦 develop統合済み・Phase 14 closeout待ち |
| 16 | Pedestrian Simulation | 🟦 develop統合済み・Phase 15 closeout待ち |
| 17 | Railway Infrastructure | ⏳ 待機 |
| 18 | Railway Operations | ⏳ 待機 |
| 19 | Multimodal Transit | ⏳ 待機 |
| 20 | Industry / Jobs / Economy | ⏳ 待機 |
| 21 | Logistics / Freight | ⏳ 待機 |
| 22 | Power Infrastructure | ⏳ 待機 |
| 23 | Urban Growth & City Generation | ⏳ 待機 |
| 24 | City Management UI | ⏳ 待機 |
| 25 | Distribution & Compatibility | ⏳ 待機 |
| 26 | Extension Platform & Localization | ⏳ 待機 |

Phase 0〜8の詳細TaskとPhase 9着手時点の計画状態は、履歴として [`docs/archive/roadmap-through-phase9-plan.md`](docs/archive/roadmap-through-phase9-plan.md) に保存しています。Phase 9〜12の完了証跡はGit履歴と各Phaseのspecification / architecture / development docsを正本として参照します。

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

## Phase 9〜12 — 完了済みFoundation

Phase 9〜12は正式closeout済み。現行ROADMAPでは完了履歴の詳細Taskを繰り返さず、実装・仕様・benchmarkの正本へ参照を集約する。

| Phase | 主な正本 / 証跡 |
| --- | --- |
| Phase 9 — 3D Simulation Foundation | `docs/specifications/world-coordinate-system.md`、3D対応Protocol / Save / E2E / benchmarkのGit履歴 |
| Phase 10 — Urban World Foundation | [`docs/specifications/building-poi.md`](docs/specifications/building-poi.md) |
| Phase 11 — Road Network Foundation | [`docs/specifications/road-network.md`](docs/specifications/road-network.md)、[`docs/development/road-network-benchmark.md`](docs/development/road-network-benchmark.md)、PR #47 |
| Phase 12 — Routing Foundation | [`docs/specifications/routing.md`](docs/specifications/routing.md)、[`docs/development/routing-benchmark.md`](docs/development/routing-benchmark.md) |

Phase 10から後続へ委譲した項目は現在も次のPhaseを正本とする。

| 項目 | 正本となるPhase / 境界 |
| --- | --- |
| Road上のBuilding / POI access | Phase 11 `RoadAccessPoint` |
| 徒歩networkへのBuilding / POI access | Phase 16 |
| Parcel / zoning / land use / development | Phase 23 |
| Building / Parcel / POIのInspector・編集UI | Phase 24 |
| 建物mesh / floor / room / entrance | 必要になるdomain Phaseで契約追加。Phase 10では`WorldVolume`のみを正本とする |
| PopulationによるPOI選択 | Phase 15 |

---

## Phase 13 — Road Traffic Simulation（develop統合済み・closeout作業中）

> **状態: 🟦 develop統合済み・closeout作業中**  
> **依存:** Phase 12  
> **統合:** PR #66、merge commit `813d45dfc8c6e3c063b5e6923aed0622b0caa27f`  
> Vehicleの正本state、Road/Lane上の固定tick移動、occupancy / car-following、Save / Protocol / Serverは実装済み。Phase 14でWeb ClientのVehicle instance描画も追加された。一方、Phase 13単独の仕様文書・複数Vehicle完走E2E・1,000 / 10,000 / 100,000 Vehicle benchmark・docs closeoutが未完了のため、Phase全体は正式closeoutしない。

- ⬜ **P13-001** — Vehicle entity・stable ID・寸法・性能値・状態遷移を仕様化する
- ✅ **P13-002** — Vehicle storeとspawn / despawn lifecycleをSimulationへ追加する
- ✅ **P13-003** — VehicleへRouteと現在Lane / progressを割り当てる状態モデルを実装する
- ✅ **P13-004** — Lane geometryに沿った3D位置・向き・速度更新を固定tickで実装する
- ✅ **P13-005** — 前走Vehicleとの距離を考慮する最小car-following modelを実装する
- ✅ **P13-006** — Lane occupancy indexを実装し、前後Vehicle検索を全件走査なしで行う
- ✅ **P13-007** — Routeに必要なLane変更を安全に実行する最小lane-change ruleを実装する
- ✅ **P13-008** — Lane終端で次Laneへ進むtransitionとRoute completionを実装する
- ✅ **P13-009** — 衝突・逆走・Lane外progressなどのtraffic invariantを検証する
- ✅ **P13-010** — Vehicle stateをcheckpoint / Save Dataへ含め、継続実行のdeterminismを確認する
- ✅ **P13-011** — Vehicle spawn/update/removeをProtocolへ追加する
- ✅ **P13-012** — Serverがsubscription volume内Vehicleだけを配信する
- ✅ **P13-013** — Web ClientでVehicleをinstance描画し、Lane方向と補間を反映する
- ✅ **P13-014** — traffic density / average speed / queue lengthの基礎metricsを計測可能にする
- ⬜ **P13-015** — 複数VehicleがRouteを完走する実Server→Browser E2Eを追加する
- ⬜ **P13-016** — 1,000 / 10,000 / 100,000 Vehicle級のtick・occupancy・snapshot benchmarkを記録する
- ⬜ **P13-017** — Road Trafficのspecification / architecture / ROADMAPを同期する

### Phase 13 implementation evidence / closeout gap

- `VehicleId` / `VehicleDimensions` / `VehiclePerformance` / `VehicleMovementState` / `VehicleSnapshot`を導入し、Vehicleをstable ID付きのSimulation正本stateとして保持する。
- `LaneOccupancyIndex`を使用し、前後Vehicle検索とtraffic progressionを全Vehicle全件比較へ依存させない。
- Route/Lane progress、3D pose、car-following、lane transition、route completion、traffic invariant、traffic metricsをSimulationへ実装済み。
- Vehicle stateはcheckpointとSave format 6へ保存され、Protocol 2.3のspawn/update/removeとServer subscription配信へ接続済み。
- Phase 14実装でWeb ClientのVehicle instance描画・補間が追加されたため、P13-013は後続Phaseの統合によって完了した。
- 現在のrepositoryにはPhase 13専用のRoad Traffic specification / architecture、`run-phase13-e2e.sh`相当の完走E2E、Vehicle 1,000 / 10,000 / 100,000 benchmarkが存在しないため、P13-001 / P13-015 / P13-016 / P13-017を残件とする。

### Phase 13 完了条件

- VehicleがRouteに沿ってLaneを移動し、前走車とLane occupancyを考慮して安全に進行できる。
- Vehicle stateを保存復元し、ServerからBrowserへ配信・補間描画できる。
- 大規模Vehicle数の基準性能が記録されている。**P13-016が未完了のため現在未達。**
- Phase単独のspecification / architecture / E2E証跡をcloseoutできる。**P13-001 / P13-015 / P13-017が未完了。**

---

## Phase 14 — Intersection & Signal Control（develop統合済み・Phase 13 closeout待ち）

> **状態: 🟦 develop統合済み・Phase 13 closeout待ち**  
> **依存:** Phase 13  
> **統合:** PR #67、merge commit `36dbf8a380aaa3c2403aba73ac6d01a17447400b`  
> Phase 14のTask実装・検証・docs同期は完了している。依存するPhase 13が正式closeout前のため、Phase 14全体は依存順に従ってcloseout待ちとする。

- ✅ **P14-001** — intersection movementとconflict relationの正本契約を仕様化する
- ✅ **P14-002** — Lane connectionから交差点movementを構築・検証する
- ✅ **P14-003** — 交差点進入待ちqueueとstop line状態を実装する
- ✅ **P14-004** — 無信号交差点の最小priority / yield ruleを実装する
- ✅ **P14-005** — Signal / Phase / Movement permissionのデータモデルを実装する
- ✅ **P14-006** — 固定cycleのsignal controllerを固定tickで実装する
- ✅ **P14-007** — red / yellow / greenに応じたVehicle停止・進入判断を実装する
- ✅ **P14-008** — downstream詰まり時にintersection内へ進入しないblocking ruleを実装する
- ✅ **P14-009** — signal controller stateをcheckpoint / Save Dataへ含める
- ✅ **P14-010** — Signal stateをProtocol / ServerからClientへ配信する
- ✅ **P14-011** — Web Clientで信号現示・stop line・queueをdebug可視化する
- ✅ **P14-012** — 複数交差点・右左折・高負荷queueのdeterministic regression testを追加する
- ✅ **P14-013** — 信号付きRoad Trafficを実Server→Browserで検証するE2Eを追加する
- ✅ **P14-014** — intersection throughput / queue処理のbenchmarkを記録する
- ✅ **P14-015** — Intersection / Signalのspecification / architecture / ROADMAPを同期する

### Phase 14 implementation evidence

- Intersection movement / conflict、queue / stop line、無信号priority / yield、Signal / Phase / permission、fixed-cycle controllerをSimulation正本stateとして実装した。
- red / yellow / green、downstream blocking、deterministic entry arbitrationをRoad Trafficへ接続した。
- signal controller stateをcheckpoint / Save Dataへ含め、Protocol 2.4 / Server / Webへ配信する。
- Web ClientはVehicle instancingに加えてsignal indication、stop line、queueをdebug表示する。
- `phase14-intersection-control.yml`、`phase14-e2e.yml`、`phase14-benchmark.yml`でregression / 実Server→Browser E2E / benchmarkを独立検証できる。
- [`docs/specifications/intersection-signal-control.md`](docs/specifications/intersection-signal-control.md)と対応architecture docsを正本として同期済み。

### Phase 14 完了条件

- Vehicleが交差点競合と信号現示を無視して侵入しない。
- queue・signal stateを保存復元・配信・可視化できる。
- 交差点処理の性能を独立benchmarkで追跡できる。
- **Task自体は達成済みだが、Phase 13正式closeoutまでPhase 14の正式closeoutを保留する。**

---

## Phase 15 — Population & Daily Activity（develop統合済み・Phase 14 closeout待ち）

> **状態: 🟦 develop統合済み・Phase 14 closeout待ち**  
> **依存:** Phase 10 / 14  
> **統合:** PR #68、merge commit `36502ce493a63e7e7261df2480fdd20e7acb0427`  
> Person / Household / Need / Activity / daily schedule / planner / Trip Request、Road Traffic・Pedestrianへのdispatch、arrival state machine、Save / statistics / Web debug / integration test / benchmark / docsは実装済み。Phase 14が依存closeout待ちのため、Phase 15全体も正式closeoutを保留する。

- ✅ **P15-001** — Person / Household / residenceのstable IDと責務境界を仕様化する
- ✅ **P15-002** — HouseholdとPersonの最小demographic stateを実装する
- ✅ **P15-003** — PersonをBuilding / POIの居住・活動場所へ関連付ける契約を実装する
- ✅ **P15-004** — Need / Activity種別と優先度・満足度の最小モデルを実装する
- ✅ **P15-005** — 時刻に基づくdaily scheduleとactivity windowを実装する
- ✅ **P15-006** — schedule / needsから次のactivity destinationを決定するplannerを実装する
- ✅ **P15-007** — activity間移動を表すTrip Requestを移動手段から独立した契約として実装する
- ✅ **P15-008** — 自家用Vehicleを利用可能なPersonのTrip RequestをRoad Trafficへ接続する
- ✅ **P15-009** — 到着・activity開始・終了・次Trip生成までのstate machineを実装する
- ✅ **P15-010** — Person / Household / schedule / activity stateをcheckpoint / Save Dataへ含める
- ✅ **P15-011** — Populationの集計snapshot / statistics配信契約を追加する
- ✅ **P15-012** — Web Clientで選択Personの居住地・目的地・現在activityをdebug表示する
- ✅ **P15-013** — 1日分のscheduleから複数Tripが生成・完了するdeterministic integration testを追加する
- ✅ **P15-014** — 1,000 / 10,000 / 100,000 Person級のplanner / tick / memory benchmarkを記録する
- ✅ **P15-015** — Population / Daily Activityのspecification / architecture / ROADMAPを同期する

### Phase 15 implementation evidence

- `PopulationStore`とstable `HouseholdId` / `PersonId` / `TripRequestId`を導入し、Populationのauthoritative stateをSimulation Coreへ保持する。
- daily schedule / Need / ActivityからTrip Requestを生成し、private Vehicleが利用可能ならRoad Trafficへ、Motor routeが成立しない場合を含む徒歩TripはPedestrianへdispatchする。
- Walking / Driving entityの到着を観測してPersonのlocation / activityを更新し、次activity / Tripへ進むstate machineを実装した。
- Population stateをcheckpoint / Save format 7へ保存し、statisticsと選択Person debug stateをServer / Webへ配信する。
- deterministic integration testと1,000 / 10,000 / 100,000 Person benchmark runner / workflowを追加した。
- [`docs/specifications/population-daily-activity.md`](docs/specifications/population-daily-activity.md)と[`docs/architecture/population-daily-activity.md`](docs/architecture/population-daily-activity.md)を正本として同期済み。
- PopulationからFoot/Pedestrianへdispatchする接続により、Phase 16のP16-009も完了した。

### Phase 15 完了条件

- Personが住居と日常scheduleを持ち、Simulation時刻からTrip需要を生成できる。
- Trip生成が特定の交通mode実装へ密結合していない。
- Population stateを保存し、継続実行してもscheduleと活動状態が破綻しない。
- **Task自体は達成済みだが、Phase 14正式closeoutまでPhase 15の正式closeoutを保留する。**

---

## Phase 16 — Pedestrian Simulation（develop統合済み・Phase 15 closeout待ち）

> **状態: 🟦 develop統合済み・Phase 15 closeout待ち**  
> **依存:** Phase 12 / 15  
> **統合:** PR #61、merge commit `8fd6f2ef866464cc6051111ef343de11948a1eaf`  
> 徒歩network・Pedestrian entity・保存・Protocol/Server/Web・E2E/benchmarkは先行実装されdevelopへ統合済み。PR #68でPopulation由来Trip Requestとの接続も実装され、P16-009を含む全Taskは完了した。Phase 15が依存closeout待ちのため、Phase 16全体の正式closeoutのみ保留する。

- ✅ **P16-001** — pedestrian network / sidewalk / crossingの正本契約を仕様化する
- ✅ **P16-002** — Road Networkから歩行可能edgeとcrossingを構築する境界を実装する
- ✅ **P16-003** — Building / POI access pointをpedestrian networkへ接続する
- ✅ **P16-004** — 徒歩専用routingとRoute resultを実装する
- ✅ **P16-005** — Pedestrian entity・stable ID・歩行速度・route progressを実装する
- ✅ **P16-006** — sidewalk geometryに沿う3D歩行更新を固定tickで実装する
- ✅ **P16-007** — crossing permission seamを実装し、横断可否で待機/再開できるようにする
- ✅ **P16-008** — 最小の歩行occupancy制約を実装し、同一edge位置の競合を抑制する
- ✅ **P16-009** — 徒歩TripをPopulationのTrip Requestへ接続する
- ✅ **P16-010** — Pedestrian stateとcrossing permissionをcheckpoint / Save Dataへ含める
- ✅ **P16-011** — PedestrianをProtocol / Serverでsubscription配信する
- ✅ **P16-012** — Web ClientでPedestrianをinstance描画・補間する
- ✅ **P16-013** — Building間徒歩Tripを実Server→Browserで検証するE2Eを追加する
- ✅ **P16-014** — 1,000 / 10,000 Pedestrianのtick・routing・occupancy benchmarkを記録する
- ✅ **P16-015** — Pedestrianのspecification / architecture / ROADMAPを同期する

### Phase 16 完了条件

- Populationが生成するTrip Requestから徒歩Tripを開始できる。
- Building / POI間を徒歩Routeで移動し、crossing permissionとoccupancyを考慮できる。
- Pedestrian stateを保存・subscription配信・Browser描画でき、多数Pedestrianで全件相互比較に依存しない。
- **Task自体は達成済みだが、Phase 15正式closeoutまでPhase 16の正式closeoutを保留する。**

---

## Phase 17 — Railway Infrastructure

> **状態: ⬜ 未着手**  
> **依存:** Phase 10 / 11 / 16  
> 線路・分岐・block・駅・ホーム・車庫を、列車運行が利用できる3D railway topologyとして確立する。

- ⬜ **P17-001** — Railway Infrastructureの軸・接続・track gauge・方向・高度契約を仕様化する
- ⬜ **P17-002** — TrackNode / TrackSegmentのstable IDと3D geometryを実装する
- ⬜ **P17-003** — Track direction / speed limit / electrification等の最小属性を実装する
- ⬜ **P17-004** — switch / junctionと進行可能connectionを表すtopologyを実装する
- ⬜ **P17-005** — train separationに使うblock sectionのInfrastructureモデルを実装する
- ⬜ **P17-006** — Station / Platformのstable ID・geometry・track connectionを実装する
- ⬜ **P17-007** — Platform access pointをUrban World / pedestrian networkへ接続する
- ⬜ **P17-008** — Depot / sidingの最小Infrastructureモデルを実装する
- ⬜ **P17-009** — Railway topologyの3D spatial queryと接続validationを実装する
- ⬜ **P17-010** — Railway Infrastructureをcheckpoint / Save Dataへ含める
- ⬜ **P17-011** — Track / Station / PlatformのProtocol配信契約を追加する
- ⬜ **P17-012** — Web ClientでTrack / Station / Platformを3D描画する
- ⬜ **P17-013** — 高架・地下・複線・分岐・駅を含むdeterministic fixtureを追加する
- ⬜ **P17-014** — Railway InfrastructureのSave→Server→Browser E2Eを追加する
- ⬜ **P17-015** — 大規模Railway topologyのquery・validation benchmarkを記録する
- ⬜ **P17-016** — Railway Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 17 完了条件

- Train operationが利用できる連続したtrack topology・block・station・platformが存在する。
- 道路同様、立体交差と接続を混同しない。
- Infrastructureを保存・配信・描画でき、pedestrian networkからPlatformへ到達できる。

---

## Phase 18 — Railway Operations

> **状態: ⬜ 未着手**  
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
