# Headless Server Architecture

## 概要

Headless ServerはASP.NET Core / Kestrel上でHTTP health endpointとbinary WebSocket endpointを提供し、1つの`SimulationWorld`をserver-authoritativeな正本として所有する。位置・速度・subscription・snapshotはフルネイティブ3Dで、current Protocolは2.2である。

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

SimulationTickService ──► SimulationRuntime
                               │
                               │ atomic capture / publish cycle
                               ▼
                     SimulationPublishSnapshot
                        ├─ Agent values
                        ├─ Pedestrian values
                        └─ Road read model + revision
                               │
                     lock-free client filtering
                               ▼
                      SnapshotPublishService
```

## State ownership

`SimulationRuntime`が`SimulationWorld`を所有する。WebSocket session、connection registry、Protocol message、publish read modelはSimulationのmutable storeを直接所有しない。

`SimulationRuntime._gate`はauthoritative mutationとatomic captureの境界である。1回のpublish cycleではClientごとにWorld queryを行わず、lock内でAgent / Pedestrian / Roadとtickを1回だけdetached valueへcaptureする。Client別`WorldVolume` filtering、message planning、encoding、network I/Oはlock外で行う。

これにより10 / 100 Clientへ異なるsubscriptionを配信しても、Client数に比例して`Step()`と同じglobal lockを長時間占有しない。

## Saveからのruntime configuration

`Simulation:SavePath`を指定した場合、Save Dataから復元した`SimulationWorld.Config`をruntimeの正本とする。

- tick schedulerは`simulation.TickRate` / `simulation.TickInterval`を使用する。
- `HelloAck`のtick rateも同じ復元値を通知する。
- subscription cell validationは`simulation.SpatialCellSize`を使用する。

起動時`ServerOptions`の`Simulation:TickRate` / `Simulation:SpatialCellSize`とSave内設定が異なっても、復元済みWorldとscheduler / guardで別々の値を使わない。新規Worldを作る場合だけServerOptionsからSimulationConfigを構築する。

## Simulation tick lifecycle

`SimulationTickService`は`BackgroundService`と`PeriodicTimer`を使い、`SimulationRuntime`が公開するtick intervalで`Step()`を呼ぶ。network receive / sendをtick loopへ持ち込まず、application stopping tokenによりgraceful shutdownする。

## Client command boundary

network receive pathからSimulation / connection stateを同期的に横断して変更し続けないため、Client commandはbounded `Channel<ClientCommand>`へ投入する。

現在のsubscription commandは`SubscribeVolume`である。2D `SubscribeArea`互換入口は持たない。

`SubscribeVolume`はcommand queueへ投入する前にserver policyで検証する。座標はfiniteかつ各軸`max >= min`を要求し、volume両端が`SpatialGrid`へ変換できることを確認する。

走査対象セル数は`cellsX × cellsY × cellsZ`で数え、`Server:MaximumSubscriptionCellCount`以下に制限する。既定値は262,144 cells、既定cell sizeはSimulation Worldの64mである。

Web Clientは16:9だけを前提にせず、21:9等の横長viewportでも既定budget内へ収まるzoom下限を適用する。Serverがより厳しいbudgetで`subscriptionVolumeTooLarge`を返した場合はClientがzoom-inして新しいvolumeを再送し、viewportだけが最後に受理されたsubscriptionより広い状態を放置しない。

## Connection state

`ClientConnectionRegistry`がactive connectionを管理する。各connectionは次を保持する。

- WebSocket
- handshake完了状態
- negotiated Protocol version
- current `WorldVolume` subscription
- subscription revision
- Clientが既に認識しているAgent ID set
- Clientが既に認識しているPedestrian ID set
- 最後に配信済みのRoad topology revisionとsubscription revision

connection切断時はregistryから削除し、subscription / known entity / Road delivery stateもconnectionと一緒に破棄する。

## Handshake

1. Clientが`Hello` frame headerで希望Protocol versionを提示する。
2. Serverは同一majorかつ`requested minor <= current minor`の場合だけ受理する。
3. negotiated versionはClientが要求したversionそのものとする。
4. Serverは同じversionをconnection state、`HelloAck` payload、以後のframe headerへ使用する。
5. handshake後は受信frame headerがnegotiated versionと完全一致することを要求する。

Server 2.2 / Client 2.0ではAgentだけ、2.1ではRoadまで、2.2ではPedestrianまで配信する。

## Atomic publish read model

`SnapshotPublishService`はSimulation tickとは別の`PeriodicTimer`で動く。各cycleでまず送信可能なsubscription済みconnectionを収集し、対象が0件ならSimulation snapshotを生成しない。

対象Clientがある場合、`SimulationRuntime.CapturePublishSnapshot()`を**cycleにつき1回**呼ぶ。このcapture内で次を同一lock・同一tick時点から取得する。

- batch `TickCount`
- 全active Agentのdetached snapshots
- 全Pedestrianのdetached snapshots
- Road Networkのimmutable read modelとrevision

その後、各connectionは共有`SimulationPublishSnapshot.Query(volume)`をlock外で実行する。Agent / Pedestrian / Road / remove metadataはbatchの同じtickを使用するため、publish途中にSimulationがstepしても1回のdelivery内へ異なるtickを混在させない。

10 / 100 Client条件のread-model queryは専用benchmark workflowでaverage / p95 / p99とallocationを記録する。

## Road topology delivery

Road Networkは現時点ではServer経由で静的topologyとして扱う。SimulationRuntimeはRoad read modelをgeneration単位で保持し、topology変更時だけrevisionを更新する。

connectionは`subscription revision + road revision`を記録し、両方が不変なら次のAgent/Pedestrian snapshot周期でRoad全体を再送しない。subscription変更またはRoad revision変更時だけ最新Road snapshotを送る。

Web側も同一Road topologyの再受信ではRoad store generationを進めず、Three.js geometryの全再構築を発生させない。

### Road payload 1 MiB boundary

Protocol frame payload上限は1 MiBであり、Road snapshotは送信前に固定layoutからpayload bytesを計算する。上限を超える場合、`ProtocolCodec.Serialize`へ到達させず対象Clientへ`InvalidRequest` / detail code `roadSnapshotTooLarge`を送る。

これはsubscription固有の拒否であり、`SnapshotDeliveryScheduler`のunexpected system faultとして記録しない。他ClientのAgent/Road/Pedestrian配信は継続する。現Protocol 2.1/2.2ではRoad chunkingを暗黙導入せず、将来必要になればversioned contractとして追加する。

## Snapshot delivery isolation

snapshot publisherはconnectionごとに最大1件のdelivery taskだけをin-flightとして保持する。同じconnectionが配送中なら次のpublish周期はqueueせずdropする。異なるconnectionのdeliveryは独立taskとして進むため、slow Clientのnetwork backpressureを他Clientへ伝播させない。

各deliveryではlinked `CancellationTokenSource`を1つ作り、各message send直前に5秒timeoutを再設定する。5秒以内に1messageを送信できないconnectionはabortしてregistryから除外する。

transport由来のexpected Client failureだけをconnection隔離対象とする。それ以外のunexpected invariant violationはCritical logとscheduler faultとしてServerへ伝播する。ただし事前に分類可能なRoad payload超過はstructured Client errorへ変換済みなのでunexpected exception扱いしない。

## Subscription revisionとremove整合性

subscription変更中に古いdeliveryが完了しても、古いvolumeで送信済みのknown Agent / Pedestrian ID集合は次deliveryでremoveを生成するために反映する。一方、Roadの「このsubscriptionへ配信済み」というrevision markerは対応するsubscription revisionが一致する場合だけcommitする。

これによりvolume移動時に旧entityのremoveを失わず、古いRoad deliveryだけで新subscriptionのRoad配信を抑止しない。

## Send serialization

同一WebSocketへhandshake/error responseとsnapshot publisherが同時sendしないよう、connection単位でsendを直列化する。Protocol serializationはsend lockの前に行い、WebSocket I/O ownershipだけを排他する。

## Logging / graceful shutdown

Server structured loggingはsource-generated `LoggerMessage`を使用する。expected Client delivery停止はDebug、unexpected snapshot delivery faultはCriticalで記録する。

application stopではhosted service cancellation tokenとWebSocket session linked tokenをcancelする。snapshot publisherは新規scheduleを停止後、既存in-flight taskを回収して終了する。integration testではserver stop後にSimulation tickが増えないことを確認する。

## 現段階の制約

Agent / Pedestrianはentityごとのframeで送信しており、汎用batching / compressionは未導入である。Roadはstatic revision抑制を行うが、1 MiB超のtopology chunkingはまだ提供しない。これらはProtocol互換性を保ったまま暗黙変更せず、計測結果を基にversioned contractとして拡張する。
