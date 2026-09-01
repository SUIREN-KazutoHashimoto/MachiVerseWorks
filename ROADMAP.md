MachiVerseWorks の作業を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** Phase 25 — Gas Infrastructure（実装・検証完了 / `develop` 統合待ち）
> **次の実装タスク:** PR #171 を `develop` へ統合し、統合後に Phase 25 を正式 closeout する

## 全体の現在地

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0〜24 | Foundation / Simulation / Infrastructure | ✅ 完了 |
| 25 | Gas Infrastructure | ▶️ 実装・検証完了 / `develop` 統合待ち |
| 26 | Optical Communication Infrastructure | ⏳ 待機 |
| 27 | Radio & Spectrum Foundation | ⏳ 待機 |
| 28 | Urban Growth & City Generation | ⏳ 待機 |
| 29 | City Management UI | ⏳ 待機 |
| 30 | Distribution & Compatibility | ⏳ 待機 |
| 31 | Extension Platform & Localization | ⏳ 待機 |

Phase 0〜24 の詳細 Task・closeout 証跡・当時の計画状態は、履歴として [`docs/archive/roadmap-through-phase24-closeout.md`](docs/archive/roadmap-through-phase24-closeout.md) に保存しています。Phase 13〜16 の正式 closeout 時点の詳細は [`docs/archive/roadmap-phase13-through-phase16-closeout.md`](docs/archive/roadmap-phase13-through-phase16-closeout.md) も参照してください。

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

この順番は、後続機能が前段の正本モデルを再利用できることを優先する。先行mergeを行っても、Phaseの正式closeout順は依存関係に従う。

---

## Phase 25 — Gas Infrastructure

> **状態: ▶️ 実装・検証完了 / `develop` 統合待ち**
> **依存:** Phase 10 / 21 / 22 / 23
> 配管によるガス供給と、LPガス等を想定した物流による配達供給を同じ都市需要へ接続する。標準の配管Simulationは接続・capacity中心とし、圧力・流量等の詳細物理計算は交換可能なsolver境界の外側へ分離する。

- ✅ **P25-001** — Pipeline Gas / Delivered Gasの責務、単位、需要・在庫・簡易solver境界を仕様化する
- ✅ **P25-002** — GasNode / GasPipe topologyとstable IDを実装する
- ✅ **P25-003** — GasSource / Storage / Regulatorのcapacity・operating state最小モデルを実装する
- ✅ **P25-004** — Building / EstablishmentをGas Loadへ関連付け、Pipeline / Delivered供給方式を表す契約を実装する
- ✅ **P25-005** — Building用途・Population / Industry activityからgas demandを計算する最小ruleを実装する
- ✅ **P25-006** — network接続とcapacityを考慮する交換可能な簡易Pipeline Gas solverを実装する
- ✅ **P25-007** — insufficient supply / pipe cut / facility停止時のunserved demand / outage stateを実装する
- ✅ **P25-008** — Delivered Gas向けBuilding / Establishment storage・inventory・capacityモデルを実装する
- ✅ **P25-009** — Delivered Gas inventory閾値から補充Orderを生成する最小ruleを実装する
- ✅ **P25-010** — Delivered Gasの補充を既存Logistics / Freightへ接続し、積載・道路輸送・配送・在庫補充を再利用する
- ✅ **P25-011** — Gas topologyの3D spatial queryと参照整合性validationを実装する
- ✅ **P25-012** — Pipeline / Delivered Gas stateをcheckpoint / Save Dataへ含める
- ✅ **P25-013** — Gas topology・demand・inventory・shipment・service stateをProtocol / Serverで配信する
- ✅ **P25-014** — Web ClientでGas pipe・施設・配送在庫・供給状態をdebug可視化する
- ✅ **P25-015** — pipe供給と配送供給の需要・障害・在庫切れ・復旧を検証するdeterministic E2Eを追加する
- ✅ **P25-016** — 大規模Gas node/loadとDelivered Gas inventory / Shipmentのtick・topology benchmarkを記録する
- ✅ **P25-017** — Gas Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 25 完了条件

- ✅ Pipeline Gasはnetwork接続とcapacityによりBuilding / Industryへ供給され、供給不足や切断をservice stateとして観測できる。
- ✅ Delivered Gasは既存Logisticsを再利用して道路輸送され、需要側storage / inventoryを補充できる。
- ✅ 配管の詳細な圧力・流量計算を標準完了条件に含めず、将来のExtensionが高精度solverを差し替えられる。
- ✅ Pipeline / Delivered Gas stateをSave Format 11のoptional checkpointとして保存・復元できる。
- ✅ Protocol 2.14 / Server / Web debug / benchmark / deterministic E2EでGas状態を検証できる。

### Phase 25 実装状況

- PR #171 で実装・検証を完了し、`develop` 統合待ち。
- Pipeline outage / recovery、Delivered Gas stockout / Shipment / replenishment / recoveryをE2Eで検証する。
- Delivered Gas checkpointは参照先Gas commodityの`Consumer` inventory存在を復元時に検証する。
- `IGasSupplySolver` の結果はWorld stateへ適用する前に、未知・重複ID、非有限値、負値、request上限超過を拒否する。
- Phase正式closeoutはPR #171を`develop`へ統合し、merge commitと最終runを記録した後に行う。

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
