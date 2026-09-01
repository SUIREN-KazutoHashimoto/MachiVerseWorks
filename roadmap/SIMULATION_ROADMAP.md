# Simulation Roadmap

このファイルは、MachiVerseWorks の **Simulation側の実装ロードマップ**です。Simulation Core、authoritative World、Simulationを成立・検証するためのServer / Protocol / Persistence / Observation / Management command境界、およびそれらに直接依存する基盤を対象とします。

純粋なread-only View表現・Camera・Selection・Inspector・描画最適化・localizationは[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)、World / City / Serverを変更するeditor・運転control・Save / Load・configuration等のUIは[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)で管理します。

SimulationがWorldの唯一の意味的正本です。Activity、Status、分類、予定、ETA、状態遷移、semantic event等の意味的処理はSimulation側で完結させ、Viewへ推測・補完・再計算させません。

MachiVerseWorks の作業を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** Phase 29 — World & Physical Environment Generation  
> **次の実装タスク:** `P29-001` — `WorldEnvironmentConfig` / world seed / geographic north / latitude・hemisphere・sea level等の正本契約を仕様化する  
> **並行可能な横断基盤:** Observation Gateway Foundation（View Phase 1と並行して境界整理可能）

## 全体の現在地

| Phase / 横断基盤 | 内容 | 状態 |
| --- | --- | --- |
| 0〜27 | Foundation / Simulation / Infrastructure / Remote Administration | ✅ 完了 |
| 28 | Radio & Spectrum Foundation | ✅ 完了 |
| Cross-cutting | Observation Gateway Foundation | ▶️ Phase 29 / View Phase 1と並行可能 |
| 29 | World & Physical Environment Generation | ▶️ 次 |
| 30 | Regional & Urban Generation | ⏳ 待機 |
| 31 | Persistent Regional & Settlement Evolution | ⏳ 待機 |
| 32 | Simulation Scheduling & Workload Optimization | ⏳ 待機 |
| 33 | Deterministic Parallel Simulation | ⏳ 待機 |
| 35 | Historical World & Replay | ⏳ 待機 |
| 36 | World & City Management Commands | ⏳ 待機 |
| 37 | Distribution & Compatibility | ⏳ 待機 |
| 38 | Extension Platform | ⏳ 待機 |

旧Phase 34を含むread-only描画計画は[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)、旧Phase 36に混在していたmutation / administration UIは[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)へ移管済みです。旧Task IDと移管先の対応は各Roadmap側にも記録します。

Phase 0〜24 の詳細 Task・closeout 証跡・当時の計画状態は、履歴として[`docs/archive/roadmap-through-phase24-closeout.md`](../docs/archive/roadmap-through-phase24-closeout.md)に保存しています。Phase 13〜16 の正式 closeout 時点の詳細は[`docs/archive/roadmap-phase13-through-phase16-closeout.md`](../docs/archive/roadmap-phase13-through-phase16-closeout.md)も参照してください。

## 依存関係の読み方

Simulation Roadmapでは依存を次のように扱う。

- 各Phase見出しの **`依存:` はPhase closeoutの必須依存** とする。依存Phaseのauthoritative contractが成立しない限り、そのPhase全体を完了扱いにしない。
- Phase内の一部Taskが安定した既存contractだけで先行できる場合は並行実装してよい。ただし未完成の依存を仮実装や別正本で補完しない。
- **Cross-cutting** は特定Phaseの直列後続ではなく、複数Phase / Clientと並行して進める横断基盤とする。必要なTask IDだけを利用側の必須依存として指定する。
- 後述の「推奨closeout順」は全体の実装・統合順を示す。矢印すべてが直接のhard dependencyを意味するわけではなく、正確なhard dependencyは各Phase見出しとTask記述を正本とする。
- View / Managementの依存はそれぞれのRoadmapを正本とし、Simulation Phase番号へ無理に同期させない。

## Simulation Roadmap 運用ルール

- 状態記号を付けるのは、単独で完了判定できる作業だけとする。
- 1タスクは原則として「1つの観測可能な成果」を持つ。
- 1タスク内に独立した成果が複数ある場合は分割する。
- E2E、benchmark、docs同期のように独立して完了可能な成果は、それぞれ別Taskとする。
- コード変更では、必要な build / test / benchmark / 実機確認まで含めて完了とする。
- 仕様や設計を変更した場合は、対応する docs / ADR の更新まで含めて完了とする。
- Protocol version / Save format version は application `VERSION` と独立して、互換性が変わるときだけ更新する。
- 「ほぼ完了」「一部完了」は ✅ にしない。残作業を別Taskへ明示的に切り出した場合のみ元Taskを完了にできる。
- 作業中に新しい依存関係が見つかった場合は、後続PhaseのTaskを更新してから実装を進める。
- Phaseから外した計画済み項目は暗黙に削除せず、対応Phase、View Roadmap、Management Roadmapまたは継続Backlogへ必ず移す。
- 完了済みPhaseの詳細は必要に応じて`docs/archive/`へ移し、現行Simulation Roadmapを次の判断に使いやすく保つ。
- **Task実装状態・`develop`統合状態・Phase正式closeoutは別の状態として扱う。** 後続Phaseの実装を依存Phase完了前に先行mergeする場合、安定した既存境界だけに依存し、未完了依存を完了扱いにせず、Simulation Roadmapへ「develop統合済み / closeout待ち」と理由を記録する。
- 先行mergeは依存順を無効化しない。依存Phaseが正式完了するまで、後続Phase全体を✅へせず、依存部分のTaskを明示的に未完了で残す。

