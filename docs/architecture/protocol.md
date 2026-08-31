# Protocol Binary Layout

MachiVerseWorksのServer / Web Client間binary protocolを定義する。ProtocolはApplication `VERSION`とSave formatから独立してversioningし、current protocol versionは **2.9** とする。

## Version compatibility

`ProtocolVersion`は`major.minor`。breaking changeはmajorを上げる。2.0でnative 3D wire contractを必須化し、以後は同一major内で後方互換なmessageを追加する。

- 2.0: Agent / 3D `SubscribeVolume`
- 2.1: `RoadNetworkSnapshot`
- 2.2: Pedestrian spawn / update / remove
- 2.3: Vehicle spawn / update / remove
- 2.4: `IntersectionControlSnapshot`
- 2.5: `InspectPerson` / `PopulationStatistics` / `PersonDebug`
- 2.6: `RailwayInfrastructureSnapshot`
- 2.7: `RailwayOperationsSnapshot`
- 2.8: `MultimodalTransitSnapshot`
- 2.9: `ClearPersonInspection`

同一majorではServer current以下のminorをClientが要求した場合に受理できる。negotiation成立versionはClientが`Hello` frame headerで要求したversionそのものとし、connection state、`HelloAck` payload、以後のframe headerへ同じ値を使う。Serverはnegotiated minorより新しいmessageを送らない。

## Common frame header

全整数値とIEEE 754 `double`はlittle-endian。Headerは固定16 bytes。payload上限は **1,048,576 bytes (1 MiB)**。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 4 | `uint32` | magic `MVWP` |
| 4 | 2 | `uint16` | protocol major |
| 6 | 2 | `uint16` | protocol minor |
| 8 | 2 | `uint16` | message type |
| 10 | 2 | `uint16` | flags, current 0 |
| 12 | 4 | `uint32` | payload length |

headerのpayload lengthと実frame長が一致しないframe、未知flags、1 MiB超payloadは拒否する。

## Message type IDs

| ID | Name | Direction | Minimum |
| ---: | --- | --- | --- |
| 1 | `Hello` | Client → Server | 2.0 |
| 2 | `HelloAck` | Server → Client | 2.0 |
| 3 | `SubscribeVolume` | Client → Server | 2.0 |
| 4 | `InspectPerson` | Client → Server | 2.5 |
| 5 | `ClearPersonInspection` | Client → Server | 2.9 |
| 100 | `AgentSpawn` | Server → Client | 2.0 |
| 101 | `AgentUpdate` | Server → Client | 2.0 |
| 102 | `AgentRemove` | Server → Client | 2.0 |
| 200 | `RoadNetworkSnapshot` | Server → Client | 2.1 |
| 300 | `PedestrianSpawn` | Server → Client | 2.2 |
| 301 | `PedestrianUpdate` | Server → Client | 2.2 |
| 302 | `PedestrianRemove` | Server → Client | 2.2 |
| 400 | `VehicleSpawn` | Server → Client | 2.3 |
| 401 | `VehicleUpdate` | Server → Client | 2.3 |
| 402 | `VehicleRemove` | Server → Client | 2.3 |
| 500 | `IntersectionControlSnapshot` | Server → Client | 2.4 |
| 600 | `PopulationStatistics` | Server → Client | 2.5 |
| 601 | `PersonDebug` | Server → Client | 2.5 |
| 700 | `RailwayInfrastructureSnapshot` | Server → Client | 2.6 |
| 710 | `RailwayOperationsSnapshot` | Server → Client | 2.7 |
| 720 | `MultimodalTransitSnapshot` | Server → Client | 2.8 |
| 900 | `Error` | Server → Client | 2.0 |

`SubscribeArea`は存在しない。Agent / Road / Pedestrian / Vehicle / Intersection / Railwayは3D `SubscribeVolume`をClient別spatial filteringの境界として使う。Protocol 2.8で追加されたMultimodal Transitは現行Serverではsubscription volumeで絞らず、subscription済みconnectionへworld-wide snapshotを配信する。Population statisticsはWorld全体集計、Person debugはstable Person ID指定のdebug contractである。

## Hello / HelloAck

`Hello` payloadは0 bytes。Client希望versionをframe headerへ設定する。

`HelloAck` payloadは6 bytes: negotiated major `uint16`、minor `uint16`、Simulation tick rate `uint16`。payload versionとframe header versionは一致しなければならない。

## SubscribeVolume

Payloadは48 bytes、`minX,minY,minZ,maxX,maxY,maxZ`の6個の`double`。全値finiteかつ各軸`max >= min`を要求する。ServerはSpatial Cell budgetを適用し、過大volumeを`InvalidRequest`として拒否できる。

## Agent messages

`AgentSpawn` / `AgentUpdate`は64 bytes。

- Agent ID `uint64`
- position X/Y/Z `double` ×3
- velocity X/Y/Z `double` ×3
- simulation tick `uint64`

