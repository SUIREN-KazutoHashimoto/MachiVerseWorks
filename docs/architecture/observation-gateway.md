# Observation Gateway Architecture

## 目的

MachiVerseWorksでは、SimulationがWorldの唯一の意味的正本を所有し、Gatewayがread-only observationの配送境界を担当し、Viewはその結果を読み取って忠実に表示する完全read-only clientとする。

GatewayがSimulation内部Storeやcommand境界へ直接依存しないよう、Server側に **Observation Gateway** という明示的なread-only観測境界を置く。GatewayのTask状態・実装順は[`../../roadmap/GATEWAY_ROADMAP.md`](../../roadmap/GATEWAY_ROADMAP.md)を正本とし、Simulation Roadmapのcross-cutting Taskとしては管理しない。

```text
MachiVerseWorks.Simulation
  authoritative state / rules / meaning / plans / history
              │
              │ detached semantic observation source
              ▼
Gateway (initially hosted in MachiVerseWorks.Server)
  ├─ observation request
  ├─ subscription / spatial filtering
  ├─ shared read-model cache
  ├─ request deduplication
  ├─ snapshot / delta planning
  ├─ protocol adaptation / encoding cache
  └─ connection / resync
              │
              │ read-only Protocol
              ▼
View
  render / camera / selection / inspector
```

Simulationを変更する操作はGatewayを通さない。管理操作は別のAdministration / Management command境界を使用する。

Gateway Roadmapの分離は責務・進捗管理の分離であり、現時点で別process / repository / deploy unitへ分離することを必須としない。まず`MachiVerseWorks.Server`内でmodule boundaryを確立し、必要になれば独立deploy可能な境界へ育てる。

## 最上位の不変条件

- SimulationがWorld state・rule・意味・状態遷移・予定・意味的eventの唯一の正本である。
- Gatewayは完全read-onlyであり、Simulation stateを変更できない。
- Gateway / Viewは意味的stateを推測、補完、再計算、予測しない。
- Viewの存在・非存在、接続数、Camera位置、Selection、描画FPS、Rendering LOD、View cache、Gateway cache状態によってSimulation結果が変化してはならない。
- GatewayはSimulationが公開した意味を配送・再利用するだけで、新しい意味を生成しない。
- ViewはGateway Observation APIだけから表示に必要なSimulation情報を取得できなければならず、Simulation内部Storeへ直接アクセスしない。
- Observation routeからauthoritative mutation APIへ到達できない。

## Simulation側の責務

Simulation側で完結させるもの:

- authoritative current state
- Activity / Status / Classification等の意味的state
- destination / schedule / planned action等の予定
- ETAや到着予定など、そのdomainで意味を持つ派生値
- state transition
- semantic event
- recent historyとして公開するevent / state
- Historical projection
- stable IDとEntity relation
- deterministic ruleとvalidation
- Gatewayへ渡すdetached semantic observation source / domain payload

例えばPersonが通勤中かどうかをGateway / View側で位置・時刻・destinationから推測してはならない。Simulationが`Activity = Commuting`相当の観測値を公開し、Gatewayはそれを配送し、Viewは表示する。

