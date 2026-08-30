# Protocol Binary Layout

MachiVerseWorks の Server / Web Client 間で使用するbinary protocolを定義する。Protocolはapplication `VERSION`とは独立してversioningし、current protocol versionは **2.4** とする。

## Version compatibility

`ProtocolVersion`は`major.minor`。breaking changeはmajorを上げる。2.0で3D wire contractを必須化し、2.1でRoad Network snapshot、2.2でPedestrian snapshot、2.3でVehicle snapshot、2.4でIntersection / Signal snapshotを追加した。

同一majorではServer current以下のminorをClientが要求した場合に受理できる。negotiation成立時のversionはClientがHello frame headerで要求したversionそのものとし、Server connection state、`HelloAck` payload、以後のframe headerで同一値を使用する。

- Protocol 2.0: Agent / 3D volume
- Protocol 2.1: `RoadNetworkSnapshot`
- Protocol 2.2: `PedestrianSpawn` / `PedestrianUpdate` / `PedestrianRemove`
- Protocol 2.3: `VehicleSpawn` / `VehicleUpdate` / `VehicleRemove`
- Protocol 2.4: `IntersectionControlSnapshot`

Serverはnegotiated minorより新しいmessageを送らない。Client要求minorがServer currentより新しい場合、またはmajorが異なる場合はnegotiationを拒否する。

## Common frame header

全整数値とIEEE 754 `double`はlittle-endian。Headerは固定16 bytes。payload lengthの最大値は **1,048,576 bytes (1 MiB)** とする。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 4 | `uint32` | magic `MVWP` |
| 4 | 2 | `uint16` | protocol major |
| 6 | 2 | `uint16` | protocol minor |
| 8 | 2 | `uint16` | message type |
| 10 | 2 | `uint16` | flags |
| 12 | 4 | `uint32` | payload length |

flagsは現在0のみを許可する。headerで宣言されたpayload lengthと実frame lengthが一致しないframeは拒否する。

## Message type IDs

| ID | Name | Direction | Minimum version |
| ---: | --- | --- | --- |
| 1 | `Hello` | Client → Server | 2.0 |
| 2 | `HelloAck` | Server → Client | 2.0 |
| 3 | `SubscribeVolume` | Client → Server | 2.0 |
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
| 900 | `Error` | Server → Client | 2.0 |

`SubscribeArea`は存在しない。Agent / Road / Pedestrian / Vehicle / Intersectionはいずれも同じ3D `SubscribeVolume`をinterest management境界として使用する。

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

全座標は有限値で、各軸`max >= min`を要求する。ServerはさらにSpatial Cell budgetを適用し、過大なvolumeを`InvalidRequest`として拒否できる。

## AgentSpawn / AgentUpdate

両messageは同じ64-byte state payloadを使用する。

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

Protocol 2.1以上で使用する。Payloadは28-byte collection headerの後に、RoadNode / RoadSegment / Lane / LaneConnection / RoadAccessPointをこの順番で連結する。

### Collection header — 28 bytes

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | simulation tick count |
| 8 | 4 | `uint32` | RoadNode count |
| 12 | 4 | `uint32` | RoadSegment count |
| 16 | 4 | `uint32` | Lane count |
| 20 | 4 | `uint32` | LaneConnection count |
| 24 | 4 | `uint32` | RoadAccessPoint count |

payload lengthは次式と完全一致しなければならない。

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

### Road validation

C# serializer、C# decoder、Web decoderは同じ構造条件を要求する。

- 各Road entity IDは0不可かつentity種別内で一意。
- Segmentのstart/end Nodeはsnapshot内に存在し、同一Nodeではない。
- LaneのSegmentはsnapshot内に存在する。
- LaneConnectionのfrom/to Laneとvia Nodeはsnapshot内に存在し、from/toは同一Laneではない。
- RoadAccessPointのSegmentはsnapshot内に存在する。
- enum / flags値、座標、幅、速度、offsetは各型の有効範囲を満たす。

構造不正なRoad topologyをserializerから生成せず、受信時も`InvalidPayload`として拒否する。

### 1 MiB boundary

Road topologyは現状1つの`RoadNetworkSnapshot` frameとして送るため、計算payloadが1 MiBを超えるsnapshotは単一frameへserializeしない。Server publisherは送信前にpayload長を計算し、そのsubscriptionだけへ`InvalidRequest` / detail code `roadSnapshotTooLarge`を返す。他ClientのpublisherやSimulation tickをfaultさせない。

将来1 MiBを超えるRoad topologyをそのまま転送する必要が生じた場合は、chunk sequence / generationをProtocol revisionとして追加する。現2.x contractで暗黙分割は行わない。

## PedestrianSpawn / PedestrianUpdate

Protocol 2.2以上で使用し、両messageは同じ81-byte payloadを持つ。

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

Movement state numeric value:

- 0: `Walking`
- 1: `WaitingForCrossing`
- 2: `WaitingForOccupancy`
- 3: `Arrived`

IDは0不可、position / velocity / speedはfinite、speedは0より大きい値を要求する。

## PedestrianRemove

Payloadは16 bytes。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Pedestrian ID |
| 8 | 8 | `uint64` | simulation tick count |

## VehicleSpawn / VehicleUpdate

Protocol 2.3以上で使用し、両messageは同じ105-byte payloadを持つ。

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

Vehicle ID / Lane IDは0不可、position / forward / speed / dimensionsはfinite、forwardはnon-zero、speedは0以上、dimensionsは0より大きい値を要求する。

Movement state numeric value:

- 0: `Driving`
- 1: `WaitingForTraffic`
- 2: `ChangingLane`
- 3: `Arrived`

## VehicleRemove

Payloadは16 bytes。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Vehicle ID |
| 8 | 8 | `uint64` | simulation tick count |

## IntersectionControlSnapshot

Protocol 2.4以上で使用する。1 frameはsubscription volume内の1 intersection controllerを表す。Payloadは31-byte controller headerと、0個以上の63-byte movement stateを連結する。

payload lengthは `31 + movementCount*63` と完全一致しなければならない。

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

`IntersectionControlMode`は0=`Unsignalized`、1=`FixedSignal`。`SignalIndication`は0=`Red`、1=`Yellow`、2=`Green`。

Intersection node / movement / connection / Lane IDは0不可。stop-line XYZはfinite。enum値とgrant flagを定義済み範囲に制限する。

可変長の2.4 controller payloadは`IntersectionControlProtocolCodec`でencode/decodeする。Server `ClientConnection`はこのmessageだけ専用codecへdispatchし、それ以外のmessageは共通`ProtocolCodec`を使用する。

## Snapshot tick semantics

1回のServer publish cycleでAgent、Pedestrian、Vehicle、Intersection、Road、remove metadataに付く`simulation tick count`は、同じauthoritative capture時点を表す。Client別のvolume filterはcapture後のimmutable read modelに対して行い、filter中にSimulation tickが進んでも同一publish batchのtick metadataは混在しない。

Intersection controllerはRoadNode位置でsubscription filterされる。Vehicleはpositionでfilterされ、既知Vehicle IDとの差分からspawn/update/removeを生成する。

## Error / decode failure

Error payload、stable error code、frame validationはProtocol boundaryで扱う。未知message、invalid payload、非有限座標、frame length不一致、unsupported minor message、negotiation後のframe version変更は安全に拒否する。Errorの表示文はProtocolへ埋め込まず、stable code / structured parameterをClient側でlocalizeする。
