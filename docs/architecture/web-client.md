# Web Client Architecture

## 目的

Browser ClientはHeadless Serverから受け取る**Protocol 2.16まで**のsnapshotを描画・debug UI stateへ変換するpresentation層である。Simulationの権威はServerに残し、ClientはSimulation更新や運行判断を行わない。

現行View実装の改善計画・Task状態は [`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md) を正本とする。本書は現在のWeb Client責務とデータフローを説明するarchitecture文書であり、将来Taskの進捗正本ではない。

## Data flow

```text
WebSocket
  -> MachiVerseConnection
  -> message type dispatch
     ├─ Agent EntityStore ----------┐
     ├─ PedestrianStore ------------┤
     ├─ VehicleStore ---------------┤
     ├─ RoadNetwork ----------------┤
     ├─ IntersectionControlStore ---┤
     ├─ Population debug -----------┤
     ├─ RailwayInfrastructureLayer -┤
     ├─ RailwayOperationsLayer -----┤
     ├─ Multimodal Transit debug ---┤
     ├─ Economy debug --------------┤
     ├─ Logistics debug ------------┤ -> WorldView / debug UI / overlays
     ├─ Power debug ----------------┤
     ├─ Water / Sewer debug --------┤
     ├─ Gas debug ------------------┤
     ├─ Optical debug --------------┤
     └─ Radio / Spectrum debug -----┘
                                      -> Web Audio
```

`connection.ts`はconnection lifecycle、Hello/HelloAck、binary frame decode、reconnectを所有する。現行Web Clientのnegotiation versionは [`WEB_CURRENT_PROTOCOL_VERSION`](../../src/web/src/person-inspection-protocol.ts) = **2.16**。wire contractはC# object graphへ依存せず、TypeScript側にもversioned decoderとして実装する。

C#側Protocolの正本は [`protocol.md`](protocol.md) と `MachiVerseWorks.Protocol` 実装である。Web側はnegotiated minorより新しいmessageを前提にせず、各specialized decoderが対応minimum versionを検証する。

## Coordinate mapping

Simulation正本座標は`(X,Y,Z)`、Zが高度。Three.js / Web Audio境界だけで次へ変換する。

`Simulation (X,Y,Z) -> Three.js / Web Audio (X,Z,Y)`

Agent、Pedestrian、Vehicle、Train、Road/Rail geometry、listener/audio emitterで同じ規則を使う。

## Camera / subscription

`OrthographicCamera`のnear/farを含む8 frustum cornerをworldへunprojectし、Simulation座標へ戻した3D AABBへpaddingを加えて`SubscribeVolume`を生成する。2D rectangleや固定高度bandは使わない。

pan/zoom中は設定周期で再評価し、ほぼ同じvolumeは再送しない。Serverが`subscriptionVolumeTooLarge`を返した場合はzoom-inして再送する。Reconnect後はHelloAck後に最新desired volumeを送る。

将来のWorld scale Camera、floating origin、Rendering LOD、View data streamingはView Roadmapで管理し、Camera / LOD / View cacheをSimulation workloadやauthoritative stateへフィードバックしない。

## Dynamic entity state

Agent / Pedestrian / Vehicleはstable ID単位のClient storeを持ち、spawn/update/removeを順序どおり適用する。previous/current 3D positionと受信間隔から描画補間を行い、Client predictionをauthoritative stateとして扱わない。

## Road / Intersection / Population

Road topologyはstatic revisionとして扱い、同一topologyの再受信で不要なThree.js geometry rebuildを避ける。Intersection Controlはcontroller/movement snapshotをdebug/render stateへ反映する。

PopulationはWorld全体statisticsと明示`InspectPerson`結果だけをUIへ表示し、全Person詳細をvolume購読しない。`ClearPersonInspection`はProtocol 2.9以降の明示clear contractを使用する。

## Railway Infrastructure

`RailwayInfrastructureLayer`はTrack / Station / Platformのstatic Three.js geometryを所有する。

Protocol 2.6 multi-frame contract:

- `isFullSnapshot=true`を受けると、**revisionが同じでも**保持中Railway stateをresetしてそのframeを新snapshot先頭として適用
- `isFullSnapshot=false`は現在保持中revisionと一致する場合だけcontinuationとして適用
- revision不一致のcontinuationは無視
- 同revision continuationはNode / Segment / Station / Platform mapへaccumulateし、その時点で構築可能なgeometryを更新

chunk index/final markerはないため、WebSocket orderingとServer側entity-order chunkingを前提にする。subscriptionだけ変わりrevisionが同じ場合でも、新delivery先頭full flagで旧volume geometryを除去できる。

## Railway Operations

Protocol 2.7 `RailwayOperationsSnapshot`はTrain position / forwardを直接受信し、`RailwayOperationsLayer`がstable Train IDごとのmeshへ適用する。snapshotに存在しないTrain meshはそのapply時に除去する。

現在のwire contractはFormation定義やFormation lengthを送らないため、Trainは**18 × 3 × 3 world-unitの固定BoxGeometry**をdebug proxyとして描画する。これは列車長・編成形状の視覚的正本ではない。

Serverの3D subscription判定もTrain body envelopeではなく`TrainSnapshot.Position` pointに基づく。長いFormationがvolumeへ一部だけ交差する意味は現Protocol 2.7では表現しない。

Railway Debugの「次到着」は、次Timetable stopの`plannedArrivalTick + service.delayTicks`を表示する**schedule-based projection**である。物理position / speed / block/platform待ちから毎tick再計算するkinematic ETAではない。新しい遅延はRailway Operationsのarrival時に`DelayTicks`へ反映された後の表示から効く。

## Multimodal Transit

Protocol 2.8はLine / Stop / Pattern、realtime Bus / Taxi position/state、arrival estimateを受ける。Transit Debugはroute、stop/line数、Bus/Taxi、`estimatedArrivalTick`を表示する。

このTransit arrival estimateはMultimodal Transit contractであり、前節のRailway Debug schedule projectionとは別物として扱う。Railway Serviceへの参照があってもUI上のarrival semanticsを混同しない。

## Economy / Logistics / Infrastructure / Communication

Protocol 2.10〜2.16で追加されたdomain snapshotは、それぞれのTypeScript decoderとdebug stateへ適用する。

| Protocol | Web Client domain |
| --- | --- |
| 2.10 | Economy |
| 2.11 | Logistics / Freight |
| 2.12 | Power |
| 2.13 | Water / Sewer |
| 2.14 | Gas |
| 2.15 | Optical Communication |
| 2.16 | Radio / Spectrum |

これらのdebug viewはSimulation stateの観測手段であり、Client側の表示状態をSimulation正本へ戻さない。将来の本格的なregional overlay、Inspector、Dashboard、管理UIはView Roadmapへ分離する。

## Reconnect / lifecycle

WebSocket close時にdynamic entity、static topology、Population / Transit / Economy / Logistics / Infrastructure / Communication等のconnection-local view/debug stateをclearし、新sessionへ旧connectionのentity/revision stateを持ち越さない。

`dispose()`ではThree.js geometry/materialとAudio resourceを解放する。

## Localization

`locales/manifest.json`の`defaultLocale`が起動locale正本。Protocol Errorはnumeric/stable codeとstructured parameterのまま受信し、Client resourceへ変換する。

Localization architectureは[`localization.md`](localization.md)、実装計画は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)のView Phase 5を参照する。

## Verification

- unit: codec、store、revision/chunk適用、UI projection
- render: Three.js matrix / geometryを実観測
- E2E: `.github/workflows/e2e.yml`からServer / WebSocket / headless browserを接続
- performance: decode/frame metricsとbenchmarkを必要なdomainごとに計測

Protocol binary契約は[`protocol.md`](protocol.md)を正本とする。