## View / Managementへの移管記録

未着手のClient側計画は責務ごとに分離する。

### View Roadmap

- 旧`P29-026` — Terrain / Water / GeographicFeature / 地名の3D描画 → View Phase 3 `V3-001`
- 旧`P30-028`のWeb Client 3D可視化部分 → View Phase 4 `V4-001`
- 旧Phase 34 `P34-001`〜`P34-015` — World Rendering & Rendering LOD → 主にView Phase 6
- 旧`P35-010`と`P35-015`のtimeline rendering部分 → View Phase 9
- 旧`P36-003` / `P36-004` — read-only Selection / Inspector → View Phase 7
- 旧Phase 38 Localization `P38-010`〜`P38-015`とlocalization関連closeout → View Phase 10

### Management Roadmap

- 旧`P36-005`〜`P36-013`のeditor / override UI部分 → Management Phase 2
- 旧`P36-014`のClient command state管理 → Management Phase 1 / 4
- 旧`P36-015`の運転control UI → Management Phase 3
- 旧`P36-017`のServer configuration UI → Management Phase 3
- 旧`P36-018`のSave / Load UI → Management Phase 3
- 旧`P36-019`のconfirmation / error UI → Management Phase 4
- 旧`P36-020` / `P36-021`のManagement UI検証部分 → Management Phase 4

旧`P36-016`のDashboard / statistics分析系はView / Managementへ移管せず、将来のAnalytics Listener / analysis clientとして別途設計する。

完了済みの`P25-014`、`P26-013`、`P28-016`等のdebug可視化は各Simulation PhaseのE2E・closeout証跡として既に完了履歴へ組み込まれているため、履歴保持のため本ロードマップに残す。

## World-scale Simulationの不変条件

Phase 29以降の広域World・都市成長・最適化では、以下を設計上の不変条件とする。

- **Simulation FidelityはCamera距離・表示状態・都市/郊外/農村の区分で変更しない。** 遠方・非表示地域を人口等の集計値だけへ置換して別ルールで進めない。
- **Viewは完全read-onlyである。** Viewの存在・非存在、接続数、Camera、Selection、FPS、Rendering LOD、View cacheでSimulation結果を変えない。
- **CameraやRendering LODはSimulation結果へ影響しない。** 同一seed・初期状態・外部入力・経過時間なら、観測した地域と一度も描画しなかった地域で同一のauthoritative stateを得る。
- **負荷軽減はSimulationの省略ではなく不要な計算の排除で行う。** Event scheduling、dirty update、dependency tracking、spatial index、時刻からの派生値、deterministic parallelism等を使用する。
- **Global coarse fieldは生成・検索・indexの補助表現であり、詳細Simulationの代替正本にしない。** Simulation Entityが存在する任意地域は、同じ契約とdeterministicな詳細World stateへ展開できる。
- Rendering LOD / culling / View cacheは[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)の責務とし、Simulation stateやworkloadの判定条件に使用しない。
- Management commandは明示的な外部入力としてSimulation結果を変更できるが、必ずserver-authoritative command境界を通し、Viewの観測操作とは混同しない。

## Observation Gateway Foundation — Cross-cutting

> **状態: ▶️ Phase 29 / View Phase 1と並行可能**  
> **必須依存:** 現行SimulationRuntime / Server publish / Protocol 2.x  
> Viewを含むread-only clientがSimulation内部Storeへ直接依存せず、同じauthoritative observationを効率良く共有できる境界を確立する。詳細設計は[`docs/architecture/observation-gateway.md`](../docs/architecture/observation-gateway.md)を正本とする。

