# Protocol Binary Layout

MachiVerseWorks の Server / Web Client 間で使用するbinary protocolを定義する。Protocolはapplication VERSIONとは独立してversioningし、Phase 9のcurrent protocol versionは **2.0** とする。

## Version compatibility

`ProtocolVersion`は`major.minor`。breaking changeはmajorを上げる。Phase 9の3D必須wire contractは2.0であり、1.xの2D payloadへ暗黙fallbackしない。

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

| ID | Name | Direction |
| ---: | --- | --- |
| 1 | `Hello` | Client → Server |
| 2 | `HelloAck` | Server → Client |
| 3 | `SubscribeVolume` | Client → Server |
| 100 | `AgentSpawn` | Server → Client |
| 101 | `AgentUpdate` | Server → Client |
| 102 | `AgentRemove` | Server → Client |
| 900 | `Error` | Server → Client |

`SubscribeArea`は存在しない。subscriptionは3D volumeだけを扱う。

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

位置・速度はXYZ全成分が必須であり、2D constructorやZ省略wire layoutは提供しない。

## AgentRemove

Payloadは16 bytes: Agent ID 8 bytes + simulation tick count 8 bytes。

## Error / decode failure

Error payload、stable error code、frame validationは従来どおりProtocol boundaryで扱う。未知message、invalid payload、非有限座標、frame length不一致は安全に拒否する。
