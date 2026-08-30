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

GitHub Actionsでは`.github/workflows/phase12-routing-benchmark.yml`を正規入口とし、BenchmarkDotNet artifactを`phase12-routing-benchmark`として保存する。

## Baseline

実測baselineはPhase 12実装branchのGitHub Actions成功runを正本として、この文書へrun IDと主要値を追記する。
