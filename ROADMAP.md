MachiVerseWorks の作業を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** Phase 23 — Power Infrastructure
> **次の実装タスク:** P23-001 Generator / Substation / PowerLine / Loadの正本契約を仕様化する

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
| 18 | Railway Operations | ✅ 完了 |
| 19 | Multimodal Transit | ✅ 完了 |
| 20 | Server Administration Console | ✅ 完了 |
| 21 | Industry / Jobs / Economy | ✅ 完了 |
| 22 | Logistics / Freight | ✅ 完了 |
| 23 | Power Infrastructure | ▶️ 次 |
| 24 | Water & Sewer Infrastructure | ⏳ 待機 |
| 25 | Gas Infrastructure | ⏳ 待機 |
| 26 | Optical Communication Infrastructure | ⏳ 待機 |
| 27 | Radio & Spectrum Foundation | ⏳ 待機 |
| 28 | Urban Growth & City Generation | ⏳ 待機 |
| 29 | City Management UI | ⏳ 待機 |
| 30 | Distribution & Compatibility | ⏳ 待機 |
| 31 | Extension Platform & Localization | ⏳ 待機 |

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
  -> Server Administration Console
  -> Industry / Jobs / Economy
  -> Logistics / Freight
  -> Power Infrastructure
  -> Water / Sewer Infrastructure
  -> Gas Infrastructure
  -> Optical Communication Infrastructure
  -> Radio / Spectrum Foundation
  -> Urban Growth / City Generation
  -> City Management UI
  -> Distribution / Compatibility
  -> Extension Platform / Localization
