# Headless Server Architecture

## 概要

Headless ServerはASP.NET Core / Kestrel上でHTTP health endpointとbinary WebSocket endpointを提供し、1つの`SimulationWorld`をserver-authoritativeな正本として所有する。current Protocolは **2.16**。実装上のversion正本は[`../../src/protocol/ProtocolVersion.cs`](../../src/protocol/ProtocolVersion.cs)、binary契約は[`protocol.md`](protocol.md)である。position、observation subscription、snapshotはnative 3Dを基本とする。

Serverでは、read-onlyなObservation Gatewayとauthoritative mutationを扱うAdministration / Management command boundaryを明示的に分離する。

```text
Kestrel
├─ GET /health
├─ /ws
│  └─ WebSocketSessionHandler
│     ├─ ClientConnectionRegistry
│     ├─ Observation Request
│     │      │
│     │      ▼
│     │ Observation Gateway
│     │  ├─ subscription / inspect
│     │  ├─ shared read-model cache
│     │  ├─ request deduplication
│     │  ├─ snapshot / delta planning
│     │  └─ protocol encoding / delivery
│     │
│     └─ future Management command transport
│            │
│            ▼
│       authoritative command queue
└─ /mcp  (explicitly enabled only)
   └─ Remote MCP adapter

stdin ──► ServerConsoleService ──► AdminCommandParser
                                      │
                                      ▼
                         bounded AdminCommandQueue
                                      │
                                      ▼
                         AdminCommandExecutorV2
                                      │
                                      ▼
SimulationTickService ─────────► SimulationRuntime
                                      │ atomic mutation/capture
                                      ▼
                            SimulationWorld
                                      │
                                      ▼
                       detached observation read models
                                      │
                                      ▼
                          Observation Gateway
```

Observation Gatewayの詳細は[`observation-gateway.md`](observation-gateway.md)、実装計画・Task状態は[`../../roadmap/GATEWAY_ROADMAP.md`](../../roadmap/GATEWAY_ROADMAP.md)を参照する。

## State ownership

`SimulationRuntime`が`SimulationWorld`を所有する。WebSocket session、connection registry、Protocol message、Observation Gateway、publish read model、Administration Console、Remote MCP adapterはSimulation mutable storeを直接所有しない。

`SimulationRuntime._gate`はauthoritative mutationとatomic observation captureの境界である。publish cycleではClientごとにWorld queryを繰り返さず、lock内で必要なdetached snapshot / read modelをcaptureし、spatial filtering、message planning、cache、encoding、network I/O等を可能な限りlock外で行う。

Administration / Management mutationも同じauthoritative runtime境界を通るため、Simulation tick途中へtransport処理が未管理で割り込まない。Console parse、MCP transport、Observation delivery、表示、file I/OはWorld lockの責務ではない。

## Observation Gateway boundary

View向けWebSocket処理はread-only Observation Gatewayを経由する。

Observation Gatewayが扱うもの:

- `SubscribeVolume`等の観測範囲request
- Inspect系の明示target request
- detached read modelの共有
- spatial filtering
- static / dynamic snapshot delivery
- revision-driven cache
- request deduplication
- encoded payload再利用
- reconnect / resync
- slow client isolation

Observation Gatewayが扱わないもの:

- authoritative mutation
- Simulation rule実行
- Activity / ETA / classification等の意味的判定
- current stateからfuture stateを予測する処理
- ViewのCamera / LODによるSimulation workload / fidelity変更

ViewからServerへの通信が存在しても、それが観測対象を指定するだけならread-only invariantを壊さない。

## Observation cache

Observation Gatewayは同一authoritative resultの再生成・再encodeを避けるため、read-only cacheを持てる。

cache freshnessはwall-clock TTLだけでなく、domainのtick / revision / generation markerを優先する。

代表的なcache:

- Entity Observation Cache: Entity ID + observation revision
- Spatial Observation Cache: chunk / region + observation revision
- Static Revision Cache: Road / Railway / Terrain / Building topology等
- Encoded Payload Cache: negotiated Protocol version + observation revision

同じrevisionの同一requestが同時に来た場合はrequest deduplicationで1回の生成処理を共有できる。

cacheはSimulationが公開した意味を保存・再利用するだけで、新しいsemantic stateを生成しない。

## Administration / Management command boundary

Phase 20でstdinをtransportとして扱い、command実行契約から分離した。Phase 27ではRemote MCPも同じcommand境界を安全に再利用する。将来のManagement Clientも同じserver-authoritative原則に従う。

