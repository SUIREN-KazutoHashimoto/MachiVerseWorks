# World Environment / Detailed 3D Terrain Architecture

## Authoritative boundary

World EnvironmentとDetailed Terrainの正本は`MachiVerseWorks.Simulation`に置く。ServerはSimulation snapshotをProtocolへ写像し、View / Observation Gatewayはread-only consumerとする。

```text
WorldEnvironmentConfig
        |
        v
WorldEnvironmentGenerator ----> RegionalEnvironmentSample
        |                              |
        |                              +--> SettlementCandidateRegion
        |
        +--> TerrainSurface ----------> TerrainSurfaceSample
        |       |
        |       +--> TerrainVolume ---> Air / Water / Soil / Rock / Void
        |       |
        |       +--> TerrainConstraintEvaluator
        |
        +--> GeographicFeature -------> NaturalToponym / provenance
                                         |
SimulationWorld ------------------------+
        |
        +--> Checkpoint / Save
        |
        +--> WorldEnvironmentSnapshot
                  |
                  v
        Server mapper / publisher
                  |
                  v
          Protocol 2.17 / msg 800
                  |
                  v
       Observation Gateway / View
```

## Global fieldとDetailed Terrainを分ける理由

広域気候・大陸性・海岸距離・主要水系は数百km級のscaleを扱う一方、道路・鉄道・建物が必要とするslopeやsurface normalはm〜km級のdetailを必要とする。同じresolutionで扱うと、広域worldを高密度gridとして常駐させるか、都市detailを粗くするかの二択になる。

そのためPhase29は次の2層を採用する。

1. `WorldEnvironmentGenerator`: 任意座標から広域fieldをdeterministicに評価するstateless基盤
2. `TerrainSurface` / `TerrainVolume`: 必要regionだけlazyにdetail queryできる局所基盤

両者は同じworld seedとnative 3D world座標を共有するが、cache/resolution/用途は分離する。

## Determinism

生成処理はOS乱数やprocess-local hashに依存しない。固定64-bit mixingと座標quantization、連続value noiseを使い、同じconfig/seed/coordinateから同じ結果を返す。

Terrain partitionはcache boundaryに過ぎず、partition IDを地形特徴そのもののseedには使わない。したがって同じ座標はどのquery経路から参照しても同じheightを返す。

Simulation tickは自然環境baselineへ影響しない。将来時間変化する環境を追加する場合も、静的地形とdynamic environmental stateを別契約にする。

## Global Environment query

`QueryEnvironment(WorldPoint)`は`RegionalEnvironmentSample`を返す。Z入力は地域検索のcontextとして受け取るが、返却Position.Zは生成された地表標高へ置き換える。

内部では以下を順に求める。

1. broad elevation / landform
2. world north投影によるlatitude
3. configured coastline distanceまたはdeterministic coastline estimate
4. climate
5. hydrology
6. ruggedness / buildability
7. settlement score

`ConfiguredCoastlineDistanceMeters`が存在する場合、海岸距離推定を完全にoverrideする。外部world設定とprocedural resultを曖昧に混ぜないためである。

## Settlement candidate selection

候補gridからOceanを除外し、各点を環境classへ分類する。最初に環境classごとのbest candidateを採用し、その後にweighted score順で残りを埋める。

これにより「最適地だけに全都市が集中する」選択を避けつつ、完全random placementにはしない。後続の都市生成Phaseはcandidate scoreを追加評価してよいが、自然環境の再計算を別実装してはならない。

## Terrain partition

`SimulationWorld.GetTerrainPartition`はXYを固定size partitionへ写像し、`TerrainSurface`と`TerrainVolume`をlazy生成する。

partition cacheはSaveしない。configから再構築できるderived dataであり、Save sizeとmigration負担を増やさないためである。

法線計算はpartition端でも隣接座標のunchecked deterministic sampleを利用する。partition境界に人工的なridge/normal discontinuityを作らない。

## Surface / Volume dual model

`TerrainSurface`はprimary ground surfaceを高速に取得するAPIである。一方、`TerrainVolume`は任意XYZのmatterを取得する。

この二重契約により、通常の道路・建物配置はsurface APIだけで高速に処理できる一方、地下・水中・cave・複数surfaceを必要とするdomainはvolume APIへ進める。

`GetSurfaces(x, y, minZ, maxZ)`は同一XYに複数intersectionを返す。Phase29 baselineではprimary ground、Ocean / Lake / River / Tributary / Floodplainのwater surface、deterministic cavity floor/ceilingを扱う。cavity radiusはdepthにより制限し、ceilingをprimary groundより下へ維持することでsurface queryとmatter queryを一致させる。

