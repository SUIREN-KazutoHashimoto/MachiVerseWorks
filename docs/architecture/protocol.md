# Protocol Binary Layout

MachiVerseWorks の Server / Web Client 間で使用するbinary protocolを定義する。Protocolはapplication VERSIONとは独立してversioningし、current protocol versionは **2.2** とする。

## Version compatibility

`ProtocolVersion`は`major.minor`。breaking changeはmajorを上げる。2.0で3D wire contractを必須化し、2.1でRoad Network snapshot、2.2でPedestrian snapshotを追加した。

同一majorではServer current以下のminorをClientが要求した場合に受理できる。negotiation成立時のversionはClientがHello frame headerで要求したversionそのものとし、Server connection state、`HelloAck` payload、以後のframe headerで同一値を使用する。

- Protocol 2.0: Agent / 3D volume
- Protocol 2.1: `RoadNetworkSnapshot`
- Protocol 2.2: `PedestrianSpawn` / `PedestrianUpdate` / `PedestrianRemove`

Serverはnegotiated minorより新しいmessageを送らない。Client要求minorがServer currentより新しい場合、またはmajorが異なる場合はnegotiationを拒否する。

## Common frame header

全整数値とIEEE 754 `double`はlittle-endian。Headerは固定16 bytes。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 4 | `uint32` | magic `MVWP` |
| 4 | 2 | `uint16` | protocol major |
| 6 | 2 | `uint16` | protocol minor |
| 8 | 2 | `uint16` | message type |
| 10 | 2 | `uint16` | flags |
| 12 | 4 | `uint32` | payload length |

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
| 900 | `Error` | Server → Client | 2.0 |

`SubscribeArea`は存在しない。Agent / Road / Pedestrianはいずれも同じ3D `SubscribeVolume`をinterest management境界として使用する。

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

全座標は有限値で、各軸`max >= min`を要求する。

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

Protocol 2.1以上で使用する。Payloadは28-byte headerの後にRoadNode / RoadSegment / Lane / LaneConnection / RoadAccessPoint配列を連結する。各collection countとpayload lengthは一致しなければならず、dangling referenceや重複IDをClient側でも拒否する。

詳細なRoad topologyの意味は[`road-network.md`](road-network.md)を参照する。

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

## Error / decode failure

Error payload、stable error code、frame validationはProtocol boundaryで扱う。未知message、invalid payload、非有限座標、frame length不一致、unsupported minor message、negotiation後のframe version変更は安全に拒否する。
