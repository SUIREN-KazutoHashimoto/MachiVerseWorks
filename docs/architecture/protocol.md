# Protocol Binary Layout

MachiVerseWorks の Server / Web Client 間で使用する最小 binary protocol を定義します。

Protocol は application version とは独立して versioning します。Phase 3 の current protocol version は **1.0** です。

## Version compatibility

`ProtocolVersion` は `major.minor` の2要素で表します。

- `major` が異なる場合は互換とみなさない。
- 同じ `major` では、受信側が要求側以上の `minor` を実装している場合に受理できる。
- breaking change は `major` を上げる。
- 後方互換な message / field の追加は将来 `minor` を上げて表現する。
- application の `VERSION` と Protocol version は連動させない。

`Hello` frame の header version を Client が要求する version とし、Server は `HelloAck` payload で実際に採用した version を返します。

## Common frame header

すべての整数値と IEEE 754 `double` は **little-endian** です。

Header は固定 **16 bytes** です。

| Offset | Size | Type | Field | 内容 |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | `uint32` | magic | ASCII `MVWP` (`4D 56 57 50`) |
| 4 | 2 | `uint16` | protocol major | Protocol major version |
| 6 | 2 | `uint16` | protocol minor | Protocol minor version |
| 8 | 2 | `uint16` | message type | stable message type ID |
| 10 | 2 | `uint16` | flags | Phase 3 では常に `0` |
| 12 | 4 | `uint32` | payload length | headerを除く payload bytes |

Phase 3 では payload 上限を 1 MiB とします。受信 frame の実長が `16 + payload length` と一致しない場合は frame を拒否します。

## Message type IDs

Message type ID は wire contract です。enum の並び順変更で再採番しません。

| ID | Name | Direction |
| ---: | --- | --- |
| 1 | `Hello` | Client → Server |
| 2 | `HelloAck` | Server → Client |
| 3 | `SubscribeArea` | Client → Server |
| 100 | `AgentSpawn` | Server → Client |
| 101 | `AgentUpdate` | Server → Client |
| 102 | `AgentRemove` | Server → Client |
| 900 | `Error` | Server → Client |

`1-99` は connection/control、`100-199` は Agent replication 用として扱います。未定義 ID は `UnknownMessageType` として安全に拒否します。

## Message payloads

### Hello

Payload は **0 bytes** です。Client が要求する Protocol version は frame header に入ります。

### HelloAck

Payload は **6 bytes** です。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 2 | `uint16` | negotiated protocol major |
| 2 | 2 | `uint16` | negotiated protocol minor |
| 4 | 2 | `uint16` | simulation tick rate |

### SubscribeArea

Payload は **32 bytes** です。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `double` | min X |
| 8 | 8 | `double` | min Y |
| 16 | 8 | `double` | max X |
| 24 | 8 | `double` | max Y |

座標は有限値で、`maxX >= minX`、`maxY >= minY` を満たす必要があります。

### AgentSpawn / AgentUpdate

両 message は同じ **48 bytes** の state payload を使います。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Agent ID |
| 8 | 8 | `double` | position X |
| 16 | 8 | `double` | position Y |
| 24 | 8 | `double` | velocity X |
| 32 | 8 | `double` | velocity Y |
| 40 | 8 | `uint64` | simulation tick count |

Protocol project は Simulation project の内部型を参照しません。`AgentId` は wire 上では安定した `uint64` として転送し、Server / Client の各境界で対応する型へ変換します。

### AgentRemove

Payload は **16 bytes** です。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 8 | `uint64` | Agent ID |
| 8 | 8 | `uint64` | simulation tick count |

### Error

Error payload は可変長です。

| Offset | Size | Type | Field |
| ---: | ---: | --- | --- |
| 0 | 2 | `uint16` | stable error code |
| 2 | 2 | `uint16` | parameter count |
| 4 | ... | repeated | parameter entries |

各 parameter entry は次の順です。

1. `uint16 keyByteLength`
2. UTF-8 key bytes
3. `uint16 valueByteLength`
4. UTF-8 value bytes

Phase 3 では parameter 数を最大16、keyを最大64 UTF-8 bytes、valueを最大256 UTF-8 bytesとします。

## Stable error codes

| Code | Name | 用途 |
| ---: | --- | --- |
| 1 | `UnsupportedProtocolVersion` | version negotiation failure |
| 2 | `InvalidFrame` | frame header / length 等が不正 |
| 3 | `UnknownMessageType` | 未知の message type |
| 4 | `InvalidPayload` | payload layout / value が不正 |
| 5 | `InvalidRequest` | request の意味上の検証エラー |
| 1000 | `InternalServerError` | Server内部エラー |

wire 上に user-facing message を直接入れません。Client は stable error code と structured parameter を locale resource へ渡して表示文言を構築します。

Phase 3 で定義する stable parameter key は次です。

- `requestedVersion`
- `supportedVersion`
- `messageType`
- `detailCode`
- `field`

parameter は補助情報であり、表示文そのものを送信する用途には使いません。

## Decode failure

frame decoder は通常の不正入力を例外として外へ投げず、`ProtocolDecodeError` で分類します。

- `FrameTooShort`
- `InvalidMagic`
- `UnsupportedFlags`
- `PayloadTooLarge`
- `FrameLengthMismatch`
- `UnknownMessageType`
- `InvalidPayload`

Protocol boundary で失敗を分類し、Server側の接続処理が close / error response / logging の判断を行います。