- `ServerConsoleService`はstdinの1行入力、EOF、host cancellationだけを扱う。
- `AdminCommandParser`はquoted token、`--option=value`、Invariant Culture数値、stable ID、enum表現を`AdminCommand`へ変換する。
- `AdminCommandQueue`はbounded / single-reader channelで、producerへ無制限bufferを許さない。
- `AdminCommandExecutorV2`はqueueをFIFOに逐次処理し、structured result / error codeを返す。
- mutationは`SimulationRuntime`の明示境界だけからauthoritative Worldへ到達する。
- Remote MCPは認証・scope・request isolationを通過した後に同じAdministration境界へ接続し、Simulation内部Storeへ直接アクセスしない。
- 将来のManagement ClientもBrowser側から直接Storeを変更せず、Simulation Roadmapで定義するserver-authoritative command境界を使用する。
- read-only View moduleへこのcommand clientを混在させない。

Consoleはlocal trusted operator向け、Remote MCPは別のauthentication / authorization境界を持つ。Management Clientも利用者向けのpermission / confirmation境界を別途持つ。

Remote MCP詳細は[`remote-mcp-administration.md`](remote-mcp-administration.md)、Management計画は[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)を参照する。

## Administration ordering and pause

`SimulationTickService`のautomatic tickとAdministration / Management mutationは`SimulationRuntime`の同じlockで直列化される。`simulation pause`後はautomatic `Step()`がno-opになる。paused中の`simulation step [count]`だけが明示回数のWorld stepを進めるため、queue中のmutation、manual step、resumeの順序を管理できる。

Administration executor自体もsingle-readerなので、同時producerが存在しても受理済みcommandはqueue順に1件ずつ実行する。

## Runtime topology invalidation

Road / Railway Infrastructure等のrevision-driven observation read modelはauthoritative topology変更時にrevisionを進め、関連cacheをstale化する。次のcaptureで新しいread modelを生成し、接続済みClientは保持しているdelivery stateとの差によって更新を受ける。

World replacement時は関連revision / connection-local delivery stateが新しいWorldと矛盾しないよう再配信可能な状態へ移行する。dynamic Entityはknown-ID差分処理等によってupdate / removeへ収束し、新Worldの現在stateが次回Observation publishの正本になる。

## Administration save / load

`world save`はruntime lock中に`SimulationCheckpoint`をcaptureし、lockを解放した後でserialization / file writeを行う。長時間file I/O中にSimulation lockを保持しない。

`world load`はfile readとdeserializeを先に完了し、検証済み`SimulationWorld`を短時間にatomic差し替えする。Save formatの正本は[`persistence.md`](persistence.md)と[`../specifications/save-data.md`](../specifications/save-data.md)である。

World load後はObservation Gatewayのrevision / cache / connection-local delivery stateを新Worldに対して再同期可能な状態へ移す。

## Saveからのruntime configuration

`Simulation:SavePath`を指定した場合、Save Dataから復元した`SimulationWorld.Config`をruntime正本とする。

- schedulerは復元Worldのtick intervalを使用
- `HelloAck` tick rateも同じ値
- observation subscription cell validationも復元Worldのspatial cell sizeを使用

新規WorldだけServerOptionsからSimulationConfigを構築する。

## Tick lifecycle

`SimulationTickService`は`BackgroundService` / `PeriodicTimer`から`SimulationRuntime.Step()`を呼ぶ。network receive / sendをtick loopへ持ち込まない。application stopping tokenでgraceful shutdownする。

## Observation Request boundary

現行Web Viewからのrequestはread-only observationとして扱う。

現行例:

- 3D `SubscribeVolume`
- `InspectPerson`
- `ClearPersonInspection`

`SubscribeVolume`はfinite、各軸`max >= min`、SpatialGrid変換可能性、`MaximumSubscriptionCellCount`を受付時に検証する。2D `SubscribeArea`互換入口はない。

将来generic inspect / historical observationを追加する場合も、mutation commandとは別contractを維持する。

## Connection state

`ClientConnectionRegistry`はactive connectionごとに少なくとも次を保持する。

- WebSocket / handshake state / negotiated Protocol version
- current `WorldVolume` / subscription revision
- dynamic entity delivery state
- static topology revision / subscription state
- Person inspection等のconnection-local Observation Request state
- send serialization / in-flight delivery state

切断時はconnection-local stateを破棄する。Observation connection stateをSimulation Entity namespaceと混在させない。

## Handshake / capability boundary

1. Clientが`Hello` frame headerで希望versionを提示
2. majorが同じでrequested minorがServer current以下なら受理
3. negotiated versionは要求versionそのもの
4. `HelloAck`と以後のframe headerも同じversion
5. handshake後は受信header versionの完全一致を要求

Server current 2.16はminorごとに次を追加する。

