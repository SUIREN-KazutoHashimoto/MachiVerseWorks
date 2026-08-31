# Save Data 基盤仕様

MachiVerseWorks のauthoritativeな`SimulationWorld`を停止点から同じ状態へ復元するSave Data契約を定義する。

## 目的

- authoritative Simulation状態を保存・復元できること。
- save → load後もstable ID、tick、乱数系列、route progressを継続できること。
- application version、Protocol version、表示localeに依存しないこと。
- default保存APIで生成できたデータはdefault読込APIで復元できること。

## Save format version

current formatは `formatVersion = 7` とする。Save format versionはルート`VERSION`とProtocol versionから独立する。

migration対象:

- Format 3: Agent + Building / POI。Road Networkは空として復元する。
- Format 4: Format 3 + Road Network。Pedestrian stateは空として復元する。
- Format 5: Format 4 + Pedestrian state。Vehicle stateは空として復元する。
- Format 6: Format 5 + Vehicle state。Population stateは空として復元する。
- Format 7: Format 6 + Household / Person / daily schedule / Need / active Population Trip state。
- Format 8: Format 7 + Railway Infrastructure。
- Format 9: Format 8 + Railway Operations。
- Format 10: Format 9 + Multimodal Transit / Journey / Passenger / Taxi state。

Format 2以前、および10より新しい未知versionは拒否する。

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
- `nextHouseholdId`, `households`
- `nextPersonId`, `persons`
- `nextTripRequestId`

Agent / Building / POI / Road / Populationのfield意味は各仕様を参照する。表示文字列ではなくraw numeric valueとstable IDを保存する。

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

## Format 7 Population state

Householdは次を保持する。

- `id`
- residence Building / POI endpoint

Personは次を保持する。

- `id`
- `householdId`
- demographics: age / employed / student / private vehicle availability
- residence / current location
- current activity / travel state
- optional destination / destination activity
- optional active `TripRequestId` / travel mode / Pedestrian ID / Vehicle ID
- daily activity windows
- Need kind / satisfaction / decay rate

Population plannerのderived decision cacheは保存しない。schedule / Need / current activity / active Trip execution referenceをauthoritative stateとして保存し、load後に同じfixed-tick state machineを継続する。

Format 6以前のsaveはHousehold / Person collectionを空、next Population IDを初期値としてmigrationする。

## Phase 14 Signal controller state

Phase 14の固定cycle Signalには、Save Dataへ追加すべき独立mutable stateがない。

- movement / conflict / phase topologyは保存済みRoadNode / Lane / LaneConnectionから決定的に派生する。
- current phase / phase tickは保存済み`tickCount`と`tickRate`から決定的に派生する。
- current-tick queue / entry grantは次tickで再計算されるephemeral observationであり、継続状態として保存しない。

このためPhase 14ではSave formatを上げず、Road Traffic stateを含む既存authoritative入力からcontrollerを再構築する。`IntersectionControlSaveTests`がsave → load前後で同一tickのcontroller mode / phase / indicationを比較する。

将来adaptive signalがdetector履歴、manual offset、preemption、学習状態などの独立mutable stateを持つ場合は、その時点で明示Save fieldとformat versionを追加する。

## Restore順序

Format 7は次の順で復元する。

1. JSON / resource limit / required field検証
2. Simulation config / time / random state検証
3. Agent / Building / POI検証
4. Road topology / access参照検証
5. Pedestrian ID / Trip endpoint / speed / progress / state検証
6. Vehicle ID / dimensions / performance / Route / progress / state検証
7. Household / Person ID、Household所属、Building / POI endpoint、schedule / Need、active Trip参照検証
8. Road Network復元
9. derived Road Traffic topologyとIntersection Control topology再構築
10. Vehicle stateとLane occupancy index復元
11. derived Pedestrian Network再構築
12. walking route再計算
13. 保存されたPedestrian route progress / crossing permissionを適用
14. Household / Person / active Population Trip stateを復元

いずれかが不正な場合は部分Worldを返さない。

## 時間とdeterminism

`elapsedTicks`は`tickCount`とSimulation TickRateから得られるdeterministic elapsed timeと一致しなければならない。`randomState`はseedではなく保存時点のdeterministic random generator状態を保持する。

Vehicle / Pedestrian / Populationのcheckpoint復元後も、同じRoad Network / Route / Trip / schedule入力のもとでは同じfixed-tick continuationを得る。固定cycle Signalのphaseも同じ`tickCount`から復元される。

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
- PedestrianCrossing: 4,000,000
- Vehicle: 1,000,000
- Household: 1,000,000
- Person: 1,000,000

単一entity内のnested collectionにも独立した上限を適用する。

- `vehicles[].routeSteps`: 100,000 / Vehicle
- `persons[].schedule`: 4,096 / Person
- `persons[].needs`: `NeedKind`の定義数。現行は7 / Person
- `blockSections[].segmentIds`: 100,000 / BlockSection
- `depots[].trackSegmentIds`: 100,000 / Depot
- `railwayOperations.routes[].trackSegmentIds`: 100,000 / RailwayRoute
- `railwayOperations.timetables[].stops`: 100,000 / Timetable
- Railway Operations Timetable stop総数: 1,000,000 / World

同じlimit contractをserialize / deserializeへ適用する。deserializeではtop-level collectionに加えてnested collectionも`Utf8JsonReader`でDTO materialization前に検証し、巨大配列を先に確保しない。nested scannerはJSON path / parent contextを追跡するため、同名の`trackSegmentIds`でもDepotとRailwayRouteを混同しない。

serializeでは`SimulationCheckpoint`からSave DTO配列へ投影する前に同じnested上限を検証する。BlockSection / DepotのSave上限はSimulation正本の100,000件membership上限を超えて設定できない。

Vehicle Route、Person Schedule、Railway membership / Routeなどは、128 MiBのSave Data byte上限と単一entity上限の組み合わせで総入力量も制約する。Timetable stopは既存のWorld単位capacity contractを明示化し、単一Timetable上限とは別に総数も制限する。

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
- Population statistics / Person debug view
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
- non-finite XYZ / velocity / bounds / speed / progress / Need値
- invalid enum numeric value
- dangling Building / POI / Road / Household reference
- Pedestrian endpointがBuilding/POIを同時またはどちらも参照する状態
- Pedestrian route progressが再構築routeと整合しない状態
- Vehicle Routeがmissing Lane / Segment / LaneConnectionを参照する状態
- Vehicle progress / state / speedがRouteまたはLane occupancy invariantと整合しない状態
- Household / Person residenceやactivity destinationがmissing Building / POIを参照する状態
- Person active Trip / travel state / Pedestrian / Vehicle参照が相互に矛盾する状態


## Format 8〜10 Railway / Multimodal state

Format 8はTrack / connection / block / Station / Platform / Depotとnext stable IDを保存する。Format 9はFormation / Route / Timetable / Service / Trainの定義とmutable operation stateを追加する。

Format 10は`simulation.multimodalTransit`へTransit Stop / Line / Service Pattern / Trip / Bus・Taxi Vehicle / Taxi Request / Journey / Passengerと各next stable IDを追加する。active Bus/TaxiのRoad Vehicle参照、Railway Pattern/JourneyのRailway Service参照、PopulationのTransit Trip参照はrestore時に整合性検証する。

Format 9以前のsaveはMultimodal Transitを空としてmigrationする。Format 10のtransfer中Passengerはsave/load後も同一Journey leg/stateからfixed tick進行を継続する。
