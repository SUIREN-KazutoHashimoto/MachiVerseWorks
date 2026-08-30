# Save Data 基盤仕様

MachiVerseWorks のauthoritativeな`SimulationWorld`を停止点から同じ状態へ復元するSave Data契約を定義する。

## 目的

- authoritative Simulation状態を保存・復元できること。
- save → load後もstable ID、tick、乱数系列、route progressを継続できること。
- application version、Protocol version、表示localeに依存しないこと。
- default保存APIで生成できたデータはdefault読込APIで復元できること。

## Save format version

current formatは `formatVersion = 6` とする。Save format versionはルート`VERSION`とProtocol versionから独立する。

migration対象:

- Format 3: Agent + Building / POI。Road Networkは空として復元する。
- Format 4: Format 3 + Road Network。Pedestrian stateは空として復元する。
- Format 5: Format 4 + Pedestrian state。Vehicle stateは空として復元する。
- Format 6: Format 5 + Vehicle state。

Format 2以前、および6より新しい未知versionは拒否する。

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
- `pedestrianCrossings`
- `nextVehicleId`, `vehicles`

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

## Format 6 Vehicle state

各Vehicleは次を保持する。

- `id`
- dimensions: length / width / height
- performance: maximum speed / acceleration / comfortable deceleration / minimum gap / time headway
- Route step配列: Lane ID、RoadSegment ID、start/end segment offset、distance、estimated travel time、exit LaneConnection ID
- `routeStepIndex`
- `routeProgressMeters`
- `speedMetersPerSecond`
- `state`: `VehicleMovementState` numeric value

Vehicleのworld position / forward vectorは独立して保存しない。Road/Lane topologyとRoute progressから復元時に再計算する。Lane occupancy indexも保存せず、Vehicle checkpointをstable ID順に復元しながら再構築する。

## Phase 14 Signal controller state

Phase 14の固定cycle Signalには、Save Dataへ追加すべき独立mutable stateがない。

- movement / conflict / phase topologyは保存済みRoadNode / Lane / LaneConnectionから決定的に派生する。
- current phase / phase tickは保存済み`tickCount`と`tickRate`から決定的に派生する。
- current-tick queue / entry grantは次tickで再計算されるephemeral observationであり、継続状態として保存しない。

このためPhase 14ではSave formatを7へ上げず、Format 6のauthoritative入力からcontrollerを再構築する。`IntersectionControlSaveTests`がsave → load前後で同一tickのcontroller mode / phase / indicationを比較する。

将来adaptive signalがdetector履歴、manual offset、preemption、学習状態などの独立mutable stateを持つ場合は、その時点で明示Save fieldとformat versionを追加する。

## Restore順序

Format 6は次の順で復元する。

1. JSON / resource limit / required field検証
2. Simulation config / time / random state検証
3. Agent / Building / POI検証
4. Road topology / access参照検証
5. Pedestrian ID / Trip endpoint / speed / progress / state検証
6. Vehicle ID / dimensions / performance / Route / progress / state検証
7. Road Network復元
8. derived Road Traffic topologyとIntersection Control topology再構築
9. Vehicle stateとLane occupancy index復元
10. derived Pedestrian Network再構築
11. walking route再計算
12. 保存されたPedestrian route progress / crossing permissionを適用

いずれかが不正な場合は部分Worldを返さない。

## 時間とdeterminism

`elapsedTicks`は`tickCount`とSimulation TickRateから得られるdeterministic elapsed timeと一致しなければならない。`randomState`はseedではなく保存時点のdeterministic random generator状態を保持する。

Vehicle / Pedestrianのcheckpoint復元後も、同じRoad Network / Route / Trip / crossing permission入力のもとでは同じfixed-tick continuationを得る。固定cycle Signalのphaseも同じ`tickCount`から復元される。

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
- PedestrianCrossing: 1,000,000
- Vehicle: 1,000,000

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
- derived Road Traffic topology / Lane occupancy index
- derived Intersection movement / conflict / fixed-cycle phase state
- benchmark / diagnostics

Crossing permissionはPedestrian自身のroute progressとは分離して保存する。Signal / intersection controlとの連携を拡張する場合も、Pedestrian route stateとcontroller stateの正本を混在させない。

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
- Vehicle Routeがmissing Lane / Segment / LaneConnectionを参照する状態
- Vehicle progress / state / speedがRouteまたはLane occupancy invariantと整合しない状態
