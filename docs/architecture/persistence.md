# Persistence Architecture

## Boundary

`MachiVerseWorks.Persistence`はauthoritativeな`SimulationWorld`とversioned JSON Save Dataの変換境界である。実行loop、file slot、UI、network sessionを所有しない。

依存方向は **Persistence → Simulation**。SimulationはPersistenceやJSON DTOを参照せず、`SimulationCheckpoint`だけをin-memory persistence contractとして公開する。

```text
SimulationWorld
   │ CreateCheckpoint()
   ▼
SimulationCheckpoint
   │ WorldSaveSerializer
   ▼
Save Format 11 JSON
```

loadは逆方向に、byte/resource validation → JSON DTO → checkpoint validation → `SimulationWorld.RestoreCheckpoint()`の順で行う。失敗時に部分Worldを返さない。

## Current format

current Save formatは **11**。Application `VERSION`およびProtocol versionとは独立してversioningする。実装上の正本は [`SaveFormatVersion.Current`](../../src/MachiVerseWorks.Persistence/SaveFormatVersion.cs) である。

- Format 3: Agent + Building / POI
- Format 4: Road Network
- Format 5: Pedestrian
- Format 6: Vehicle
- Format 7: Household / Person / daily activity / Need / active Population Trip
- Format 8: Railway Infrastructure
- Format 9: Railway Operations
- Format 10: Multimodal Transit / Journey / Passenger / Taxi
- Format 11: Economy checkpoint

Format 3〜10はmigration pathを持ち、未導入domainを空stateとして補う。Format 11導入後のLogistics / Power / Water・Sewer / Gas / Optical / Radio等は、後方互換なoptional sub-stateとして追加し、field欠落時はdomainごとのempty/default stateへ復元する。Format 2以前とcurrentより新しい未知formatは拒否する。

## Checkpoint ownership

`SimulationCheckpoint`は少なくとも次のauthoritative stateを保持する。

- Simulation config、tick、elapsed tick、deterministic random state
- Agent / Building / POI
- RoadNode / RoadSegment / Lane / LaneConnection / RoadAccessPoint
- Pedestrian / crossing progress
- Vehicle / Route progress
- Household / Person / schedule / Need / active Trip reference
- Railway Infrastructure: TrackNode / TrackSegment / TrackConnection / BlockSection / Station / Platform / PlatformAccessPoint / Depot
- Railway Operations: Formation / RailwayRoute / Timetable / RailwayService / Train
- Multimodal Transit: Stop / Line / ServicePattern / Trip / TransitVehicle / TaxiRequest / Journey / Passenger
- Economy: Company / Establishment / Job / Employment / Household economy
- Format 11 optional sub-state: Logistics / Power / Water・Sewer / Gas / Optical / Radio等
- 各stable-ID namespaceのnext ID

描画cache、Web connection、Protocol frame、derived routing graph、Lane occupancy index、fixed-signal derived phase、runtime ownership dictionaryなどはSave Dataの正本にしない。

## Restore order

参照先を先に復元する。current Format 11の大きな順序は次のとおり。

1. byte limit / JSON構造 / required field / collection countを検証
2. Simulation config / tick / random stateを検証
3. Agent / Building / POI
4. Road topology / access
5. Pedestrian / Vehicle
6. Household / Person / Population active Trip
7. Railway Infrastructure
8. Railway Operations
9. Multimodal TransitとRoad Vehicle / Railway Service / Population Tripのcross-domain reference
10. Economyおよび存在するoptional domain sub-stateを依存順に検証・復元
11. derived index / graph / ownership stateを再構築

Railway OperationsはTrack / Block / Station / Platform / Depotが存在してから検証し、Multimodal TransitはRoad/Railway/Populationの参照先が復元されてから受理する。Economy以降のoptional domainもBuilding / Establishment / Road / Power等、それぞれの参照先を先に復元してから受理する。

## Derived state rebuild

Saveへ重複保存せず、authoritative inputから再構築する代表例:

- Road / Lane routing graphとcache
- Pedestrian walking graph
- Road Traffic derived topology / Lane occupancy
- Intersection movement / conflict / fixed-cycle phase
- Railway Operationsのroute step geometry、stop route distance、Block / Platform owner index
- publish read model / spatial query index

restore後のfixed-tick continuationは保存したauthoritative stateとdeterministic rebuild結果から継続する。

## Resource-limit architecture

`WorldSaveLimits`をserialize / deserializeの共通contractとして使用する。UTF-8 Save全体はdefault 128 MiBで、主要top-level collectionにもconfigured count上限を設ける。

さらにnested collectionをDTO materialization前にpath/context付き`Utf8JsonReader` scannerで検証する。

- `vehicles[].routeSteps`: 100,000 / Vehicle
- `persons[].schedule`: 4,096 / Person
- `persons[].needs`: `NeedKind`定義数 / Person
- `blockSections[].segmentIds`: 100,000 / BlockSection
- `depots[].trackSegmentIds`: 100,000 / Depot
- `railwayOperations.routes[].trackSegmentIds`: 100,000 / Route
- `railwayOperations.timetables[].stops`: 100,000 / Timetable
- Timetable stop total: 1,000,000 / World

同名propertyでもparent contextを区別する。たとえばDepotとRailwayRouteの`trackSegmentIds`は別contractとして扱う。write側もcheckpointからDTO配列へ投影する前に同じ上限を適用する。Format 11 optional domainは各domain checkpoint validationと対応するSave limitを追加適用する。

## Validation layers

外部Saveは次の層で拒否する。

- JSON / unknown field / required field
- byte / top-level / nested collection limit
- finite値、enum、stable ID、next-ID capacity
- Road / Pedestrian / Vehicle / Population domain invariant
- Railway topology / membership / Station・Platform・Depot reference
- Railway Operations Route / Timetable / Service / Train semantic reference
- Multimodal Transit Stop / Pattern / Trip / Vehicle / Journey / Passengerとcross-domain reference
- Economy / Logistics / Utility / Communication / Radio checkpointのstable ID、cross-domain reference、capacity / inventory / service-state invariant

Protocolへ送れない単一aggregateをSaveからauthoritative stateへ導入しないため、BlockSection / Depot membershipにはSimulation側100,000件hard limitもある。

## Stream behavior

`Save(Stream, ...)`はdestinationへ書く前にserialization result全体がconfigured byte limit内であることを確認し、limit超過時にpartial Saveを残さない。

詳細なfield契約とmigrationは[`../specifications/save-data.md`](../specifications/save-data.md)を正本とする。
