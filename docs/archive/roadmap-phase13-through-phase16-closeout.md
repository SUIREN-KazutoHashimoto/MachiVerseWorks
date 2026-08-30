# Phase 13〜16 Closeout Record

Phase 13 Road Traffic Simulation から Phase 16 Pedestrian Simulation までの正式closeout時点のTask状態と検証証跡を保存する。

closeout日: 2026-08-30

## Phase 13 — Road Traffic Simulation

すべてのTaskを完了した。

- ✅ **P13-001** — Vehicle entity・stable ID・寸法・性能値・状態遷移を仕様化する
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
- ✅ **P13-015** — 複数VehicleがRouteを完走する実Server→Browser E2Eを追加する
- ✅ **P13-016** — 1,000 / 10,000 / 100,000 Vehicle級のtick・occupancy・snapshot benchmarkを記録する
- ✅ **P13-017** — Road Trafficのspecification / architecture / ROADMAPを同期する

### Phase 13 evidence

- 実装本体: PR #66、merge commit `813d45dfc8c6e3c063b5e6923aed0622b0caa27f`
- 正本仕様: [`../specifications/road-traffic.md`](../specifications/road-traffic.md)
- architecture: [`../architecture/road-traffic.md`](../architecture/road-traffic.md)
- dedicated Server→Browser E2E: GitHub Actions run `33303599948` success
- dedicated 1k / 10k / 100k Vehicle benchmark: GitHub Actions run `33303599957` success
- benchmark baseline: [`../development/road-traffic-benchmark.md`](../development/road-traffic-benchmark.md)
- closeout candidate CI: run `33303599974` success
- Dependency Review: run `33303600017` success

100,000 Vehicle baselineはfull tick平均155.8082msであり、30Hz realtime budgetを超える。この値は性能保証ではなく、今後の最適化・回帰検知のためのbaselineとして記録する。Phase 13の完了条件は性能目標達成ではなく、大規模Vehicle数を計測可能にして基準値を残すことなので、性能課題を明示した上でcloseoutする。

## Phase 14 — Intersection & Signal Control

すべてのTaskを完了し、Phase 13の正式closeoutによって依存条件も満たした。

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

### Phase 14 evidence

- 実装本体: PR #67、merge commit `36dbf8a380aaa3c2403aba73ac6d01a17447400b`
- specification / architecture: `intersection-signal-control.md`
- closeout candidate Intersection Control run `33303599952` success
- closeout candidate Signal Traffic E2E run `33303599986` success
- benchmark baselineは[`../development/phase14-intersection-benchmark.md`](../development/phase14-intersection-benchmark.md)を正本とする。

## Phase 15 — Population & Daily Activity

すべてのTaskを完了し、Phase 14の正式closeoutによって依存条件も満たした。

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

### Phase 15 evidence

- 実装本体: PR #68、merge commit `36502ce493a63e7e7261df2480fdd20e7acb0427`
- specification: [`../specifications/population-daily-activity.md`](../specifications/population-daily-activity.md)
- architecture: [`../architecture/population-daily-activity.md`](../architecture/population-daily-activity.md)
- closeout candidate Population benchmark run `33303599942` success
- deterministic integration / Save / Server / Web testsはcloseout candidate CI run `33303599974`で成功した。

## Phase 16 — Pedestrian Simulation

すべてのTaskを完了し、Phase 15の正式closeoutによって依存条件も満たした。

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

### Phase 16 evidence

- 先行実装本体: PR #61、merge commit `8fd6f2ef866464cc6051111ef343de11948a1eaf`
- Population Trip Request接続: PR #68
- specification: [`../specifications/pedestrian-simulation.md`](../specifications/pedestrian-simulation.md)
- architecture: [`../architecture/pedestrian-simulation.md`](../architecture/pedestrian-simulation.md)
- closeout candidate Pedestrian E2E run `33303600005` success
- closeout candidate Pedestrian benchmark run `33303600018` success

## Closeout dependency chain

正式closeoutは依存順に次の通り確定する。

```text
Phase 13 Road Traffic
  -> Phase 14 Intersection / Signal
  -> Phase 15 Population / Daily Activity
  -> Phase 16 Pedestrian
  -> Phase 17 Railway Infrastructure（次）
```

Phase 13の残件を検証して完了したことで、先行mergeされていたPhase 14〜16の依存待ちが解消された。以後の現行ROADMAPはPhase 17以降を主対象とする。