`AgentRemove`はAgent ID + tickの16 bytes。

## RoadNetworkSnapshot

Protocol 2.1以上。28-byte headerの後にNode / Segment / Lane / LaneConnection / RoadAccessPointを連結する。

Header: tick `uint64` + 各collection count `uint32` ×5。

固定item長:

- RoadNode: 33 bytes = ID、kind、XYZ
- RoadSegment: 25 bytes = ID、kind、start/end Node ID
- Lane: 35 bytes = ID、Segment ID、direction、order、width、speed limit
- LaneConnection: 33 bytes = ID、from/to Lane、via Node、TurnMovement
- RoadAccessPoint: 41 bytes = ID、Segment ID、offset、Building ID、POI ID、mode flags

payload lengthは `28 + nodes*33 + segments*25 + lanes*35 + connections*33 + accessPoints*41` と一致する。ID uniquenessとsnapshot内参照整合性をcodecで検証する。現ProtocolではRoad snapshotをchunkせず、1 MiB超過はServerが送信前にstructured Errorへ変換する。

## Pedestrian messages

Protocol 2.2以上。Spawn / Updateは81 bytes。

- Pedestrian ID、TripRequest ID
- position XYZ、velocity XYZ
- walking speed
- movement state byte
- tick `uint64`

movement state: Walking / WaitingForCrossing / WaitingForOccupancy / Arrived。RemoveはID + tickの16 bytes。

## Vehicle messages

Protocol 2.3以上。Spawn / Updateは105 bytes。

- Vehicle ID、Lane ID
- position XYZ、forward XYZ
- speed、length、width、height
- movement state byte
- tick `uint64`

movement state: Driving / WaitingForTraffic / ChangingLane / Arrived。RemoveはID + tickの16 bytes。

## IntersectionControlSnapshot

Protocol 2.4以上。1 frameは1 intersection controller。31-byte controller header + `movementCount * 63` bytes。

Controller header:

- tick `uint64`
- intersection RoadNode ID `uint64`
- control mode `uint8`
- phase index `uint16`
- phase tick `uint64`
- movement count `uint32`

Movement item:

- movement ID / LaneConnection ID / from Lane ID / to Lane ID
- TurnMovement byte
- stop-line XYZ doubles
- SignalIndication byte
- queue length `uint32`
- entry-granted byte

## Population messages

### InspectPerson

Protocol 2.5以上。Client → Serverの8-byte Person ID request。0は不可。missing Personは`InvalidRequest`。

### ClearPersonInspection

Protocol 2.9以上。Client → Server、payloadは0 bytes。connectionに保持しているPerson inspection targetを明示的にclearする。2.8以下ではmessage type 5を送信しない。

### PopulationStatistics

固定56 bytes。Household / Person、travel state、activity別countとtickを持つ。多数Person detailを毎publishで転送しない。

### PersonDebug

固定100 bytes。Person / Household、residence/current/destination endpoint、activity/travel state、active Trip / TravelMode / Pedestrian / Vehicle reference、tickを持つ。optional stable IDは0、optional enumは`0xff`をnull sentinelとする。

## RailwayInfrastructureSnapshot

Protocol 2.6以上、message 700。static Railway topologyをrevision付きで配信する。

### Header — 41 bytes

- revision `uint64`
- `isFullSnapshot` `uint8` (0/1)
- Node / Segment / Connection / Block / Station / Platform / PlatformAccessPoint / Depot count `uint32` ×8

### Items

- TrackNode 33 bytes: ID、kind、XYZ
- TrackSegment 43 bytes: ID、start/end Node ID、direction、gauge、speed limit、electrification、usage
- TrackConnection 32 bytes: ID、from/to Segment ID、via Node ID
- BlockSection: 12-byte header (ID + count) + `8 * segmentIds`
- Station 56 bytes: ID + 3D bounds 6 doubles
- Platform 88 bytes: ID、Station ID、TrackSegment ID、start/end offset、3D bounds
- PlatformAccessPoint 24 bytes: ID、Platform ID、RoadAccessPoint ID
- Depot: 60-byte header (ID + 3D bounds + count) + `8 * trackSegmentIds`

### Chunk semantics

1 MiBを超えるsnapshotは`RailwayInfrastructureProtocolChunker`が**entity境界**で複数frameへ分割する。1つのBlockSection / Depot item自体は分割しない。

- 全chunkは同じ`revision`を持つ
- source snapshotがfullの場合、**最初のchunkだけ**`isFullSnapshot=true`
- continuation chunkは`isFullSnapshot=false`
- encode orderは Node → Segment → Connection → Block → Station → Platform → PlatformAccessPoint → Depot
- chunk index / total count / explicit final markerは持たず、WebSocketのframe orderingとincremental applyを前提にする
- Clientは`isFullSnapshot=true`を受けた時点で同revisionでも保持中Railway stateをresetし、そのframeから新しいsnapshotを組み立てる
- `isFullSnapshot=false`は現在保持中revisionと一致する場合だけcontinuationとして適用し、revision不一致なら無視する

