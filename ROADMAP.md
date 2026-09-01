MachiVerseWorks の作業を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** Phase 27 — Remote Administration & MCP Integration  
> **次の実装タスク:** `P27-015` — Remote MCP Client→HTTPS reverse proxy→`/mcp`→Admin command境界→SimulationRuntimeまでを実Serverで検証するE2Eを追加する

## 全体の現在地

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0〜26 | Foundation / Simulation / Infrastructure | ✅ 完了 |
| 27 | Remote Administration & MCP Integration | 🚧 実装中（PR #176 / closeout待ち） |
| 28 | Radio & Spectrum Foundation | ⏳ 待機 |
| 29 | World & Physical Environment Generation | ⏳ 待機 |
| 30 | Regional & Urban Generation | ⏳ 待機 |
| 31 | City Management UI | ⏳ 待機 |
| 32 | Distribution & Compatibility | ⏳ 待機 |
| 33 | Extension Platform & Localization | ⏳ 待機 |

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
  -> Remote Administration / MCP
  -> Radio / Spectrum Foundation
  -> World / Physical Environment Generation
  -> Regional / Urban Generation
  -> City Management UI
  -> Distribution / Compatibility
  -> Extension Platform / Localization
```

この順番は、後続機能が前段の正本モデルを再利用できることを優先する。先行mergeを行っても、Phaseの正式closeout順は依存関係に従う。Phase 27 は Server 横断の Remote Administration 境界として実装順に挿入するが、Phase 28 以降の Simulation domain が MCP 実装へ直接依存することを意味しない。

Phase 28 完了後は、Phase 0〜28 の詳細・closeout証跡を `docs/archive/` へ退避し、現行ROADMAPをPhase 29以降中心へ整理する。

---

## Phase 25 — Gas Infrastructure

> **状態: ✅ 完了**  
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

- PR #171 を `develop` へ統合済み（merge commit `2563b595a61a8639767282e959f77ea0c9096ad4`）。
- Pipeline outage / recovery、Delivered Gas stockout / Shipment / replenishment / recoveryをE2Eで検証する。
- Delivered Gas checkpointは参照先Gas commodityの`Consumer` inventory存在を復元時に検証する。
- `IGasSupplySolver` の結果はWorld stateへ適用する前に、未知・重複ID、非有限値、負値、request上限超過を拒否する。

---

## Phase 26 — Optical Communication Infrastructure

> **状態: ✅ 完了**  
> **依存:** Phase 10 / 21 / 23  
> 光ファイバーを中心とする固定通信のphysical topology、access、traffic demand、bandwidth、congestion、障害を都市Entityへ接続する。標準Simulationはroutingとcapacity中心とし、光損失・分散等の詳細伝送計算は交換可能なsolver境界の外側へ分離する。

- ✅ **P26-001** — Optical Communicationの責務、traffic / bandwidth単位、簡易solverと詳細光伝送solverの境界を仕様化する
- ✅ **P26-002** — OpticalNode / FiberLinkのstable IDと3D topologyを実装する
- ✅ **P26-003** — Exchange / CoreGateway / AggregationNode / AccessNodeの最小Infrastructureモデルを実装する
- ✅ **P26-004** — Building / Establishmentをfixed communication accessへ関連付ける契約を実装する
- ✅ **P26-005** — Building用途・Population / Industry activityからcommunication traffic demandを計算する最小ruleを実装する
- ✅ **P26-006** — topology routingとbottleneck capacityを考慮する交換可能な簡易Optical Network solverを実装する
- ✅ **P26-007** — capacity超過時のcongestion・available bandwidth・簡易latency stateを実装する
- ✅ **P26-008** — Fiber cut・node停止・停電による通信outageと復旧を実装する
- ✅ **P26-009** — 将来のRadio Site / Base Station等がbackhaulとしてOptical Networkへ接続できる参照境界を実装する
- ✅ **P26-010** — Optical topologyの3D spatial queryと参照整合性validationを実装する
- ✅ **P26-011** — Optical Communication stateをcheckpoint / Save Dataへ含める
- ✅ **P26-012** — Optical topology・traffic・capacity・congestion・outageをProtocol / Serverで配信する
- ✅ **P26-013** — Web ClientでFiber / node / access / congestion / outageをdebug可視化する
- ✅ **P26-014** — traffic増加・Fiber cut・停電・backhaul復旧を検証するdeterministic E2Eを追加する
- ✅ **P26-015** — 大規模Optical node/link/loadのrouting・tick・topology benchmarkを記録する
- ✅ **P26-016** — Optical Communication Infrastructureのspecification / architecture / ROADMAPを同期する

### Phase 26 完了条件

- ✅ Building / IndustryがOptical Networkへ接続され、traffic demandとlink / node capacityに応じてbandwidth・congestion・outage stateが変化する。
- ✅ Radio等の後続domainがbackhaulとして参照できる安定した通信Infrastructure境界を持つ。
- ✅ 詳細な光伝送物理を標準完了条件に含めず、将来のExtensionが高精度solverを差し替えられる。
- ✅ Protocol 2.15 / Server / Web debugでbandwidth・congestion・簡易latency・equipment power・backhaul stateを観測できる。
- ✅ deterministic E2EでFiber reroute・停電・backhaul outage/recoveryを検証し、1k / 5k Optical load benchmarkをCIで記録する。

### Phase 26 closeout evidence

- PR #172 を `develop` へ統合済み（merge commit `2a0ee18846f45894c870114545330b2aaf1ab746`）。
- final head `314a0592ade0b39835ccff0890b5b21fec63f541` で Dependency Review `33484684321`、Optical Benchmark `33484684353`、CI `33484684372`、Benchmarks `33484684355`、End-to-end `33484684486` がすべて成功した。
- 標準solverはstable ID順のdeterministic shortest-pathとbottleneck capacity allocationを使用し、85%以上のFiber利用率をcongestionとして観測する。
- 簡易latencyはroute hop数とFiber utilizationから決定論的に算出し、詳細な光損失・分散・波長設計は標準solverの対象外とする。
- Save Format 11のoptional `OpticalCheckpoint`として保存・復元し、旧Saveとの互換性を維持する。

---

## Phase 27 — Remote Administration & MCP Integration

> **状態: 🚧 実装中（PR #176 / P27-015 closeout待ち）**  
> **依存:** Phase 4 / 20  
> Phase 20で確立したserver-authoritative Administration command境界を、HTTPSのRemote MCP Serverから安全に再利用できるようにする。ChatGPT等のMCP Clientから状態確認・調査・運転制御・明示的に許可したmutationを実行できる一方、任意shell実行やSimulation内部への直接アクセスを公開しない。

- ✅ **P27-001** — Remote Administration / MCPの責務・trust boundary・read/write/destructive分類・権限モデルを仕様化する
- ✅ **P27-002** — MCP transport / tool adapter / Phase 20 Admin command境界 / SimulationRuntimeの責務分離をarchitecture文書化し、Remote AdminのADRを追加する
- ✅ **P27-003** — MCP Serverのhost境界と設定モデルを実装し、通常Server起動時に明示設定で有効化できるようにする
- ✅ **P27-004** — HTTPS向けStreamable HTTP `/mcp` endpointとMCP protocol negotiation / tool discoveryを実装する
- ✅ **P27-005** — Remote MCPのauthentication / authorizationとcredential取扱いを実装し、匿名のwrite操作を許可しない
- ✅ **P27-006** — Server version / runtime health / Simulation status / tick / pause state等を取得するread-only Toolを実装する
- ✅ **P27-007** — metrics / bounded log query / diagnostic stateを取得するread-only Toolを実装する
- ✅ **P27-008** — Entity inspect / query系の既存Administration境界をMCP Toolへmappingし、Simulation内部Storeを直接公開しない
- ✅ **P27-009** — `pause` / `step` / `resume` / `save`等の運転操作を既存AdminCommandQueue / executor経由のwrite Toolとして公開する
- ✅ **P27-010** — 明示的に許可したEntity create / update / remove等のmutationをAdmin command境界経由でMCP Toolへmappingする
- ✅ **P27-011** — destructive操作のconfirmation metadata・role / scope・stable result/error codeを実装し、read-only権限と分離する
- ✅ **P27-012** — request size / concurrency / timeout / cancellation / rate limit / result sizeの上限を実装し、slowまたは不正なRemote ClientをServer全体から隔離する
- ✅ **P27-013** — Cloudflare等のHTTPS reverse proxy配下でcache bypass・forwarded header・origin保護・TLS終端を安全に扱えるdeployment契約と設定例を整備する
- ✅ **P27-014** — arbitrary shell / executable / file path実行、unknown Tool、権限不足、oversized input、command injection、malformed MCP requestがServer停止や権限昇格へ波及しないnegative testを追加する
- ⬜ **P27-015** — Remote MCP Client→HTTPS reverse proxy→`/mcp`→Admin command境界→SimulationRuntimeまでを実Serverで検証するE2Eを追加し、readとwriteの双方を確認する
- ✅ **P27-016** — Remote Administration / MCPのspecification / architecture / ADR / security / deployment / Server README / ROADMAPを同期する

### Phase 27 完了条件

- ✅ Remote MCP ClientからServer状態・Simulation状態・主要diagnosticを取得できる。
- ✅ mutationはPhase 20のserver-authoritative Admin command境界を必ず通り、MCP adapterがSimulation内部Storeを直接変更しない。
- ✅ read / write / destructive操作の権限が分離され、認証なしのwrite、任意shell実行、無制限のfile/process操作を公開しない。
- ⬜ Cloudflare等のreverse proxy相当のHTTPS経路でもStreamable HTTP MCPとして接続でき、cache・timeout・request/result size・cancellationを安全に扱えることを実E2Eで確認する。
- ✅ 実Kestrel Server E2EでRemote MCPのread / write / failure isolationを継続検証できる。

### Phase 27 実装状況

- PR #176 でRemote MCP host、Streamable HTTP `/mcp`、read/write/destructive scope、Administration queue mapping、resource limit、negative test、deployment docsを実装中。
- Review対応として、timeout/cancel済みのqueued Admin commandを実行前に破棄し、timeout後のmutation遅延適用を防止する。
- `entity_query`はRemoteからの無制限全件列挙を廃止し、stable ID指定の単一Entity inspectへ限定する。
- Browser利用時は`AllowedOrigins`完全一致のCORS preflightを認証前に処理し、wildcard Originを許可しない。
- log / metricsのresult size制御はJSONを途中切断せず、valid structured resultを維持する。
- Phase全体の正式closeoutはP27-015のHTTPS reverse proxy E2EとPR最終CI成功後に行う。

---

## Phase 28 — Radio & Spectrum Foundation

> **状態: ⬜ 未着手**  
> **依存:** Phase 10 / 23 / 26  
> LTE等の特定通信方式へ依存しないRadio / Spectrumの共通基盤を作り、周波数・送受信機・アンテナ・伝搬・干渉を都市の3D空間上で扱えるようにする。標準Simulationは軽量な簡易伝搬を用い、詳細な電磁界・ray tracing等は交換可能なsolver境界の外側へ分離する。

- ⬜ **P28-001** — Radio / Spectrum Foundationの用途非依存責務、単位、determinism、solver境界を仕様化する
- ⬜ **P28-002** — SpectrumBand / RadioChannelと周波数・bandwidth・overlapのstable契約を実装する
- ⬜ **P28-003** — RadioSite / Transmitter / Receiver / Antenna / Emissionのstable IDとstateモデルを実装する
- ⬜ **P28-004** — Antennaの3D position・orientation・gain・簡易radiation pattern契約を実装する
- ⬜ **P28-005** — Transmissionのfrequency・bandwidth・transmit power・operating stateを実装する
- ⬜ **P28-006** — Receiverの受信帯域・sensitivityと送受信候補を評価する共通契約を実装する
- ⬜ **P28-007** — Radio Foundationから独立して差し替え可能な`IRadioPropagationSolver`相当のsolver境界を実装する
- ⬜ **P28-008** — 距離・周波数・送信電力・antenna gainからreceived powerを求める軽量な標準propagation solverを実装する
- ⬜ **P28-009** — Building `WorldVolume`を使うLoS / NLoS・簡易obstruction / penetration penaltyを実装する
- ⬜ **P28-010** — 周波数帯域が重なるEmissionを候補化する簡易interference計算を実装する
- ⬜ **P28-011** — received power・noise / interference・SINR等の用途非依存Radio Link resultを実装する
- ⬜ **P28-012** — 大量Transmitterを全件走査しない3D spatial index / candidate queryを実装する
- ⬜ **P28-013** — Radio Siteの電力供給とOptical backhaul参照を既存Infrastructureへ接続する
- ⬜ **P28-014** — Radio / Spectrum stateをcheckpoint / Save Dataへ含める
- ⬜ **P28-015** — Radio site・spectrum・emission・coverage / link resultをProtocol / Serverで配信する
- ⬜ **P28-016** — Web ClientでRadio site・antenna・channel・簡易coverage / interferenceをdebug可視化する
- ⬜ **P28-017** — 複数周波数・複数送信源・遮蔽・干渉・停電/backhaul障害を検証するdeterministic E2Eを追加する
- ⬜ **P28-018** — 大規模Transmitter / Receiver / spectrum query / propagationのbenchmarkを記録する
- ⬜ **P28-019** — Radio & Spectrum Foundationのspecification / architecture / ROADMAPを同期する

### Phase 28 完了条件

- LTE / 5G / Wi-Fi / Broadcast等の個別方式をRadio Foundationの正本へ埋め込まず、共通の周波数・送受信・アンテナ・伝搬・干渉結果を扱える。
- 3D World上の位置・建物遮蔽・複数Emissionを考慮した軽量でdeterministicな標準Radio Simulationが成立する。
- 詳細なreflection / diffraction / multipath / terrain / material / ray tracing等を標準完了条件に含めず、将来のExtensionが高精度propagation solverを差し替えられる。

---

## Phase 29 — World & Physical Environment Generation

> **状態: ⬜ 未着手**  
> **依存:** Phase 9 / 10 / 11 / 12 / 17 / 24 / 28  
> 都市生成より上流にある世界・気候・地形・水系・地理Featureをauthoritativeかつdeterministicに生成し、道路・建物・交通・後続都市生成が自然環境へ従うための物理世界を確立する。世界全体は粗い解像度、選択地域は高解像度とする二段階生成を基本とする。

### 29.1 Global World Generation

- ⬜ **P29-001** — `WorldEnvironmentConfig` / world seed / geographic north / latitude・hemisphere・sea level等の正本契約を仕様化する
- ⬜ **P29-002** — named presetではなくlatitude・continentality・maritime influence・temperature・seasonality・precipitation等の連続parameterをauthoritative inputとして定義する
- ⬜ **P29-003** — coarse world grid上にOcean / Continent / Island等をdeterministicに生成する
- ⬜ **P29-004** — large-scale elevation・mountain range・plain・basin等の地形形成fieldを生成する
- ⬜ **P29-005** — latitude・elevation・maritime influence等から簡易Climate / temperature / precipitation fieldをdeterministicに派生する
- ⬜ **P29-006** — watershed / drainage / major river / lake / coastをcoarse world上へ生成する
- ⬜ **P29-007** — `RegionalEnvironmentField`として地域ごとのclimate・hydrology・terrain tendency・buildability入力をquery可能にする
- ⬜ **P29-008** — exact coastline distance等のconfigured値と生成後derived値を混同しないprecedence / derivation契約を定義する
- ⬜ **P29-009** — 世界全体から複数のcity candidate regionを抽出し、自然条件・交通可能性・水アクセス等の基礎scoreを計算する
- ⬜ **P29-010** — 最高score固定ではなくweighted deterministic selectionによりcoastal / river / basin / mountain / cold / dry / island等の都市立地多様性を保持する

### 29.2 Detailed Terrain & Geographic Features

- ⬜ **P29-011** — 選択RegionをSimulation解像度へ展開する`TerrainSurface`正本モデルを仕様化する
- ⬜ **P29-012** — arbitrary `(X,Y)` のsurface height・normal・slope・roughness・surface material queryを実装する
- ⬜ **P29-013** — mountain / hill / ridge / valley / basin / plateau / cliff / plain等を含む高解像度Terrain generatorを実装する
- ⬜ **P29-014** — river / tributary / lake / coast / floodplain等のregional hydrology geometryを詳細Terrainへ接続する
- ⬜ **P29-015** — heightfieldだけを最終正本とせず、Air / Water / Soil / Rock / Voidを扱えるoptional `TerrainVolume` / chunk境界を定義する
- ⬜ **P29-016** — 同一XYに複数surfaceを持てるcavity / cave / overhang対応の3D terrain query境界を実装する
- ⬜ **P29-017** — terrain solid / cavityをWorldVolume・3D spatial queryと共存させ、将来のsubway / tunnel / sewer / basement等の地下構造を同一World座標へ配置できるようにする
- ⬜ **P29-018** — terrain collision / ground snapping / surface trackingを実装し、Agent / Pedestrian / Road / Buildingが平面gridへ暗黙依存しないようにする
- ⬜ **P29-019** — Road / Railway / Building placementがslope・surface・solid volumeを参照できるterrain constraint APIを実装する
- ⬜ **P29-020** — `GeographicFeature`のstable ID・type・geometry / area・parent relation・elevation range契約を仕様化する
- ⬜ **P29-021** — Mountain / Mountain Range / River / Tributary / Lake / Valley / Basin / Plain / Plateau / Pass / Cape / Bay / Coast / Island / Peninsula / Cave等をTerrainからdeterministicに識別する
- ⬜ **P29-022** — Geographic Featureへseed-deterministicな自然地名を付与し、同一seed・設定で同じFeature名を再生成する
- ⬜ **P29-023** — 自然地名を単なる表示ラベルにせず、後続の都市名・地区名・道路名・橋梁名・駅名等が由来を参照できるToponym provenance契約を実装する
- ⬜ **P29-024** — WorldEnvironment / Terrain / GeographicFeature / Toponymをcheckpoint / Save Dataへ統合する
- ⬜ **P29-025** — World / Terrain / GeographicFeature / ToponymをProtocol / Serverへ配信する
- ⬜ **P29-026** — Web Clientのflat `GridHelper`依存を置換し、terrain mesh・water・主要Geographic Feature・地名を3D描画する
- ⬜ **P29-027** — 同一seedから同じcoarse world・selected region・terrain・river・feature・toponymを得るreproducibility E2Eを追加する
- ⬜ **P29-028** — coarse world / detailed regionのgeneration・query・memory benchmarkを記録する
- ⬜ **P29-029** — World / Terrain / Geographic Featureのspecification / architecture / ADR / ROADMAPを同期する

### Phase 29 完了条件

- 世界全体の粗い環境条件と、選択Regionの高解像度Terrainが同一seed・設定から再現可能に生成される。
- Terrainがsurface queryだけでなく、将来の洞窟・自然空洞・地下Infrastructureを許容する3D volume境界を持つ。
- 道路・線路・建物・Agentがterrain height / slope / solid stateをqueryでき、平面gridを物理世界の正本として扱わない。
- 河川・山・谷・盆地・峠・湾等がstableな`GeographicFeature`として存在し、自然地名と由来を保存・配信・描画できる。
- 植生・動物・生態系の詳細Simulation、侵食・地滑り・洪水の高度Simulation、cut/fill・造成・干拓等のterrain modificationはPhase 29完了条件へ含めない。

---

## Phase 30 — Regional & Urban Generation

> **状態: ⬜ 未着手**  
> **依存:** Phase 10〜19 / 21〜29 の主要都市・自然環境モデル  
> Phase 29の自然環境から都市立地の理由と歴史を生成し、その履歴に基づいて道路・街区・Parcel・Land Use・Building・POI・人間由来の地名・道路標識を形成する。完成都市を一度にランダム生成せず、environment-driven / history-driven / iterativeな生成を正本方針とする。

### 都市生成の原則

- **Deterministic** — 同じseed・設定・input worldから同じ都市を再生成できる。
- **Environment-driven** — 地形・水系・気候・災害risk・建設costが都市立地と形状へ影響する。
- **History-driven** — 小集落→交通→中心形成→拡張→郊外化→再開発の履歴を蓄積して現在都市を作る。
- **Iterative** — Generate → Evaluate → Improveを反復し、一発生成へ固定しない。
- **Multi-objective** — accessibility・terrain adaptation・cost・risk・compactness等を同時評価する。
- **Quality-first** — 初期都市生成はrealtime完了を要求せず、常識的な範囲で計算時間を品質向上へ使える。

### 30.1 Settlement & Historical Urban Growth

- ⬜ **P30-001** — Settlement / CityOrigin / RegionalRole / historical growth eventの正本契約を仕様化する
- ⬜ **P30-002** — flatness・water access・transport potential・buildability・resource access・flood risk・steep slope・isolation・construction cost等からUrban Suitabilityを評価する
- ⬜ **P30-003** — Phase 29のcandidate regionからweighted deterministic selectionで都市のorigin locationを決定する
- ⬜ **P30-004** — river plain / estuary / bay / basin / valley / mountain pass等からCityOrigin / RegionalRole / InitialEconomyの基礎傾向を派生する
- ⬜ **P30-005** — 初期Settlementと周辺Settlement / regional connectionを生成する
- ⬜ **P30-006** — 地形・河川・峠・海岸等を考慮してprimary road / intercity corridorを生成する
- ⬜ **P30-007** — Railway等の大規模transport corridorを需要・地形・都市成長履歴から形成できるgeneration境界を実装する
- ⬜ **P30-008** — population / economy growthに応じたcity center formation・urban expansion・suburbanizationを段階生成する
- ⬜ **P30-009** — congestion / accessibility / land pressure等に応じたredevelopment・new center formationの履歴ruleを実装する
- ⬜ **P30-010** — 自然地名をSettlement / City / District等の人間側名称へ継承・変形するNaming provenance ruleを実装する
- ⬜ **P30-011** — 都市生成履歴をevent / generation stageとして保存し、最終形状だけでなく由来を追跡可能にする

### 30.2 Detailed Urban Fabric & Signage

- ⬜ **P30-012** — Parcel境界・Zone種別・土地利用・占有/development stateの正本契約を仕様化する
- ⬜ **P30-013** — Historical Road Networkからterrain-awareな詳細Road / Lane networkを生成する
- ⬜ **P30-014** — Road NetworkからBlock / Parcelをdeterministicに生成するsubdivisionを実装する
- ⬜ **P30-015** — Road access・parcel size・slope・flood risk・land value・land use等からdevelopment suitabilityを評価する
- ⬜ **P30-016** — Zone / Land Useに応じたBuilding用途・規模・density・height候補を生成する
- ⬜ **P30-017** — 空ParcelへのBuilding / POI development lifecycleを実装する
- ⬜ **P30-018** — demand変化に応じたredevelopment / vacancyの最小ruleを実装する
- ⬜ **P30-019** — station district / central business district / industrial area / suburb / old town等を都市履歴とaccessibilityから形成する
- ⬜ **P30-020** — 初期Population / Household / Jobを生成都市へ配置するseeding処理を実装する
- ⬜ **P30-021** — Railway / Power / Water / Sewer / Gas / Optical / Radio等の既存Infrastructureを壊さず、地形へ適応するgeneration constraintを定義する
- ⬜ **P30-022** — 自然地名・Settlement履歴・District hierarchyを由来としてRoad / Bridge / Tunnel / Station / District等の名称をdeterministicに生成する
- ⬜ **P30-023** — Road geometry・hierarchy・destination・Geographic Featureを解析するRoad Context Analysisを実装する
- ⬜ **P30-024** — steep grade / sharp curve / rock slope / floodplain / river crossing / mountain pass / tunnel / coastal lowland等から必要なwarning / geographic / guidance signを決定する
- ⬜ **P30-025** — destination name・distance・direction・route contextを使う案内標識と、河川名・峠名・橋梁名・トンネル名等の地名標識をdeterministicに生成する
- ⬜ **P30-026** — Road Signをstable ID付き都市Entityとして配置し、Road Segment / Lane / GeographicFeature / named destinationへの参照を保持する
- ⬜ **P30-027** — Parcel / Zone / generation history / human toponym / Road Signをcheckpoint / Save Dataへ統合する
- ⬜ **P30-028** — Parcel / Zone / development / urban naming / Road SignをProtocol / Serverへ配信し、Web Clientで3D可視化する

### Generation Quality / Validation

- ⬜ **P30-029** — `CityQualityReport`を実装し、TerrainAdaptation / RoadConnectivity / AverageSlopeCost / Accessibility / CongestionRisk / LandUseConsistency / FloodExposure / UrbanCompactness等を独立評価する
- ⬜ **P30-030** — 弱いquality dimensionに応じて道路・土地利用・中心配置等を改善するGenerate → Evaluate → Improve loopを実装する
- ⬜ **P30-031** — 同一seed・設定で同一都市・名称・標識・quality reportを生成するreproducibility E2Eを追加する
- ⬜ **P30-032** — river city / port city / basin city / valley city / mountain city / cold city / dry inland city / island city等のdeterministic fixtureを追加する
- ⬜ **P30-033** — Draft / Standard / High Quality等のgeneration quality presetとiteration budgetを定義し、時間上限ではなく再現可能なbudgetで品質差を制御する
- ⬜ **P30-034** — 小/中/大規模都市のgeneration時間・memory・quality metrics・初期Simulation負荷benchmarkを記録する
- ⬜ **P30-035** — World→Region Selection→Terrain→Settlement→Historical Growth→Urban Fabric→Validationのspecification / architecture / ADR / ROADMAPを同期する

### Phase 30 完了条件

- 都市立地が自然環境とregional contextから説明可能で、完成都市を単純noiseから直接生成しない。
- 都市のRoad / Parcel / Land Use / Building / POIが地形・水系・歴史的成長へ適応している。
- 自然地名から都市名・地区名・道路名・橋・トンネル・駅名等へ由来を追跡できる。
- 道路標識がランダム装飾ではなく、地形・道路形状・destination・named Geographic Featureから必要性と内容を導出して生成される。
- 生成品質を独立評価でき、同じseed・quality presetから同じ都市を再現できる。

---

## Phase 31 — City Management UI

> **状態: ⬜ 未着手**  
> **依存:** Phase 30  
> Browserから都市状態を調査・編集・管理するためのserver-authoritative UIとcommand境界を整える。

- ⬜ **P31-001** — Build / Edit commandの認可・validation・ack/error契約を仕様化する
- ⬜ **P31-002** — Protocolへserver-authoritative command request / resultの共通枠組みを追加する
- ⬜ **P31-003** — Web Clientで3D Entityを選択するpicking / selection基盤を実装する
- ⬜ **P31-004** — Building / Parcel / POI / Person / Vehicle / GeographicFeature / RoadSign等をServer read modelから表示するInspector基盤を実装する
- ⬜ **P31-005** — Road / Laneのbuild / edit / remove commandとUIを実装する
- ⬜ **P31-006** — Building / POI / Parcel / Zoneのbuild / edit commandとUIを実装する
- ⬜ **P31-007** — Railway track / station / platformのbuild / edit commandとUIを実装する
- ⬜ **P31-008** — Power Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P31-009** — Water / Sewer Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P31-010** — Gas Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P31-011** — Optical Communication Infrastructureのbuild / edit commandとUIを実装する
- ⬜ **P31-012** — Radio Site / Antenna / Spectrum設定のbuild / edit commandとUIを実装する
- ⬜ **P31-013** — Geographic Feature名・都市/地区/道路名・Road Signのserver-authoritative edit / override境界を実装する
- ⬜ **P31-014** — command失敗時にClient側だけ状態が進まないoptimistic-state禁止またはrollback方針を実装する
- ⬜ **P31-015** — Simulation speed / pause / resume等の運転controlをServer commandとして実装する
- ⬜ **P31-016** — Population / Traffic / Transit / Economy / Logistics / Power / Utility / Communication / RadioのDashboard統計を実装する
- ⬜ **P31-017** — Server configurationの変更可能項目・restart必要項目を分離してUI化する
- ⬜ **P31-018** — current Save formatのsave / load操作をServer経由で実行する管理UIを追加する
- ⬜ **P31-019** — destructive commandのconfirmationとstable error localizationを実装する
- ⬜ **P31-020** — Inspector / build / edit / naming / signage / config / save操作のBrowser E2Eを追加する
- ⬜ **P31-021** — 大規模都市でselection・terrain・overlay・dashboardが描画hot pathを阻害しないperformance testを追加する
- ⬜ **P31-022** — City Management UIのarchitecture / UX contract / ROADMAPを同期する

### Phase 31 完了条件

- 都市の主要Entity・Terrain・Geographic Feature・Road SignをBrowserから選択・調査できる。
- build/edit操作は必ずServer-authoritative commandを経由し、Clientだけで正本状態を変更しない。
- 自動生成された名称・標識を由来情報を保持したまま明示的にoverrideできる。
- 主要statisticsと運転設定を管理UIから確認できる。

---

## Phase 32 — Distribution & Compatibility

> **状態: ⬜ 未着手**  
> **依存:** Phase 31  
> Save migrationと配布物を整備し、開発環境外でもversion付き成果物として起動・更新・復元できる状態にする。

### Save互換性

- ⬜ **P32-001** — Save migrationのsupport範囲・失敗契約・version policyを仕様化する
- ⬜ **P32-002** — Save formatごとのmigration stepを登録できるframeworkを実装する
- ⬜ **P32-003** — repositoryに旧Save format fixtureを保持し、自動migration testを追加する
- ⬜ **P32-004** — migration中断・unsupported version・破損dataを安全に拒否する
- ⬜ **P32-005** — migration前後でstable IDと継続可能stateを保持するintegration testを追加する

### 配布・Deployment

- ⬜ **P32-006** — Server standalone binaryのsupported OS / architecture matrixを定義する
- ⬜ **P32-007** — Windows / Linux向けServer publish artifactをCIで生成する
- ⬜ **P32-008** — 必要性を検証した上で追加architecture / OS向けartifactを生成する
- ⬜ **P32-009** — Web Client production buildのbase path / Server endpoint設定をdeployment向けに整理する
- ⬜ **P32-010** — static hosting向けWeb Client artifactをCIで生成する
- ⬜ **P32-011** — Server用container imageとruntime configuration契約を実装する
- ⬜ **P32-012** — release artifactへVERSION・commit SHA・license / third-party noticeを同梱する
- ⬜ **P32-013** — release artifactのchecksum / SBOM等、配布時に必要なintegrity metadataを生成する
- ⬜ **P32-014** — package / binary / Web / containerを起動するrelease smoke testをCIへ追加する
- ⬜ **P32-015** — install / upgrade / rollback / backup / restore手順をdocument化する
- ⬜ **P32-016** — develop→main release時のversion / artifact / release note手順を自動化可能な形へ整理する
- ⬜ **P32-017** — Distribution / Compatibilityのarchitecture / development docs / ROADMAPを同期する

### Phase 32 完了条件

- 開発toolchainを手作業構築しなくても、配布artifactからServerとWeb Clientを起動できる。
- 対応対象の旧Save Dataを明示的なmigration経路で読み込める。
- release artifactのversion・commit・license・integrity情報を追跡できる。

---

## Phase 33 — Extension Platform & Localization

> **状態: ⬜ 未着手**  
> **依存:** Phase 32  
> 正本Simulationと互換性境界を壊さず、外部拡張・高精度solver・追加localeを導入できる公開拡張基盤を作る。

### Extension Platform

- ⬜ **P33-001** — Extension / Modで公開する範囲と非公開内部APIの境界を仕様化する
- ⬜ **P33-002** — Extension manifest・stable ID・version・dependency契約を定義する
- ⬜ **P33-003** — data-only extensionとcode extensionを分離したloading modelを設計する
- ⬜ **P33-004** — code extensionの信頼境界・権限・非sandbox性を明示し、安全なdefault policyを実装する
- ⬜ **P33-005** — Simulationへextension contentとPower / Water / Sewer / Gas / Optical / Radio / Terrain等のsolver providerを登録するversioned public APIを実装する
- ⬜ **P33-006** — Extension固有Save Dataをnamespace付きで保存し、missing extension時の挙動を定義する
- ⬜ **P33-007** — Protocolへextension固有wire typeを直接衝突させない拡張契約を設計する
- ⬜ **P33-008** — Extensionのload order / dependency cycle / incompatible versionをvalidationする
- ⬜ **P33-009** — Extension packageの開発・test用templateとsample extensionを追加する

### Localization

- ⬜ **P33-010** — `ja-JP`をdefaultにしたlocale discovery / fallback policyを再確認・固定する
- ⬜ **P33-011** — 追加locale resource packを導入できるWeb Client loading境界を実装する
- ⬜ **P33-012** — 数値・日時・単位・plural等のlocale formattingを共通化する
- ⬜ **P33-013** — stable error code / structured parameterから各localeの表示文を生成するcoverageを拡張する
- ⬜ **P33-014** — translation key欠落・未使用key・parameter不一致をCIで検出する
- ⬜ **P33-015** — 少なくとも1つの追加localeで主要UI / Inspector / Dashboard / error表示をE2E確認する

### Closeout

- ⬜ **P33-016** — Extension有無・solver差し替え有無・追加locale有無でSave / Protocol / Simulation determinismが壊れないintegration testを追加する
- ⬜ **P33-017** — Extension loading・solver provider・localizationのstartup / memory costをbenchmarkする
- ⬜ **P33-018** — Extension author guide / solver provider guide / localization guide / compatibility policyを整備する
- ⬜ **P33-019** — architecture / ADR / ROADMAPを同期し、Phase 10〜33で計画した旧Backlogのcloseoutを確認する

### Phase 33 完了条件

- 既存Simulation内部実装へ直接依存せず、versionedな公開境界からExtensionを追加できる。
- 標準の軽量Infrastructure / Terrain solverを維持したまま、Extensionが高精度な物理solverを安全に差し替えられる。
- Extension固有stateがSave Dataと衝突せず、missing/incompatible extensionを安全に扱える。
- `ja-JP`以外のlocaleを主要UIへ追加でき、Protocol / Save / Simulationへ翻訳済み文言を持ち込まない。

---

## 継続Backlog / Phase 29以降への移管

Phase 9以降で未割当だったterrain系項目はPhase 29へ正式移管する。

| 項目 | 現在の扱い |
| --- | --- |
| terrain model / terrain collision | Phase 29.2 |
| ground snapping / surface追従 | Phase 29.2 |
| Terrain Foundation — terrain height / surface / slope / 3D spatial query / Save / Protocol / Web描画 | Phase 29.2 |
| Terrain Interaction — terrain collision / ground snapping / Road / Building / Pedestrian接続 | Phase 29.2 |
| Parcel / zoning / land use / development | Phase 30.2 |
| City generation | Phase 30.1 / 30.2 |
| Geographic Feature / natural toponym | Phase 29.2 |
| Human place naming / road / bridge / tunnel / station naming | Phase 30.1 / 30.2 |
| Terrain-aware road signage | Phase 30.2 |

以下は今回のPhase 29 / 30の標準完了条件には含めず、将来の独立Backlogとして保持する。

- Physics Foundation — 重力、落下、ジャンプ、垂直速度・加速度、物理stateのSave / Protocol / E2E
- Airborne Movement — 飛行可能Entity、空中経路、飛行高度ルール、3D空間交通との競合境界
- Advanced Terrain Modification — cut / fill、grading、reclamation、quarry、dam等の人為的地形変更
- Advanced Natural Dynamics — erosion、landslide、real-time river flow、flood simulation等
- Advanced Cave Generation — cave network、natural tunnel、arch、underground water等の高度生成
- Natural Environment Simulation — vegetation、biome、habitat、wildlife、animal / ecosystem simulation

## 新規Backlogの扱い

Phase 10以降の実装中に新しい大テーマが見つかった場合は、既存Phaseへ無理に詰め込まない。

1. 既存Phaseの完了に必須なら、そのPhaseへ独立Taskとして追加する。
2. 完了に必須でない大テーマなら、このROADMAP末尾へBacklogとして記録する。
3. 着手時にWhat / Whyを`docs/specifications/`、Howを`docs/architecture/`またはADRへ切り分ける。
4. 実装・保存・配信・描画・検証のどこまでをPhase完了条件とするか明示する。
5. Phase完了時に、残件が暗黙に持ち越されていないことを確認する。
