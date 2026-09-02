# ADR-0009: Regional GenerationをSimulation authoritative stateとして決定論的に生成する

- Status: Accepted
- Date: 2026-09-02

## Context

Phase29までにGlobal Environment、Detailed 3D Terrain、GeographicFeature、Natural ToponymをSimulationのauthoritative stateとして定義した。

Phase30では、その地理条件の上へSettlement、道路、District、Parcel、Building、POI、人口・雇用初期状態、人間活動由来の地名、道路標識を生成する必要がある。

ここで次の設計リスクがある。

- ViewやServerが独自に街を生成するとconsumerごとに都市状態が変わる
- generated planと既存Road / Building / Population / Economy runtime IDを同一視すると、履歴・Save・再生成の意味が壊れる
- process-localな乱数やcollection順に依存するとSave / fixture / benchmarkの再現性を失う
- 完成都市だけを直接配置すると、将来の時間発展で「どのSettlementがなぜ成長したか」を説明できない
- ProtocolやGatewayへ生成規則を入れるとSimulation state ownershipが崩れる

## Decision

Regional & Urban Generationのauthoritative stateと生成規則は`MachiVerseWorks.Simulation`が所有する。

同じWorld Environment seed、generation volume、quality preset、explicit overrideから同じRegional snapshotを生成する。

Phase30 domainにはSettlement、GrowthEvent、RegionalCorridor、District、Parcel、GeneratedBuilding、GeneratedPoi、HumanToponym、RoadSign等のstable generation IDを設ける。IDはseed、座標、親ID、domain saltから決定論的に導出する。

生成結果とlive Simulation storeは分離する。

- generated snapshotは歴史・配置計画・provenanceの正本
- materializationは初期Worldへ既存Road / Building / Population / Economy APIを通して展開する境界
- live RoadSegmentId等を生成履歴のIDとして再利用しない

生成pipelineはGenerate → Evaluate / Improve → Enrich → authoritative captureとし、EnrichではBlock、Parcel suitability、role district、development state、Road Context、structure naming、sign ruleを適用する。

Human ToponymはNatural Toponymまたは親Human Toponymへのprovenanceを保持する。

Regional stateはSimulation Checkpoint / versioned Save Dataへ含め、load前にbounded collection scan、その後にID・enum・参照・数値rangeを検証する。

Protocol 2.18のRegional Generation payloadとServer mapperはpassive projectionに限定する。subscription、cache、interest management、deliveryはObservation Gateway / Server側の別責務とする。

## Consequences

### Positive

- 同一seedから同一都市を再現できる
- fixture、Save round-trip、benchmarkの比較が安定する
- View / Gatewayが都市生成ロジックを持たない
- 地理feature → Settlement → District / Road / Station等の地名provenanceを追跡できる
- Phase31以降の時間発展が、初期historical growthとstable generation IDsを参照できる
- 既存Road / Population / Economy domainを再利用でき、二重のauthoritative storeを避けられる

### Negative

- generated planとmaterialized runtime stateの二種類のIDを扱う必要がある
- deterministic hash saltやgenerator ruleを変更すると同一seed結果が変わるため、変更は互換性影響として扱う必要がある
- 高品質presetでは初期生成costとallocationが増える
- generated snapshotをSaveへ含めるためSave Data sizeが増える

### Mitigation

- stable generation IDとruntime IDを型で分離する
- generator key / provenanceを保存する
- Draft / Standard / HighQuality presetを用意する
- collection / geometryをSave load前にbounded scanする
- deterministic fixtureとbenchmarkをCI / performance確認に利用する

## Alternatives considered

### Client / Viewでprocedural generationする

却下。read-only Viewの責務に反し、consumerごとのstate divergenceを招く。

### Server / Gatewayで都市生成する

却下。配送・cache境界がdomain state ownershipを持つことになり、Simulationのauthoritative modelを破る。

### 生成時に直接Road / Building runtime storeだけを作る

却下。初期都市の歴史、Settlement relation、Human Toponym provenance、生成品質評価を独立して保存・観測しにくくなる。

### 非決定論的な乱数で自然さを優先する

却下。自然さはseed付き決定論的noise / hashで表現し、再現性を優先する。
