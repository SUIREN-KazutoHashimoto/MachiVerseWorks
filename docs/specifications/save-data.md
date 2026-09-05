# Save Data 基盤仕様

MachiVerseWorksのauthoritativeな`SimulationWorld`を停止点から同じstateへ復元するSave Data契約を定義する。

## 目的

- authoritative Simulation stateを保存・復元できること
- save → load後もstable ID、tick、乱数系列、route / trip progressを継続できること
- Application version、Protocol version、表示localeに依存しないこと
- default保存APIで生成したデータをdefault読込APIで復元できること
- 外部入力をDTO materialization前からboundedに扱うこと

## Save format version

current formatは **`formatVersion = 11`** とする。Save format versionはルート`VERSION`とProtocol versionから独立する。実装上の正本は [`SaveFormatVersion.Current`](../../src/persistence/SaveFormatVersion.cs) である。

migration対象:

- Format 3: Agent + Building / POI。Road Network以降は空stateへmigration
- Format 4: Format 3 + Road Network
- Format 5: Format 4 + Pedestrian
- Format 6: Format 5 + Vehicle
- Format 7: Format 6 + Household / Person / daily schedule / Need / active Population Trip
- Format 8: Format 7 + Railway Infrastructure
- Format 9: Format 8 + Railway Operations
- Format 10: Format 9 + Multimodal Transit / Journey / Passenger / Taxi
- Format 11: Format 10 + Economy checkpoint

Phase 22以降のLogistics / Power / Water・Sewer / Gas / Optical / Radio / World Environment / Regional Generation等は、既存Format 11へ**additive optional sub-state**として追加する。各fieldが存在しない既存Format 11 Saveは、そのdomainを空またはdefault stateとして復元できるため、これらの追加だけではformat versionを上げない。

Format 2以前、および11より新しい未知versionは拒否する。

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
- Economy
- Economy checkpoint配下のoptional sub-stateとしてLogistics / Power / Water・Sewer / Gas / Optical / Radio / World Environment / Regional Generation等

表示文字列ではなくraw numeric value、enum code、stable IDを保存する。Human Toponymは表示済み翻訳文字列ではなくdomain nameとprovenanceを保存する。

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

## Multimodal Transit — Format 10+

`simulation.multimodalTransit`へTransit Stop / Line / Service Pattern / Trip / Bus・Taxi Vehicle / Taxi Request / Journey / Passengerと各next IDを保存する。

active Bus/TaxiのRoad Vehicle reference、Railway Pattern/JourneyのRailway Service reference、PopulationのTransit Trip referenceをrestore時に整合性検証する。

Format 9以前はMultimodal Transitを空へmigrationする。transfer中Passengerも同じJourney leg/stateからfixed tick進行を継続する。

## Economy — Format 11+

Format 11はCompany / Establishment / Job / Employment / Household economy等のEconomy checkpointを追加する。Format 10以前からmigrationした場合は空Economy stateとして開始する。

Format 11導入後のdomainは互換なoptional sub-stateとして追加される。代表例:

- Logistics: Commodity / Inventory / Order / Shipment
- Power: topology / generation / load / service state
- Water / Sewer: topology / flow / service state
- Gas: pipeline / delivered gas / inventory / service state
- Optical: topology / capacity / congestion / outage state
- Radio / Spectrum: site / antenna / emission / link / spectrum state
- World Environment: environment config / GeographicFeature / Natural Toponym
- Regional Generation: Settlement / historical growth / corridor / District / Parcel / generated Building・POI / Human Toponym / Road Sign / quality report

各domain固有の保存項目・参照整合性は対応する`docs/specifications/`のdomain仕様を正本とする。optional field欠落を理由にFormat 11全体を拒否せず、そのdomainの定義済みempty/default migrationを適用する。

Regional Generationはgenerated planそのものを保存し、materialize済みのRoad / Building / Population / Economy runtime stateとは別のstable generation IDを保持する。load後も生成履歴・人間由来地名provenance・RegionalQualityReportを失わない。

## Signal controller state

固定cycle Intersection Signalには独立mutable Save stateを追加しない。

- movement / conflict / phase topologyはRoadNode / Lane / LaneConnectionから決定的に派生
- current phase / phase tickは`tickCount`と`tickRate`から決定的に派生
- current-tick queue / entry grantはephemeral observation

将来adaptive signalが独立mutable stateを持つ場合だけ新しいSave field / formatを追加する。

