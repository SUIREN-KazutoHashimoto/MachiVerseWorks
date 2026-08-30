# Save Data 基盤仕様

MachiVerseWorks のauthoritativeな`SimulationWorld`を停止点から同じ状態へ復元するSave Data契約を定義する。

## 目的

- authoritative Simulation状態を保存・復元できること。
- save → load後もstable ID、tick、乱数系列、route progressを継続できること。
- application version、Protocol version、表示localeに依存しないこと。
- default保存APIで生成できたデータはdefault読込APIで復元できること。

## Save format version

current formatは `formatVersion = 5` とする。Save format versionはルート`VERSION`とProtocol versionから独立する。

migration対象:

- Format 3: Agent + Building / POI。Road Networkは空として復元する。
- Format 4: Format 3 + Road Network。Pedestrian stateは空として復元する。
- Format 5: Format 4 + Pedestrian state。

Format 2以前、および5より新しい未知versionは拒否する。

## 共通Simulation state

`simulation`は少なくとも次を保持する。

- `tickRate`, `seed`, `spatialCellSize`
- `tickCount`, `elapsedTicks`, `randomState`
- `nextAgentId`, `agents`
- `nextBuildingId`, `buildings`
- `nextPoiId`, `pois`
- `nextRoadNodeId`, `roadNodes`
- `nextRoadSegmentId`, `roadSegments`
- `nextLaneId`, `lanes`
- `nextLaneConnectionId`, `laneConnections`
- `nextRoadAccessPointId`, `roadAccessPoints`
- `nextPedestrianId`, `pedestrians`

Agent / Building / POI / Roadのfield意味は各仕様を参照する。表示文字列ではなくraw numeric valueとstable IDを保存する。

## Format 5 Pedestrian state

各Pedestrianは次を保持する。

- `id`
- `tripRequestId`
- `originBuildingId` / `originPoiId`: どちらか一方だけ非null
- `destinationBuildingId` / `destinationPoiId`: どちらか一方だけ非null
- `mode`: `TravelMode` numeric value
- `walkingSpeedMetersPerSecond`
- `legIndex`
- `progressMeters`
- `state`: `PedestrianMovementState` numeric value

Walking graphそのものやroute leg配列は保存しない。Road Networkから決定的に再構築し、origin / destinationから同じrouteを再計算したうえで`legIndex`と`progressMeters`を適用する。

`Arrived`状態も明示削除されるまで保存する。これによりPedestrian stable IDとTrip完了stateをload後も保持できる。

## Restore順序

Format 5は次の順で復元する。

1. JSON / resource limit / required field検証
2. Simulation config / time / random state検証
3. Agent / Building / POI検証
4. Road topology / access参照検証
5. Pedestrian ID / Trip endpoint / speed / progress / state検証
6. Road Network復元
7. derived Pedestrian Network再構築
8. walking route再計算
9. 保存されたroute progressを適用

いずれかが不正な場合は部分Worldを返さない。

## 時間とdeterminism

`elapsedTicks`は`tickCount × SimulationConfig.TickDuration.Ticks`と一致しなければならない。`randomState`はseedではなく保存時点のdeterministic random generator状態を保持する。

Pedestrianのcheckpoint復元後も、同じRoad Network / Trip / crossing permission入力のもとでは同じfixed-tick continuationを得る。

## Resource limits

`WorldSaveLimits`のdefault上限:

- UTF-8 Save Data: 128 MiB
- Agent: 1,000,000
- Building: 1,000,000
- POI: 1,000,000
- RoadNode: 1,000,000
- RoadSegment: 1,000,000
- Lane: 2,000,000
- LaneConnection: 4,000,000
- RoadAccessPoint: 1,000,000
- Pedestrian: 1,000,000

同じlimit contractをserialize / deserializeへ適用する。collection件数は`Utf8JsonReader`でDTO materialization前にも検証し、巨大配列を先に確保しない。

`Save(Stream, ...)`は全体がlimit内であることを確認してからdestinationへ書き込むため、limit超過時にpartial Saveを残さない。

## 保存しない情報

- application version
- Protocol version
- locale / 翻訳済み文字列
- Web camera / connection / subscription
- Audio Client state
- Server connection / WebSocket state
- derived Pedestrian Network graph
- benchmark / diagnostics

Crossing permissionはPhase 16ではSignal / intersection controlから供給されるruntime入力境界として扱い、Pedestrian自身のroute progressとは分離する。

## 拒否条件

少なくとも次を拒否する。

- malformed JSON / unknown field / unsupported format
- configured byte / collection count超過
- required field欠落
- invalid Simulation config / elapsed time / overflow
- 0または重複stable ID
- `next*Id`が保存済み最大ID以下
- non-finite XYZ / velocity / bounds / speed / progress
- invalid enum numeric value
- dangling Building / POI / Road reference
- Pedestrian endpointがBuilding/POIを同時またはどちらも参照する状態
- Pedestrian route progressが再構築routeと整合しない状態
