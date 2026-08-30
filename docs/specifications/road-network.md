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

`Order`は**物理距離ではなく内側から外側への順序key**であり、0からの連番を要求しない。表示や将来のlane center geometryを求める場合は同一segment・directionのLaneを`Order`昇順に並べ、対象Laneより前の`WidthMeters`合計 + 自Lane幅の1/2をcenter offsetとする。したがって可変幅Laneでもgap / overlapを作らず、`Order=0, 5, 20`のような欠番を距離へ直接換算しない。Forward / Reverseでは道路中心線に対するoffset符号を反転する。

## LaneConnection / intersection

車線間移動は`LaneConnection`だけが表す。connectionは`FromLaneId`、`ToLaneId`、`ViaNodeId`、`TurnMovement`を持つ。From Laneの退出nodeとTo Laneの進入nodeは同じ`ViaNodeId`でなければならず、Via Nodeは`Intersection`でなければならない。

`TurnMovement`は`Unspecified`、`Straight`、`Left`、`Right`、`UTurn`を持つ。これは現Phaseでは意味分類であり、信号現示や優先関係はPhase 14で扱う。

既存`LaneConnection`の`ViaNodeId`として参照されているIntersectionは、参照を解消するまで`Endpoint`へ降格できない。RoadNode種別変更によって既存connectionの不変条件を破壊することを禁止する。

## Building / POI access boundary

`RoadAccessPoint`はRoadSegment上の正規化offset `[0,1]` と、任意の`BuildingId` / `PoiId`、`RoadAccessMode`を持つ。BuildingまたはPOIの少なくとも一方を必須とし、参照先はSimulation内に存在しなければならない。位置はsegment両端から導出でき、都市オブジェクト側のgeometryをRoad modelへ複製しない。

`RoadAccessMode`は`Motor`と`Foot`のflagsを持つ。将来の歩道・駐車場・鉄道接続はこの境界を拡張し、Building / POIそのものへRoad固有topologyを埋め込まない。

RoadAccessPointから参照されているBuilding / POIは、該当RoadAccessPointを削除または更新して参照を解消するまで削除できない。Road側とUrban World側のどちらから操作してもdangling stable IDを残さない。

## atomic mutation

追加・更新は全validation完了後にstateを変更する。参照されているNode / Segment / Lane、RoadAccessPointから参照されているBuilding / POIは、先に参照を解消しない限り削除できない。Lane更新やRoadSegment更新で既存connectionが不正になる場合は更新をrollbackする。LaneConnectionのViaNodeとなるIntersectionの種別変更もstate変更前に拒否する。

失敗したmutationはstable ID counterを含む正本stateを成功時だけ進め、途中状態やdangling connectionを観測可能にしない。

## 3D spatial query

RoadNodeは3D cell、RoadSegmentは3D AABBをspatial indexへ登録する。巨大segmentはcell全展開を避け、別集合でbroad-phase判定する。query結果はsegment AABBとvolumeの交差を確認し、選択segmentのendpoint node、lane、内部lane connection、access pointを一緒に返す。

Serverのmulti-client配信ではauthoritative Road storeをClientごとにqueryせず、publish cycleのimmutable Road read modelを各volumeへfilterする。静的topology revisionとsubscription revisionが不変ならRoad snapshotを再送しない。

## Protocol境界

Road Network wire contractはProtocol 2.1以上。entity種別ごとのID uniquenessとSegment→Node、Lane→Segment、LaneConnection→Lane/Node、RoadAccessPoint→Segmentの参照整合性をC# serializer / decoderとWeb decoderで同じように検証する。

Road snapshotは現Protocolでは単一frameで、payload上限は1 MiB。上限超過はServerが送信前に検出し対象subscriptionへ構造化Errorを返す。publisher全体のfaultにはしない。binary layoutは[`../architecture/protocol.md`](../architecture/protocol.md)を正本とする。

## Phase 11以降

経路コスト・route searchはPhase 12、車両状態とlane走行はPhase 13、信号とintersection制御はPhase 14で扱う。Phase 11はそれらが参照する静的な3D topologyだけを正本化する。
