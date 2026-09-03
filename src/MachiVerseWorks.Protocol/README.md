# MachiVerseWorks.Protocol

ServerとWeb Clientのbinary wire contractを管理します。Application `VERSION`やSave formatとは独立してversioningします。

## Current contract

現在のProtocolは **2.23** です。

- 2.0: 16-byte little-endian frame header、1 MiB payload上限、3D `SubscribeVolume`、Agent
- 2.1: `RoadNetworkSnapshot`
- 2.2: Pedestrian spawn / update / remove
- 2.3: Vehicle spawn / update / remove
- 2.4: `IntersectionControlSnapshot`
- 2.5: `InspectPerson` / `PopulationStatistics` / `PersonDebug`
- 2.6: `RailwayInfrastructureSnapshot` (700)
- 2.7: `RailwayOperationsSnapshot` (710)
- 2.8: `MultimodalTransitSnapshot` (720)
- 2.9: `ClearPersonInspection`を追加し、Person inspectorの明示clear lifecycleを定義
- 2.10: Economy observation
- 2.11: Logistics observation
- 2.12: Power observation
- 2.13: Water / Sewer observation
- 2.14: Gas observation
- 2.15: Optical observation
- 2.16: Radio / Spectrum observation
- 2.17: World Environment observation
- 2.18: Regional Generation observation
- 2.19: Persistent Regional Evolution observation
- 2.20: Entity Inspection request / response
- 2.21: `PopulationStatistics` に Transit count を追加
- 2.22: `RegionalGenerationSnapshotChunk` (811) を追加し、Regional Generationのlogical snapshotを1 MiB以下の複数frameへ分割可能化
- 2.23: Multimodal Transit deliveryを`SubscribeVolume`単位に絞り込み、world-wide single-frame依存を解消

同一majorではClientがServer current以下のminorを要求できます。negotiated minorより新しいmessageは送信しません。Protocol 1.x / `SubscribeArea` / 2D wire contractは現行経路にありません。

`SubscribeVolume` / `InspectPerson` / `ClearPersonInspection` / Entity Inspection requestはClient→Serverのread-only Observation Requestです。Worldを変更するcommandはObservation Protocolへ追加せず、ServerのAdministration / Management command boundaryへ分離します。

## Domain codecs

core frame / Agent / Road / Pedestrianは`ProtocolCodec`、domain固有の可変layoutは専用codecへ分離します。

- `IntersectionControlProtocolCodec`
- `PopulationProtocolCodec`
- `RailwayInfrastructureProtocolCodec` + `RailwayInfrastructureProtocolChunker`
- `RailwayOperationsProtocolCodec`
- `MultimodalTransitProtocolCodec`
- `EconomyProtocolCodec`
- `LogisticsProtocolCodec`
- `PowerProtocolCodec`
- `WaterSewerProtocolCodec`
- `GasProtocolCodec`
- `OpticalProtocolCodec`
- `RadioProtocolCodec`
- `WorldEnvironmentProtocolCodec`
- `RegionalGenerationProtocolCodec` + `RegionalGenerationSnapshotChunkProtocolCodec` + `RegionalGenerationSnapshotChunker`
- `PersistentRegionalEvolutionProtocolCodec`
- `EntityInspectionProtocol`

Railway Infrastructureは1 MiBを超えるsnapshotをentity境界で複数frameへ分割できます。Regional GenerationはProtocol 2.22以降でlogical JSON snapshotをchunk transportへ載せ、各frameを1 MiB以下に保ちます。Protocol 2.23以降のMultimodal TransitはClientの`SubscribeVolume`へ絞り込んで配信し、なお上限を超える場合はstructured Errorを返します。同一subscription revisionで同じoversize Errorは繰り返し送信しません。

codecはstable ID、enum、finite値、payload length、collection構造などwire境界で検証します。Simulationのmutable storeやWeb UI表示文言はProtocolへ持ち込みません。

Current contractのversion表記は`ProtocolVersion.Current`と同期させ、Protocolのversion変更時には両方を更新します。この同期はProtocol testでも検証します。

binary layout、message ID、chunk semantics、互換性ルールの正本は[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)を参照してください。
