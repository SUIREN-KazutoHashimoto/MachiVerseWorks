# Road Network Architecture

## 責務境界

Road Networkの正本は`MachiVerseWorks.Simulation`に置く。`RoadNode`、`RoadSegment`、`Lane`、`LaneConnection`、`RoadAccessPoint`は表示都合ではなくRoutingとTrafficが参照するSimulation stateであり、Web ClientはProtocol snapshotから再構築した描画専用stateだけを保持する。

`MachiVerseWorks.Persistence`はRoad NetworkをSave formatへ変換し、`MachiVerseWorks.Protocol`はwire representationを定義する。`MachiVerseWorks.Server`はsubscription volumeでSimulation snapshotを取得してProtocolへ写像する。Web Clientは`RoadNetworkStore`でstable IDを保持し、Three.js rendererへ渡す。

## 接続の正本

geometryの交差はtopology接続を意味しない。Segment間接続は共有`RoadNode`、Lane間移動は`LaneConnection`だけが表す。したがって同一XYで交差する地上道路と高架道路、地下道路は、明示Nodeを共有しない限り互いに到達不能のままである。

Intersectionは`RoadNodeKind.Intersection`として明示する。Endpointへ2本目のSegmentを接続する操作は拒否し、暗黙のintersection生成を禁止する。

## 3D spatial query

RoadNodeは既存world gridと同じcell sizeで3D cellへ登録する。RoadSegmentは両端から得られる3D AABBをcellへ登録し、過大なcell展開になる長大Segmentはlarge-segment集合へ退避してAABB broad-phaseで判定する。query結果には選択Segmentのendpoint Node、Lane、内部LaneConnection、RoadAccessPointを含める。

## Building / POI境界

道路固有情報をBuilding / POIへ埋め込まず、`RoadAccessPoint`がRoadSegment上の正規化offsetとBuilding / POI stable IDを参照する。これにより後続のRouting、Pedestrian、Parking等が同じUrban World entityを共有できる。

## Save / Protocol互換性

Road Network追加に伴いSave formatは4、Protocolは2.1へ更新する。Save format 3は道路なしのWorldとしてformat 4へ移行できる。Protocol 2.0 connectionは引き続きAgent messageを利用でき、Road Network snapshotは2.1をnegotiationしたconnectionだけへ送る。

## Browser描画

Simulation `(X, Y, Z)` は既存契約どおりThree.js `(X, Z, Y)`へ写像する。RoadSegmentは中心線、Laneはsegment水平法線方向へ幅と順序に基づいてoffsetした線、Intersectionはpointとして描画する。描画座標からtopologyを推測・変更しない。

## 後続Phase

Phase 12はこのLane / LaneConnectionをrouting graphへ変換する。Phase 13はRouteに沿うVehicle stateを追加し、Phase 14はIntersection movementとsignal controlを追加する。Phase 11の静的topologyへroute cacheやVehicle occupancyを混在させない。
