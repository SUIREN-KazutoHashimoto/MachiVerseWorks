# Save Data 基盤仕様

MachiVerseWorksのauthoritativeな`SimulationWorld`を停止点から同じstateへ復元するSave Data契約を定義する。

## 目的

- authoritative Simulation stateを保存・復元できること
- save → load後もstable ID、tick、乱数系列、route / trip progressを継続できること
- Application version、Protocol version、表示localeに依存しないこと
- default保存APIで生成したデータをdefault読込APIで復元できること
- 外部入力をDTO materialization前からboundedに扱うこと

## Save format version

current formatは **`formatVersion = 10`** とする。Save format versionはルート`VERSION`とProtocol versionから独立する。

migration対象:

- Format 3: Agent + Building / POI。Road Network以降は空stateへmigration
- Format 4: Format 3 + Road Network
- Format 5: Format 4 + Pedestrian
- Format 6: Format 5 + Vehicle
- Format 7: Format 6 + Household / Person / daily schedule / Need / active Population Trip
- Format 8: Format 7 + Railway Infrastructure
- Format 9: Format 8 + Railway Operations
- Format 10: Format 9 + Multimodal Transit / Journey / Passenger / Taxi

Format 2以前、および10より新しい未知versionは拒否する。

## 共通Simulation state

`simulation`はSimulation config、time / random state、各domain entity、各stable-ID namespaceのnext IDを保持する。主なdomainは次のとおり。

- Agent / Building / POI
- RoadNode / RoadSegment / Lane / LaneConnection / RoadAccessPoint
- Pedestrian / crossing progress
- Vehicle / Route progress
- Household / Person / schedule / Need / active Trip
- Railway Infrastructure
- Railway Operations
- Multimodal Transit

表示文字列ではなくraw numeric value、enum code、stable IDを保存する。

## Pedestrian state — Format 5+

各PedestrianはID / TripRequest、origin / destination endpoint、TravelMode、walking speed、leg index、progress、movement stateを保持する。

Walking graphやroute leg配列は保存しない。Road Networkから決定的に再構築し、保存したroute progressを適用する。`Arrived`も明示削除されるまで保存する。

## Vehicle state — Format 6+

各Vehicleはstable ID、dimensions / performance、Lane / RoadSegment / LaneConnectionを参照するRoute step配列、route step index / progress、speed、movement stateを保持する。

world position / forward vectorやLane occupancy indexは独立保存せず、Road topologyとRoute progressから復元する。

## Population state — Format 7+

Householdはstable IDとresidence endpointを保持する。

Personはstable ID、Household、demographics、residence / current location、activity / travel state、destination、active TripRequest / mode / Pedestrian / Vehicle reference、daily activity windows、Needを保持する。

plannerのderived decision cacheは保存しない。Format 6以前はPopulation collectionを空へmigrationする。

## Railway Infrastructure — Format 8+

TrackNode / TrackSegment / TrackConnection / BlockSection / Station / Platform / PlatformAccessPoint / Depotと各next IDを保存する。

BlockSection / Depot membershipは最大100,000 TrackSegment。RoadAccessPointを参照するPlatformAccessPointはrestore時にも参照先と`Foot` accessを要求する。

Format 7以前はRailway Infrastructureを空へmigrationする。

## Railway Operations — Format 9+

Formation / RailwayRoute / Timetable / RailwayService / Trainと各next IDを保存する。

mutable Train stateにはroute distance、3D pose、speed、movement state、Block / Platform / Depot reference、dwell departure tickを含む。Serviceはlifecycle、delay、next-stop index、Train referenceを保持する。

Railway Infrastructureを先に復元し、Route connectivity、Timetable / Station / Platform、Depot、Service / Train referenceを再検証する。Block / Platform owner indexはTrain stateから再構築する。

Format 8以前はRailway Operationsを空へmigrationする。

## Multimodal Transit — Format 10

`simulation.multimodalTransit`へTransit Stop / Line / Service Pattern / Trip / Bus・Taxi Vehicle / Taxi Request / Journey / Passengerと各next IDを保存する。

active Bus/TaxiのRoad Vehicle reference、Railway Pattern/JourneyのRailway Service reference、PopulationのTransit Trip referenceをrestore時に整合性検証する。