## Infrastructure constraint boundary

Road / Railway / BuildingはTerrain実装詳細へ直接依存せず、`TerrainConstraintEvaluator`を利用する。

返却値にはallow/denyだけでなくmaximum slope、elevation range、water/void intersection、reasonを含める。Water / Voidはfootprintの`MinZ..MaxZ`内のsurface intersectionとmatter sampleを使って判定するため、上空のbridge相当volumeと地表水を混同せず、深いbasement / tunnel相当volume内のcavityも検出できる。

これにより後続Phaseでcut-and-fillやbridge/tunnel costを追加するときも、呼び出し側APIを壊さずconstraint resultを拡張できる。

## GeographicFeature entity

Procedural fieldそのものと、人間が意味を付けて参照する自然地形entityを分ける。

`GeographicFeature`はstable IDを持つため、次の参照先になれる。

- 自然地名
- 都市/行政区域由来説明
- 観光・POI生成
- transport route planning
- Save / Protocol

feature detectionは対象volumeをdeterministic gridで走査し、environment sampleからtypeを判定する。Phase29ではbaseline detectionであり、GIS級の境界抽出精度を目標にしない。`Geometry`は構造比較し、別world / 別processで同一seed・config・volumeから再生成したfeatureを値として同一判定できる。

## Toponym provenance

名前は`GeographicFeature`の表示propertyに埋め込まず、`NaturalToponym`として分離する。provenanceを持たせることで、将来のrename、言語別名称、親地名継承、addon generator差し替えで「どこから来た名前か」を追跡できる。

checkpoint / Protocol validationはsource featureに加え、non-null / non-zeroのparent Toponym IDが同じcollection内に存在することも要求する。

## Save boundary

World Environmentは既存Save format 11の`EconomyCheckpoint` optional extensionへ格納する。これは既存Power / Water / Gas / Optical / Radioと同じ後方互換拡張方式である。

保存するのはconfigと、authoritative workflowで明示的にmaterializeされたfeature / toponymである。Terrain partition、regional query result、`SubscribeVolume`観測のために生成したfeature / toponym cacheは保存しない。read-only observationがcheckpoint stateを変更しないことをSave bytesのinvariance testで固定する。

旧Saveでは`worldEnvironment`がnullになるため、Simulation seedからdefault configを作る。新Saveではcheckpoint configを`SimulationConfig`構築時に復元し、保存済みfeature/toponymがあれば戻す。未materializeの自然feature/toponymは同じconfig/seed/volumeから再生成する。

`WorldSaveLimits`はfeature数、toponym数、featureごとのgeometry点数を個別に制限し、`WorldSaveSerializer.NestedLimits`がDTO materialization前のJSON scanでも同じ上限を適用する。

## Observation boundary

`CreateDetailedWorldEnvironmentSnapshot`は購読volume内を固定sample gridで観測し、global sampleと同じXYのdetailed terrain sampleを対応させる。snapshot生成はderived observationであり、checkpoint対象dictionaryへfeature / toponymを追加しない。

Serverはclientごとの`SubscribeVolume`を使ってsnapshotを作り、Protocol 2.17未満のclientへは送らない。同一publish cycle内で同一`WorldVolume`を持つclientは1つのsnapshot/messageを共有し、procedural generationを接続数ぶん繰り返さない。

Protocol payloadはcamelCase JSONを16-byte binary frame header内へ格納する。Phase29は可変geometry/textを多く含むため、binary固定長item化よりschema可読性を優先した。ただし1 MiB上限、sample/feature/geometry/text上限、finite/enum discriminant/ID/reference validationはcodecで必須とする。

## Failure / validation policy

- configのNaN/Infinity、無効latitude、zero environment seedはconstructorで拒否
- terrain queryのnon-finite coordinateは拒否
- checkpointのduplicate/zero stable ID、missing parent/source参照は拒否
- Protocolのoversized collection、未知discriminant、invalid geometry/referenceは拒否
- Saveのworld environment可変collectionはmaterialization前後でbounded validationする
- Server publish timeoutやWebSocket errorは既存connection policyに従う

## Performance policy

Global Environmentはworld全域をgrid materializationせず、point query主体にする。Detailed Terrainはpartition cacheを用いる。benchmarkでは両者を別々に測定し、large-world reference snapshotとdetail snapshotの生成costも記録する。

将来最適化でmemoization、chunking、multi-resolution cacheを追加してよいが、同じseed/config/coordinateのsemantic resultを変更する最適化はbreaking behaviorとして扱う。