subscription変更でRailway revision自体が変わらなくても、Serverは新しいfiltered snapshotの先頭をfullとして送るため、Clientは旧volume由来stateを残さない。

SimulationのBlockSection / Depot membership hard limitは100,000件で、単一可変itemがProtocol 1 MiB上限を超えないようにする。

## RailwayOperationsSnapshot

Protocol 2.7以上、message 710。dynamic Train stateと関連Service / Timetableだけを配信する。**single-frame contract**でchunkingしない。

### Header — 20 bytes

- tick `uint64`
- Train count `uint32`
- Service count `uint32`
- Timetable count `uint32`

### Train — 129 bytes

- ID / Formation ID / Service ID / Route ID
- position XYZ、forward XYZ、speed
- movement state byte
- current Block / current Platform / assigned Platform / current Depot / dwell departure tick

optional IDsは0 sentinel。

### Service — 77 bytes

- ID / Formation / Route / Timetable / origin Depot / destination Depot / planned start
- service state byte
- delay ticks
- next stop index `int32`
- Train ID (0 = none)

### Timetable

12-byte header (ID + stop count) + 40-byte Stop。

StopはStation ID、planned arrival、planned departure、minimum dwell、preferred Platform ID (0 = none)の5個の`uint64`。

`RailwayOperationsProtocolCodec.GetPayloadLength()`がallocation前に正確なpayload長を算出する。1 MiB超過時、Serverはmessage 710を送らず`InvalidRequest` / `detailCode=railwayOperationsSnapshotTooLarge`を対象subscriptionへ返す。partial snapshotや暗黙chunkingは行わない。

## MultimodalTransitSnapshot

Protocol 2.8以上、message 720。28-byte headerの後にLine / Stop / Pattern / realtime Vehicle / Arrival Estimateを連結する。

Headerはtick `uint64` + Line / Stop / Pattern / Vehicle / Arrival count `uint32` ×5。

固定/可変item長:

- Line 9 bytes: ID + mode
- Stop 57 bytes: ID + kind + XYZ + Lane / Station / Platform ID
- Pattern header 28 bytes: ID + Line ID + RailwayService ID + stop count
- Pattern Stop 24 bytes: Stop ID + travel ticks from previous + dwell ticks
- Vehicle 70 bytes: ID + kind + Trip/RoadVehicle reference + stop index + XYZ + state + ETA/dwell tick
- Arrival Estimate 32 bytes: Stop ID + Line ID + Vehicle ID + estimated arrival tick

optional stable IDは0 sentinel。Bus StopはLane、Railway StopはStation（任意Platform）、Railway PatternはRailway Serviceを参照する。Arrival Estimateは同frame内のStop / Line / Vehicleを参照しなければならない。2.7以下へmessage 720を送らない。

現行Serverはmessage 720のLine / Stop / Pattern / Vehicle / Arrival EstimateをClient `SubscribeVolume`でfilterせず、`publishSnapshot.MultimodalTransit`全体からmessageを作成する。したがってTransit deliveryは現時点ではworld-wideであり、volume-based interest managementは将来拡張事項である。`MultimodalTransitProtocolCodec.GetPayloadLength()`で送信前にpayload長を計算し、1 MiB超過時はmessage 720をserializeせず`InvalidRequest` / `detailCode=multimodalTransitSnapshotTooLarge`へ変換する。

## Snapshot tick semantics

traffic publish cycle内のAgent / Pedestrian / Vehicle / Intersection / Road / Railway / Transit dataは同じauthoritative captureを基礎にする。Agent / Pedestrian / Vehicle / Intersection / Road / Railwayはcapture後のimmutable read modelへClient別volume filterを適用するが、Multimodal Transitは同じcapture tickのworld-wide snapshotをfilterせず配信する。

Population publisherは専用serviceであり、別publish intervalのtickがtraffic snapshotと異なることを許容する。各message自身のtickを観測時点とする。

## Codec separation

- core: `ProtocolCodec`
- Intersection: `IntersectionControlProtocolCodec`
- Population: `PopulationProtocolCodec`
- Railway Infrastructure: `RailwayInfrastructureProtocolCodec`
- Railway Operations: `RailwayOperationsProtocolCodec`
- Multimodal Transit: `MultimodalTransitProtocolCodec`

Server / Webはcommon headerのmessage typeから対応decoderへdispatchする。Simulation内部classをwire object graphとして直接露出しない。

## Error / decode failure

unknown message、invalid payload、non-finite値、frame length不一致、unsupported minor message、negotiation後のversion変更を安全に拒否する。Error表示文はProtocolへ埋め込まず、stable error code / structured parameterをClient側でlocalizeする。
