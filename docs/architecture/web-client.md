# Web Client Architecture

## 目的

Browser ViewはHeadless ServerのObservation Gatewayから受け取るsnapshot / delta / inspection resultを描画stateへ変換する**完全read-onlyなpresentation層**である。

Simulationの権威・意味的処理・予定・状態遷移はServer / Simulation側に残し、ViewはSimulation更新、運行判断、Activity判定、ETA再計算、分析集計等を行わない。

現行View実装の改善計画・Task状態は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)を正本とする。World / City / Serverを変更するUIは[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)で管理する。Observation GatewayのServer側設計は[`observation-gateway.md`](observation-gateway.md)を参照する。

## Read-only invariant

- Viewはauthoritative mutation commandを送信しない。
- `SubscribeVolume`、Inspect系requestはObservation Requestとして扱い、World stateを変更しない。
- ViewはSimulation内部Storeへ直接アクセスしない。
- ViewのCamera / Selection / LOD / cache / FPS /接続数でSimulation結果を変えない。
- Viewが意味的stateを推測・補完・予測しない。
- Management ClientがView componentを再利用しても、command clientはView moduleとは別に保持する。

## Data flow

```text
WebSocket
  -> MachiVerseConnection
  -> Observation message dispatch
     ├─ Agent EntityStore ----------┐
     ├─ PedestrianStore ------------┤
     ├─ VehicleStore ---------------┤
     ├─ RoadNetwork ----------------┤
     ├─ IntersectionControlStore ---┤
     ├─ Population observation -----┤
     ├─ RailwayInfrastructureLayer -┤
     ├─ RailwayOperationsLayer -----┤
     ├─ Multimodal Transit ---------┤
     ├─ Economy observation --------┤ -> WorldView / Inspector / read-only layers
     ├─ Logistics observation ------┤
     ├─ Power observation ----------┤
     ├─ Water / Sewer observation --┤
     ├─ Gas observation ------------┤
     ├─ Optical observation --------┤
     └─ Radio / Spectrum observation┘
                                      -> Web Audio
```

`connection.ts`はconnection lifecycle、Hello / HelloAck、binary frame decode、reconnectを所有する。現行Web Clientのnegotiation versionは[`WEB_CURRENT_PROTOCOL_VERSION`](../../src/web/src/person-inspection-protocol.ts) = **2.16**。wire contractはC# object graphへ依存せず、TypeScript側にもversioned decoderとして実装する。

C#側Protocolの正本は[`protocol.md`](protocol.md)と`MachiVerseWorks.Protocol`実装である。Web側はnegotiated minorより新しいmessageを前提にせず、各specialized decoderが対応minimum versionを検証する。

## Observation Request

ViewからServerへ送る要求は、観測対象を指定するものに限定する。

現行例:

- `SubscribeVolume`: Camera周辺の観測範囲を指定
- `InspectPerson`: Person詳細の観測targetを指定
- `ClearPersonInspection`: connection-local inspection targetを解除

これらはSimulation mutationではない。将来generic Entity inspectionやHistorical observation requestを追加する場合も、authoritative commandとは別contractとして維持する。

## Coordinate mapping

Simulation正本座標は`(X,Y,Z)`、Zが高度。Three.js / Web Audio境界だけで次へ変換する。

`Simulation (X,Y,Z) -> Three.js / Web Audio (X,Z,Y)`

Agent、Pedestrian、Vehicle、Train、Road / Rail geometry、listener / audio emitterで同じ規則を使う。

## Camera / subscription

`OrthographicCamera`のnear / farを含む8 frustum cornerをworldへunprojectし、Simulation座標へ戻した3D AABBへpaddingを加えて`SubscribeVolume`を生成する。2D rectangleや固定高度bandは使わない。

pan / zoom中は設定周期で再評価し、ほぼ同じvolumeは再送しない。Serverが`subscriptionVolumeTooLarge`を返した場合はzoom-inして再送する。Reconnect後はHelloAck後に最新desired volumeだけを送る。

将来のWorld scale Camera、floating origin、Rendering LOD、View data streamingはView Roadmapで管理し、Camera / LOD / View cacheをSimulation workloadやauthoritative stateへフィードバックしない。

## View-local state

Viewが所有してよいstateはPresentation用途に限定する。

- Camera / focus / follow
- Selection / hover
- renderer resource
- mesh / material / asset cache
- visibility / culling / Rendering LOD
- connection-local known entity / revision state
- interpolation用previous / current visual state

これらはauthoritative World stateではない。

## Dynamic entity state

Agent / Pedestrian / Vehicleはstable ID単位のClient storeを持ち、spawn / update / removeを順序どおり適用する。previous / current 3D positionと受信間隔から描画補間を行う。

補間はdisplay refresh間のvisual smoothingに限定し、Client predictionをauthoritative stateやsemantic futureとして扱わない。

## Object Selection / Inspector

SelectionはView-local stateであり、Simulationへ選択状態そのものを反映しない。

InspectorはObservation Gatewayが提供する値を表示する。目標contractは次の4区分とする。

- Current: 現在のauthoritative observation
- Recent Past: Simulationが公開した直近state / semantic event
- Planned Future: Simulationが公開したschedule / planned action / estimate
- Relations: stable IDによる所属・destination・関連Entity

