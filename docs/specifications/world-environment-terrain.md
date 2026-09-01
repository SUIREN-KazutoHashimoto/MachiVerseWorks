# World Environment / Detailed 3D Terrain 仕様

## 目的

Simulationは都市・交通・建物より下位にある自然環境をauthoritative stateとして所有する。Phase29では、広域の環境傾向を表すGlobal Environmentと、道路・鉄道・建物・Agentが直接参照できるDetailed 3D Terrainを分離して導入する。

Viewやcamera位置は地形生成の正本にならない。同じ`WorldEnvironmentConfig`とworld seedから、Server再起動やSave復元をまたいでも同じ主要地形特徴を得られることを要求する。

## WorldEnvironmentConfig

`WorldEnvironmentConfig`は少なくとも次を持つ。

- `WorldSeed`: 自然環境生成専用のdeterministic seed
- `GeographicNorth`: 3D world座標に対する水平north vector
- `LatitudeDegrees`: 基準緯度。符号から北半球/南半球を導出する
- `SeaLevelMeters`: 海面標高
- `Continentality`: 0..1の大陸性
- `MaritimeInfluence`: 0..1の海洋影響
- `MeanAnnualTemperatureCelsius`: 基準年平均気温
- `SeasonalityCelsius`: 季節振幅
- `AnnualPrecipitationMillimeters`: 基準年降水量
- `ConfiguredCoastlineDistanceMeters`: 外部設定された海岸距離がある場合のoverride
- `GlobalScaleMeters`: 広域field生成scale
- `TerrainDetailScaleMeters`: detailed terrain生成scale

Simulationの既存`Seed=0`互換は維持し、環境側のdefault seedだけを非0へ正規化する。

## Global Environment

`WorldEnvironmentGenerator`は連続座標に対してdeterministicに`RegionalEnvironmentSample`を返す。

### 地形区分

最低限、Ocean / Continent / Islandを区別する。広域標高は大陸scale、tectonic/ridge scale、basin scale、island scaleを別々のnoise layerとして合成する。

### 気候

気温・季節性・降水量は以下の影響を受ける。

- 緯度
- 標高によるlapse rate
- 海岸距離
- maritime influence / continentality
- 広域のdeterministic local variation

外部から`ConfiguredCoastlineDistanceMeters`が与えられた場合、それを推定値より優先する。

### 水系

広域fieldからflow direction、drainage、major river / tributary、lake、flood riskを導出する。Phase29の水系はbaselineであり、時間発展する侵食・堆積・洪水simulationは扱わない。

### Settlement candidate

自然環境から`Buildability`と`SettlementScore`を算出し、都市・街・村・集落配置の候補を提供する。candidate選択はdeterministicで、単純な最高score一極集中ではなく、Coastal / River / Basin / Mountain / Cold / Dry / Island / InlandPlainの環境多様性を先に確保してから残りをscore順に選ぶ。

## Detailed 3D Terrain

Global Environmentは都市圏の直接geometryとして使わない。`TerrainSurface`は広域標高へdetail layerを重ね、局所のheight、normal、slope、roughness、material、surface waterを返す。

`TerrainPartition`は一定world sizeでlazy生成されるcache単位であり、partition境界は自然地形の意味境界ではない。境界上のnormal/slope計算は隣接側のdeterministic sampleも参照できる。

## TerrainVolume

Detailed Terrainの正本はheightfieldだけに限定しない。`TerrainVolume`は任意の`WorldPoint`について以下を返す。

- `Air`
- `Water`
- `Soil`
- `Rock`
- `Void`

地下のdeterministic cavityを`Void`として扱い、同じXYに対してground surface、water surface、cavity floor/ceilingなど複数surfaceを返せる。

Phase29ではcave/overhangの完全なmesh生成は行わないが、将来の地下空間・複雑地形を表現できるquery contractを先に固定する。

## Terrain constraint API

`TerrainConstraintEvaluator`はRoad / Railway / Building / Generic用途でterrain constraintを返す。

- maximum slope
- elevation range
- surface water intersection
- underground void intersection
- allow / denyとreason

baseline slope limitはRailway 4°、Building 8°、Road 12°、Generic 18°。これは最終的な土木工学モデルではなく、Phase29以降のnetwork/building placementが共通terrain APIを参照するための初期契約である。

`SnapToGround`は任意XYZを同じXYのprimary ground surfaceへsnapする。

## GeographicFeature

自然地形上の意味的featureを`GeographicFeature`としてstable entity化する。

対象type:

- Mountain / MountainRange
- River / Tributary / Lake
- Valley / Basin / Plain / Plateau / Pass
- Cape / Bay / Coast / Island / Peninsula
- Cave

各featureはstable ID、type、3D bounds、geometry、area、optional parent、最低/最高標高を持つ。同一seed/config/対象volumeでは検出結果とstable IDが一致する。

## 自然地名

自然地名は`NaturalToponym`としてfeatureとは別entityにする。名前生成はdeterministicで、`ToponymProvenance`に次を保持する。

- provenance kind
- source GeographicFeature ID
- optional parent Toponym ID
- generator key

Phase29 baselineのgenerator keyは`phase29-natural-v1`。

## Save / restore

`WorldEnvironmentCheckpoint`は`EconomyCheckpoint`のoptional extensionとして保存する。既存Save format 11は維持し、旧Saveに`worldEnvironment`が無い場合はSimulation seedからdefault環境を再構築する。

保存対象:

- `WorldEnvironmentConfig`
- 既にmaterializeされた`GeographicFeature`
- `NaturalToponym`とprovenance

Terrain partition cache自体は保存しない。config/seedから再生成可能なderived cacheだからである。

## Observation / Protocol

Protocol 2.17で`WorldEnvironmentSnapshot`（message type 800）を追加する。Clientの`SubscribeVolume`に対し、Serverは以下を送る。

- config
- subscribed 3D volume
- global environment samples
- detailed terrain surface samples
- GeographicFeature
- NaturalToponym / provenance
- simulation tick

payloadは1 MiB frame上限内で、sample / feature / geometry / text件数をcodecで制限する。Viewはこのsnapshotをread-onlyで描画・debug表示するだけで、terrainや地名を再生成しない。

## Reproducibility

Phase29 E2EはServerを別processとして再起動し、同じseed/config・同じsubscription volumeで受信したsnapshotからtickだけ除外したSHA-256 digestが一致することを検証する。

## Performance

benchmarkは以下を分けて測定する。

- Global Environment point query throughput
- Detailed Terrain surface query throughput
- large-world reference snapshot生成
- detailed reference snapshot生成

global fieldとdetailed terrainは同じbenchmarkに混ぜて一つの数値にしない。

## Phase29で扱わないもの

次は後続Phaseへ送る。

- advanced ecosystem / vegetation succession
- 動的erosion / sediment
- full flood simulation
- cut-and-fill / retaining wall / tunnel施工simulation
- detailed terrainのView rendering / mesh LOD / shader

Phase29の責務は、これらを後から追加できるauthoritative自然環境・3D terrain境界を確立することにある。
