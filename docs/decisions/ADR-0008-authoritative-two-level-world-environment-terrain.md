# ADR-0008: World EnvironmentをGlobal FieldとDetailed 3D Terrainの二層でSimulation正本化する

- Status: Accepted
- Date: 2026-09-02

## Context

都市の時間発展、複数都市・街・村・集落、道路・鉄道・建物配置を自然地形へ従わせるには、海・大陸・島・山地・平野・盆地・河川・湖・気候といった広域環境と、局所的な地表勾配・法線・地下空間を同じworld座標上で扱う必要がある。

一方で、world全域を道路設計に必要なresolutionで常時materializeするとmemory/生成costが大きく、逆に広域fieldだけをterrain正本にすると都市detailが不足する。

また、View cameraや描画LODに合わせて地形を生成すると、Simulation結果がobserver依存になり、Headless Server、Save復元、複数client間で同じworldを保証できない。

## Decision

World Environment / Terrainの正本を`MachiVerseWorks.Simulation`に置き、次の二層に分ける。

1. **Global Environment Field**
   - seed/config/座標からstatelessかつdeterministicに評価する
   - Ocean / Continent / Island、広域標高、気候、水系、settlement suitabilityを担当する
   - world全域を高密度gridとして保存しない

2. **Detailed 3D Terrain**
   - 必要regionをlazy partitionとして評価する
   - height / normal / slope / roughness / materialを提供する
   - `TerrainVolume`でAir / Water / Soil / Rock / Voidをquery可能にする
   - 同一XYの複数surfaceを許容する

`GeographicFeature`と`NaturalToponym`はprocedural fieldから検出されるstable entityとしてSimulationが所有する。自然地名はprovenanceを必須とする。

Terrain partition cacheはSaveしない。`WorldEnvironmentConfig`、materialize済みfeature、toponymをSaveし、cacheはseed/configから再生成する。

ObservationではSimulation snapshotをServerがProtocolへ写像する。View / Observation Gatewayは地形・feature・地名を再生成しない。

## Consequences

### Positive

- Headless ServerとViewで自然環境の正本が一つになる
- camera/LOD非依存のdeterminismを維持できる
- 広大なworldを高密度terrain gridとして常駐させずに済む
- Road / Railway / Buildingが共通terrain constraint APIを利用できる
- heightfieldの高速性を維持しつつ、地下・cave・overhang拡張余地を確保できる
- feature stable IDによりSave、Protocol、地名、将来の観光/行政/route参照を統一できる

### Negative

- GlobalとDetailedの2つのresolution/caching戦略を保守する必要がある
- 完全なmesh/voxel terrainを最初から持つ方式よりquery APIの抽象化が増える
- procedural generatorのsemantic変更は同seed worldの再現性へ影響するため、慎重なversioningが必要になる

## Alternatives considered

### View側で必要範囲だけterrain生成

却下。cameraやclient実装がSimulation stateへ影響し、Headless / multiplayer / Save再現性を壊す。

### World全域を単一の高解像度heightfieldとして保存

却下。広大worldでmemoryとSave sizeが過大になり、地下・複数surfaceも表現できない。

### Global fieldだけをそのまま都市detailとして使用

却下。道路・鉄道・建物constraintに必要な局所勾配/normal/roughnessを十分に表現できない。

### 最初からfull voxel / mesh terrainをauthoritativeにする

現Phaseでは採用しない。複雑性とmemory costが高く、Phase29の目的である自然環境baselineと共通query境界を越える。`TerrainVolume` contractを先に固定し、必要になった時点で内部表現を高度化できるようにする。

## Follow-up

- detailed terrain rendering / LOD / shaderはView roadmapで実装する
- erosion / sediment / flood / cut-and-fillは後続Simulation Phaseへ送る
- procedural generatorの互換性が必要になった場合、generator versionをSave provenanceへ明示する
