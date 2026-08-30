# Protocol Binary Layout

MachiVerseWorks の Server / Web Client 間で使用するbinary protocolを定義する。Protocolはapplication `VERSION`とは独立してversioningし、current protocol versionは **2.5** とする。

## Version compatibility

`ProtocolVersion`は`major.minor`。breaking changeはmajorを上げる。2.0で3D wire contractを必須化し、以後は同一major内で後方互換なmessageを追加する。

- Protocol 2.0: Agent / 3D `SubscribeVolume`
- Protocol 2.1: `RoadNetworkSnapshot`
- Protocol 2.2: `PedestrianSpawn` / `PedestrianUpdate` / `PedestrianRemove`
- Protocol 2.3: `VehicleSpawn` / `VehicleUpdate` / `VehicleRemove`
- Protocol 2.4: `IntersectionControlSnapshot`
- Protocol 2.5: `InspectPerson` / `PopulationStatistics` / `PersonDebug`

同一majorではServer current以下のminorをClientが要求した場合に受理できる。negotiation成立時のversionはClientがHello frame headerで要求したversionそのものとし、Server connection state、`HelloAck` payload、以後のframe headerで同一値を使用する。

Serverはnegotiated minorより新しいmessageを送らない。Client要求minorがServer currentより新しい場合、またはmajorが異なる場合はnegotiationを拒否する。

## Common frame header

全整数値とIEEE 754 `double`はlittle-endian。Headerは固定16 bytes。payload lengthの最大値は **1,048,576 bytes (1 MiB)** とする。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 4 | `uint32` | magic `MVWP` |
| 4 | 2 | `uint16` | protocol major |
| 6 | 2 | `uint16` | protocol minor |
| 8 | 2 | `uint16` | message type |
| 10 | 2 | `uint16` | flags, currently 0 |
| 12 | 4 | `uint32` | payload length |

headerで宣言されたpayload lengthと実frame lengthが一致しないframe、未知flags、1 MiBを超えるpayloadは拒否する。

## Message type IDs

| ID | Name | Direction | Minimum version |
| ---: | --- | --- | --- |
| 1 | `Hello` | Client → Server | 2.0 |
| 2 | `HelloAck` | Server → Client | 2.0 |
| 3 | `SubscribeVolume` | Client → Server | 2.0 |
| 4 | `InspectPerson` | Client → Server | 2.5 |
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
| 900 | `Error` | Server → Client | 2.0 |

`SubscribeArea`は存在しない。Agent / Road / Pedestrian / Vehicle / Intersectionはいずれも同じ3D `SubscribeVolume`をinterest management境界として使用する。Population statisticsはWorld全体の集計、Person debugはstable Person ID指定のdebug contractであり、volume内全Person詳細を転送しない。

## Hello / HelloAck

`Hello` payloadは0 bytes。Clientが希望するProtocol versionをframe headerへ設定する。

`HelloAck` payloadは6 bytes。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 2 | `uint16` | negotiated major |
| 2 | 2 | `uint16` | negotiated minor |
| 4 | 2 | `uint16` | Simulation tick rate |

HelloAck payload versionとframe header versionは一致しなければならない。

## SubscribeVolume

Payloadは48 bytes。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `double` | min X |
| 8 | 8 | `double` | min Y |
| 16 | 8 | `double` | min Z |
| 24 | 8 | `double` | max X |
| 32 | 8 | `double` | max Y |
| 40 | 8 | `double` | max Z |

全座標はfiniteで、各軸`max >= min`を要求する。ServerはSpatial Cell budgetを適用し、過大なvolumeを`InvalidRequest`として拒否できる。

## AgentSpawn / AgentUpdate

両messageは同じ64-byte payloadを使用する。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Agent ID |
| 8 | 8 | `double` | position X |
| 16 | 8 | `double` | position Y |
| 24 | 8 | `double` | position Z |
| 32 | 8 | `double` | velocity X |
| 40 | 8 | `double` | velocity Y |
| 48 | 8 | `double` | velocity Z |
| 56 | 8 | `uint64` | simulation tick count |

`AgentRemove`はAgent ID 8 bytes + tick count 8 bytesの16-byte payload。

## RoadNetworkSnapshot

Protocol 2.1以上。Payloadは28-byte collection headerの後にRoadNode / RoadSegment / Lane / LaneConnection / RoadAccessPointをこの順番で連結する。

### Collection header — 28 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | simulation tick count |
| 8 | 4 | `uint32` | RoadNode count |
| 12 | 4 | `uint32` | RoadSegment count |
| 16 | 4 | `uint32` | Lane count |
| 20 | 4 | `uint32` | LaneConnection count |
| 24 | 4 | `uint32` | RoadAccessPoint count |