例えばPersonの位置・時刻・destinationからViewが`Commuting`を判定したり、Trainのposition / speedからView独自ETAを意味的正本として生成したりしない。

## Road / Intersection / Population

Road topologyはstatic revisionとして扱い、同一topologyの再受信で不要なThree.js geometry rebuildを避ける。Intersection Controlはcontroller / movement snapshotをrender stateへ反映する。

PopulationはWorld全体の既存Observationと明示Inspect結果だけを表示し、全Person詳細をvolume購読しない。`ClearPersonInspection`はProtocol 2.9以降の明示clear contractを使用する。

人口分析や長期統計をBrowser Viewで再集計しない。分析が必要な場合は将来のAnalytics Listener / analysis clientへ分離する。

## Railway Infrastructure

`RailwayInfrastructureLayer`はTrack / Station / Platformのstatic Three.js geometryを所有する。

Protocol 2.6 multi-frame contract:

- `isFullSnapshot=true`を受けると、revisionが同じでも保持中Railway stateをresetしてそのframeを新snapshot先頭として適用
- `isFullSnapshot=false`は現在保持中revisionと一致する場合だけcontinuationとして適用
- revision不一致のcontinuationは無視
- 同revision continuationはNode / Segment / Station / Platform mapへaccumulateし、その時点で構築可能なgeometryを更新

chunk index / final markerはないため、WebSocket orderingとServer側entity-order chunkingを前提にする。subscriptionだけ変わりrevisionが同じ場合でも、新delivery先頭full flagで旧volume geometryを除去できる。

## Railway Operations

Protocol 2.7 `RailwayOperationsSnapshot`はTrain position / forwardを直接受信し、`RailwayOperationsLayer`がstable Train IDごとのmeshへ適用する。snapshotに存在しないTrain meshはそのapply時に除去する。

現在のwire contractはFormation定義やFormation lengthを送らないため、Trainは固定BoxGeometryをdebug proxyとして描画する。これは列車長・編成形状の視覚的正本ではない。production Viewへ昇格する際はSimulation / Protocolから必要なauthoritative observationを追加し、View側で編成情報を推測しない。

Railway Debugの「次到着」はSimulation / Serverが提供するschedule semanticsだけを表示する。物理position / speed / block / platform待ちからView側で意味的ETAを再計算しない。

## Multimodal Transit

Protocol 2.8はLine / Stop / Pattern、realtime Bus / Taxi position / state、arrival estimateを受ける。Viewはroute、stop / line、Bus / Taxi、`estimatedArrivalTick`等の提供値を表示する。

arrival semanticsはSimulation / Protocol contractを正とし、mode間でViewが独自に意味を統一・再計算しない。

## Economy / Logistics / Infrastructure / Communication

Protocol 2.10〜2.16で追加されたdomain snapshotは、それぞれのTypeScript decoderとread-only stateへ適用する。

| Protocol | View domain |
| --- | --- |
| 2.10 | Economy |
| 2.11 | Logistics / Freight |
| 2.12 | Power |
| 2.13 | Water / Sewer |
| 2.14 | Gas |
| 2.15 | Optical Communication |
| 2.16 | Radio / Spectrum |

現在のdebug表示はSimulation stateの観測手段である。将来production Viewへ昇格する場合も、View側の表示状態をSimulation正本へ戻さない。

分析overlayやDashboardのための意味的集計はView Roadmapに含めない。Simulationが直接公開するstateを視覚化するだけのlayerはViewに置ける。

## Historical View

Simulation Phase 35がread-only Historical projectionを提供した後、Viewは同じrendering pipelineで過去Worldを表示する。

- timeline / time sliderはHistorical observation targetを変更するだけ
- live Simulationを停止・巻き戻し・変更しない
- 過去時点のSelection / InspectorもHistorical projectionから取得する

## Reconnect / lifecycle

WebSocket close時にdynamic entity、static topology、Population / Transit / Economy / Logistics / Infrastructure / Communication等のconnection-local View stateをclearし、新sessionへ旧connectionのentity / revision stateを持ち越さない。

`dispose()`ではThree.js geometry / materialとAudio resourceを解放する。

## Localization

`locales/manifest.json`の`defaultLocale`が起動locale正本。Protocol ErrorやObservation codeはnumeric / stable codeとstructured parameterのまま受信し、Client resourceへ変換する。

Localization architectureは[`localization.md`](localization.md)、実装計画は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)のLocalization Phaseを参照する。

## Managementとの分離

Editor、build / edit / remove、pause / resume、Server configuration、Save / Load等はViewの責務ではない。

Management Clientが同じ3D画面を必要とする場合はread-only View componentを再利用し、commandはManagement shellの別clientからServer command境界へ送る。

Management計画は[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)を参照する。

## Verification

- unit: codec、store、revision / chunk適用、UI projection
- render: Three.js matrix / geometryを実観測
- E2E: Server / WebSocket / headless browserを接続
- invariance: View接続・Camera・Selection・LOD・cache差でSimulation state digestが変わらないことを検証
- performance: decode / frame / cache metricsとbenchmarkを必要なdomainごとに計測

Protocol binary契約は[`protocol.md`](protocol.md)、Observation Gatewayは[`observation-gateway.md`](observation-gateway.md)を正本とする。