| Protocol | Capability / domain |
| --- | --- |
| 2.0 | Agent / `SubscribeVolume` |
| 2.1 | Road Network |
| 2.2 | Pedestrian |
| 2.3 | Vehicle |
| 2.4 | Intersection Control |
| 2.5 | Population statistics / Person debug |
| 2.6 | Railway Infrastructure |
| 2.7 | Railway Operations |
| 2.8 | Multimodal Transit |
| 2.9 | `ClearPersonInspection` |
| 2.10 | Economy |
| 2.11 | Logistics / Freight |
| 2.12 | Power |
| 2.13 | Water / Sewer |
| 2.14 | Gas |
| 2.15 | Optical Communication |
| 2.16 | Radio / Spectrum |

negotiated minorより新しいmessageを送らない。

## Observation publish read model

Observation publish serviceはSimulation tickとは別周期で動かせる。送信対象が0なら不要なcaptureを避けられるが、View有無によってSimulation自体のstate / workload policy / fidelityを変更してはならない。

capture対象は同一authoritative boundaryから得たdetached dataとし、network threadがSimulationのmutable collectionを直接列挙し続けない。

domainごとに次の配送契約を使い分ける。

- spatial subscriptionでfilterするdynamic / static domain
- revision-driven static read model
- explicit inspect target
- historical read-only projection
- domain固有のbounded observation snapshot

既存のworld-wide statisticsは現行Protocol互換のObservationとして配送できるが、View側で分析を追加しない。将来のAnalytics ListenerはCamera向けObservationと別feedを持てるようにする。

Protocol 2.10〜2.16のEconomy / Logistics / Infrastructure / Communication / Radioも同じServer-authoritative原則に従い、対応minorをnegotiationしたconnectionだけへmessageを送る。詳細なfield / unit / payload contractは[`protocol.md`](protocol.md)と各`docs/specifications/`を正本とする。

## Static / dynamic delivery rules

Road topologyはrevision-drivenで、subscription revision + topology revisionが変わらなければ同じstatic snapshotを無駄に再送しない。

Railway InfrastructureはProtocol 2.6のmulti-frame contractを持ち、1 MiB超snapshotをentity境界でchunkできる。Railway Operations等のsingle-frame domainはcodecのpayload lengthをpreflightし、契約上の上限超過をconnection-localなstructured Errorへ変換する。
slow Viewはconnection単位のdelivery task / timeoutで隔離し、他ClientやSimulation tickへbackpressureを波及させない。

## Subscription revision / remove consistency

subscription変更中に古いdeliveryが完了しても、dynamic known-ID stateは次deliveryのremove生成へ利用できる。一方、static topologyの「配信済みrevision」markerは対応subscription revisionが一致する場合だけcommitする。

これによりvolume移動時のremove欠落と、古いstatic deliveryによる新subscription配信抑止を避ける。

## Send serialization

同一WebSocketへhandshake / error responseとsnapshot publisherが同時sendしないようconnection単位でsendを直列化する。serializationはsend lockの前に行い、lockはWebSocket I/O ownershipだけを守る。

同一Protocol version / observation revision / payloadを多数Viewへ送信する場合は、connection固有metadataと分離可能な範囲でencode済みpayload cacheを検討する。

## Logging / shutdown

expected Client delivery停止とunexpected system faultを区別してstructured logへ記録する。shutdownではhosted serviceとWebSocket sessionをcancelし、新規delivery schedulingを止めてin-flight taskを回収する。

Administration / Managementではunknown command、invalid number / enum、missing entity、reference conflict、queue full、invalid simulation state、I/O errorをstructured resultへ変換し、Server process faultへ昇格させない。Remote MCPでもauthorization failure、oversized input、timeout、rate limit等をrequest単位で隔離する。

## 現行制約

- Protocolは2.x minor negotiationを使用し、Clientが対応しない新domain snapshotを送らない
- 一部static / dynamic snapshotはsingle-frame上限を持ち、Railway Infrastructure等だけが明示chunk contractを持つ
- domainごとのspatial filtering / world-wide deliveryの差は各domain契約に従う
- Administration Consoleはlocal trusted operator interface、Remote MCPは明示設定と認証を必要とするremote interfaceである
- Browser Viewはread-onlyとし、Selection / Inspector / Historical viewingをView Roadmapで管理する
- editor / runtime control / configuration / Save UIはManagement Roadmapで管理する
- AnalyticsはView / Managementの必須責務へ混在させない

将来のSimulation semantic source / authoritative command contractは[`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)、Observation Request / subscription / cache / delivery / Protocol adaptationは[`../../roadmap/GATEWAY_ROADMAP.md`](../../roadmap/GATEWAY_ROADMAP.md)、read-only View実装は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)、Management UIは[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)を参照する。

binary layoutは[`protocol.md`](protocol.md)、Observation Gatewayは[`observation-gateway.md`](observation-gateway.md)、Web側state適用は[`web-client.md`](web-client.md)、Administration command grammarは[`../specifications/server-administration-console.md`](../specifications/server-administration-console.md)を参照する。