payload lengthは次式と完全一致する。

`28 + nodeCount*33 + segmentCount*25 + laneCount*35 + connectionCount*33 + accessPointCount*41`

### RoadNode — 33 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | RoadNode ID |
| 8 | 1 | `uint8` | RoadNodeKind |
| 9 | 8 | `double` | X |
| 17 | 8 | `double` | Y |
| 25 | 8 | `double` | Z |

### RoadSegment — 25 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | RoadSegment ID |
| 8 | 1 | `uint8` | RoadKind |
| 9 | 8 | `uint64` | start RoadNode ID |
| 17 | 8 | `uint64` | end RoadNode ID |

### Lane — 35 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Lane ID |
| 8 | 8 | `uint64` | RoadSegment ID |
| 16 | 1 | `uint8` | LaneDirection |
| 17 | 2 | `uint16` | order |
| 19 | 8 | `double` | width metres |
| 27 | 8 | `double` | speed limit m/s |

### LaneConnection — 33 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | LaneConnection ID |
| 8 | 8 | `uint64` | from Lane ID |
| 16 | 8 | `uint64` | to Lane ID |
| 24 | 8 | `uint64` | via RoadNode ID |
| 32 | 1 | `uint8` | TurnMovement |

### RoadAccessPoint — 41 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | RoadAccessPoint ID |
| 8 | 8 | `uint64` | RoadSegment ID |
| 16 | 8 | `double` | normalized segment offset `[0,1]` |
| 24 | 8 | `uint64` | Building ID, 0 = none |
| 32 | 8 | `uint64` | POI ID, 0 = none |
| 40 | 1 | `uint8` | RoadAccessMode flags |

Road entity IDは0不可かつ種別内で一意。参照先Node / Segment / Lane / Connectionはsnapshot内に存在しなければならない。RoadAccessPointはBuilding / POI参照をstable IDで伝える。

単一Road snapshotが1 MiBを超える場合、現2.xでは暗黙chunkingせずsubscriptionへ明示errorを返す。

## PedestrianSpawn / PedestrianUpdate

Protocol 2.2以上。両messageは同じ81-byte payloadを持つ。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Pedestrian ID |
| 8 | 8 | `uint64` | TripRequest ID |
| 16 | 8 | `double` | position X |
| 24 | 8 | `double` | position Y |
| 32 | 8 | `double` | position Z |
| 40 | 8 | `double` | velocity X |
| 48 | 8 | `double` | velocity Y |
| 56 | 8 | `double` | velocity Z |
| 64 | 8 | `double` | walking speed m/s |
| 72 | 1 | `uint8` | movement state |
| 73 | 8 | `uint64` | simulation tick count |

Movement state: 0=`Walking`, 1=`WaitingForCrossing`, 2=`WaitingForOccupancy`, 3=`Arrived`。

`PedestrianRemove`はPedestrian ID 8 bytes + tick count 8 bytesの16-byte payload。

## VehicleSpawn / VehicleUpdate

Protocol 2.3以上。両messageは同じ105-byte payloadを持つ。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Vehicle ID |
| 8 | 8 | `uint64` | Lane ID |
| 16 | 8 | `double` | position X |
| 24 | 8 | `double` | position Y |
| 32 | 8 | `double` | position Z |
| 40 | 8 | `double` | forward X |
| 48 | 8 | `double` | forward Y |
| 56 | 8 | `double` | forward Z |
| 64 | 8 | `double` | speed m/s |
| 72 | 8 | `double` | length m |
| 80 | 8 | `double` | width m |
| 88 | 8 | `double` | height m |
| 96 | 1 | `uint8` | Vehicle movement state |
| 97 | 8 | `uint64` | simulation tick count |

Vehicle movement state: 0=`Driving`, 1=`WaitingForTraffic`, 2=`ChangingLane`, 3=`Arrived`。

`VehicleRemove`はVehicle ID 8 bytes + tick count 8 bytesの16-byte payload。

## IntersectionControlSnapshot

Protocol 2.4以上。1 frameはsubscription volume内の1 intersection controllerを表す。Payloadは31-byte controller headerと0個以上の63-byte movement stateを連結する。

payload lengthは `31 + movementCount*63` と完全一致する。

### Controller header — 31 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | simulation tick count |
| 8 | 8 | `uint64` | intersection RoadNode ID |
| 16 | 1 | `uint8` | IntersectionControlMode |
| 17 | 2 | `uint16` | phase index |
| 19 | 8 | `uint64` | phase tick |
| 27 | 4 | `uint32` | movement count |

