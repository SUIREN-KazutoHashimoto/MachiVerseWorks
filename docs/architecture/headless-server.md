# Headless Server Architecture

## 概要

Headless ServerはASP.NET Core / Kestrel上でHTTP health endpointとbinary WebSocket endpointを提供し、1つの`SimulationWorld`をserver-authoritativeな正本として所有する。current Protocolは **2.8**。position、subscription、snapshotはnative 3Dである。

```text
Kestrel
├─ GET /health
└─ /ws
   └─ WebSocketSessionHandler
      ├─ ClientConnectionRegistry
      └─ bounded ClientCommandQueue
             │
             ▼
      ClientCommandProcessor

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
                         SimulationPublishSnapshot
                            ├─ spatial domains
                            │    └─ client-volume filtering
                            └─ Multimodal Transit
                                 └─ world-wide mapping
                                      │
                                      ▼
                             SnapshotPublishService

PopulationPublishService ──► statistics / Person debug
```

## State ownership

`SimulationRuntime`が`SimulationWorld`を所有する。WebSocket session、connection registry、Protocol message、publish read model、Administration ConsoleはSimulation mutable storeを直接所有しない。

`SimulationRuntime._gate`はauthoritative mutationとatomic captureの境界である。1 publish cycleではClientごとにWorld queryせず、lock内で必要なdetached snapshot/read modelを1回captureする。Agent / Road / Pedestrian / Vehicle / Intersection / RailwayのClient別`WorldVolume` filtering、message planning、encoding、network I/Oはlock外で行う。Multimodal Transitは同じcaptureを使うが、現行2.8ではClient volumeでfilterせずworld-wide messageへmapする。

Administration mutationも同じ`SimulationRuntime._gate`を通るため、Simulation tickの途中へstdin処理が割り込まない。Consoleのparseや表示、file I/OはWorld lockの責務ではない。

## Administration command boundary

Phase 20ではstdinをtransportとして扱い、command実行契約から分離する。

- `ServerConsoleService`はstdinの1行入力、EOF、host cancellationだけを扱う。
- `AdminCommandParser`はquoted token、`--option=value`、Invariant Culture数値、stable ID、enum表現を`AdminCommand`へ変換する。
- `AdminCommandQueue`はbounded / single-reader channelで、producerへ無制限bufferを許さない。
- `AdminCommandExecutorV2`はqueueをFIFOに逐次処理し、structured `AdminCommandResultCode`と人間向けmessageを返す。
- mutation/readは`SimulationRuntime.Read` / `Mutate`、pause/resume/manual step、checkpoint/world replacement APIだけからauthoritative Worldへ到達する。
- Remote Admin / City Management UIはstdin serviceを再利用せず、認証・認可・監査を別途通過した後に同じcommand queue/executor境界へ接続できる。

Consoleはローカルtrusted operator向けであり、認証境界ではない。外部公開可能なRemote Admin endpointとして扱わない。

## Administration ordering and pause

`SimulationTickService`のautomatic tickとAdministration mutationは`SimulationRuntime`の同じlockで直列化される。`simulation pause`後はautomatic `Step()`がno-opになる。paused中の`simulation step [count]`だけが明示回数のWorld stepを進めるため、queue中のmutation、manual step、resumeの順序は再現可能である。

Administration executor自体もsingle-readerなので、同時producerが存在しても受理済みcommandはqueue順に1件ずつ実行する。

## Runtime topology invalidation

Road / Railway Infrastructureのpublish read modelはrevision-drivenである。Administrationによるtopology mutation成功時、または`world load`でauthoritative Worldを差し替えた時に、対応revisionを単調増加させてcached read modelを破棄する。次のpublish captureで新しいread modelを生成し、接続済みClientは保持しているlast delivered revisionとの差によって再配信を受ける。

World replacement時はRoadとRailwayの両revisionを進める。dynamic Agent / Pedestrian / Vehicle stateは既存known-ID差分処理によってupdate/removeへ収束し、新Worldの現在stateが次回publishの正本になる。

## Administration save / load

`world save`はruntime lock中に`SimulationCheckpoint`をcaptureし、lockを解放した後でdetached `SimulationWorld`をrestoreしてserialize/file writeする。長時間file I/O中にSimulation lockを保持しない。

