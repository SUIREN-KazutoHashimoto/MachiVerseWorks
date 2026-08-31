# Road Network Foundation Specification

## 目的

Road NetworkはRouting・Road Traffic・Intersection Control・Pedestrian・Transitが共有するSimulation正本トポロジーである。表示用meshやWeb Client stateではなくstable ID、3D geometry、明示connectionを保存する。

## 座標とgeometry

Simulation共通契約に従いX/Yを水平面、Zを高度、1 world unitを1 metreとする。`RoadNode.Position`だけがnode位置の正本で、`RoadSegment`は`StartNodeId`と`EndNodeId`を参照する直線3D segmentである。

segment同士が座標上で交差しても接続とはみなさない。平面crossing、高架、地下、橋、トンネルのいずれも共有`RoadNode`が存在しない限りtopology上は非接続。

## Stable ID

RoadNode / RoadSegment / Lane / LaneConnection / RoadAccessPointは独立`ulong` namespace。0 invalid、1から単調増加、削除後も再利用しない。Checkpoint / Saveは各next IDを保存する。

## RoadNode / RoadSegment

`RoadNodeKind`: Endpoint / Intersection。Endpointのincident segmentは最大1本。複数接続は明示Intersectionを使う。

RoadSegmentの両endpointはIDだけでなく3D positionも異ならなければならず、zero-length geometryを許可しない。Create / UpdateRoadSegment、incident segmentを持つUpdateRoadNode、Checkpoint / Save restoreで同じinvariantを適用する。

`RoadKind`: Local / Collector / Arterial / Highway / Service。

## Lane

LaneはRoadSegmentに所属しDirection、Order、WidthMeters、SpeedLimitMetersPerSecondを持つ。ForwardはStart→End、ReverseはEnd→Start。同一segment・direction内でOrderはunique。幅 / speedはfiniteかつ正。

Orderは物理距離ではなく内側→外側の順序key。連番を要求しない。lane centerを求める場合はOrder昇順で先行Lane幅の合計 + 自Lane幅/2をoffsetとし、Forward/Reverseで道路中心線に対する符号を反転する。

## LaneConnection / intersection

Lane間遷移は`LaneConnection`だけが表す。From Laneのexit nodeとTo Laneのentry nodeは同じ`ViaNodeId`で、Via NodeはIntersection。

`TurnMovement`: Unspecified / Straight / Left / Right / UTurn。信号現示・優先関係はIntersection Controlが扱う。

既存LaneConnectionのViaNodeとして参照されるIntersectionは、connectionを解消するまでEndpointへ降格できない。

## RoadAccessPoint / Building / POI boundary

`RoadAccessPoint`はRoadSegment上offset `[0,1]`、任意Building / POI、`RoadAccessMode`を持つ。BuildingまたはPOIの少なくとも一方が必要で、参照先は存在しなければならない。位置はSegment geometryから導出する。

`RoadAccessMode`はMotor / Foot flags。RoadAccessPointから参照されているBuilding / POIは、参照を解消するまで削除できない。

### Railway Platform accessとのcross-domain lifecycle

`PlatformAccessPoint`がRoadAccessPointを参照する場合、Road側mutationもRailwayのwalking invariantを守る。

- 参照中RoadAccessPointは削除できない
- 参照中RoadAccessPointから`Foot` flagを外す更新は拒否する
- Segment / offset / Building / POI等は、RoadAccessPoint自身の参照/invariantを満たし`Foot`を維持する限り更新可能
- valid updateでもPedestrian Networkをinvalidateし、Platform walking routeを次回query時に新geometry / endpointから再構築する

このdependency guardによりRailway側へdangling / non-walkable RoadAccessPoint referenceを残さない。PlatformAccessPoint側の選択semanticsは[`railway-infrastructure.md`](railway-infrastructure.md)を正本とする。

## Atomic mutation

追加・更新は全validation後にstateをcommitする。参照されるNode / Segment / Lane、Building / POI / RoadAccessPointは先に参照を解消しなければ削除できない。

Lane / Segment updateで既存connectionが不正になる場合はrollbackする。失敗mutationはstable ID counterを含むauthoritative stateを途中変更しない。

## 3D spatial query

RoadNodeは3D cell、RoadSegmentは3D AABBをspatial indexへ登録する。巨大segmentはcell全展開を避け別集合でbroad-phase判定する。

query結果はsegment AABBとvolumeの交差を確認し、選択Segmentのendpoint Node、Lane、内部LaneConnection、RoadAccessPointを返す。

Serverのmulti-client配信はpublish cycleのimmutable Road read modelを各volumeへfilterする。static topology revisionとsubscription revisionが不変なら同一Road snapshotを再送しない。

## Protocol boundary

Road NetworkはProtocol 2.1以上。entity ID uniquenessとSegment→Node、Lane→Segment、LaneConnection→Lane/Node、RoadAccessPoint→Segmentのwire参照をcodecで検証する。

Road snapshotはsingle-frame、payload上限1 MiB。超過はServerが送信前に検出し対象subscriptionへstructured Errorを返す。binary layoutは[`../architecture/protocol.md`](../architecture/protocol.md)を正本とする。

## Domain layering

Road Networkは静的3D topologyの正本で、Routingがcost/search、Road TrafficがVehicle state、Intersection Controlがsignal/priority、Pedestrianがwalking graph、Multimodal TransitがBus/Taxi movement referenceとして再利用する。これらderived/domain stateをRoad topologyへ逆流させない。
