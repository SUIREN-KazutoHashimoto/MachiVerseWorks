# Railway Infrastructure Specification

## Purpose

Phase 17で導入したauthoritativeな静的Railway Infrastructureを定義する。scopeはTrack topology、BlockSection、Station、Platform、pedestrian access、Depot。Train / Timetable / Service等の動的運行stateは[`railway-operations.md`](railway-operations.md)が所有する。

## Coordinate / topology

Railway geometryはSimulation共通の3D world座標を使う。X/Yが水平metres、Zが高度metres。`TrackSegment`は2つの`TrackNode`を結ぶ直線3D segmentである。

geometry上の交差はconnectivityを意味しない。共有`TrackNode`と明示`TrackConnection`がある場合だけsegment間を遷移できる。橋、トンネル、stacked track、same-XY crossingを暗黙接続しない。

## Stable IDs

TrackNode / TrackSegment / TrackConnection / BlockSection / Station / Platform / PlatformAccessPoint / Depotは独立したmonotonic `ulong` stable IDを持つ。0はinvalid。Checkpoint / Saveはentity IDとnext-ID counterの両方を保持する。

## TrackNode / TrackSegment

Node kind:

- `Endpoint`: incident segmentは最大1
- `Junction`: 複数segmentを接続可能
- `Switch`: 複数segmentを接続可能なswitch topology

Segmentはstart/end Node、direction、gauge、speed limit、electrification、usageを持つ。gauge / speedはfiniteかつ正。start/end IDおよび3D positionが同じzero-length segmentをauthoritative stateとして許可しない。

## TrackConnection

`TrackConnection`が唯一のsegment-to-segment traversable edge。from/to Segmentはvia Junction/Switchへincidentし、direction contractがarrival / departureを許可しなければならない。

Connectionはdirected。双方向遷移が必要なら2 connectionを定義する。

## Connectivity diagnostics

`ValidateRailwayInfrastructure()`は次を返す。

- `TrackComponentCount`: TrackSegmentをvertex、各**directed** `TrackConnection`を診断上は**undirected adjacency**として扱ったときのweakly connected component数
- `TraversableConnectionCount`: 登録済みdirected `TrackConnection` record数

したがって`TrackComponentCount`はdirected reachabilityやstrongly connected component数ではない。Track direction / Connection directionを考慮した「経路として到達できるか」はRailway Route validation側の責務である。

## BlockSection

1つ以上のTrackSegment IDを持ち、1 Segmentは高々1 BlockSectionへ所属する。1 BlockSectionのmembershipは最大100,000件。

このhard limitはpublic mutation、Checkpoint / Save restoreの両方へ適用し、単一BlockSection itemがProtocol 1 MiB frameへ収まる範囲を保証する。Block ownership / reservationはRailway Operations stateである。

## Station / Platform

Stationは3D `WorldVolume`。Platformは1 Stationと1 TrackSegmentを参照し、segment上の`[startSegmentOffset,endSegmentOffset]`と3D boundsを持つ。

`0 <= start < end <= 1`を要求する。

## Platform pedestrian access

`PlatformAccessPoint`はPlatformと既存`RoadAccessPoint`を結ぶ。作成時、RoadAccessPointが存在し`RoadAccessMode.Foot`を含むことを要求する。

この参照はlifecycle invariantでもある。

- PlatformAccessPointから参照されているRoadAccessPointは削除できない
- 参照中RoadAccessPointの`Foot` flagを外す更新は拒否する
- Segment / offset / Building / POI等の他fieldは、RoadAccessPoint自身のinvariantを満たし`Foot`を維持する限り更新できる
- RoadAccessPoint更新はderived Pedestrian Networkをinvalidateし、次route queryで再構築する

### `FindWalkingRouteToPlatform`

1 Platformに複数accessがある場合の選択はdeterministicである。

1. PlatformAccessPointをstable ID昇順で列挙
2. 各RoadAccessPointからPOI endpoint、次にBuilding endpointの順で候補化
3. reachable routeの`TotalLengthMeters`が最短のものを選択
4. 距離が完全一致する場合は小さいPlatformAccessPoint IDを優先
5. 同一PlatformAccessPoint内でPOI / Building route距離まで同一なら、候補順によりPOIが先に維持される

到達可能なaccessが1つもなければ`InvalidOperationException`。

## Depot / siding

Depotは3D volumeと1つ以上のTrackSegmentを持つ。membershipはSiding / Depot usageを受理しMainlineを拒否する。1 Depotのmembershipも最大100,000件。

Train storage / departure / completion lifecycleはRailway Operationsが所有する。

## Spatial query

`CreateRailwayInfrastructureSnapshot(WorldVolume)`は3D volumeと交差するTrack、関連Station / Platform / Depot等を返す。Segmentは3D AABBでbroad-phase選択する。

## Persistence

Railway InfrastructureはSave Format 8で導入された。current Save Format 10でも同じstable topology contractを保持する。Format 3〜7は空Railway stateへmigrationする。

外部Saveではtop-level collectionとnested Block/Depot membershipの両方をDTO materialization前にbounded scanする。100,000件domain hard limitはrestore validationにも適用する。

## Protocol / Server distribution

Protocol 2.6 message 700 `RailwayInfrastructureSnapshot`を使用する。revision、`isFullSnapshot`、Node / Segment / Connection / Block / Station / Platform / PlatformAccessPoint / Depotを持つ。

Serverはstatic topology revisionとClient subscriptionを追跡し、subscription変更またはrailway revision変更時にfiltered snapshotを送る。毎Simulation tick同一topologyを再送しない。

### Multi-frame contract

1 MiBを超えるsnapshotはentity境界でchunkする。

- 同deliveryの全frameは同じrevision
- full deliveryの**先頭frameだけ**`isFullSnapshot=true`
- continuationは`false`
- entity orderはNode → Segment → Connection → Block → Station → Platform → PlatformAccessPoint → Depot
- Block / Depot 1 itemは分割しない
- chunk index / total count / final markerは持たない

Web Clientはfull frameを受けると同revisionでも旧stateをresetする。同revision continuationだけをaccumulateし、revisionが一致しないcontinuationは無視する。WebSocket orderingを前提とする。

subscriptionだけが変化してrevisionが同じ場合も、新filtered delivery先頭はfull=trueなので旧volume由来entityを残さない。

## Web rendering

Trackは3D line、Station / Platformはwireframe volumeとして描画する。Simulation `(X,Y,Z)`をThree.js `(X,Z,Y)`へ境界変換する。static geometryはRailway Infrastructure layerが所有する。

## Verification

- Simulation: topology / crossing isolation / access lifecycle / route tie-break / checkpoint
- Persistence: Format 8+ roundtrip / migration / bounded membership
- Protocol: 2.6 codec / malformed payload / chunk boundary
- Web: decoder / full+continuation revision semantics / Three.js geometry
- E2E: `.github/workflows/e2e.yml`の`save-server-browser-railway`
- Benchmark: `.github/workflows/benchmarks.yml`の`railway-10k-100k`

benchmark scenarioと参考baselineは[`../development/railway-infrastructure-benchmark.md`](../development/railway-infrastructure-benchmark.md)を参照する。
