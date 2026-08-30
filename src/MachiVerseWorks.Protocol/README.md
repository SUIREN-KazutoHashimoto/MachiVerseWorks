# MachiVerseWorks.Protocol

ServerとClientのbinary wire contractを管理します。

現在の正本契約:

- Protocol version: `2.2`
- 16-byte little-endian frame header / payload上限1 MiB
- stable `MessageType` ID
- `Hello` / `HelloAck`
- 3D `SubscribeVolume`
- XYZ position / XYZ velocityを持つAgent spawn / update / remove
- Protocol 2.1以降の`RoadNetworkSnapshot`
- Protocol 2.2以降のPedestrian spawn / update / remove
- stable error code + structured parameter
- serializer / deserializerで同一のRoad topology validation

Protocol 2.0はPhase 9のネイティブ3D化に伴うbreaking changeです。2.1でRoad Network、2.2でPedestrianを追加しました。同一majorではnegotiated minorまでのmessageだけを送信し、古いminorへ新しいmessageを暗黙送信しません。

Road Networkはentity IDの重複とNode / Segment / Lane / Connection / AccessPointの参照整合性をcodec境界で検証します。単一Road snapshotが1 MiB payload上限を超える場合、Serverは対象subscriptionへ構造化Errorを返し、publisher全体をfaultさせません。

Protocol projectはSimulationの内部mutable stateやWeb UI表示文言を直接参照しません。

binary layoutと互換性ルールの詳細は[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)を参照してください。
