# Performance Guidelines

MachiVerseWorks の性能評価・最適化の共通ルールを定めます。

性能はこのプロジェクトの重要な要件ですが、推測による複雑化を避け、再現可能な計測結果を基準に判断します。

## 1. 原則

- 最適化前に baseline を取得する。
- 症状ではなく bottleneck を計測する。
- 平均値だけでなく percentile と worst case を見る。
- throughput と latency を区別する。
- Server、Simulation、Protocol、Web Client のどこで時間を使っているか分けて測る。
- 速くなった代わりに正しさ、再現性、保守性を失っていないか確認する。

## 2. 初期 PoC の基準

初期実装では次を参考値とします。これは永久的な仕様や保証値ではありません。

- Simulation target: 30 ticks/sec を基準に評価する。
- 30 Hz の 1 tick budget: 約 33.3 ms。
- Snapshot publish: 20 Hz 前後を初期検証値とする。
- Client rendering: Simulation tick と独立して表示 refresh rate に追従する。

Agent 数や都市規模の正式目標は、PoC benchmark を取得してから決めます。

## 3. Simulation metrics

最低限追跡する候補:

- tick duration average
- tick duration p50 / p95 / p99
- maximum tick duration
- ticks/sec
- active Agent count
- total Agent count
- vehicles / pedestrians / transit entities
- processed entities/sec
- routing requests/sec
- allocations/tick
- allocated bytes/sec
- Gen0 / Gen1 / Gen2 GC count
- GC pause time
- working set / managed heap size

性能変更では、変更対象に関係する指標を比較します。

## 4. Server / Protocol metrics

- snapshot build time
- serialization time
- entities per snapshot
- snapshot bytes
- bytes/sec per client
- command queue depth
- outgoing queue depth
- dropped/coalesced update count
- active connection count
- send latency
- interest area entity count

Protocol を圧縮した結果 CPU cost が増える場合など、bandwidth と CPU の両方を比較します。

## 5. Web Client metrics

- frame time average / p95
- FPS
- main-thread long task
- visible entity count
- draw calls
- triangles / instances
- snapshot decode time
- interpolation/update time
- rendering time
- JS heap size / allocation rate

FPS だけで改善を判断しない。Simulation data が欠落したり、視覚的な更新頻度を下げただけでは性能改善と扱わない。

## 6. Benchmark の記録条件

benchmark 結果には可能な範囲で次を残す。

```text
commit:
branch:
OS:
CPU:
RAM:
.NET SDK/runtime:
Node/browser:
configuration: Release / Production
seed:
world size:
agent count:
vehicle count:
simulation speed:
warmup duration:
measurement duration:
```

比較時は同じ条件を使用する。

## 7. Warmup

JIT、cache、world initialization の影響を benchmark 本体から分離する。

- 起動直後だけを測らない。
- .NET の tiered compilation / JIT warmup を考慮する。
- world generation と steady-state simulation を必要に応じて別 benchmark にする。
- Browser は shader compile や resource upload の初回 cost を区別する。

## 8. Benchmark の種類

### Microbenchmark

小さな algorithm / data layout / serialization 処理の比較に使用する。

将来的には `BenchmarkDotNet` を第一候補とする。

Microbenchmark の結果だけで全体性能改善を断定しない。

### Simulation benchmark

固定 seed / population / duration で Simulation loop を動かす。

主に tick time、allocation、throughput を測る。

### End-to-end benchmark

Server + Protocol + Web Client を接続して測る。

主に snapshot、network、decode、render の全体挙動を確認する。

## 9. Profiling

最適化前に profiler を使える場合は使用する。

候補:

- `dotnet-counters`
- `dotnet-trace`
- `dotnet-gcdump`
- PerfView / Visual Studio Profiler
- browser Performance / Memory profiler

profile から hot path が特定できていない状態で、広範囲な low-level optimization を行わない。

## 10. Allocation

allocation はゼロであること自体を目的にしない。

優先して調べる:

1. 毎 tick × Agent 数で増える allocation
2. 大きな temporary buffer
3. full snapshot ごとの object graph
4. logging / formatting allocation
5. network serialization buffer

必要に応じて array reuse、pooling、SoA、struct を検討する。

## 11. Full scan

大規模都市では全件走査が支配的になる可能性がある。

特に次を監視する。

- 全 Agent scan
- 全 Vehicle scan
- 全 Building / POI scan
- renderer の全 entity synchronization
- interest area 外まで含む snapshot build

ただし entity 数が小さい段階で複雑な index を導入せず、threshold を計測して判断する。

## 12. Parallelism

並列化では wall-clock time だけでなく以下も確認する。

- CPU utilization
- synchronization cost
- false sharing
- load imbalance
- job count
- contention
- deterministic result

logical core 数へ固定した worker 数をコードへハードコードしない。

小さすぎる job を大量生成しない。chunk/range size は benchmark で決める。

## 13. Regression

性能改善 PR には、可能なら before / after を記載する。

例:

```text
Scenario: 100k agents, seed 1234, 30 Hz target

Before:
  tick p50: 28.1 ms
  tick p95: 41.7 ms
  allocation: 12.4 MB/tick

After:
  tick p50: 20.8 ms
  tick p95: 27.3 ms
  allocation: 3.1 MB/tick
```

数値がない場合は「性能改善済み」と断定せず、構造改善または性能改善候補として扱う。

## 14. CI benchmark

通常 CI では短時間で安定して再現できる benchmark だけを gating 対象にする。

- shared GitHub runner の絶対時間だけで厳しい threshold を設定しない。
- correctness-oriented stress test と performance benchmark を区別する。
- 長時間 benchmark は manual / scheduled workflow を将来検討する。

## 15. Optimization acceptance

複雑な最適化を採用する場合、最低限次を説明できること。

- 何が bottleneck だったか
- どの metric が改善したか
- どの scenario で計測したか
- memory / latency / bandwidth など別の cost は増えていないか
- 正常系・境界条件を壊していないか
- rollback や比較が可能か

## 16. Performance debt

暫定 implementation を採用した場合は、曖昧な TODO だけで残さず、必要なら Issue または ROADMAP へ次を残す。

- 現在の制約
- 想定される scale limit
- 再評価する条件
- 取得すべき metric