## Restore順序

Format 11は大きく次の順で復元する。

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
11. Economyおよび存在するFormat 11 optional domain sub-stateを依存順に復元・検証。World EnvironmentをRegional Generationより先に検証し、Regional seed / provenanceの前提を確定する
12. routing / walking / traffic / signal / railway ownership等のderived stateを再構築

いずれかが不正なら部分Worldを返さない。

## 時間とdeterminism

`elapsedTicks`は`tickCount`とSimulation TickRateから得られるdeterministic elapsed timeと一致しなければならない。`randomState`はseedだけでなく保存時点のdeterministic generator状態を保持する。

Vehicle / Pedestrian / Population / Railway / Multimodal Transit / Economy / Logistics / Infrastructureは、同じauthoritative inputと後続tickのもとでsave/load後もdeterministic continuationを得る。

Regional Generationは同一seed / generation inputで再現可能であることに加え、保存済みsnapshotをload時に再生成へ置換せず、その時点のauthoritative generated stateを復元する。

## Resource limits

`WorldSaveLimits`のdefault top-level上限:

- UTF-8 Save Data: 128 MiB
- Agent: 1,000,000
- Building / Station / Depot: 1,000,000（shared infrastructure-site limit）
- POI: 1,000,000
- RoadNode / TrackNode: 1,000,000（shared infrastructure-node limit）
- RoadSegment / TrackSegment / BlockSection: 1,000,000（shared infrastructure-segment limit）
- Lane: 2,000,000
- LaneConnection / TrackConnection: 4,000,000（shared infrastructure-connection limit）
- RoadAccessPoint / Platform / PlatformAccessPoint: 1,000,000（shared infrastructure-access-point limit）
- Pedestrian: 1,000,000
- PedestrianCrossing: 4,000,000
- Vehicle: 1,000,000
- Household: 1,000,000
- Person: 1,000,000

既存constructor parameterとのsource compatibilityを維持するため、shared infrastructure limitの入力名は当面`maximumBuildingCount` / `maximumRoadNodeCount` / `maximumRoadSegmentCount` / `maximumLaneConnectionCount` / `maximumRoadAccessPointCount`を維持する。public propertyでは同じ値を`MaximumInfrastructureSiteCount` / `MaximumInfrastructureNodeCount` / `MaximumInfrastructureSegmentCount` / `MaximumInfrastructureConnectionCount` / `MaximumInfrastructureAccessPointCount`として明示し、Road名propertyは互換aliasとして同値を返す。したがってこれらのcustom limit変更は対応するRoad / Railway / Regional generation collectionの上限へ意図的に再利用される。

単一entity内のnested collectionにも独立した上限を適用する。

- `vehicles[].routeSteps`: 100,000 / Vehicle
- `persons[].schedule`: 4,096 / Person
- `persons[].needs`: `NeedKind`定義数。現行7 / Person
- `blockSections[].segmentIds`: 100,000 / BlockSection
- `depots[].trackSegmentIds`: 100,000 / Depot
- `railwayOperations.routes[].trackSegmentIds`: 100,000 / RailwayRoute
- `railwayOperations.timetables[].stops`: 100,000 / Timetable
- Railway Operations Timetable stop総数: 1,000,000 / World
- `economy.regionalGeneration.snapshot.corridors[].geometry`: GeographicFeature geometryと同じbounded point limit

Regional generation snapshotではSettlement / GrowthEvent / Corridor / District / Parcel / GeneratedBuilding / GeneratedPoi / HumanToponym / RoadSignの各collectionもDTO materialization前に上限検証する。

同じlimit contractをserialize / deserializeへ適用する。deserializeでは`Utf8JsonReader`がJSON path / parent contextを追跡し、DTO materialization前にnested件数を拒否する。同名`trackSegmentIds`や`geometry`でもdomain contextを区別する。

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
- materialized RoadSignのnearest RoadSegment / Lane bindingなど、authoritative generated stateから再導出できるprojection
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
- Economy / Logistics / Utility / Communication / Radioのstable ID、cross-domain reference、capacity / inventory / service-state invariant不整合
- Regional Generationのduplicate / zero stable ID、missing Settlement / District / Parcel / Building / HumanToponym / Corridor reference、seed mismatch、invalid geometry / quality range

実装境界は[`../architecture/persistence.md`](../architecture/persistence.md)を参照する。