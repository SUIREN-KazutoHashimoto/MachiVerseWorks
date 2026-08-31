# Logistics / Freight Benchmark Baseline

Phase 22 の Logistics / Freight に対する performance regression baseline を記録する。

この値は性能保証ではなく、同一 benchmark fixture / runner 世代で将来の回帰を発見するための基準値として扱う。

## 対象

`LogisticsBenchmarks` は `InventoryCount = 100 / 1,000` を使い、次を測定する。

- `Tick`: Inventory と同数の完了 Shipment 履歴を保持した定常 Simulation の fixed tick。完了 Freight Vehicle は resident Road Traffic state に残さない。
- `RoutingBatch`: InventoryCount 回の Road Routing request。
- `Snapshot`: authoritative Commodity / Inventory / Order / Shipment history を含む Logistics snapshot の生成と managed allocation。

Tick fixture は大量 Shipment を単一 Lane へ同時 spawn する workload にはしない。大量同時 dispatch は Road Traffic の安全間隔 validation に依存して測定が成立しないため、Shipment history / Inventory に対する定常 tick と route query を分離して測定する。

## 計測環境

- GitHub Actions Benchmarks run: `33450666769`
- Job: `logistics-inventory-100-1000`
- BenchmarkDotNet: `0.15.8`
- Job: `ShortRun` (`IterationCount=3`, `LaunchCount=1`, `WarmupCount=3`)
- OS: Ubuntu 24.04.4 LTS
- CPU: AMD EPYC 9V74 2.87GHz, 2 physical / 4 logical cores
- .NET SDK: 10.0.400
- Runtime: .NET 10.0.11, X64 RyuJIT x86-64-v3
- GC: Concurrent Workstation

## Baseline

| Method | Inventory / Shipment count | Mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| Tick | 100 | 1.264 us | 0.2740 us | 0.0150 us | 693 B |
| RoutingBatch | 100 | 3.835 us | 0.0761 us | 0.0042 us | 0 B |
| Snapshot | 100 | 11.794 us | 2.4786 us | 0.1359 us | 36,992 B |
| Tick | 1,000 | 6.222 us | 0.4112 us | 0.0225 us | 837 B |
| RoutingBatch | 1,000 | 38.602 us | 0.9454 us | 0.0518 us | 0 B |
| Snapshot | 1,000 | 211.310 us | 8.0151 us | 0.4393 us | 350,238 B |

## Interpretation

- Tick は 100 -> 1,000件で約4.9倍になり、完了 Shipment history と Inventory を持つ長期稼働状態でもこの baseline では sub-millisecond を維持した。
- RoutingBatch は request 数にほぼ比例して増加した。
- Snapshot は全 authoritative history を materialize するため、1,000件で約350KB / operation の managed allocation が発生する。Protocol / Server の debug entry は別途256件へ bounded しているが、Simulation snapshot 自体は全履歴を返す契約である。
- Freight Vehicle は到着後に `VehicleStore` から解放するため、配送累計数が Road Traffic resident vehicle count を直接増やし続けない。

将来 Shipment history の保持方式や snapshot contract を変更する場合は、この baseline とメモリ傾向を比較し、改善・回帰を明示する。
