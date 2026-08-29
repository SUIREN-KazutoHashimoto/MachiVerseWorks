# Road Network Foundation Specification

## 目的

Road NetworkはRouting・Road Traffic・Intersection Controlが共有するSimulation正本トポロジーである。表示用meshやWeb Client状態ではなく、stable ID・3D geometry・明示的接続を保存する。

## 座標とgeometry

既存のSimulation World契約に従い、X/Yを水平面、Zを高度、1 world unitを1 metreとする。`RoadNode.Position`だけがnode位置の正本で、`RoadSegment`は`StartNodeId`と`EndNodeId`を参照する直線3D segmentである。segmentを曲線化する場合は後続仕様でgeometry表現を拡張する。

座標上でsegment同士が交差しても接続とはみなさない。平面交差、同高度のcrossing、高架、地下、橋、トンネルのいずれも、共有`RoadNode`が存在しない限りtopology上は非接続である。この規則により高さや投影交差だけから誤ったintersectionを生成しない。

## stable ID

`RoadNodeId`、`RoadSegmentId`、`LaneId`、`LaneConnectionId`、`RoadAccessPointId`はそれぞれ独立した`ulong` namespaceを持つ。0は無効、1から単調増加し、削除後も再利用しない。checkpoint / Save Dataは各next IDを保存する。

## RoadNode / RoadSegment

`RoadNodeKind`は`Endpoint`と`Intersection`を持つ。Endpointのincident segmentは最大1本で、複数segmentを接続する場合は明示的にIntersectionへ変更する。これによりgeometry上の偶然の接触をtopologyへ昇格させない。

`RoadKind`は`Local`、`Collector`、`Arterial`、`Highway`、`Service`を定義する。

## Lane

LaneはRoadSegmentに所属し、`Direction`、`Order`、`WidthMeters`、`SpeedLimitMetersPerSecond`を持つ。`Forward`はsegmentのStart→End、`Reverse`はEnd→Startである。同一segment・同一direction内で`Order`は一意とする。幅と速度上限はfiniteかつ0より大きい。

## LaneConnection / intersection

車線間移動は`LaneConnection`だけが表す。connectionは`FromLaneId`、`ToLaneId`、`ViaNodeId`、`TurnMovement`を持つ。From Laneの退出nodeとTo Laneの進入nodeは同じ`ViaNodeId`でなければならず、Via Nodeは`Intersection`でなければならない。

`TurnMovement`は`Unspecified`、`Straight`、`Left`、`Right`、`UTurn`を持つ。これは現Phaseでは意味分類であり、信号現示や優先関係はPhase 14で扱う。

## Building / POI access boundary

`RoadAccessPoint`はRoadSegment上の正規化offset `[0,1]` と、任意の`BuildingId` / `PoiId`、`RoadAccessMode`を持つ。BuildingまたはPOIの少なくとも一方を必須とし、参照先はSimulation内に存在しなければならない。位置はsegment両端から導出でき、都市オブジェクト側のgeometryをRoad modelへ複製しない。

`RoadAccessMode`は`Motor`と`Foot`のflagsを持つ。将来の歩道・駐車場・鉄道接続はこの境界を拡張し、Building / POIそのものへRoad固有topologyを埋め込まない。

## atomic mutation

追加・更新は全validation完了後にstateを変更する。参照されているNode / Segment / Laneは先に参照を解消しない限り削除できない。Lane更新で既存connectionが不正になる場合は更新をrollbackする。

## 3D spatial query

RoadNodeは3D cell、RoadSegmentは3D AABBをspatial indexへ登録する。巨大segmentはcell全展開を避け、別集合でbroad-phase判定する。query結果はsegment AABBとvolumeの交差を確認し、選択segmentのendpoint node、lane、内部lane connection、access pointを一緒に返す。

## Phase 11以降

経路コスト・route searchはPhase 12、車両状態とlane走行はPhase 13、信号とintersection制御はPhase 14で扱う。Phase 11はそれらが参照する静的な3D topologyだけを正本化する。
