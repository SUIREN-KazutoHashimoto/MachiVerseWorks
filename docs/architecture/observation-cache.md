# Observation Cache Architecture

Gateway Phase 2の共有Observation cacheは、Simulationが公開したdetached observationを複数Clientで再利用するための最適化層である。cacheはSimulation state、domain rule、semantic stateを所有・生成しない。

## Authoritative marker

cacheのcorrectnessはwall-clock TTLではなく、Simulation側のauthoritative markerで決める。

- `ObservationGeneration`: World replacementごとに増加する。異なるgenerationのentryは共有しない。
- `ObservationRevision`: Simulation tick、authoritative mutation、fixture適用で増加する。停止中のmutationでもtickに依存せずstale判定できる。
- topology revision: Road / Railway等、tickをまたいで再利用できるstatic read modelに使用する。
- negotiated `ProtocolVersion`: encoded payload cacheのkeyに含め、異なるwire contract間でframeを共有しない。

`SimulationRuntime`はgeneration / observation revisionをsnapshotと同じlock内で取得する。World replacementでtickが巻き戻ってもgenerationが異なるため、旧Worldのentryをhitさせない。

## Cache layer

`ObservationCache`はGateway-owned singletonとして`AddObservationGateway()`から登録する。

### Entity Observation Cache

`EntityObservationCacheKey`はEntity kind、Entity ID、`ObservationRevision`で構成する。Phase 2ではPerson inspectionのprotocol read modelを共有する。

### Spatial Observation Cache

`SpatialObservationCacheKey`はObservation kind、`WorldVolume`、`ObservationRevision`で構成する。Phase 2ではdynamic Entity queryとWorld Environment mappingを共有する。

同一revision・同一volumeを複数Viewerが要求した場合、`SimulationPublishSnapshot.QueryEntities()`の結果を共有する。connection-localなknown-ID stateはcacheへ入れず、Spawn / Update / Remove planningは従来どおりconnectionごとに行う。

### Static Revision Cache

`StaticObservationCacheKey`はstatic kind、`WorldVolume`、generation + topology revisionで構成する。Road / Railwayのspatial read modelをtickをまたいで共有する。

static dataでもwire payloadへcurrent tickを含む場合は、read modelだけをtopology revisionで再利用し、encoded frameはdynamic observation revisionで分離する。Road snapshotはこの扱いである。Railway infrastructure frameはtickを含まないためtopology revision単位で再利用できる。

### In-flight deduplication

各entryは`ConcurrentDictionary<TKey, Lazy<T>>`相当のsingle-flight構造で生成する。同一keyが同時にmissしてもfactoryを1回だけ実行し、他requestは同じresultを共有する。

新しいgenerationが観測された後に古いgenerationのdeliveryが遅れて到着した場合、そのrequestはshared cacheをbypassする。旧requestによってcurrent generationを巻き戻さない。

### Encoded Payload Cache

`EncodedObservationCacheKey`はpayload kind、negotiated `ProtocolVersion`、authoritative revision、payload identityで構成する。

Phase 2では次を安全な共有対象とする。

- Road snapshot frame
- Railway infrastructure chunk frame
- Intersection control frame
- Railway operations frame
- Multimodal transit frame
- Population statistics frame
- Person inspection frame
- World Environment frame

Agent / Pedestrian / VehicleのSpawn / Update / Removeはconnection-local known-ID stateでmessage typeが変わるため、Phase 2では共有encoded cacheへ入れない。

## Evictionとmemory budget

デフォルト上限:

- Entity entries: 32,768
- Spatial entries: 4,096
- Static entries: 2,048
- Encoded entries: 8,192
- Encoded bytes: 64 MiB
- retained dynamic revisions: currentを含む直近3 revision

新しいdynamic revisionではretention windowより古いEntity / Spatial / dynamic encoded entryを削除する。generation変更では全cache namespaceを破棄する。Static entryはtick進行だけでは破棄せず、generation / topology revision keyで分離する。

上限超過時は古いentryからbounded evictionする。memory budgetはcorrectness条件ではなくresource boundであり、eviction後は同じauthoritative sourceから再構築する。

## Metrics / benchmark

`ObservationCacheMetrics`は次を取得できる。

- hit / miss
- build count
- encoding count
- eviction count
- encoded bytes
- cache layer別entry数

`ObservationCacheBenchmarks`は同一World rangeを16 / 64 Viewerが読むspatial workloadと、同一Protocol payloadを16 / 64 Viewerへ送るencoding workloadについてcache disabled / enabledを比較し、BenchmarkDotNetでCPU時間とallocationを記録する。

`ObservationCacheBenchmarkRunner`は同じworkloadについてhit rate、build count、encoding count、encoded bytes、64 MiB memory budgetをCSVへ記録する。PRでは`.github/workflows/gateway-observation-cache-benchmark.yml`がartifact `benchmark-gateway-observation-cache`を保存する。

## Regression contract

`ObservationCacheTests`で少なくとも次を固定する。

- cache disabled / miss / hitでpayload semanticsが一致する。
- same revision / same requestの並列factory実行が1回にdeduplicateされる。
- dynamic revision変更で再構築され、tick相当のrevision進行だけではStatic cacheを失わない。
- World generation変更で旧entryを共有せず、遅延した旧generation requestがcache generationを巻き戻さない。
- encoded payloadはdirect encodeとbyte-equivalentで、Protocol versionごとに分離される。
- encoded cacheが設定memory budgetを超えて成長し続けない。

## Non-goals

Phase 2 cacheは次を行わない。

- Simulation semantic stateの推測・再計算
- View接続数やCamera位置によるSimulation fidelity変更
- connection-local delivery stateの共有
- wall-clock TTLをauthoritative freshnessとして使用
- historical observation namespaceの実装

reconnect / resync / committed delivery revisionの統一はGateway Phase 3、historical namespaceはGateway Phase 5で扱う。
