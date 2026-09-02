# Regional & Urban Generation Architecture

Simulation Phase30のRegional & Urban Generationの実装境界を定義する。

## Ownership

Regional generationのauthoritative stateは`MachiVerseWorks.Simulation`が所有する。Protocol、Server、Gateway、Web Viewは生成規則や都市状態の正本を持たない。

主なauthoritative contractは次のとおり。

- `RegionalGenerationSnapshot`
- `Settlement` / `HistoricalGrowthEvent`
- `RegionalCorridor`
- `District` / `Parcel` / `GeneratedBuilding` / `GeneratedPoi`
- `HumanToponym` / `RoadSign`
- `RegionalQualityReport`

`SimulationWorld`は生成済みsnapshotを内部stateとして保持し、Checkpoint / Save Dataへ含める。consumerへ返すsnapshotはdetached copyとし、observerからauthoritative stateを変更できない。

## Input boundary

Phase30はPhase29の`WorldEnvironmentGenerator`を地形・気候・水系・地理featureの決定論的入力として利用する。

同じ次の入力から同じRegional snapshotを生成することを契約とする。

- World Environment seed
- generation volume
- quality preset
- explicit settlement / iteration override

randomnessを必要とする箇所はseed、座標、domain saltからstable hashを生成して決定する。実行順やprocess-localな`Random` stateへ依存しない。

## Generation pipeline

生成は次の境界で構成する。

1. `RegionalGenerator.Generate`
   - settlement candidate selection
   - suitability評価
   - Settlement origin / regional role / initial economy
   - historical growth
   - regional/intercity road corridor
   - railway corridor plan
   - base district / parcel / building / POI
   - base human toponym / road sign
   - `RegionalQualityReport`
   - Generate → Evaluate → Improve loop
2. `RegionalGenerationEnricher.Enrich`
   - role由来Station / Industrial / CBD / Suburb district
   - nearest road orientationに従うBlock subdivision
   - road access / parcel size / slope / flood / land-value / use-fitによるparcel suitability
   - Vacant / Developing / Occupied / Redevelopingの初期development state
   - Road Context Analysis
   - Bridge / Tunnel name provenance
   - place-name / direction / terrain hazard sign
3. `SimulationWorld` authoritative capture
   - detached snapshot化
   - Checkpoint / Save Data対象化

Enrichment後のstateをauthoritative snapshotとして保存する。生成途中のmutable collectionを外部公開しない。

## Stable ID and provenance

Phase30独自domainは、生成順序だけを意味する一時indexではなくstable IDを持つ。

- `SettlementId`
- `RegionalCorridorId`
- `GrowthEventId`
- `DistrictId`
- `ParcelId`
- `GeneratedBuildingId`
- `GeneratedPoiId`
- `HumanToponymId`
- `RoadSignId`

IDはseed、座標、親ID、domain saltなどから決定論的に導出する。

人間活動由来の地名は`HumanToponymProvenance`を持ち、Phase29の`NaturalToponym`または親Human Toponymとの関係を保持する。Settlement名からDistrict / Station / Road / Bridge / Tunnelへ派生する場合も親参照を失わない。

## Generated plan and live-world materialization

Regional generation snapshotと既存Simulation live storeは分離する。

`MaterializeRegionalGeneration`は初期Worldの空のurban / population / economy stateに対してのみ実行でき、generated planを既存authoritative storeへ展開する。

- Corridor → `RoadNode` / `RoadSegment` / `Lane` / `LaneConnection`
- GeneratedBuilding → `Building`
- GeneratedPoi → `Poi`
- Parcel / Building → `RoadAccessPoint`
- Settlement population → `Household` / `Person`
- Settlement jobs / economy → `Company` / `Establishment` / `Job` / Employment

この分離により、Phase30の生成履歴・stable generation IDと、既存交通/人口/economy domainのruntime IDを混同しない。

`CreateRegionalRoadSignPlacements`はmaterialize後にRoadSignを最寄りの実`RoadSegment`と`Lane`へdeterministicにbindingし、destination Settlement / HumanToponymとの参照も保持する。

## Railway and infrastructure boundary

Phase30のRailwayはregional corridor planまでを所有し、既存Railway Infrastructureの詳細topology / operationsを再実装しない。

Power / Water / Sewer / Gas / Optical / Radio等もPhase30で別domain stateを複製せず、`EvaluateRegionalInfrastructureConstraint`から地形・空洞・傾斜・Settlement proximity等の配置制約を提供する。各infrastructure domainがauthoritative topologyを所有する。

## Observation and Protocol

Simulationは`RegionalGenerationSnapshot`をauthoritative observation sourceとして返す。

Protocol 2.18の`RegionalGenerationSnapshotMessage` / message type 810はdomain stateのpassive DTO projectionであり、Protocolが都市生成規則を所有しない。Server mapperも変換のみを担当する。

subscription、cache、interest management、配送頻度、chunking等はObservation Gateway / Server delivery側の責務であり、Phase30 domain semanticsへ混在させない。

## Persistence and input hardening

Regional generationは`EconomyCheckpoint.RegionalGeneration`からSimulation Checkpoint / Save Dataへ含める。

Save loadでは以下の二段階で防御する。

1. JSON deserialization前のnested collection pre-scan
2. materialization前のCheckpoint件数・ID・enum・reference・finite/range validation

対象はSettlement、GrowthEvent、Corridor、District、Parcel、GeneratedBuilding、GeneratedPoi、HumanToponym、RoadSign、およびcorridor geometryを含む。

これにより巨大なcollectionをDTOへmaterializeしてから拒否する経路を避ける。

## Quality and performance

`RegionalQualityReport`は少なくとも次を0..1で評価する。

- terrain adaptation
- road connectivity
- slope cost
- accessibility
- congestion risk
- land-use consistency
- flood exposure
- urban compactness
- polycentric balance

`RegionalGenerationOptions`はDraft / Standard / HighQuality presetを持ち、Settlement targetとImprove iteration budgetを段階化する。

`RegionalGenerationBenchmarks`はpreset別に次を測定する。

- Regional snapshot生成
- Regional generation + live-world materialization
- allocation (`MemoryDiagnoser`)

## Test boundary

Phase30の回帰検証は次を含む。

- same seed / volume / presetのsnapshot一致
- Checkpoint / Save round-trip
- detached observation
- multiple settlement / historical growth
- block subdivision
- parcel suitability
- road context / structure naming / sign rule
- materialized road / lane / population / jobs
- sign → actual road/lane reference
- river / port / basin / valley / mountain / cold / dry inland / island fixtures
- Protocol 2.18 round-trip / version gate / reference validation

root application `VERSION`の変更をPhase30実装検証の前提にしないため、専用`Regional Generation Validation` workflowでbuild/testを実行する。application version transitionはrelease / integration時の別境界である。
