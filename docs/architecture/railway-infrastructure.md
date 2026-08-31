# Railway Infrastructure Architecture

## Boundary

Railway Infrastructureはauthoritative Simulation stateで、Track topology / Station / Platform / Depotを所有する。Train運行stateはRailway Operationsへ分離する。

```text
SimulationWorld / RailwayInfrastructureStore
  -> SimulationCheckpoint / Save Format 8+
  -> SimulationRuntime / RailwayInfrastructureReadModel
  -> Protocol 2.6 message 700
  -> Web RailwayInfrastructureLayer
```

## Simulation ownership

`SimulationWorld`がpublic command / snapshot API、`Internal/RailwayInfrastructureStore`がdictionary、stable-ID counter、topology invariant、spatial filter、checkpoint projectionを所有する。

geometryからconnectivityを推論せず、`TrackConnection`だけをtraversable edgeとする。Platform pedestrian integrationは`PlatformAccessPoint -> RoadAccessPoint` referenceで行う。

RoadAccessPoint lifecycleはcross-domain guardを持つ。PlatformAccessPoint参照中はRoadAccessPoint削除と`Foot` flag除去を拒否し、validな位置/endpoint更新はPedestrian Network invalidate後に許可する。

BlockSection / Depot membershipは各100,000件hard limit。public mutation / checkpoint restoreで同じ上限を適用する。

## Connectivity diagnostic

`ValidateConnectivity()`はdirected TrackConnectionを**undirected adjacency**へ追加してTrackSegmentのweak component数を数える。`TraversableConnectionCount`は元のdirected connection record数。

これはhealth/topology diagnosticであり、direction-aware route reachabilityを意味しない。Route constructionはRailway Operations側でdirected connection / TrackDirectionを再検証する。

## Persistence boundary

Save Format 8がRailway Infrastructureを導入し、current Format 10でも同じentity / next-ID contractを保持する。Format 3〜7は空Railway stateへmigrationする。

BlockSection / Depot nested membershipはSaveのpre-materialization scannerでもboundedに検証し、restore後にProtocol配信不能な単一aggregateを導入しない。

## Server read model

`SimulationRuntime.CapturePublishSnapshot()`はworld lock下でRailway stateとrevisionをdetached read modelへcaptureする。Client別3D filteringはcapture後に行う。

connectionはlast sent railway revisionとsubscription revisionを持つ。両方不変なら同じstatic topologyを毎tickserializeしない。

## Protocol chunk boundary

message 700のpayload headerはrevision + `isFullSnapshot` + 8 collection counts。`RailwayInfrastructureProtocolChunker`は1 MiB上限を超えるaggregateをentity境界でsplitする。

ChunkBuilderはNode → Segment → Connection → Block → Station → Platform → PlatformAccessPoint → Depotの順にitemを詰める。source snapshotがfullなら最初のemitted chunkだけfull=true、それ以降はfalse。全chunkは同revision。

単一Block / Depot itemはsplitしない。Simulationの100,000件membership limitにより正当stateの可変item単体overflowを防ぐ。

## Web assembly boundary

`RailwayInfrastructureLayer.apply()`の契約:

- full=true: 保持中mapをresetし、そのframe revisionをcurrentに設定
- full=false: current revisionと一致しなければignore
- 同revision continuation: Node / Segment / Station / Platform mapへaccumulate
- 各apply後、現在揃っているmapからgeometryを再構築

full=trueはrevisionが既に同じでも必ずresetする。これが「subscription変更のみ・topology revision不変」のfiltered snapshot切替を成立させる。

Protocolにはchunk index / total / final markerがない。WebSocket frame orderingとServerのordered splitをtransport contractとして使う。

## Walking access selection

`FindWalkingRouteToPlatform`はPlatformAccessPoint stable ID昇順でaccessを評価し、各RoadAccessPointのPOI→Building候補から最短walking routeを選ぶ。exact-length tieは低いPlatformAccessPoint IDを優先し、同access内の完全tieは候補列挙順のPOIが維持される。

## Verification / performance

- Simulation: explicit topology / weak component diagnostic / access guards / checkpoint
- Persistence: Format 8+ / bounded membership
- Protocol: 2.6 codec / chunking / 1 MiB boundary
- Web: full reset / continuation accumulation / revision mismatch ignore
- E2E: `save-server-browser-railway`
- Benchmark: `railway-10k-100k`

workflowは`.github/workflows/e2e.yml`と`.github/workflows/benchmarks.yml`へ集約済み。Railway Infrastructure benchmarkのscenarioとreference baselineは[`../development/railway-infrastructure-benchmark.md`](../development/railway-infrastructure-benchmark.md)に記録する。
