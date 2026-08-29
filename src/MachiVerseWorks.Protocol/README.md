# MachiVerseWorks.Protocol

Server と Client の binary wire contract を管理します。

Phase 3 では次を正本として実装します。

- Protocol version: `1.0`
- 16-byte little-endian frame header
- stable `MessageType` ID
- `Hello` / `HelloAck`
- `SubscribeArea`
- Agent spawn / update / remove
- stable error code + structured parameter
- serializer / deserializer

Protocol project は Simulation の内部状態や Web UI 表示文言を直接参照しません。

binary layout と互換性ルールの詳細は [`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md) を参照してください。