### Movement state — 63 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | IntersectionMovement ID |
| 8 | 8 | `uint64` | LaneConnection ID |
| 16 | 8 | `uint64` | from Lane ID |
| 24 | 8 | `uint64` | to Lane ID |
| 32 | 1 | `uint8` | TurnMovement |
| 33 | 8 | `double` | stop-line X |
| 41 | 8 | `double` | stop-line Y |
| 49 | 8 | `double` | stop-line Z |
| 57 | 1 | `uint8` | SignalIndication |
| 58 | 4 | `uint32` | queue length |
| 62 | 1 | `uint8` | entry granted this tick, 0 or 1 |

`IntersectionControlMode`: 0=`Unsignalized`, 1=`FixedSignal`。`SignalIndication`: 0=`Red`, 1=`Yellow`, 2=`Green`。

## InspectPerson

Protocol 2.5以上。Clientがdebug対象Personをstable IDで選択するrequest。Payloadは8 bytes。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Person ID |

Person IDは0不可。存在しないPersonを指定した場合、Serverは`InvalidRequest`を返す。

## PopulationStatistics

Protocol 2.5以上。Payloadは固定56 bytes。多数Personのdetailを毎publishで転送せず、集計のみを固定長で配信する。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 4 | `uint32` | Household count |
| 4 | 4 | `uint32` | Person count |
| 8 | 4 | `uint32` | AtActivity count |
| 12 | 4 | `uint32` | Walking count |
| 16 | 4 | `uint32` | Driving count |
| 20 | 4 | `uint32` | Home count |
| 24 | 4 | `uint32` | Work count |
| 28 | 4 | `uint32` | Education count |
| 32 | 4 | `uint32` | Shopping count |
| 36 | 4 | `uint32` | Healthcare count |
| 40 | 4 | `uint32` | Recreation count |
| 44 | 4 | `uint32` | Errand count |
| 48 | 8 | `uint64` | simulation tick count |

各state/activity別countはPerson countとの整合をServer側Simulation snapshotから生成する。

## PersonDebug

Protocol 2.5以上。Payloadは固定100 bytes。optional IDは0、optional enumは`0xff`をnull sentinelとして使用する。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Person ID |
| 8 | 8 | `uint64` | Household ID |
| 16 | 8 | `uint64` | residence Building ID, 0 = none |
| 24 | 8 | `uint64` | residence POI ID, 0 = none |
| 32 | 8 | `uint64` | current Building ID, 0 = none |
| 40 | 8 | `uint64` | current POI ID, 0 = none |
| 48 | 1 | `uint8` | current ActivityKind |
| 49 | 1 | `uint8` | PersonTravelState |
| 50 | 8 | `uint64` | destination Building ID, 0 = none |
| 58 | 8 | `uint64` | destination POI ID, 0 = none |
| 66 | 1 | `uint8` | destination ActivityKind, `0xff` = none |
| 67 | 8 | `uint64` | active TripRequest ID, 0 = none |
| 75 | 1 | `uint8` | active TravelMode, `0xff` = none |
| 76 | 8 | `uint64` | Pedestrian ID, 0 = none |
| 84 | 8 | `uint64` | Vehicle ID, 0 = none |
| 92 | 8 | `uint64` | simulation tick count |

Person / Household IDは0不可。residenceとcurrent endpointはBuilding / POIのどちらか一方を必須とし、destinationだけは両方0を許す。enumは定義済みnumeric valueのみ許可する。

## Snapshot tick semantics

1回のServer publish cycleでAgent、Pedestrian、Vehicle、Intersection、Roadのtick metadataは同じauthoritative capture時点を表す。Client別volume filterはcapture後のimmutable read modelに対して行う。

Population publisherもSimulation lock経由でauthoritative tickに対応するstatistics / Person debugを取得するが、traffic snapshot publisherとは独立serviceであり、別publish interval間でtick値が異なることは許容する。各message自身のtick countを観測時点として扱う。

## Codec separation

固定的なcore messageは`ProtocolCodec`を使用する。domain固有のlayoutは専用codecへ分離する。

- Intersection: `IntersectionControlProtocolCodec`
- Population: `PopulationProtocolCodec`

Serverはcommon headerのmessage typeを読み、専用codec対象だけを対応codecへdispatchする。Web ClientもTraffic / Population frameを判別し、対応decoderへ渡す。

## Error / decode failure

Error payload、stable error code、frame validationはProtocol boundaryで扱う。未知message、invalid payload、非有限座標、frame length不一致、unsupported minor message、negotiation後のframe version変更は安全に拒否する。

Error表示文はProtocolへ埋め込まず、stable code / structured parameterをClient側でlocalizeする。