- ✅ **OBS-001** — Observation Requestとauthoritative mutation commandをProtocol / Server責務として明示的に分離する
- ⬜ **OBS-002** — SimulationRuntimeからCurrent / Recent / Planned / relation等を必要に応じてdetached read modelとして取得できる共通Observation境界を設計する
- ⬜ **OBS-003** — 現行publish / subscription / inspection処理をServer内のObservation Gateway責務として整理する
- ⬜ **OBS-004** — Entity / Spatial / Static topology等のread modelをtick / revision / generation基準で共有するcache基盤を実装する
- ⬜ **OBS-005** — 同一revisionの同一Observation Requestを重複生成しないrequest deduplicationを実装する
- ⬜ **OBS-006** — negotiated Protocol versionとobservation revisionが一致する再利用可能payloadのencoded cache境界を実装する
- ⬜ **OBS-007** — cache invalidation / eviction / World replacement / reconnect / resyncをauthoritative revisionと整合させる
- ⬜ **OBS-008** — generic Entity inspectionでCurrent / Recent Past / Planned Future / Relationsを意味生成なしに配信できるcontractを設計する
- ⬜ **OBS-009** — View未接続 / 単一View / 複数View / Camera・Selection・cache差でSimulation state digestが一致するinvariance E2Eを追加する
- ⬜ **OBS-010** — cache hit / miss / eviction / deduplicationのequivalenceとServer側CPU / allocation / encoding benchmarkを記録する
- ⬜ **OBS-011** — Observation Gateway architecture / Protocol / Server README / Roadmapを同期する

### Observation Gateway完了条件

- read-only ViewはObservation APIだけで必要なSimulation情報を取得できる。
- Observation Gatewayからauthoritative mutation APIへ到達する経路がない。
- GatewayはSimulationが生成した意味を配送・cacheするだけで、意味的stateを新規生成しない。
- 同一revisionのcache hit / miss / rebuildで同一Observation結果を得られる。
- Viewの接続状態・Camera・Selection・LOD・cache状態によってSimulation state digestが変化しない。

## Phase 10以降の推奨closeout順

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
  -> Persistent Regional / Settlement Evolution
  -> Simulation Scheduling / Workload Optimization
  -> Deterministic Parallel Simulation
  -> Historical World / Replay
  -> World / City Management Commands
  -> Distribution / Compatibility
  -> Extension Platform