`world load`はfile readとdeserializeを先に完了し、検証済み`SimulationWorld`を`SimulationRuntime.ReplaceWorld`で短時間にatomic差し替えする。差し替え時にfixture pending state、Road/Railway read-model cacheを破棄し、revisionを進める。

Railway Infrastructureのadministrative update/removeは既存Create APIのvalidationだけでは表現できないため、候補`SimulationCheckpoint`を構築し、`SimulationWorld.RestoreCheckpoint`の完全な参照整合性validationを先に通す。validation成功後のみ現在Railway storeへ反映する。Railway Operationsが初期化済みの場合は既存`EnsureRailwayInfrastructureMutable`契約に従いInfrastructure mutationを拒否する。

## Saveからのruntime configuration

`Simulation:SavePath`を指定した場合、Save Dataから復元した`SimulationWorld.Config`をruntime正本とする。

- schedulerは復元Worldのtick intervalを使用
- `HelloAck` tick rateも同じ値
- subscription cell validationも復元Worldのspatial cell sizeを使用

新規WorldだけServerOptionsからSimulationConfigを構築する。

## Tick lifecycle

`SimulationTickService`は`BackgroundService` / `PeriodicTimer`から`SimulationRuntime.Step()`を呼ぶ。network receive/sendをtick loopへ持ち込まない。application stopping tokenでgraceful shutdownする。

## Client command boundary

network receive pathからSimulation stateを同期的に変更し続けず、Client commandはbounded `Channel<ClientCommand>`へ投入する。

現行subscription commandは3D `SubscribeVolume`。finite、各軸`max >= min`、SpatialGrid変換可能性、`MaximumSubscriptionCellCount`をcommand queue前に検証する。2D `SubscribeArea`互換入口はない。

## Connection state

`ClientConnectionRegistry`はactive connectionごとに少なくとも次を保持する。

- WebSocket / handshake state / negotiated Protocol version
- current `WorldVolume` / subscription revision
- dynamic entity delivery state
- static Road revision/subscription state
- static Railway Infrastructure revision/subscription state
- send serialization / in-flight delivery state

切断時はconnection-local stateを破棄する。Administrationの`connection list` / `show` / `disconnect`はこのregistryだけを操作し、Simulation Entity namespaceと混在させない。

## Handshake / capability boundary

1. Clientが`Hello` frame headerで希望versionを提示
2. majorが同じでrequested minorがServer current以下なら受理
3. negotiated versionは要求versionそのもの
4. `HelloAck`と以後のframe headerも同じversion
5. handshake後は受信header versionの完全一致を要求

Server 2.8はminorごとに次を追加配信する。

- 2.0 Agent
- 2.1 Road
- 2.2 Pedestrian
- 2.3 Vehicle
- 2.4 Intersection Control
- 2.5 Population statistics / Person debug
- 2.6 Railway Infrastructure
- 2.7 Railway Operations
- 2.8 Multimodal Transit

negotiated minorより新しいmessageを送らない。

## Atomic publish read model

`SnapshotPublishService`はSimulation tickとは別周期で動く。subscription済み送信対象が0なら不要なcaptureを避ける。

capture対象は同一Simulation lock / tick時点のdetached dataで、少なくともAgent / Pedestrian / Vehicle、Intersection state、Road、Railway Infrastructure、Railway Operations、Multimodal Transitを含む。Agent / Pedestrian / Vehicle / Intersection / Road / Railway Operations / Railway Infrastructureはcapture後にClient volumeでfilterする。Multimodal Transitはcapture全体をconnectionへmapし、volume boundsを適用しない。

Population statistics / Person inspectorは専用`PopulationPublishService` / inspect command boundaryを持ち、traffic snapshot publish intervalと独立してよい。

## Static Road delivery

Road topologyはrevision-driven。connectionはsubscription revision + road revisionを記録し、両方不変なら同じRoad snapshotを毎tick再送しない。subscription変更またはtopology revision変更時にfiltered snapshotを送る。

Road snapshotはsingle-frame。payload 1 MiB超過をsend前に検出し、対象subscriptionへ`InvalidRequest` / `roadSnapshotTooLarge`を返す。publisher全体のfaultにはしない。

## Railway Infrastructure delivery

Railway InfrastructureはProtocol 2.6のstatic/revision-driven read modelである。subscription変更またはrailway revision変更時にfiltered snapshotを送る。

