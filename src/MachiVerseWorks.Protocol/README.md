# MachiVerseWorks.Protocol

ServerとClientのbinary wire contractを管理します。

現在の正本契約:

- Protocol version: `2.0`
- 16-byte little-endian frame header
- stable `MessageType` ID
- `Hello` / `HelloAck`
- 3D `SubscribeVolume`
- XYZ position / XYZ velocityを持つAgent spawn / update
- Agent remove
- stable error code + structured parameter
- serializer / deserializer

Protocol 2.0はPhase 9のネイティブ3D化に伴うbreaking changeです。Protocol 1.xの2D payload、`SubscribeArea`、Z省略layoutへの暗黙fallbackは提供しません。

Protocol projectはSimulationの内部状態やWeb UI表示文言を直接参照しません。

binary layoutと互換性ルールの詳細は[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)を参照してください。