Format 9以前はMultimodal Transitを空へmigrationする。transfer中Passengerも同じJourney leg/stateからfixed tick進行を継続する。

## Signal controller state

固定cycle Intersection Signalには独立mutable Save stateを追加しない。

- movement / conflict / phase topologyはRoadNode / Lane / LaneConnectionから決定的に派生
- current phase / phase tickは`tickCount`と`tickRate`から決定的に派生
- current-tick queue / entry grantはephemeral observation

将来adaptive signalが独立mutable stateを持つ場合だけ新しいSave field / formatを追加する。

## Restore順序

Format 10は大きく次の順で復元する。

1. UTF-8 byte limit、JSON、unknown field、required field、top-level / nested collection count検証
2. Simulation config / tick / elapsed time / random state検証
3. Agent / Building / POI
4. Road topology / RoadAccessPoint
5. Pedestrian / crossing progress
6. Vehicle / Route / Lane occupancy rebuild
7. Household / Person / Population active Trip
8. Railway Infrastructure
9. Railway Operations
10. Multimodal TransitとRoad / Railway / Population cross-domain reference
11. routing / walking / traffic / signal / railway ownership等のderived stateを再構築

いずれかが不正なら部分Worldを返さない。

## 時間とdeterminism

`elapsedTicks`は`tickCount`とSimulation TickRateから得られるdeterministic elapsed timeと一致しなければならない。`randomState`はseedだけでなく保存時点のdeterministic generator状態を保持する。

Vehicle / Pedestrian / Population / Railway / Multimodal Transitは、同じauthoritative inputと後続tickのもとでsave/load後もdeterministic continuationを得る。

## Resource limits

`WorldSaveLimits`のdefault top-level上限:

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
- `persons[].needs`: `NeedKind`定義数。現行7 / Person
- `blockSections[].segmentIds`: 100,000 / BlockSection
- `depots[].trackSegmentIds`: 100,000 / Depot
- `railwayOperations.routes[].trackSegmentIds`: 100,000 / RailwayRoute
- `railwayOperations.timetables[].stops`: 100,000 / Timetable
- Railway Operations Timetable stop総数: 1,000,000 / World

同じlimit contractをserialize / deserializeへ適用する。deserializeでは`Utf8JsonReader`がJSON path / parent contextを追跡し、DTO materialization前にnested件数を拒否する。同名`trackSegmentIds`でもDepotとRailwayRouteを混同しない。

serializeでも`SimulationCheckpoint`からSave DTO配列へ投影する前に同じnested上限を検証する。BlockSection / DepotのSave上限はSimulation正本の100,000件membership上限を超えて設定できない。

`Save(Stream, ...)`は全体がbyte limit内であることを確認してからdestinationへ書くため、limit超過時にpartial Saveを残さない。

## 保存しない情報

- Application version / Protocol version
- locale / 翻訳済み文字列
- Web camera / connection / subscription / WebSocket state
- Audio Client state
- derived routing / pedestrian graph
- Road Traffic topology / Lane occupancy index
- Intersection movement / conflict / fixed-cycle phase
- Railway runtime owner index
- Population statistics / Person debug view
- benchmark / diagnostics

## 拒否条件

少なくとも次を拒否する。

- malformed JSON / unknown field / unsupported format
- configured byte / top-level / nested count超過
- required field欠落、overflow、non-finite値、invalid enum
- 0または重複stable ID、`next*Id`不整合
- dangling Building / POI / Road / Household reference
- Pedestrian endpoint / route progress不整合
- Vehicle Route / progress / occupancy invariant不整合
- Household / Person residence、destination、active Trip reference不整合
- Railway Track / Connection / Block / Station / Platform / PlatformAccessPoint / Depot reference不整合
- Railway Route / Timetable / Service / Train semantic reference不整合
- Multimodal Stop / Pattern / Trip / Vehicle / Taxi / Journey / Passenger reference不整合
- active Transit stateとRoad Vehicle / Railway Service / Population Tripのcross-domain不整合

実装境界は[`../architecture/persistence.md`](../architecture/persistence.md)を参照する。