Simulation側Taskは[`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)を正本とする。

## Gatewayの責務

GatewayはServer内のread-only delivery boundaryとして、次を担当できる。

- Simulationのdetached observation source / snapshotの取得
- Cameraや明示targetに基づくObservation Request / subscription管理
- spatial filtering
- explicit inspect request
- snapshot / delta planning
- entity spawn / update / remove delivery
- chunking / payload upper bound管理
- negotiated Protocolへのadaptation / encoding
- reconnect時の再同期
- slow client / timeout / cancellationのconnection-local isolation
- read-model cache
- request deduplication
- encoded payload cache
- cache eviction

Gatewayが行ってはいけないもの:

- Activity等の意味的判定
- World ruleの実行
- ETA / route / demand / classification等の意味的再計算
- current stateからfuture stateを予測すること
- 複数stateを解釈して新しいsemantic eventを生成すること
- Simulation stateのmutation
- View都合によるSimulation workload / fidelity変更

Gateway側Taskは[`../../roadmap/GATEWAY_ROADMAP.md`](../../roadmap/GATEWAY_ROADMAP.md)を正本とする。

## Protocol ownership

`MachiVerseWorks.Protocol`は共有wire componentだが、Roadmap上の責務は次のように分ける。

- domainの意味、field / unit、authoritative semantic payload: Simulation
- Observation Request、subscription control、delivery envelope、serialization / negotiation / resync: Gateway
- authoritative mutation commandの意味・validation: Simulation
- mutation commandを発行するUI / Client state: Management

同じProtocol projectに実装されることを理由に責務を1 Roadmapへ統合しない。

## Observation Request

ViewからGatewayへ送信する通信が存在しても、それがSimulation mutationでなければread-only境界を壊さない。

代表例:

- `SubscribeVolume`: 観測したい3D範囲を指定する
- `InspectPerson`: 詳細を取得したいEntityを指定する
- `ClearPersonInspection`: connection-localなinspection targetを解除する
- 将来のgeneric entity inspection / historical observation request

これらは「何を見るか」を指定するrequestであり、World stateを変更するcommandとは別contractとして扱う。

## Cache設計

Gatewayは、同じauthoritative observationを複数Clientや複数requestで再利用するためcacheを持てる。

### Revision基準

cache freshnessは原則としてwall-clock TTLだけに依存せず、Simulationが公開するtick / revision / generation等のauthoritative generation markerを使用する。

例:

```text
(EntityKind, EntityId, ObservationRevision)
(WorldChunkId, ObservationRevision)
(TopologyKind, TopologyRevision)
```

新しいauthoritative revisionがpublishされた時点で旧cacheをstaleとして扱う。

### Entity Observation Cache

Inspector等で使用するEntity単位のread modelを共有する。

- current state
- recent state / event
- planned future
- relation / reference

複数Clientが同じEntityを観測しても、同一revisionならread model生成を共有できる。

### Spatial Observation Cache

World / Region / Chunk単位のdetached read modelを共有する。Cameraごとの細かなvolumeに対して毎回Simulationをqueryし直すのではなく、再利用可能なspatial unitを基礎にfilterできる設計を検討する。

### Static / Revision Cache

Road、Railway、Building geometry、Terrain等、revisionが変わるまで再生成不要なread modelはrevision-driven cacheを使用する。

### Request Deduplication

同一revisionに対して同一の高コストObservation Requestが同時に発生した場合、同じ生成処理を並列に重複実行せず、1つの結果を共有できるようにする。

### Encoded Payload Cache

同一Protocol version / same observation revision / same payloadを多数Clientへ配信する場合は、encode済みpayloadを再利用できる。ただしconnection固有metadataを含むframeは安全に分離する。

これらの最適化TaskはGateway Roadmap Phase 2を正本とする。cacheを無効化しても正しいObservationへfallbackできなければならない。

## 短期履歴とCacheの境界

Gatewayが受信済みObservation Snapshotを短時間保持することは許容する。ただし、その差分を解釈してsemantic eventを新規生成してはならない。

```text
許可:
position@tick100
position@tick101
position@tick102
を観測cacheとして保持する

禁止:
上記position差分から
"Stationへ到着した"
というsemantic eventをGatewayが生成する
```

意味的なrecent eventが必要な場合はSimulationが`RecentEvent`等のsemantic sourceとして公開する。

## View側で許可するローカルstate

ViewはPresentationに必要なローカルstateのみ所有する。

- Camera position / orientation / zoom
- Selection / hover / focus / follow target
- renderer resource
- mesh / material / asset cache
- client-side visibility / culling / LOD state
- snapshot間interpolation用previous/current visual state
- connection-local desired observation state

これらはauthoritative World stateではなく、Simulationへフィードバックしない。

Viewの実装計画は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)を正本とする。

## View Inspector契約

Object Inspectorは少なくとも次の観測軸を表示できる構造を目標とする。

- Current: 現在のauthoritative observation
- Recent Past: Simulationが公開した直近state / semantic event
- Planned Future: Simulationが公開したschedule / planned state
- Relations: 所属、destination、参照Entity等

Simulationが意味を生成し、Gateway Phase 4が意味を変えず配送し、View Phase 7 / 8が表示する。Gateway / ViewはCurrentからRecent PastやPlanned Futureを推測しない。

## Historical Observation

Historical projectionの意味と再構築はSimulation Phase 35が所有する。Gateway Phase 5はhistorical tick / range / selected Entityのread-only request、filtering、delivery、cache namespace、cancellationを担当し、View Phase 9がtimeline / historical renderingを担当する。

Gatewayがhistoryを再構築したりlive Simulationを巻き戻したりしない。

## Managementとの分離

Management ClientはViewと同じ画面技術やread-only View componentを再利用してよいが、責務は別とする。

```text
                    ┌─ Gateway ─────────────> View
Simulation Runtime ─┤      └───────────────> Management read side
                    └─ Command Boundary <─── Management write side
```

- Gateway / View module自体はmutation APIを参照しない。
- Management shellがread-only Viewを埋め込む場合も、mutationは別のManagement command clientから送信する。
- command成功後はGatewayからauthoritative resultを再観測する。
- Editor、pause / resume、Save / Load、configuration変更、destructive operationはGateway / View責務に含めない。

Managementの実装計画は[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)を正本とする。

## Analyticsとの分離

人口統計、経済分析、交通分析、heatmap、trend、長期集計等はGateway / Viewで意味的に計算しない。必要になった場合は専用Analytics Listener / data pipelineを設ける。

Analyticsが同じSimulationを観測する場合でも、Camera中心のView subscriptionと全World分析用feedは要求特性が異なるため、同一delivery contractへ無理に統合しない。

## 実装配置

初期実装では新しい独立processを必須としない。既存`MachiVerseWorks.Server`内で、現在のpublish / subscription / inspection処理をGateway責務として明確化する。

将来、負荷・deployment・security上の必要性が生じた場合に別processへ分離できるよう、Simulationとの境界はdetached semantic source / stable contractに保つ。

## 検証原則

- Gateway / View未接続と接続中で同一Simulation inputから同一authoritative state digestを得る。
- Camera位置、subscription、selection、LODを変更してもSimulation state digestが変わらない。
- 複数View接続でSimulation結果が変わらない。
- cache hit / missで返却するobservation内容が同一revisionなら一致する。
- cache eviction後に再構築しても同一revisionの内容が一致する。
- Gateway経由からauthoritative mutation APIへ到達できないことをtestする。
- Gateway内部実装を変更しても公開Observation contractが同じならViewを分岐させない。

## 関連文書

- [`overview.md`](overview.md)
- [`simulation-core.md`](simulation-core.md)
- [`headless-server.md`](headless-server.md)
- [`web-client.md`](web-client.md)
- [`protocol.md`](protocol.md)
- [`../../roadmap/README.md`](../../roadmap/README.md)
- [`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)
- [`../../roadmap/GATEWAY_ROADMAP.md`](../../roadmap/GATEWAY_ROADMAP.md)
- [`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)
- [`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)
