# Regional & Urban Generation

Simulation Phase30で追加するRegional & Urban Generationの現行domain仕様を定義する。

## Purpose

Worldを単一中心都市として生成するのではなく、地形・水系・気候・交通可能性から複数のSettlementを成立させ、それぞれが異なる役割・初期経済・歴史的成長を持つpolycentricな地域を生成する。

Phase30は「完成した街を一度に置く」のではなく、origin、center formation、urban expansion、suburbanization、redevelopment、新中心形成という履歴を持つ初期都市状態を生成する。

## Determinism

同じWorld Environment seed、generation volume、quality preset、明示overrideからは同じRegional generation resultを得なければならない。

stable ID、Settlement位置、役割、道路、District、Parcel、Building、POI、地名、標識、quality resultはprocess-localな乱数状態へ依存しない。

## Settlement

Settlementは最低限、次の意味を持つ。

- stable `SettlementId`
- 3D center
- environment classification
- origin kind
- regional role
- initial economy
- suitability
- initial population / jobs
- influence radius
- human toponym reference

Settlement suitabilityは地形平坦性、水アクセス、交通可能性、建設可能性、資源アクセス、浸水risk、急傾斜risk、孤立度、建設costを考慮する。

複数Settlementは最小距離と異なるenvironmentを考慮して選択し、一極集中だけを正解としない。

## Regional role and initial economy

地域条件からPort、TransportHub、Industrial、Resource、Administrative、Market、Agricultural、LocalService等の役割を与える。

役割はInitial Economyへ接続し、Trade、Manufacturing、PortTrade、Transport、ResourceExtraction、Services、Agriculture等の初期状態を決める。

## Historical growth

各Settlementは`HistoricalGrowthEvent`を持ち、最低限次のstageを表現できる。

- Origin
- CenterFormation
- UrbanExpansion
- Suburbanization
- Redevelopment
- NewCenterFormation

Population / Job deltaと理由を保持し、将来Phaseの時間発展が「どこから成長したか」を参照できるようにする。

## Regional corridor

Settlement間はterrain-awareなregional corridorで接続する。

- Primary / Regional / Intercity road
- Railway plan

Road corridorは距離だけでなくterrain ruggedness、flood risk等をconstruction costへ反映する。

RailwayはPhase30ではregional planを表し、詳細なTrack / Station / Operationsのauthoritative topologyは既存Railway domainに任せる。

## District / Block / Parcel

Settlement内部はDistrictへ分ける。

- OldTown
- CentralBusiness
- StationDistrict
- IndustrialArea
- Suburb
- ResidentialQuarter

Settlement roleと人口規模に応じてStation / Industrial / CBD / Suburb districtを追加できる。

District内Blockは最寄りの非鉄道corridorの方向を参照し、道路reserveを残してdeterministicに分割する。

Parcelはstable ID、Zone、development state、development suitability、land value、optional generated buildingを持つ。

ZoneはResidential、Commercial、Industrial、MixedUse、Civic、Agricultural、OpenSpaceを表す。

## Parcel suitability and development

Parcel suitabilityは少なくとも以下を考慮する。

- road access
- parcel size suitability
- slope safety
- flood safety
- land value fit
- district / regional roleに対するuse fit

初期development stateはVacant / Developing / Occupied / Redevelopingを区別する。

需要が弱いparcelを必ず建築済みにせずvacancyを許容する。高land value・高需要等の条件ではredevelopmentを表現できる。

## Building / POI

開発済みParcelには用途、3D bounds、floors、capacity、historical stageを持つGeneratedBuildingを生成する。

Settlement roleに応じ、SettlementCenter、Market、Station、CivicCenter、IndustrialHub、Port等のPOIを生成する。

live Simulationへmaterializeする場合は既存Building / Poi storeを利用し、Phase30独自のruntime Building storeを作らない。

## Population / Jobs

初期World materializationではSettlement populationをHousehold / Personへ、jobsをCompany / Establishment / Jobへ展開する。

Employment数はpopulationとjob capacityの範囲内で作成し、既存Economy contractの参照整合性を守る。

## Human toponym

人間活動由来の地名は自然地名とは別の`HumanToponym`として扱う。

Settlement、District、Road、Bridge、Tunnel、Stationの名前を生成でき、自然featureまたは親Human Toponymへのprovenanceを保持する。

名前そのものだけを保存せず、由来をSave / Checkpoint後も保持する。

## Road Context and signs

Road Context Analysisはcorridorから次を判定できる。

- maximum grade
- sharp turn
- flood risk
- river / water crossing
- rock slope
- mountain pass
- tunnel need
- coastal lowland
- relevant GeographicFeature
- destination Settlement

RoadSignはDirection / PlaceNameおよび地形hazardのsignを持ち、corridor stable IDを参照する。

materialize後はsignを実RoadSegment / Laneへbindingできる。destination signはSettlement / HumanToponymへの参照を保持する。

## Infrastructure placement constraint

Power、Water、Sewer、Gas、Optical、Radio、Railway等のauthoritative infrastructureをPhase30が所有することはしない。

Phase30は地形・地下void・water・slope・nearest Settlement等を使った配置constraintを提供し、各infrastructure domainが最終topologyを所有する。

## Quality report

生成結果は`RegionalQualityReport`を持つ。

最低限、terrain adaptation、road connectivity、average slope cost、accessibility、congestion risk、land-use consistency、flood exposure、urban compactness、polycentric balanceを評価する。

Generatorはpresetごとのiteration budget内でEvaluate → Improveを実行できる。

## Quality preset

- Draft: 少ないSettlement targetとiterationで高速生成
- Standard: 通常品質
- HighQuality: 多いSettlement targetとiterationで改善探索を増やす

presetはdeterministic resultの入力の一部とする。

## Fixtures

回帰fixtureとして少なくとも次を持つ。

- river
- port
- basin
- valley
- mountain
- cold
- dry inland
- island

fixtureは地形ごとの生成規則が将来の最適化で消失していないことを確認する。

## Save / observation

Regional generation resultはSimulation authoritative stateでありCheckpoint / Save Dataへ保存する。

load時はcollection件数・geometry長をdeserialization前からbounded scanし、その後stable ID / enum / reference / numeric rangeを検証する。

observerへはdetached snapshotを返す。Protocol 2.18のRegional Generation payloadはこのsnapshotの意味を転送するだけで、生成規則や配送cacheをdomain仕様へ混在させない。

## Performance

Regional generationは通常tickのhot pathではなく初期生成処理であるが、品質presetごとの生成時間とallocationをbenchmarkする。

生成だけと、既存Road / Population / Economy storeへのmaterializationを分けて計測可能にする。