```

Observation Gatewayは特定domain Phaseの意味的依存ではなく、read-only配信の横断基盤としてPhase 29以降と並行実装できる。上記は推奨closeout順であり、全矢印を直接hard dependencyとはみなさない。正確な必須依存は各Phase見出しを正本とする。View側の依存順は[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)、Management側は[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)で独立管理する。先行mergeを行っても、Simulation Phaseの正式closeoutは各Phaseの必須依存に従う。Phase 27はServer横断のRemote Administration境界として実装順に挿入したが、Phase 28以降のSimulation domainがMCP実装へ直接依存することを意味しない。

Phase 28 完了後の詳細Task・closeout証跡は当面このSimulation Roadmapに残し、Phase 29以降の進行に合わせて`docs/archive/`へ整理する。

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

> **状態: ✅ 完了**  
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
- ✅ **P27-015** — Remote MCP Client→HTTPS reverse proxy→`/mcp`→Admin command境界→SimulationRuntimeまでを実Serverで検証するE2Eを追加し、readとwriteの双方を確認する
- ✅ **P27-016** — Remote Administration / MCPのspecification / architecture / ADR / security / deployment / Server README / ROADMAPを同期する

### Phase 27 完了条件

- ✅ Remote MCP ClientからServer状態・Simulation状態・主要diagnosticを取得できる。
- ✅ mutationはPhase 20のserver-authoritative Admin command境界を必ず通り、MCP adapterがSimulation内部Storeを直接変更しない。
- ✅ read / write / destructive操作の権限が分離され、認証なしのwrite、任意shell実行、無制限のfile/process操作を公開しない。
- ✅ Cloudflare等のreverse proxy相当のHTTPS経路でもStreamable HTTP MCPとして接続でき、TLS終端・header forwarding・read/writeを実E2Eで確認できる。
- ✅ 実Kestrel Server E2EでRemote MCPのread / write / failure isolationを継続検証できる。

### Phase 27 closeout evidence

- PR #176 の機能検証head `e3bac189386c749670a8bbde63fabf8de633226b` で Dependency Review `33500080830`、CI `33500080954`、End-to-end `33500080979` が成功した。
- `remote-mcp-administration` E2Eで、実Kestrel Serverの前段に自己署名証明書でTLS終端するHTTPS reverse proxyを立て、MCP Clientから`server_status`、`simulation_pause`、`simulation_resume`を実行し、authoritative `SimulationRuntime`の状態変化まで検証する。
- reverse proxyは`Authorization`とMCP protocol headerを転送し、`X-Forwarded-Proto=https`等を付与する。E2E clientは任意証明書を許可せず、生成した証明書のthumbprint一致のみを信頼する。
- Review対応として、timeout後mutationの遅延適用防止、Entity全件列挙廃止、CORS preflight、valid JSON result、stable `io_error`、operation別allowlist、structured negative test、日本語docs同期を実施し、10件のreview threadをすべてresolveした。
- PR #176 を `develop` へ統合済み（merge commit `b832262e6f04371226aa860b82a6763048cde63b`）。Phase 27を正式closeoutし、Phase 28へ移行する。

---

## Phase 28 — Radio & Spectrum Foundation

> **状態: ✅ 完了**  
> **依存:** Phase 10 / 23 / 26  
> LTE等の特定通信方式へ依存しないRadio / Spectrumの共通基盤を作り、周波数・送受信機・アンテナ・伝搬・干渉を都市の3D空間上で扱えるようにする。標準Simulationは軽量な簡易伝搬を用い、詳細な電磁界・ray tracing等は交換可能なsolver境界の外側へ分離する。

- ✅ **P28-001** — Radio / Spectrum Foundationの用途非依存責務、単位、determinism、solver境界を仕様化する
- ✅ **P28-002** — SpectrumBand / RadioChannelと周波数・bandwidth・overlapのstable契約を実装する
- ✅ **P28-003** — RadioSite / Transmitter / Receiver / Antenna / Emissionのstable IDとstateモデルを実装する
- ✅ **P28-004** — Antennaの3D position・orientation・gain・簡易radiation pattern契約を実装する
- ✅ **P28-005** — Transmissionのfrequency・bandwidth・transmit power・operating stateを実装する
- ✅ **P28-006** — Receiverの受信帯域・sensitivityと送受信候補を評価する共通契約を実装する
- ✅ **P28-007** — Radio Foundationから独立して差し替え可能な`IRadioPropagationSolver`相当のsolver境界を実装する
- ✅ **P28-008** — 距離・周波数・送信電力・antenna gainからreceived powerを求める軽量な標準propagation solverを実装する
- ✅ **P28-009** — Building `WorldVolume`を使うLoS / NLoS・簡易obstruction / penetration penaltyを実装する
- ✅ **P28-010** — 周波数帯域が重なるEmissionを候補化する簡易interference計算を実装する
- ✅ **P28-011** — received power・noise / interference・SINR等の用途非依存Radio Link resultを実装する
- ✅ **P28-012** — 大量Transmitterを全件走査しない3D spatial index / candidate queryを実装する
- ✅ **P28-013** — Radio Siteの電力供給とOptical backhaul参照を既存Infrastructureへ接続する
- ✅ **P28-014** — Radio / Spectrum stateをcheckpoint / Save Dataへ含める
- ✅ **P28-015** — Radio site・spectrum・emission・coverage / link resultをProtocol / Serverで配信する
- ✅ **P28-016** — Web ClientでRadio site・antenna・channel・簡易coverage / interferenceをdebug可視化する
- ✅ **P28-017** — 複数周波数・複数送信源・遮蔽・干渉・停電/backhaul障害を検証するdeterministic E2Eを追加する
- ✅ **P28-018** — 大規模Transmitter / Receiver / spectrum query / propagationのbenchmarkを記録する
- ✅ **P28-019** — Radio & Spectrum Foundationのspecification / architecture / ROADMAPを同期する

### Phase 28 完了条件

- ✅ LTE / 5G / Wi-Fi / Broadcast等の個別方式をRadio Foundationの正本へ埋め込まず、共通の周波数・送受信・アンテナ・伝搬・干渉結果を扱える。
- ✅ 3D World上の位置・建物遮蔽・複数Emissionを考慮した軽量でdeterministicな標準Radio Simulationが成立する。
- ✅ 詳細なreflection / diffraction / multipath / terrain / material / ray tracing等を標準完了条件に含めず、将来のExtensionが高精度propagation solverを差し替えられる。
- ✅ Radio / Spectrum stateをcheckpoint / Save Dataへ保存・復元し、Protocol 2.16 / Server / Web debugで観測できる。
- ✅ Power / Optical backhaul障害をRadio operational stateへ反映し、3D spatial candidate queryとbenchmarkで大規模候補検索を継続検証できる。

### Phase 28 closeout evidence

- PR #179 の機能検証head `2565fd95dd47fd13ad29d6cd2dee78a6c81fa562` で Dependency Review `33515371520`、CI `33515371598`、Benchmarks `33515371514`、Radio Benchmark `33515371511`、Optical Benchmark `33515371584`、End-to-end `33515371583` がすべて成功した。
- `radio-spectrum-server-browser` E2Eでは実Kestrel ServerからProtocol 2.16のRadio / Spectrum snapshotをBrowserへ配信し、4 Radio Site、4 Antenna、2 Transmitter、2 Receiver、3 Emission、3 Linkを観測する。3つの周波数、重複帯域による干渉候補、Building遮蔽、Power line outage、Optical backhaul outage、復旧、およびWeb debug overlay描画をpass条件として検証する。
- `IRadioPropagationSolver`を方式非依存の差し替え境界とし、標準solverは距離・周波数・送信電力・antenna gain・Building obstruction・noise / interferenceからreceived powerとSINRをdeterministicに算出する。reflection / diffraction / multipath / terrain / material / ray tracingは標準solverの責務外とする。
- 50,000 Transmitter相当の3D candidate query benchmarkは平均 `11.993 ms`、200,000 propagation evaluationは平均 `19.894 ms`。Radio Benchmark run `33515371511` で継続記録する。
- `WorldVector`を明示JSON構築可能にしてAntenna offset / orientationをSave Dataで保持し、Radio entity stable ID・infrastructure binding・explicit link bindingをcheckpoint復元後も維持する。

---

## Phase 29 — World & Physical Environment Generation

> **状態: ▶️ 次**  
> **依存:** Phase 9 / 10 / 11 / 12 / 17 / 24 / 28  
> 都市生成より上流にある世界・気候・地形・水系・地理Featureをauthoritativeかつdeterministicに生成し、道路・建物・交通・後続都市生成が自然環境へ従うための物理世界を確立する。世界規模の低解像度fieldは生成・検索を支援する上位表現として使用し、任意のRegion / Partitionを同一ルールのSimulation解像度へdeterministicに展開できる構成とする。Camera距離や表示状態によって詳細World stateの有無・精度を変えない。

### 29.1 Global World Generation

- ⬜ **P29-001** — `WorldEnvironmentConfig` / world seed / geographic north / latitude・hemisphere・sea level等の正本契約を仕様化する
- ⬜ **P29-002** — named presetではなくlatitude・continentality・maritime influence・temperature・seasonality・precipitation等の連続parameterをauthoritative inputとして定義する
- ⬜ **P29-003** — global environment field上にOcean / Continent / Island等をdeterministicに生成する
- ⬜ **P29-004** — large-scale elevation・mountain range・plain・basin等の地形形成fieldを生成する
- ⬜ **P29-005** — latitude・elevation・maritime influence等から簡易Climate / temperature / precipitation fieldをdeterministicに派生する
- ⬜ **P29-006** — watershed / drainage / major river / lake / coastをglobal field上へ生成する
- ⬜ **P29-007** — `RegionalEnvironmentField`として地域ごとのclimate・hydrology・terrain tendency・buildability入力をquery可能にする
- ⬜ **P29-008** — exact coastline distance等のconfigured値と生成後derived値を混同しないprecedence / derivation契約を定義する
- ⬜ **P29-009** — 世界全体から複数のSettlement candidate regionを抽出し、自然条件・交通可能性・水アクセス等の基礎scoreを計算する
- ⬜ **P29-010** — 最高score固定ではなくweighted deterministic selectionによりcoastal / river / basin / mountain / cold / dry / island等の立地多様性を保持する

### 29.2 Detailed Terrain & Geographic Features

- ⬜ **P29-011** — 任意Region / PartitionをSimulation解像度へ展開する`TerrainSurface`正本モデルを仕様化する
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
- ⬜ **P29-025** — World / Terrain / GeographicFeature / ToponymをObservation Gateway / Protocol / Serverへ配信する
- ⬜ **P29-027** — 同一seedから同じglobal environment field・任意detailed partition・terrain・river・feature・toponymを得るreproducibility E2Eを追加する
- ⬜ **P29-028** — global field / detailed partitionのgeneration・query・memory benchmarkを記録する
- ⬜ **P29-029** — World / Terrain / Geographic Featureのspecification / architecture / ADR / ROADMAPを同期する

> 旧`P29-026`のWeb Client 3D描画はView Roadmap Phase 3 `V3-001`へ移管した。`P29-025`が必要なObservation contractを提供した時点でView側を着手可能とする。

### Phase 29 完了条件

- 世界全体の環境fieldと、任意Region / Partitionの高解像度Terrainが同一seed・設定から再現可能に生成される。
- Global fieldは詳細Simulationの代替正本として使わず、Simulation Entityが存在する地域はCamera位置に依存せず同一精度の詳細World stateを持てる。
- Terrainがsurface queryだけでなく、将来の洞窟・自然空洞・地下Infrastructureを許容する3D volume境界を持つ。
- 道路・線路・建物・Agentがterrain height / slope / solid stateをqueryでき、平面gridを物理世界の正本として扱わない。
- 河川・山・谷・盆地・峠・湾等がstableな`GeographicFeature`として存在し、自然地名と由来を保存・配信できる。
- 植生・動物・生態系の詳細Simulation、侵食・地滑り・洪水の高度Simulation、cut/fill・造成・干拓等のterrain modificationはPhase 29完了条件へ含めない。

---

## Phase 30 — Regional & Urban Generation

> **状態: ⬜ 未着手**  
> **依存:** Phase 10〜19 / 21〜29 の主要都市・自然環境モデル  
> Phase 29の自然環境から複数のSettlementが成立する理由と歴史を生成し、その相互関係と履歴に基づいて道路・街区・Parcel・Land Use・Building・POI・人間由来の地名・道路標識を形成する。単一の中心都市や完成都市を一度にランダム生成せず、environment-driven / history-driven / iterative / polycentricな地域生成を正本方針とする。

### 都市・地域生成の原則

- **Deterministic** — 同じseed・設定・input worldから同じ地域とSettlement群を再生成できる。
- **Environment-driven** — 地形・水系・気候・災害risk・建設costがSettlement立地と形状へ影響する。
- **History-driven** — 小集落→交通→中心形成→拡張→郊外化→再開発の履歴を蓄積して現在の地域を作る。
- **Polycentric** — 一極集中を前提とせず、都市・町・村・集落が複数の中心と役割を形成できる。
- **Iterative** — Generate → Evaluate → Improveを反復し、一発生成へ固定しない。
- **Multi-objective** — accessibility・terrain adaptation・cost・risk・compactness・regional balance等を同時評価する。
- **Quality-first** — 初期地域生成はrealtime完了を要求せず、常識的な範囲で計算時間を品質向上へ使える。

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
- ⬜ **P30-011** — Settlementごとの生成履歴とSettlement間関係をevent / generation stageとして保存し、最終形状だけでなく由来を追跡可能にする

### 30.2 Detailed Urban Fabric & Signage

- ⬜ **P30-012** — Parcel境界・Zone種別・土地利用・占有/development stateの正本契約を仕様化する
- ⬜ **P30-013** — Historical Road Networkからterrain-awareな詳細Road / Lane networkを生成する
- ⬜ **P30-014** — Road NetworkからBlock / Parcelをdeterministicに生成するsubdivisionを実装する
- ⬜ **P30-015** — Road access・parcel size・slope・flood risk・land value・land use等からdevelopment suitabilityを評価する
- ⬜ **P30-016** — Zone / Land Useに応じたBuilding用途・規模・density・height候補を生成する
- ⬜ **P30-017** — 初期生成履歴として空ParcelへのBuilding / POI developmentを段階生成する
- ⬜ **P30-018** — 初期生成履歴としてdemand変化に応じたredevelopment / vacancyの最小ruleを実装する
- ⬜ **P30-019** — station district / central business district / industrial area / suburb / old town等を都市履歴とaccessibilityから形成する
- ⬜ **P30-020** — 初期Population / Household / Jobを複数Settlementへ配置するseeding処理を実装する
- ⬜ **P30-021** — Railway / Power / Water / Sewer / Gas / Optical / Radio等の既存Infrastructureを壊さず、地形とSettlement networkへ適応するgeneration constraintを定義する
- ⬜ **P30-022** — 自然地名・Settlement履歴・District hierarchyを由来としてRoad / Bridge / Tunnel / Station / District等の名称をdeterministicに生成する
- ⬜ **P30-023** — Road geometry・hierarchy・destination・Geographic Featureを解析するRoad Context Analysisを実装する
- ⬜ **P30-024** — steep grade / sharp curve / rock slope / floodplain / river crossing / mountain pass / tunnel / coastal lowland等から必要なwarning / geographic / guidance signを決定する
- ⬜ **P30-025** — destination name・distance・direction・route contextを使う案内標識と、河川名・峠名・橋梁名・トンネル名等の地名標識をdeterministicに生成する
- ⬜ **P30-026** — Road Signをstable ID付き都市Entityとして配置し、Road Segment / Lane / GeographicFeature / named destinationへの参照を保持する
- ⬜ **P30-027** — Parcel / Zone / generation history / human toponym / Road Signをcheckpoint / Save Dataへ統合する
- ⬜ **P30-028** — Settlement network / Parcel / Zone / development / urban naming / Road SignをObservation Gateway / Protocol / Serverへ配信する

> 旧`P30-028`に含まれていたWeb Client 3D可視化はView Roadmap Phase 4 `V4-001`へ分離した。必要なObservation contractが完成した時点でView側を着手可能とする。

### Generation Quality / Validation

- ⬜ **P30-029** — `RegionalQualityReport`を実装し、TerrainAdaptation / RoadConnectivity / AverageSlopeCost / Accessibility / CongestionRisk / LandUseConsistency / FloodExposure / UrbanCompactness / PolycentricBalance等を独立評価する
- ⬜ **P30-030** — 弱いquality dimensionに応じて道路・土地利用・Settlement中心配置等を改善するGenerate → Evaluate → Improve loopを実装する
- ⬜ **P30-031** — 同一seed・設定で同一Settlement network・都市形状・名称・標識・quality reportを生成するreproducibility E2Eを追加する
- ⬜ **P30-032** — river region / port region / basin region / valley region / mountain region / cold region / dry inland region / island region等のdeterministic fixtureを追加する
- ⬜ **P30-033** — Draft / Standard / High Quality等のgeneration quality presetとiteration budgetを定義し、時間上限ではなく再現可能なbudgetで品質差を制御する
- ⬜ **P30-034** — 小/中/大規模Settlement networkのgeneration時間・memory・quality metrics・初期Simulation負荷benchmarkを記録する
- ⬜ **P30-035** — World→Terrain→Settlement Network→Historical Growth→Urban Fabric→Validationのspecification / architecture / ADR / ROADMAPを同期する

### Phase 30 完了条件

- Settlement立地が自然環境とregional contextから説明可能で、単一の中心都市や完成都市を単純noiseから直接生成しない。
- 都市・町・村・集落が複数存在し、道路・鉄道等のregional networkで関係しながら異なる規模・役割・成長履歴を持てる。
- 各SettlementのRoad / Parcel / Land Use / Building / POIが地形・水系・歴史的成長へ適応している。
- 自然地名からSettlement名・地区名・道路名・橋・トンネル・駅名等へ由来を追跡できる。
- 道路標識がランダム装飾ではなく、地形・道路形状・destination・named Geographic Featureから必要性と内容を導出して生成される。
- 生成品質を独立評価でき、同じseed・quality presetから同じpolycentricな地域を再現できる。

---

## Phase 31 — Persistent Regional & Settlement Evolution

> **状態: ⬜ 未着手**  
> **依存:** Phase 15 / 19 / 21 / 22 / 24〜30  
> Phase 30が生成した初期Worldを固定された完成品として扱わず、Simulation時間の進行に応じて都市・町・村・集落・Parcel・Building・交通・地域間関係が継続的に変化するauthoritativeな地域Simulationを確立する。Settlementの規模分類は固定typeではなく実際の人口・機能・サービス・接続性から派生させ、一極集中を強制しない。

- ⬜ **P31-001** — Persistent Regional Simulationの責務、時間粒度、Settlement / Parcel / Buildingのauthoritative境界を仕様化する
- ⬜ **P31-002** — Settlement population・jobs・services・density・accessibility等からHamlet / Village / Town / City等を派生分類するstable ruleを実装する
- ⬜ **P31-003** — Settlement center / territory / influenceを固定境界ではなく実World stateから再評価できる契約を実装する
- ⬜ **P31-004** — 既存Population / Householdの転居・転入・転出を住宅・雇用・生活利便性・交通accessibilityへ接続する
- ⬜ **P31-005** — 既存Industry / Jobs / EconomyとPopulationを接続し、Settlement内外の雇用・通勤需要を継続更新する
- ⬜ **P31-006** — 商業・教育・医療等のserviceごとに到達可能性とservice catchment / influenceを計算する最小モデルを実装する
- ⬜ **P31-007** — Settlement間の物流・商流を既存Logistics / Freightへ接続し、地域間依存をauthoritative stateとして観測できるようにする
- ⬜ **P31-008** — Population / Economy / Accessibility / Land ValueからParcel単位の住宅・商業・工業等のdevelopment demandを計算する
- ⬜ **P31-009** — development demandとParcel suitabilityから空地への新規Building / POI建設を時間経過イベントとして実装する
- ⬜ **P31-010** — BuildingのbuiltAt / condition / use / capacity等を用いるaging・renovation・用途変更・redevelopment lifecycleを実装する
- ⬜ **P31-011** — demand低下・事業停止・人口減少等からvacancy・closure・abandonment・demolition・空地化を実装する
- ⬜ **P31-012** — 交通量・人口・産業・service需要からRoad / Transit / Utilityへの整備・増強需要signalを生成する共通境界を実装する
- ⬜ **P31-013** — 既存Road / Transit networkの接続性変化がSettlement成長・土地利用・通勤・物流へフィードバックする最小ruleを実装する
- ⬜ **P31-014** — 既存Settlement外で人口・雇用・交通nodeが集積した場合に新しいSettlementが成立できるemergence ruleを実装する
- ⬜ **P31-015** — 人口・service・建物が減少したSettlementの縮小・分類降格・廃村化を履歴を失わず表現する
- ⬜ **P31-016** — 通勤・物流・service依存・連続市街地等から複数SettlementのMetro / Urban Region関係を動的に派生する
- ⬜ **P31-017** — 単一中心への固定吸収を避け、複数中心が競合・補完・専門化できるregional interaction ruleを実装する
- ⬜ **P31-018** — Settlement growth / decline / Building lifecycle / regional relationの主要変化をstable historical eventとして記録する
- ⬜ **P31-019** — Persistent Regional stateと必要な履歴をcheckpoint / Save Data / Observation Gateway / Protocolへ統合する
- ⬜ **P31-020** — 複数都市・町・村・集落が100年以上成長・停滞・衰退・再成長するlong-run deterministic E2Eを追加する
- ⬜ **P31-021** — 大都市・郊外・農村・遠隔集落を同一ruleで進めるWorld-scale Simulation benchmarkを記録する
- ⬜ **P31-022** — Persistent Regional & Settlement Evolutionのspecification / architecture / ADR / ROADMAPを同期する

### Phase 31 完了条件

- 初期生成後もSettlement / Parcel / Building / Population / Economy / Transportの状態が時間経過で継続的に変化する。
- 都市・町・村・集落の分類と影響圏が実Simulation状態から派生し、固定テンプレートや単一中心への強制収束に依存しない。
- 遠隔地・郊外・農村を集計値だけの別Simulationへ置換せず、都市部と同じauthoritative model・ruleで成長・衰退を再現できる。
- 建設・老朽化・用途変更・再開発・閉鎖・解体・Settlement成立/消滅等が履歴として追跡できる。

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
- ⬜ **P35-009** — Historical query / snapshot metadata / timelineをObservation Gateway / Protocol / Serverへ配信する
- ⬜ **P35-011** — live Simulationを停止・巻き戻しせずHistorical Viewへ提供できるread-only projectionを実装する
- ⬜ **P35-012** — retention / snapshot interval / event compactionを設定可能にし、保持対象期間の再構築可能性を損なわないpolicyを実装する
- ⬜ **P35-013** — Historical stateをSave Dataへ統合し、load後もtimelineを継続できるようにする
- ⬜ **P35-014** — 100年以上のSettlement / Building / Network変化を指定時点へ再構築するdeterministic Replay E2Eを追加する
- ⬜ **P35-015** — history storage size / snapshot creation / reconstruction time benchmarkを記録する
- ⬜ **P35-016** — Historical World & Replayのspecification / architecture / ADR / ROADMAPを同期する

> 旧`P35-010`と`P35-015`のtimeline rendering benchmark部分はView Roadmap Phase 9へ移管した。

### Phase 35 完了条件

- 「この場所・建物・Settlementが昔どうだったか」をstable IDと時間から追跡できる。
- 指定時点のWorldをdeterministicに再構築し、read-only projectionとしてObservation Gateway / Protocolから提供できる。
- Historical projectionの参照がlive Simulationのauthoritative stateへ影響しない。

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
- read-only Viewへmutation command責務を持ち込まない。

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

## 継続Backlog / Phase 29以降への移管

Phase 9以降で未割当だったterrain系項目はPhase 29へ正式移管する。

| 項目 | 現在の扱い |
| --- | --- |
| terrain model / terrain collision | Phase 29.2 |
| ground snapping / surface追従 | Phase 29.2 |
| Terrain Foundation — terrain height / surface / slope / 3D spatial query / Save / Protocol | Phase 29.2 |
| Terrain / Water / GeographicFeature 3D描画 | View Roadmap Phase 3 |
| Terrain Interaction — terrain collision / ground snapping / Road / Building / Pedestrian接続 | Phase 29.2 |
| Parcel / zoning / land use / initial development | Phase 30.2 |
| Initial regional / city generation | Phase 30.1 / 30.2 |
| Persistent settlement / building evolution | Phase 31 |
| Simulation workload optimization | Phase 32 |
| Deterministic parallelism | Phase 33 |
| Rendering LOD / world-scale view | View Roadmap Phase 6 |
| read-only Selection / Inspector | View Roadmap Phase 7 |
| Current / Recent / Planned Inspector | View Roadmap Phase 8 |
| Historical replay | Phase 35 |
| Historical timeline / time slider | View Roadmap Phase 9 |
| World / City Management UI | Management Roadmap |
| Management localization / command UX | Management Roadmap Phase 4 |
| Addon management UI | Management Roadmap Phase 5 |
| View Addon / rendering extension | View Roadmap Phase 12 |
| Dashboard / statistics analysis | 将来Analytics Listener / analysis client |
| Web View localization | View Roadmap Phase 10 |
| Geographic Feature / natural toponym | Phase 29.2 |
| Human place naming / road / bridge / tunnel / station naming | Phase 30.1 / 30.2 |
| Terrain-aware road signage | Phase 30.2 |

以下は今回のPhase 29〜35の標準完了条件には含めず、将来の独立Backlogとして保持する。

- Physics Foundation — 重力、落下、ジャンプ、垂直速度・加速度、物理stateのSave / Protocol / E2E
- Airborne Movement — 飛行可能Entity、空中経路、飛行高度ルール、3D空間交通との競合境界
- Advanced Terrain Modification — cut / fill、grading、reclamation、quarry、dam等の人為的地形変更
- Advanced Natural Dynamics — erosion、landslide、real-time river flow、flood simulation等
- Advanced Cave Generation — cave network、natural tunnel、arch、underground water等の高度生成
- Natural Environment Simulation — vegetation、biome、habitat、wildlife、animal / ecosystem simulation
- Analytics Platform — 長期統計、分析、trend、heatmap、analysis storage / query / client

## 新規Backlogの扱い

Phase 10以降の実装中に新しい大テーマが見つかった場合は、既存Phaseへ無理に詰め込まない。

1. 既存Phaseの完了に必須なら、そのPhaseへ独立Taskとして追加する。
2. 純read-only Viewの大テーマなら[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)へ移す。
3. Management UI / command clientの大テーマなら[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)へ移す。
4. 分析・統計・trend等ならView / Managementへ混在させずAnalytics系Backlogとして保持する。
5. Simulation側で完了に必須でない大テーマなら、このSimulation Roadmap末尾へBacklogとして記録する。
6. 着手時にWhat / Whyを`docs/specifications/`、Howを`docs/architecture/`またはADRへ切り分ける。
7. 実装・保存・配信・検証のどこまでをPhase完了条件とするか明示する。
8. Phase完了時に、残件が暗黙に持ち越されていないことを確認する。
