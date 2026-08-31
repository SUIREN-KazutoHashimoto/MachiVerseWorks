# MachiVerseWorks.Protocol

ServerとWeb Clientのbinary wire contractを管理します。Application `VERSION`やSave formatとは独立してversioningします。

## Current contract

現在のProtocolは **2.8** です。

- 2.0: 16-byte little-endian frame header、1 MiB payload上限、3D `SubscribeVolume`、Agent
- 2.1: `RoadNetworkSnapshot`
- 2.2: Pedestrian spawn / update / remove
- 2.3: Vehicle spawn / update / remove
- 2.4: `IntersectionControlSnapshot`
- 2.5: `InspectPerson` / `PopulationStatistics` / `PersonDebug`
- 2.6: `RailwayInfrastructureSnapshot` (700)
- 2.7: `RailwayOperationsSnapshot` (710)
- 2.8: `MultimodalTransitSnapshot` (720)

同一majorではClientがServer current以下のminorを要求できます。negotiated minorより新しいmessageは送信しません。Protocol 1.x / `SubscribeArea` / 2D wire contractは現行経路にありません。

## Domain codecs

core frame / Agent / Road / Pedestrianは`ProtocolCodec`、domain固有の可変layoutは専用codecへ分離します。

- `IntersectionControlProtocolCodec`
- `PopulationProtocolCodec`
- `RailwayInfrastructureProtocolCodec` + `RailwayInfrastructureProtocolChunker`
- `RailwayOperationsProtocolCodec`
- `MultimodalTransitProtocolCodec`

Railway Infrastructureは1 MiBを超えるsnapshotをentity境界で複数frameへ分割できます。Railway OperationsはProtocol 2.7のsingle-frame contractで、Serverが送信前にpayload長をpreflightします。Multimodal Transitは2.8以降だけへ配信します。

codecはstable ID、enum、finite値、payload length、collection構造などwire境界で検証します。Simulationのmutable storeやWeb UI表示文言はProtocolへ持ち込みません。

binary layout、message ID、chunk semantics、互換性ルールの正本は[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)を参照してください。
