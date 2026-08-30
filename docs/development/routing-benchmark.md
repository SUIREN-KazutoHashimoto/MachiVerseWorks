# Phase 12 Routing Benchmark

## 目的

Phase 12 Routing Foundationの探索時間、allocation、route cache hit時のcostをsmall / medium / large graphで継続観測する。

対象は`RoutingBenchmarks`とし、Road / Lane topologyを直線状のdirected graphとして構築する。

## Graph size

| 区分 | Lane数 |
| --- | ---: |
| small | 100 |
| medium | 10,000 |
| large | 100,000 |

各Laneは10m程度のRoadSegmentへ対応し、隣接Lane間を明示`LaneConnection`で接続する。Z座標にも小さな変化を与え、3D distance計算を含める。

## Benchmark case

### CachedRoute

同一`RouteRequest`を事前に1回解決し、LRU cache hitする経路探索を計測する。

このcaseではrouting graph rebuild、endpoint resolve、Dijkstraを行わない。

### SearchCacheMiss

起点座標のbit patternを呼び出しごとに微小に変え、同じtopology上でroute cache missを発生させる。

このcaseでは次を含む。

- nearest Lane 3D resolve
- Dijkstra search
- immutable `RouteResult` materialization
- LRU cache insert / eviction

## 実行方法

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj \
  --configuration Release -- \
  --filter '*RoutingBenchmarks*' --job short
```

GitHub Actionsでは`.github/workflows/benchmarks.yml`の`routing-small-medium-large` jobを正規入口とし、BenchmarkDotNet artifactを`benchmark-routing`として保存する。

## Phase 12 baseline

Phase 12実装commit `5527d220f05544732700200867e142a5d692e870`をGitHub Actions run `33290004788`で計測した。

- Runner: Ubuntu 24.04.4 LTS / `ubuntu-24.04`
- CPU: Intel Xeon Platinum 8370C 2.80GHz、2 physical / 4 logical cores
- .NET SDK: 10.0.400
- Runtime: .NET 10.0.11 x64 RyuJIT
- BenchmarkDotNet: 0.15.8
- Job: ShortRun、LaunchCount=1、WarmupCount=3、IterationCount=3

| Method | Lane数 | Mean | Allocated / op |
| --- | ---: | ---: | ---: |
| CachedRoute | 100 | 42.30 ns | 0 B |
| SearchCacheMiss | 100 | 17.05 us | 49,866 B |
| CachedRoute | 10,000 | 42.72 ns | 0 B |
| SearchCacheMiss | 10,000 | 1.479 ms | 4,792,352 B |
| CachedRoute | 100,000 | 40.41 ns | 0 B |
| SearchCacheMiss | 100,000 | 15.363 ms | 43,953,980 B |

### 観測

cache hitはgraph sizeに依存せず約40〜43ns、managed allocation 0 B/opだった。cache key lookupだけで返す設計意図どおり、100,000 Laneでもendpoint scanとDijkstraを再実行していない。

cache missは100 → 10,000 → 100,000 Laneで17.05us → 1.479ms → 15.363msと増加し、100,000 Laneでも単一route探索は約15.4msだった。一方でallocationは100,000 Laneで約43.95MB/opとなるため、Phase 12のcorrectness baselineとしては許容するが、交通Simulationから高頻度にcache missを発生させる前にsearch workspace pooling、nearest-Lane spatial index、A* / hierarchical routingを優先的に検討する。

Route cacheは最大1,024 entriesに加え、保持するLane step総数を100,000へ制限する。large routeを多数cacheした場合の長寿命メモリ増幅はこのweighted LRU上限で抑制し、cache miss時の一時allocationは別課題として継続計測する。

## Closeout判定

- small / medium / largeの全6 benchmarkが成功した。
- 100,000 Lane graphでcache miss探索時間とallocationを実測した。
- cache hitは100,000 LaneでもO(1)相当のlookup pathを維持した。
- 初回benchmark artifactはrun `33290004788`の`phase12-routing-benchmark`として保存した。現行CIでは`benchmark-routing`を使用する。

以上をPhase 12の初回routing性能baselineとする。