1 MiB超snapshotは`RailwayInfrastructureProtocolChunker`でentity境界へ分割する。同deliveryの全chunkは同revisionで、先頭だけ`isFullSnapshot=true`、continuationはfalse。BlockSection / Depot 1件は分割しない。

Clientはfull chunkで旧stateをresetし、同revision continuationを順にaccumulateする。このためrailway revisionが同じままsubscriptionだけ変わった場合も、先頭full flagによって旧volume stateを残さない。

## Railway Operations delivery

Protocol 2.7のdynamic message 710はvisible Trainと、そのTrainが参照するService / Timetableをmappingする。Train visibilityはpublish snapshotにある**Train position point**を3D subscriptionへ照合する。

message 710はsingle-frame。`RailwayOperationsProtocolCodec.GetPayloadLength()`でpayload長をpreflightし、1 MiB超過時はpartial snapshotを送らず`InvalidRequest` / `railwayOperationsSnapshotTooLarge`へ変換する。1 Clientの大規模subscriptionをpublisher全体のfaultへ波及させない。

## Multimodal Transit delivery

Protocol 2.8のmessage 720はLine / Stop / Pattern、realtime Bus・Taxi state、arrival estimateを同じpublish captureからmapする。Road TrafficとRailway Operationsのauthoritative movementを複製せず、Multimodal Transitのcross-mode stateだけをwireへ投影する。

現行`PublishConnectionAsync`は`publishSnapshot.MultimodalTransit`全体を`MultimodalTransitMessageMapper`へ渡すため、message 720にはClient `SubscribeVolume`によるspatial filterを適用しない。Clientがsnapshot publisherへ参加するにはsubscription済みである必要があるが、そのvolume boundsはTransitのLine / Stop / Pattern / Vehicle / Arrival Estimate選択には使わない。Protocol 2.8のTransit deliveryはworld-wideで、volume-based interest managementは将来拡張事項である。

2.7以下へmessage 720を送らない。

## Snapshot delivery isolation

connectionごとに最大1件のdelivery taskをin-flightにし、同connectionが配送中なら次周期をqueueせずdropする。異なるconnectionのdeliveryは独立taskなのでslow Clientのbackpressureを他Clientへ伝播させない。

各message sendへtimeoutを適用し、transport由来のexpected Client failureはconnection単位で隔離する。unexpected invariant violationはscheduler faultとして扱う一方、事前分類可能なpayload超過はstructured Client errorへ変換する。

## Subscription revision / remove consistency

subscription変更中に古いdeliveryが完了しても、dynamic known-ID stateは次deliveryのremove生成へ利用できる。一方、static topologyの「配信済みrevision」markerは対応subscription revisionが一致する場合だけcommitする。

これによりvolume移動時のremove欠落と、古いstatic deliveryによる新subscription配信抑止を避ける。

## Send serialization

同一WebSocketへhandshake/error responseとsnapshot publisherが同時sendしないようconnection単位でsendを直列化する。serializationはsend lockの前に行い、lockはWebSocket I/O ownershipだけを守る。

## Logging / shutdown

expected Client delivery停止とunexpected system faultを区別してstructured logへ記録する。shutdownではhosted serviceとWebSocket sessionをcancelし、新規delivery schedulingを止めてin-flight taskを回収する。

Administration Consoleではunknown command、invalid number/enum、missing entity、reference conflict、queue full、invalid simulation state、I/O errorをstructured resultへ変換し、Server process faultへ昇格させない。stdin EOFはConsole Serviceだけを終了し、`exit` / `stop` commandだけが`IHostApplicationLifetime.StopApplication()`を通じてgraceful shutdownを要求する。

## 現行制約

- Agent / Pedestrian / Vehicleは汎用aggregate compressionを持たない
- Roadはsingle-frameでoversize error
- Railway Infrastructureだけが明示multi-frame chunk contractを持つ
- Railway Operationsはsingle-frame + structured oversize error
- Protocol 2.8 Multimodal Transitは現行world-wide deliveryで、Client volume filteringは未実装
- Administration Consoleはlocal trusted operator interfaceであり、remote authentication / authorizationは未実装

binary layoutは[`protocol.md`](protocol.md)、Web側state適用は[`web-client.md`](web-client.md)、Administration command grammarは[`../specifications/server-administration-console.md`](../specifications/server-administration-console.md)を参照する。
