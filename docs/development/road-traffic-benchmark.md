# Phase 13 Road Traffic Benchmark Baseline

Phase 13 の Vehicle tick / Lane occupancy / snapshot cost を継続計測するための基準を記録する。

## Runner

`RoadTrafficBenchmarkRunner` を次のコマンドで実行する。

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --road-traffic --warmup 5 --ticks 20
```

対象 Vehicle 数は 1,000 / 10,000 / 100,000。

各 scenario は 100 Vehicle / Lane に決定的に分散し、Road topology を作成した checkpoint に Vehicle checkpoint を設定して restore する。setup / restore cost は測定対象へ含めない。

測定項目:

- `tick`: `SimulationWorld.Step()` 1回の wall-clock latency と current-thread allocation
- `occupancy`: 全 Vehicle について `LaneOccupancyIndex.TryGetLeader()` を1回ずつ行う full sweep latency
- `snapshot`: `CreateAllVehicleSnapshots()` 1回の wall-clock latency と current-thread allocation

## Baseline

GitHub Actions `Phase 13 Road Traffic Benchmark` run `33303599957`、head commit `ba89e6f919264ae49a790906c8af6428c26f6651` で取得した closeout baseline。

環境:

- GitHub-hosted `ubuntu-latest`
- .NET SDK `10.0.400`（`global.json`）
- warmup 5 ticks / measurement 20 iterations
- 100 Vehicle / Lane

| Vehicles | Lanes | Tick avg | Tick p95 | Tick p99 | Tick alloc/op | Occupancy avg | Occupancy p95 | Occupancy p99 | Snapshot avg | Snapshot p95 | Snapshot p99 | Snapshot alloc/op | Managed bytes |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 | 10 | 15.5826 ms | 28.5986 ms | 35.3152 ms | 995,664 B | 0.2368 ms | 0.3984 ms | 0.6694 ms | 0.3411 ms | 0.9523 ms | 2.1630 ms | 160,024 B | 13,557,824 B |
| 10,000 | 100 | 14.6444 ms | 25.8920 ms | 27.9203 ms | 9,956,064 B | 1.4889 ms | 3.2750 ms | 4.1999 ms | 0.6818 ms | 1.2685 ms | 1.5923 ms | 1,600,024 B | 15,703,936 B |
| 100,000 | 1,000 | 155.8082 ms | 179.5764 ms | 185.2966 ms | 99,560,414.8 B | 13.2382 ms | 18.8324 ms | 37.1174 ms | 3.4865 ms | 4.7761 ms | 6.3101 ms | 16,000,024 B | 242,741,376 B |

## Interpretation

このbaselineでは、100,000 Vehicleのfull tick平均は155.8082msであり、30Hzの約33.33ms/tick budgetを超える。したがってPhase 13 closeout時点で「100,000 Vehicleを30Hz realtimeで処理できる」という性能保証は行わない。

一方、Phase 13の完了条件は大規模Vehicle数の基準性能を計測・記録し、回帰を追跡可能にすることである。100,000 Vehicleまで同一runnerで完走し、tick / occupancy / snapshotを分離して数値化できたため、その条件は満たす。

特にtick allocationはVehicle数にほぼ比例して増加しており、100,000 Vehicleで約99.6MB/opとなる。今後のRoad Traffic性能改善では、Vehicle tick中の一時allocation削減と100,000 Vehicle caseのtick latencyを優先的な観測対象とする。

GitHub-hosted runner は hardware 共有条件が変化し得るため、単一 run の微小差を性能回帰と断定しない。Vehicle 数に対する scaling curve、桁違いの latency 悪化、allocation の構造的増加を中心に比較する。

## Automation

`.github/workflows/benchmarks.yml`の`vehicles-1k-10k-100k` jobがRoad Traffic runnerを実行し、`benchmark-road-traffic` CSV artifactを14日保持する。