```

この順番は、後続機能が前段の正本モデルを再利用できることを優先する。先行mergeを行っても、Phaseの正式closeout順は依存関係に従う。Phase 20はServer横断の管理境界としてPhase 19後へ挿入するが、Phase 21以降の各Simulation domainがAdministration Consoleの実装へ直接依存することを意味しない。

---

## Phase 9〜18 — 完了済みFoundation / Simulation Domains

Phase 9〜18は正式closeout済み。現行ROADMAPでは完了履歴の詳細Taskを繰り返さず、実装・仕様・benchmarkの正本へ参照を集約する。

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
| Phase 18 — Railway Operations | [`docs/specifications/railway-operations.md`](docs/specifications/railway-operations.md)、[`docs/architecture/railway-operations.md`](docs/architecture/railway-operations.md)、[`docs/development/railway-operations-benchmark.md`](docs/development/railway-operations-benchmark.md)、Phase 18 E2E / Railway Operations benchmark workflow、PR #131 |

### Phase 13〜16 closeout

Phase 13の未完了だったP13-001 / P13-015 / P13-016 / P13-017を完了し、依存順にPhase 14→15→16の正式closeout待ちを解消した。詳細Taskと検証runは [`docs/archive/roadmap-phase13-through-phase16-closeout.md`](docs/archive/roadmap-phase13-through-phase16-closeout.md) を正本とする。

Phase 13 benchmarkでは1,000 / 10,000 / 100,000 Vehicleを同一runnerで計測した。100,000 Vehicleのfull tick平均は155.8082msで30Hz realtime budgetを超えるため、性能保証とはせず、今後の最適化・回帰検知baselineとして明示的に保持する。

Phase 10から後続へ委譲した計画済み項目は現在も次のPhaseを正本とする。

| 項目 | 正本となるPhase / 境界 |
| --- | --- |
| Road上のBuilding / POI access | Phase 11 `RoadAccessPoint` |
| 徒歩networkへのBuilding / POI access | Phase 16 |
| Parcel / zoning / land use / development | Phase 28 |
| Building / Parcel / POIのInspector・編集UI | Phase 29 |
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

> **状態: ✅ 完了**
> **依存:** Phase 17
> Train・service・timetable・station stop・block separationを実装し、再現可能な鉄道運行を成立させる。

- ✅ **P18-001** — Train / Formation / Service / Timetableの責務とstable ID契約を仕様化する
- ✅ **P18-002** — Train formationの長さ・性能・capacityを表す最小モデルを実装する
- ✅ **P18-003** — Railway routeをTrack sequenceとして構築・検証する処理を実装する
- ✅ **P18-004** — Serviceとstop sequence / planned arrival / departureを表すTimetableを実装する
- ✅ **P18-005** — TrainをTrack geometryに沿って3D移動させる固定tick更新を実装する
- ✅ **P18-006** — block occupancy / reservationを実装し、同一blockへの危険な進入を防ぐ
- ✅ **P18-007** — station approach / stop position / dwell / departureを実装する
- ✅ **P18-008** — platform assignmentとoccupied platformの競合処理を実装する
- ✅ **P18-009** — timetableとの差からdelayを計算し、後続stopへ伝播する
- ✅ **P18-010** — depotからの出庫・service開始・終端・入庫のlifecycleを実装する
- ✅ **P18-011** — Train / Service / Timetable状態をcheckpoint / Save Dataへ含める
- ✅ **P18-012** — Train位置・service・delay・platform stateをProtocol / Serverで配信する
- ✅ **P18-013** — Web ClientでTrainを描画し、駅の発着情報をdebug表示する
- ✅ **P18-014** — 複数列車・複数駅・遅延を含む1運行周期のdeterministic E2Eを追加する
- ✅ **P18-015** — 大規模Train/Service数のtick・routing・block処理benchmarkを記録する
- ✅ **P18-016** — Railway Operationsのspecification / architecture / ROADMAPを同期する

### Phase 18 完了条件

- TrainがTimetableに基づいて駅間を走行・停車し、block競合を起こさない。
- delay・platform・service stateを保存復元・配信できる。
- 同一seed / timetableで再現可能な運行結果を得られる。

### Phase 18 closeout

- `Phase 18 Railway Operations E2E` run `33318887371`: 実Server→WebSocket→headless browserでProtocol 2.7をnegotiationし、2 Train / 2 Service / 2 Station / 2 Platformについて移動、Platform割当、dwell、delayを観測した。両Serviceは完了し、delayは276 / 717 tickだった。
- `Phase 18 Railway Operations Benchmark` run `33318887363`: 100 / 1,000 Train・Serviceのfixed tickとsnapshotをShortRunで計測した。基準値は [`docs/development/railway-operations-benchmark.md`](docs/development/railway-operations-benchmark.md) を正本とする。
- Phase 18のProtocol / Save / Web / E2E / benchmarkを含む最終検証はPR #131を統合単位とする。

---

## Phase 19 — Multimodal Transit

> **状態: ✅ 完了**
> **依存:** Phase 14 / 16 / 18
> 徒歩・自動車・Bus・Taxi・Railwayを共通Tripとして組み合わせ、公共交通を含む移動を成立させる。

- ✅ **P19-001** — Transit Stop / Line / Service pattern / Trip legの共通契約を仕様化する
- ✅ **P19-002** — Bus stopとRoad Laneの接続モデルを実装する
- ✅ **P19-003** — Bus service / stop sequence / timetableの最小モデルを実装する
- ✅ **P19-004** — Bus VehicleをRoad Trafficへ接続し、停留所停車・dwellを実装する
- ✅ **P19-005** — Taxi Vehicle / request / pickup / drop-offの状態モデルを実装する
- ✅ **P19-006** — Taxi requestをVehicleへ割り当てる最小dispatch policyを実装する
- ✅ **P19-007** — 徒歩・Bus・Railwayを組み合わせるmultimodal journey graphを構築する
- ✅ **P19-008** — transfer timeとaccess/egress walkingを含むjourney planningを実装する
- ✅ **P19-009** — Population Trip Requestから利用可能modeを選ぶ最小mode-choice policyを実装する
- ✅ **P19-010** — waiting / boarding / riding / transfer / alightingのPassenger state machineを実装する
- ✅ **P19-011** — Multimodal transit stateをcheckpoint / Save Dataへ含める
- ✅ **P19-012** — Transit line / realtime vehicle / arrival estimateをProtocol / Serverで配信する
- ✅ **P19-013** — Web Clientでroute・stop・vehicle・arrival情報をdebug表示する
- ✅ **P19-014** — 徒歩→鉄道→徒歩、Bus、Taxiを含むTripを実Server→Browserで検証するE2Eを追加する
- ✅ **P19-015** — journey planning / transfer / dispatchのbenchmarkを記録する
- ✅ **P19-016** — Multimodal Transitのspecification / architecture / ROADMAPを同期する

### Phase 19 完了条件

- Personが単一交通modeへ固定されず、徒歩・道路・公共交通を組み合わせて目的地へ移動できる。
- BusとTaxiが既存Road Trafficを再利用し、鉄道も共通Journeyへ統合される。
- transferを含むTripを保存復元して継続できる。

### Phase 19 closeout evidence

- Simulation / Save: BusとTaxiはRoad Trafficを再利用し、Railway Serviceを共通Journeyへ投影する。transfer中Passengerのcheckpoint / Save Format 10 continuationを検証する。
- Protocol / Web: Protocol 2.8 message 720でLine / Stop / Pattern / realtime Bus・Taxi / arrival estimateを配信し、Transit Debugへ表示する。
- E2E: `Phase 19 Multimodal Transit E2E` run `33337277275`で実Server→WebSocket→headless browserを検証し、Bus / Taxi / Railway snapshot、Road-backed Bus movement、arrival estimate / Transit Debug表示を確認した。
- Benchmark: `Phase 19 Multimodal Transit Benchmark` run `33337277231`でjourney planning / nearest Taxi dispatch / transfer checkpoint continuationをShortRun計測した。baselineは[`docs/development/multimodal-transit-benchmark.md`](docs/development/multimodal-transit-benchmark.md)を正本とする。
- CI run `33337277276`を含む検証が成功し、PR #132として`develop`へ統合済み。Phase 19を正式closeoutする。

---

## Phase 20 — Server Administration Console

> **状態: ✅ 完了**
> **依存:** Phase 4 / 8 / 19
> Headless Serverの標準入力から、Simulationの運転制御・状態確認・主要Entityの追加/更新/削除・保存操作を安全に実行できる管理Consoleと、将来のRemote Admin / City Management UIから再利用できるserver-authoritative command境界を確立する。

- ✅ **P20-001** — Administration Consoleの目的・trust boundary・command grammar・stable result/error code・引数/単位/enum表現を仕様化する
- ✅ **P20-002** — stdin / command parser / bounded AdminCommandQueue / executor / SimulationRuntimeの責務とtick境界をarchitecture文書化し、Remote Adminから再利用する境界をADR化する
- ✅ **P20-003** — `AdminCommand` / `AdminCommandResult` / structured parameterの共通契約を実装し、表示文字列と実行結果を分離する
- ✅ **P20-004** — quoted token・`--option`・Invariant数値・stable ID・enumを扱う`AdminCommandParser`とcommand metadataベースの`help`生成を実装する
- ✅ **P20-005** — bounded single-reader `AdminCommandQueue`と逐次executorを実装し、World mutationを`SimulationRuntime`のauthoritative境界で安全に適用する
- ✅ **P20-006** — stdinを読むoptional `ServerConsoleService`をHostedServiceとして実装し、無効化設定・EOF・cancellation・graceful shutdownを扱う
- ✅ **P20-007** — `help` / `status` / `version` / `exit` / `server stop`の基本管理commandを実装する
- ✅ **P20-008** — `simulation status` / `pause` / `resume` / paused時の`step [count]`を実装し、automatic tickとmanual stepの順序をdeterministicにする
- ✅ **P20-009** — Agentの`list` / `show` / `add` / `update` / `remove`と位置・速度更新の正式Simulation APIを実装する
- ✅ **P20-010** — Buildingの`list` / `show` / `add` / `update` / `remove`とPOI・Road Access・Population参照を壊さない整合性検証を実装する
- ✅ **P20-011** — POIの`list` / `show` / `add` / `update` / `remove`とBuilding境界・参照整合性検証を実装する
- ✅ **P20-012** — Road Node / Segment / Laneの`list` / `show` / `add` / `update` / `remove` commandを既存Road Network mutation APIへ接続する
- ✅ **P20-013** — Lane Connection / Road Access Pointの`list` / `show` / `add` / `update` / `remove` commandを実装する
- ✅ **P20-014** — runtime Road topology変更時にServer read model revisionを単調増加させ、接続済みClientへ最新topologyが再配信されるinvalidate契約を実装する
- ✅ **P20-015** — Vehicleの`list` / `show` / `remove`と、Routing結果を必須にする安全な`spawn` command契約を実装する
- ✅ **P20-016** — Railway InfrastructureのNode / Segment / Connection / Block / Station / Platform / Access / Depotにread commandと既存Create APIを使うadd commandを実装する
- ✅ **P20-017** — Railway Infrastructureのupdate / removeに必要な正式Simulation APIと参照整合性validationを追加し、Console commandへ公開する
- ✅ **P20-018** — Train / Formation / Railway Route / Timetable / Serviceのread commandを実装し、既存Simulation APIで安全に表現できる生成操作だけを管理commandとして公開する
- ✅ **P20-019** — Server connectionの`list` / `show` / `disconnect` commandを実装し、Simulation Entity操作とconnection管理をnamespaceで分離する
- ✅ **P20-020** — `world save <path>`をcheckpoint captureとfile I/Oに分離して実装し、Simulation lock中の長時間I/Oを避ける
- ✅ **P20-021** — runtime `world load <path>`のWorld差し替え、known entity state、Road/Railway revision、publish read modelのinvalidate契約を設計・実装する
- ✅ **P20-022** — malformed input・unknown command・invalid enum/number・missing entity・reference conflict・queue full・invalid simulation stateをServer停止へ波及させないnegative testを追加する
- ✅ **P20-023** — parser / queue FIFO / executor / pause-step-resume / Entity mutation / Saveのunit・integration testを追加する
- ✅ **P20-024** — stdin→AdminCommandQueue→SimulationRuntime→publishまでを実Serverで検証し、pause中編集とresume後のClient反映を確認するE2Eを追加する
- ✅ **P20-025** — Administration Consoleのspecification / architecture / ADR / Server README / ROADMAPを同期する

### Phase 20 完了条件

- Headless Serverの標準入力から主要Simulation Entityを調査・追加・更新・削除でき、すべてのmutationがSimulationの正式APIとauthoritative command境界を通る。
- `pause` / `step` / `resume`を含むcommand順序が再現可能で、Console入力がSimulation tick途中の半更新状態へ直接割り込まない。
- Road / Railway等のtopology変更やWorld load後にServer側read modelとClient配信状態が正しくinvalidateされ、変更が接続済みClientへ反映される。
- 不正command・参照制約違反・stdin EOF・Console無効化・graceful shutdownでServer全体を不必要に停止させない。
- command実行契約がstdin固有実装から分離され、将来のRemote Admin / City Management UIから再利用できる。

### Phase 20 closeout evidence

- CI run `33384160233`: repository / .NET build・test / Web lint・typecheck・test・build / CI gateが成功した。
- End-to-end run `33384160236`: Phase 6〜19の既存Server→Browser回帰と、`administration-console-server-browser`が成功した。
- Benchmarks run `33384160259`が成功した。
- Dependency Review run `33384160232`が成功した。
- PR #162を`develop`へ統合済み（merge commit `62f2aee99b4be76edeef5f6cc4d88d178e25483b`）。Phase 20を正式closeoutする。

---

## Phase 21 — Industry / Jobs / Economy

> **状態: ✅ 完了**
> **依存:** Phase 15 / 19
> 企業・職場・雇用・所得・生産・消費の最小循環を作り、都市活動へ経済的な理由を与える。

- ✅ **P21-001** — Company / Establishment / Job / Economic Actorの責務とstable IDを仕様化する
- ✅ **P21-002** — Company / EstablishmentをBuilding / POIへ配置できるモデルを実装する
- ✅ **P21-003** — Job position・必要worker数・wageの最小モデルを実装する
- ✅ **P21-004** — PersonとJobを結ぶemployment stateを実装する
- ✅ **P21-005** — residenceとworkplaceから通勤activity / Trip需要を生成する
- ✅ **P21-006** — Household income / cash balanceの最小stateを実装する
- ✅ **P21-007** — Company cash balance / revenue / expenseの最小stateを実装する
- ✅ **P21-008** — Industry sectorと簡易production capacityを実装する
- ✅ **P21-009** — Householdの基本消費需要とCommercial POIでの支出を実装する
- ✅ **P21-010** — wage支払と消費による最小economic cycleを固定tick上で実装する
- ✅ **P21-011** — Economy stateをcheckpoint / Save Dataへ含める
- ✅ **P21-012** — employment / income / company / productionの集計statisticsをServer配信可能にする
- ✅ **P21-013** — Web ClientでCompany / Householdと経済統計をdebug表示する
- ✅ **P21-014** — economic cycleのdeterministic integration testを追加する
- ✅ **P21-015** — Economyのtick / snapshot benchmarkを記録する
- ✅ **P21-016** — Economyのspecification / ROADMAPを同期する

### Phase 21 完了条件

- HouseholdとCompanyの間に雇用・賃金・消費による最小循環が存在する。
- 通勤需要がPopulation / Transitへ自然に接続される。
- 経済状態がstable IDとraw valueで保存され、locale依存文言を持たない。

### Phase 21 closeout evidence

- Simulation: Company / Establishment / Job / Employment / Household economyをstable IDで保持し、production・wage・consumptionのdeterministic economic cycleをfixed tickへ統合した。Employment済みPersonのworkplaceを既存Population / Trip plannerへ接続した。
- Persistence: Economy checkpointをSave Dataへ統合し、Save Format 11として保存・復元する。旧formatは後方互換で読み込める。
- Protocol / Server / Web: Protocol 2.10 `EconomySnapshot`を追加し、Serverから集計・bounded debug entryを配信、Web ClientでdecodeしてEconomy Debugへ表示する。
- Tests / E2E: simulation / persistence / protocol / Web testsに加え、`economy-employment-server-browser`を含むEnd-to-end run `33442541636`が全11 scenario成功した。
- Benchmark: run `33442541628`が全適用job成功し、Economy BenchmarkDotNet coverageを含む。
- CI run `33442541670`、Dependency Review run `33442541634`が成功した。
- [`docs/specifications/economy.md`](docs/specifications/economy.md)を追加し、Phase 21の正本仕様とする。
- PR #163を`develop`へ統合済み（merge commit `464f2a2900ac3d93d7674d3458aa469262cc4f0a`）。Phase 21を正式closeoutする。

---

## Phase 22 — Logistics / Freight

> **状態: ✅ 完了**
> **依存:** Phase 13 / 21
> 生産・在庫・注文・Shipment・Freight Vehicleを接続し、都市内物流をSimulationする。

- ✅ **P22-001** — Commodity / Inventory / Order / Shipmentの正本契約を仕様化する
- ✅ **P22-002** — Establishmentごとのinventoryとcapacityを実装する
- ✅ **P22-003** — production / consumptionから補充Orderを生成する最小ruleを実装する
- ✅ **P22-004** — OrderをShipmentへまとめるallocation policyを実装する
- ✅ **P22-005** — Warehouse / loading point / delivery pointをUrban Worldへ接続する
- ✅ **P22-006** — Freight VehicleをRoad Trafficへ接続する
- ✅ **P22-007** — pickup / loading / transit / unloading / deliveredのShipment state machineを実装する
- ✅ **P22-008** — Freight routeと配送順序をRoutingへ接続する
- ✅ **P22-009** — 渋滞・配送遅延がinventoryへ影響する最低限の連携を実装する
- ✅ **P22-010** — Logistics stateをcheckpoint / Save Dataへ含める
- ✅ **P22-011** — Shipment / inventory / freight statisticsをProtocol / Serverで配信する
- ✅ **P22-012** — Web ClientでFreight Vehicle / Shipment / inventoryをdebug表示する
- ✅ **P22-013** — 生産→配送→在庫補充を実Server→Browserで検証するE2Eを追加する
- ✅ **P22-014** — 大規模Shipment / Inventoryのtick・routing・memory benchmarkを記録する
- ✅ **P22-015** — Logistics / Freightのspecification / architecture / ROADMAPを同期する

### Phase 22 完了条件

- 生産側の物資がShipmentとして道路網を移動し、需要側inventoryへ到着する。
- FreightがRoad Trafficの渋滞を共有し、配送遅延が観測できる。
- Logistics stateを保存復元して継続できる。

### Phase 22 closeout evidence

- Simulation: `Commodity` / `Inventory` / `LogisticsOrder` / `Shipment`をstable IDで保持し、Company production deltaを同一Company内で1回だけSupplier群へ配分する。未完了Orderは`(EstablishmentId, CommodityId)`のactive indexで管理し、ShipmentはPickup→Loading→InTransit→Unloading→Deliveredを遷移する。
- Road Traffic / Routing: Freight Vehicleは既存Road Routing / VehicleStoreを再利用し、Arrived観測後にresident Vehicleを解放する。Shipmentにはhistorical `VehicleId`を残し、配送履歴の増加がRoad Traffic stateを無制限に増やさない。
- Persistence: Economy checkpoint配下のoptional Logistics stateとしてSave Format 11へ後方互換追加した。Logistics配列はDTO materialization前のstreaming scanと復元後validationの両方でboundedに検証する。
- Protocol / Server / Web: Protocol 2.11 `LogisticsSnapshot` (`MessageType 740`)を追加し、Serverはactive Shipmentを優先したbounded debug entryを配信、Web ClientでInventory / Shipment / Freight Vehicle ID / delayをdebug表示する。
- Tests: 同一Company複数Supplierの生産量重複防止、Delivered後のFreight Vehicle解放、Save pre-materialization limit、256件超Shipment historyでのactive debug selectionを含む回帰testを追加した。
- CI: review対応コードを含むCI run `33450375345`がrepository / .NET build・test / Web lint・typecheck・test・build / CI gateまで成功した。benchmark fixture更新後もCI run `33450666762`でbuild / test成功を確認した。
- E2E: End-to-end run `33450666785`でPhase 22 Logisticsを含む既存Server→Browser回帰が成功した。benchmark fixture更新はE2E runtime契約を変更しない。
- Benchmark: Benchmarks run `33450666769`の`logistics-inventory-100-1000`で100 / 1,000 Inventory・Shipment historyのTick / RoutingBatch / Snapshotを全6ケース計測した。baselineは[`docs/development/logistics-freight-benchmark.md`](docs/development/logistics-freight-benchmark.md)を正本とする。
- Dependency Review: run `33451262959`が成功した。
- Specification / Architecture: [`docs/specifications/logistics-freight.md`](docs/specifications/logistics-freight.md)、[`docs/architecture/logistics-freight.md`](docs/architecture/logistics-freight.md)を正本とする。
- PR #165を`develop`へ統合済み（merge commit `002009d74af79a3bbeaf02675bee9d631013e7a3`）。Phase 22を正式closeoutする。

---

## Phase 23 — Power Infrastructure

> **状態: ⬜ 未着手**
> **依存:** Phase 10 / 21
> 発電・送配電・需要を都市Entityと接続し、電力供給状態をSimulationへ導入する。標準Simulationは接続・capacity・需要による簡易計算とし、高精度な潮流・電圧等の物理計算は交換可能なsolver境界の外側へ分離する。

- ⬜ **P23-001** — Generator / Substation / PowerLine / Loadの正本契約を仕様化する
- ⬜ **P23-002** — PowerNode / PowerLine topologyとstable IDを実装する
- ⬜ **P23-003** — Generator capacity / output / operating stateの最小モデルを実装する
- ⬜ **P23-004** — Building / EstablishmentをPower Loadへ関連付ける契約を実装する
- ⬜ **P23-005** — 時刻・用途・activityからload demandを計算する最小ruleを実装する
- ⬜ **P23-006** — network接続とcapacityを考慮する交換可能な簡易power balance / dispatch solver境界を実装する
- ⬜ **P23-007** — insufficient supply時のunserved demand / outage stateを実装する
- ⬜ **P23-008** — outageをBuilding / Industryの稼働状態へ反映する最小連携を実装する
- ⬜ **P23-009** — Power stateをcheckpoint / Save Dataへ含める
- ⬜ **P23-010** — Power topology / supply / demand / outageをProtocol / Serverで配信する
- ⬜ **P23-011** — Web ClientでPower networkと供給状態をdebug可視化する
- ⬜ **P23-012** — 需要変動・generator停止・outage復旧を検証するdeterministic E2Eを追加する
- ⬜ **P23-013** — 大規模Power node/loadのtick・topology benchmarkを記録する
- ⬜ **P23-014** — Power Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 23 完了条件

- 都市のBuilding / Industryに電力需要があり、発電・network capacityに応じて供給状態が変化する。
- outageを保存・配信・可視化できる。
- Power Simulationが他domainと疎結合な明確な境界を持つ。
- 標準の簡易solverを維持したまま、将来のExtensionが詳細な物理solverを提供できる責務境界が存在する。

---

## Phase 24 — Water & Sewer Infrastructure

> **状態: ⬜ 未着手**
> **依存:** Phase 10 / 21 / 23
> 上水道と下水道の3D topology、需要・排水、施設capacity、供給/処理状態を都市Entityへ接続する。標準Simulationは接続とcapacity中心の簡易計算とし、水圧・流量・管内流等の詳細水理計算は交換可能なsolver境界の外側へ分離する。

- ⬜ **P24-001** — Water / Sewerの責務、単位、流向、簡易solverと高精度水理solverの境界を仕様化する
- ⬜ **P24-002** — WaterNode / WaterPipe / SewerNode / SewerPipeのstable IDと3D topologyを実装する
- ⬜ **P24-003** — WaterSource / Reservoir / Pump / SewageTreatmentPlantのcapacity・operating state最小モデルを実装する
- ⬜ **P24-004** — Building / EstablishmentをWater / Sewer service pointへ関連付ける契約を実装する
- ⬜ **P24-005** — Building用途・Population / Industry activityからwater demandとwastewater generationを計算する最小ruleを実装する
- ⬜ **P24-006** — network接続とcapacityを考慮する交換可能な簡易Water Supply solverを実装する
- ⬜ **P24-007** — treatment到達性とnetwork capacityを考慮する交換可能な簡易Sewer solverを実装する
- ⬜ **P24-008** — unserved water / sewer unavailable / overflow等のservice stateを実装する
- ⬜ **P24-009** — pump / treatment facilityの停止や停電をBuilding / Industryのservice stateへ反映する最小連携を実装する
- ⬜ **P24-010** — Water / Sewer topologyの3D spatial queryと参照整合性validationを実装する
- ⬜ **P24-011** — Water / Sewer stateをcheckpoint / Save Dataへ含める
- ⬜ **P24-012** — Water / Sewer topology・demand・capacity・service stateをProtocol / Serverで配信する
- ⬜ **P24-013** — Web Clientで配管・施設・供給/排水状態をdebug可視化する
- ⬜ **P24-014** — 需要変動・施設停止・network切断・復旧を検証するdeterministic E2Eを追加する
- ⬜ **P24-015** — 大規模Water / Sewer node・pipe・loadのtick・topology benchmarkを記録する
- ⬜ **P24-016** — Water & Sewer Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 24 完了条件

- Building / Industryが上水道と下水道へ接続され、需要・排水量とnetwork / facility capacityに応じてservice stateが変化する。
- Water / Sewer topologyとservice stateを保存・配信・可視化できる。
- 標準Simulationの完了に詳細な水圧・流量・管内流計算を要求せず、将来のExtensionが高精度solverを差し替えられる境界を持つ。

---

## Phase 25 — Gas Infrastructure

> **状態: ⬜ 未着手**
> **依存:** Phase 10 / 21 / 22 / 23
> 配管によるガス供給と、LPガス等を想定した物流による配達供給を同じ都市需要へ接続する。標準の配管Simulationは接続・capacity中心とし、圧力・流量等の詳細物理計算は交換可能なsolver境界の外側へ分離する。

- ⬜ **P25-001** — Pipeline Gas / Delivered Gasの責務、単位、需要・在庫・簡易solver境界を仕様化する
- ⬜ **P25-002** — GasNode / GasPipe topologyとstable IDを実装する
- ⬜ **P25-003** — GasSource / Storage / Regulatorのcapacity・operating state最小モデルを実装する
- ⬜ **P25-004** — Building / EstablishmentをGas Loadへ関連付け、Pipeline / Delivered供給方式を表す契約を実装する
- ⬜ **P25-005** — Building用途・Population / Industry activityからgas demandを計算する最小ruleを実装する
- ⬜ **P25-006** — network接続とcapacityを考慮する交換可能な簡易Pipeline Gas solverを実装する
- ⬜ **P25-007** — insufficient supply / pipe cut / facility停止時のunserved demand / outage stateを実装する
- ⬜ **P25-008** — Delivered Gas向けBuilding / Establishment storage・inventory・capacityモデルを実装する
- ⬜ **P25-009** — Delivered Gas inventory閾値から補充Orderを生成する最小ruleを実装する
- ⬜ **P25-010** — Delivered Gasの補充を既存Logistics / Freightへ接続し、積載・道路輸送・配送・在庫補充を再利用する
- ⬜ **P25-011** — Gas topologyの3D spatial queryと参照整合性validationを実装する
- ⬜ **P25-012** — Pipeline / Delivered Gas stateをcheckpoint / Save Dataへ含める
- ⬜ **P25-013** — Gas topology・demand・inventory・shipment・service stateをProtocol / Serverで配信する
- ⬜ **P25-014** — Web ClientでGas pipe・施設・配送在庫・供給状態をdebug可視化する
- ⬜ **P25-015** — pipe供給と配送供給の需要・障害・在庫切れ・復旧を検証するdeterministic E2Eを追加する
- ⬜ **P25-016** — 大規模Gas node/loadとDelivered Gas inventory / Shipmentのtick・topology benchmarkを記録する
- ⬜ **P25-017** — Gas Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 25 完了条件

- Pipeline Gasはnetwork接続とcapacityによりBuilding / Industryへ供給され、供給不足や切断をservice stateとして観測できる。
- Delivered Gasは既存Logisticsを再利用して道路輸送され、需要側storage / inventoryを補充できる。
- 配管の詳細な圧力・流量計算を標準完了条件に含めず、将来のExtensionが高精度solverを差し替えられる。

---

## Phase 26 — Optical Communication Infrastructure

> **状態: ⬜ 未着手**
> **依存:** Phase 10 / 21 / 23
> 光ファイバーを中心とする固定通信のphysical topology、access、traffic demand、bandwidth、congestion、障害を都市Entityへ接続する。標準Simulationはroutingとcapacity中心とし、光損失・分散等の詳細伝送計算は交換可能なsolver境界の外側へ分離する。

- ⬜ **P26-001** — Optical Communicationの責務、traffic / bandwidth単位、簡易solverと詳細光伝送solverの境界を仕様化する
- ⬜ **P26-002** — OpticalNode / FiberLinkのstable IDと3D topologyを実装する
- ⬜ **P26-003** — Exchange / CoreGateway / AggregationNode / AccessNodeの最小Infrastructureモデルを実装する
- ⬜ **P26-004** — Building / Establishmentをfixed communication accessへ関連付ける契約を実装する
- ⬜ **P26-005** — Building用途・Population / Industry activityからcommunication traffic demandを計算する最小ruleを実装する
- ⬜ **P26-006** — topology routingとbottleneck capacityを考慮する交換可能な簡易Optical Network solverを実装する
- ⬜ **P26-007** — capacity超過時のcongestion・available bandwidth・簡易latency stateを実装する
- ⬜ **P26-008** — Fiber cut・node停止・停電による通信outageと復旧を実装する
- ⬜ **P26-009** — 将来のRadio Site / Base Station等がbackhaulとしてOptical Networkへ接続できる参照境界を実装する
- ⬜ **P26-010** — Optical topologyの3D spatial queryと参照整合性validationを実装する
- ⬜ **P26-011** — Optical Communication stateをcheckpoint / Save Dataへ含める
- ⬜ **P26-012** — Optical topology・traffic・capacity・congestion・outageをProtocol / Serverで配信する
- ⬜ **P26-013** — Web ClientでFiber / node / access / congestion / outageをdebug可視化する
- ⬜ **P26-014** — traffic増加・Fiber cut・停電・backhaul復旧を検証するdeterministic E2Eを追加する
- ⬜ **P26-015** — 大規模Optical node/link/loadのrouting・tick・topology benchmarkを記録する
- ⬜ **P26-016** — Optical Communication Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 26 完了条件

- Building / IndustryがOptical Networkへ接続され、traffic demandとlink / node capacityに応じてbandwidth・congestion・outage stateが変化する。
- Radio等の後続domainがbackhaulとして参照できる安定した通信Infrastructure境界を持つ。
- 詳細な光伝送物理を標準完了条件に含めず、将来のExtensionが高精度solverを差し替えられる。

---

## Phase 27 — Radio & Spectrum Foundation

> **状態: ⬜ 未着手**
> **依存:** Phase 10 / 23 / 26
> LTE等の特定通信方式へ依存しないRadio / Spectrumの共通基盤を作り、周波数・送受信機・アンテナ・伝搬・干渉を都市の3D空間上で扱えるようにする。標準Simulationは軽量な簡易伝搬を用い、詳細な電磁界・ray tracing等は交換可能なsolver境界の外側へ分離する。

- ⬜ **P27-001** — Radio / Spectrum Foundationの用途非依存責務、単位、determinism、solver境界を仕様化する
- ⬜ **P27-002** — SpectrumBand / RadioChannelと周波数・bandwidth・overlapのstable契約を実装する
- ⬜ **P27-003** — RadioSite / Transmitter / Receiver / Antenna / Emissionのstable IDとstateモデルを実装する
- ⬜ **P27-004** — Antennaの3D position・orientation・gain・簡易radiation pattern契約を実装する
- ⬜ **P27-005** — Transmissionのfrequency・bandwidth・transmit power・operating stateを実装する
- ⬜ **P27-006** — Receiverの受信帯域・sensitivityと送受信候補を評価する共通契約を実装する
- ⬜ **P27-007** — Radio Foundationから独立して差し替え可能な`IRadioPropagationSolver`相当のsolver境界を実装する
- ⬜ **P27-008** — 距離・周波数・送信電力・antenna gainからreceived powerを求める軽量な標準propagation solverを実装する
- ⬜ **P27-009** — Building `WorldVolume`を使うLoS / NLoS・簡易obstruction / penetration penaltyを実装する
- ⬜ **P27-010** — 周波数帯域が重なるEmissionを候補化する簡易interference計算を実装する
- ⬜ **P27-011** — received power・noise / interference・SINR等の用途非依存Radio Link resultを実装する
- ⬜ **P27-012** — 大量Transmitterを全件走査しない3D spatial index / candidate queryを実装する
- ⬜ **P27-013** — Radio Siteの電力供給とOptical backhaul参照を既存Infrastructureへ接続する
- ⬜ **P27-014** — Radio / Spectrum stateをcheckpoint / Save Dataへ含める
- ⬜ **P27-015** — Radio site・spectrum・emission・coverage / link resultをProtocol / Serverで配信する
- ⬜ **P27-016** — Web ClientでRadio site・antenna・channel・簡易coverage / interferenceをdebug可視化する
- ⬜ **P27-017** — 複数周波数・複数送信源・遮蔽・干渉・停電/backhaul障害を検証するdeterministic E2Eを追加する
- ⬜ **P27-018** — 大規模Transmitter / Receiver / spectrum query / propagationのbenchmarkを記録する
- ⬜ **P27-019** — Radio & Spectrum Foundationのspecification / architecture / ROADMAPを同期する

### Phase 27 完了条件

- LTE / 5G / Wi-Fi / Broadcast等の個別方式をRadio Foundationの正本へ埋め込まず、共通の周波数・送受信・アンテナ・伝搬・干渉結果を扱える。
- 3D World上の位置・建物遮蔽・複数Emissionを考慮した軽量でdeterministicな標準Radio Simulationが成立する。
- 詳細なreflection / diffraction / multipath / terrain / material / ray tracing等を標準完了条件に含めず、将来のExtensionが高精度propagation solverを差し替えられる。

---

## Phase 28 — Urban Growth & City Generation

> **状態: ⬜ 未着手**
> **依存:** Phase 10〜19 / 21〜27の主要都市モデル
> Parcel / Zoning / Land Useとdeterministic city generationを導入し、都市を手作業fixtureだけでなく生成・成長させられるようにする。Phase 10から委譲されたParcel / land-useの正本はこのPhaseで導入する。

- ⬜ **P28-001** — Parcel境界・Zone種別・土地利用・占有/development stateの正本契約を仕様化する
- ⬜ **P28-002** — Parcel store / stable ID lifecycleとZone designationを設定するSimulation commandを実装する
- ⬜ **P28-003** — Road access・parcel size・land useからdevelopment suitabilityを評価する
- ⬜ **P28-004** — Zoneに応じたBuilding用途・規模候補を選ぶdevelopment ruleを実装する
- ⬜ **P28-005** — 空ParcelへのBuilding development lifecycleを実装する
- ⬜ **P28-006** — demand変化に応じたredevelopment / vacancyの最小ruleを実装する
- ⬜ **P28-007** — seedからRoad Networkを生成するdeterministic generatorを実装する
- ⬜ **P28-008** — Road NetworkからParcelを生成するdeterministic subdivisionを実装する
- ⬜ **P28-009** — Parcel / ZoneからBuilding / POIを生成するdeterministic generatorを実装する
- ⬜ **P28-010** — 初期Population / Household / Jobを生成都市へ配置するseeding処理を実装する
- ⬜ **P28-011** — Railway / Power / Water / Sewer / Gas / Optical / Radio等の既存Infrastructureを壊さないgeneration constraintを定義する
- ⬜ **P28-012** — Parcel / Zone / city generation設定・seed・生成結果をSave / checkpoint契約へ統合する
- ⬜ **P28-013** — Parcel / Zone / development stateをProtocol / Serverで配信し、Web Clientで可視化する
- ⬜ **P28-014** — 同一seedで同一都市を生成するreproducibility E2Eを追加する
- ⬜ **P28-015** — 小/中/大規模都市generation時間・memory・初期Simulation負荷benchmarkを記録する
- ⬜ **P28-016** — Urban Growth / City Generationのspecification / architecture / ROADMAPを同期する

### Phase 28 完了条件

- Parcel / Zone / land-useがSimulation正本として存在し、Zone指定からBuilding developmentへ状態が遷移できる。
- 同一seed・設定から同一のRoad / Parcel / Buildingを再生成できる。
- Parcel / Zone状態を保存・配信・可視化できる。

---

## Phase 29 — City Management UI

> **状態: ⬜ 未着手**
> **依存:** Phase 28
> Browserから都市状態を調査・編集・管理するためのserver-authoritative UIとcommand境界を整える。

- ⬜ **P29-001** — Build / Edit commandの認可・validation・ack/error契約を仕様化する
- ⬜ **P29-002** — Protocolへserver-authoritative command request / resultの共通枠組みを追加する
- ⬜ **P29-003** — Web Clientで3D Entityを選択するpicking / selection基盤を実装する
- ⬜ **P29-004** — Building / Parcel / POI / Person / Vehicle等をServer read modelから表示するInspector基盤を実装する
- ⬜ **P29-005** — Road / Laneのbuild / edit / remove commandとUIを実装する
- ⬜ **P29-006** — Building / POI / Parcel / Zoneのbuild / edit commandとUIを実装する
- ⬜ **P29-007** — Railway track / station / platformのbuild / edit commandとUIを実装する
- ⬜ **P29-008** — Power Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P29-009** — Water / Sewer Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P29-010** — Gas Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P29-011** — Optical Communication Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P29-012** — Radio Site / Antenna / Spectrum設定のbuild / edit commandとUIを実装する
- ⬜ **P29-013** — command失敗時にClient側だけ状態が進まないoptimistic-state禁止またはrollback方針を実装する
- ⬜ **P29-014** — Simulation speed / pause / resume等の運転controlをServer commandとして実装する
- ⬜ **P29-015** — Population / Traffic / Transit / Economy / Logistics / Power / Utility / Communication / RadioのDashboard統計を実装する
- ⬜ **P29-016** — Server configurationの変更可能項目・restart必要項目を分離してUI化する
- ⬜ **P29-017** — current Save formatのsave / load操作をServer経由で実行する管理UIを追加する
- ⬜ **P29-018** — destructive commandのconfirmationとstable error localizationを実装する
- ⬜ **P29-019** — Inspector / build / edit / config / save操作のBrowser E2Eを追加する
- ⬜ **P29-020** — 大規模都市でselection・overlay・dashboardが描画hot pathを阻害しないperformance testを追加する
- ⬜ **P29-021** — City Management UIのarchitecture / UX contract / ROADMAPを同期する

### Phase 29 完了条件

- 都市の主要EntityをBrowserから選択・調査できる。
- build/edit操作は必ずServer-authoritative commandを経由し、Clientだけで正本状態を変更しない。
- 主要statisticsと運転設定を管理UIから確認できる。

---

## Phase 30 — Distribution & Compatibility

> **状態: ⬜ 未着手**
> **依存:** Phase 29
> Save migrationと配布物を整備し、開発環境外でもversion付き成果物として起動・更新・復元できる状態にする。

### Save互換性

- ⬜ **P30-001** — Save migrationのsupport範囲・失敗契約・version policyを仕様化する
- ⬜ **P30-002** — Save formatごとのmigration stepを登録できるframeworkを実装する
- ⬜ **P30-003** — repositoryに旧Save format fixtureを保持し、自動migration testを追加する
- ⬜ **P30-004** — migration中断・unsupported version・破損dataを安全に拒否する
- ⬜ **P30-005** — migration前後でstable IDと継続可能stateを保持するintegration testを追加する

### 配布・Deployment

- ⬜ **P30-006** — Server standalone binaryのsupported OS / architecture matrixを定義する
- ⬜ **P30-007** — Windows / Linux向けServer publish artifactをCIで生成する
- ⬜ **P30-008** — 必要性を検証した上で追加architecture / OS向けartifactを生成する
- ⬜ **P30-009** — Web Client production buildのbase path / Server endpoint設定をdeployment向けに整理する
- ⬜ **P30-010** — static hosting向けWeb Client artifactをCIで生成する
- ⬜ **P30-011** — Server用container imageとruntime configuration契約を実装する
- ⬜ **P30-012** — release artifactへVERSION・commit SHA・license / third-party noticeを同梱する
- ⬜ **P30-013** — release artifactのchecksum / SBOM等、配布時に必要なintegrity metadataを生成する
- ⬜ **P30-014** — package / binary / Web / containerを起動するrelease smoke testをCIへ追加する
- ⬜ **P30-015** — install / upgrade / rollback / backup / restore手順をdocument化する
- ⬜ **P30-016** — develop→main release時のversion / artifact / release note手順を自動化可能な形へ整理する
- ⬜ **P30-017** — Distribution / Compatibilityのarchitecture / development docs / ROADMAPを同期する

### Phase 30 完了条件

- 開発toolchainを手作業構築しなくても、配布artifactからServerとWeb Clientを起動できる。
- 対応対象の旧Save Dataを明示的なmigration経路で読み込める。
- release artifactのversion・commit・license・integrity情報を追跡できる。

---

## Phase 31 — Extension Platform & Localization

> **状態: ⬜ 未着手**
> **依存:** Phase 30
> 正本Simulationと互換性境界を壊さず、外部拡張・高精度solver・追加localeを導入できる公開拡張基盤を作る。

### Extension Platform

- ⬜ **P31-001** — Extension / Modで公開する範囲と非公開内部APIの境界を仕様化する
- ⬜ **P31-002** — Extension manifest・stable ID・version・dependency契約を定義する
- ⬜ **P31-003** — data-only extensionとcode extensionを分離したloading modelを設計する
- ⬜ **P31-004** — code extensionの信頼境界・権限・非sandbox性を明示し、安全なdefault policyを実装する
- ⬜ **P31-005** — Simulationへextension contentとPower / Water / Sewer / Gas / Optical / Radio等のsolver providerを登録するversioned public APIを実装する
- ⬜ **P31-006** — Extension固有Save Dataをnamespace付きで保存し、missing extension時の挙動を定義する
- ⬜ **P31-007** — Protocolへextension固有wire typeを直接衝突させない拡張契約を設計する
- ⬜ **P31-008** — Extensionのload order / dependency cycle / incompatible versionをvalidationする
- ⬜ **P31-009** — Extension packageの開発・test用templateとsample extensionを追加する

### Localization

- ⬜ **P31-010** — `ja-JP`をdefaultにしたlocale discovery / fallback policyを再確認・固定する
- ⬜ **P31-011** — 追加locale resource packを導入できるWeb Client loading境界を実装する
- ⬜ **P31-012** — 数値・日時・単位・plural等のlocale formattingを共通化する
- ⬜ **P31-013** — stable error code / structured parameterから各localeの表示文を生成するcoverageを拡張する
- ⬜ **P31-014** — translation key欠落・未使用key・parameter不一致をCIで検出する
- ⬜ **P31-015** — 少なくとも1つの追加localeで主要UI / Inspector / Dashboard / error表示をE2E確認する

### Closeout

- ⬜ **P31-016** — Extension有無・solver差し替え有無・追加locale有無でSave / Protocol / Simulation determinismが壊れないintegration testを追加する
- ⬜ **P31-017** — Extension loading・solver provider・localizationのstartup / memory costをbenchmarkする
- ⬜ **P31-018** — Extension author guide / solver provider guide / localization guide / compatibility policyを整備する
- ⬜ **P31-019** — architecture / ADR / ROADMAPを同期し、Phase 10〜31で計画した旧Backlogのcloseoutを確認する

### Phase 31 完了条件

- 既存Simulation内部実装へ直接依存せず、versionedな公開境界からExtensionを追加できる。
- 標準の軽量Infrastructure solverを維持したまま、Extensionが高精度な物理solverを安全に差し替えられる。
- Extension固有stateがSave Dataと衝突せず、missing/incompatible extensionを安全に扱える。
- `ja-JP`以外のlocaleを主要UIへ追加でき、Protocol / Save / Simulationへ翻訳済み文言を持ち込まない。

---

## 旧「将来 Backlog」のPhase移行

Phase 9終了時点で列挙していた将来Backlogは、以下の通りPhase 10〜31へ移行した。今後は各Phase内Taskを正本として追跡する。

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
| Server administration / runtime command console | Phase 20 |
| Industry / jobs / economy | Phase 21 |
| Logistics / freight | Phase 22 |
| Power generation / grid / demand | Phase 23 |
| Water supply / sewer network | Phase 24 |
| Pipeline gas / delivered gas | Phase 25 |
| Optical / fixed communication network | Phase 26 |
| Radio / spectrum / propagation foundation | Phase 27 |
| Parcel / zoning / land use | Phase 28 |
| City generation | Phase 28 |
| Inspector / dashboard / statistics UI | Phase 29 |
| Build / edit commands | Phase 29 |
| Server configuration UI | Phase 29 |
| Save migration | Phase 30 |
| Release packaging | Phase 30 |
| Server binary distribution | Phase 30 |
| Web Client deployment | Phase 30 |
| Container image | Phase 30 |
| Mod / extension architecture | Phase 31 |
| High-fidelity infrastructure solver extensions | Phase 31 |
| Additional locales | Phase 31 |

## Phase 9から継続する計画済み項目

Phase 9では「3D座標を正本として扱える基盤」までを完了とし、具体的な物理・地形ルールは後続へ分離していた。Phase 10〜31へ直接割り当てられない項目も消さず、現行Backlogとして保持する。

| Phase 9で非対象とした項目 | 現在の扱い |
| --- | --- |
| 道路・線路・建物ごとの高度制約 | Phase 10 / 11 / 17の3D geometry・topology・validationで扱う |
| 地下・高架を考慮したpathfinding | Phase 12で扱う |
| 旧Save formatから新formatへのmigration | Phase 30で扱う |
| 重力・落下・ジャンプ等の垂直物理 | 継続Backlog（Phase未割当） |
| 飛行・空中移動等のairborne movement | 継続Backlog（Phase未割当） |
| terrain model / terrain collision | 継続Backlog（Phase未割当） |
| ground snapping / surface追従 | 継続Backlog（Phase未割当） |

### 継続Backlog（Phase未割当）

以下は計画済みだが、Phase 10〜31の完了に必須とはしない。着手時に独立Phaseまたは既存Phaseへの追加Taskとして分解する。

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
