# Phase 15 Population Benchmark

Phase 15 Population & Daily Activity のplanner / fixed tick / managed memory baselineと、Population trip dispatch hot pathの継続回帰scenarioを記録する。

## 対象scenario

`PopulationBenchmarkRunner`はCSVの`scenario`列で次を区別する。

| Scenario | Person | 目的 |
| --- | ---: | --- |
| `idle` | 1,000 / 10,000 / 100,000 | Population Store走査、Need更新、daily plannerの基礎baseline |
| `foot-dispatch` | 1,000 / 10,000 | Population Trip Request → walking route → Pedestrian生成と継続歩行 |
| `motor-dispatch` | 1,000 / 10,000 | Population Trip Request → Motor access解決 / routing → Vehicle生成と継続走行 |

`idle`は各Household 4 Person、全Personが`Home` activity windowを1日中持つ従来baselineを維持する。交通entity生成やroute searchを混ぜないため、historical Phase 15 baselineとの比較に使う。

`foot-dispatch` / `motor-dispatch`はInfrastructureだけをwarmupした後でPersonを追加し、最初のmeasurement tickで対象Personを一斉にactivity transitionさせる。dispatch fixtureでは各Personを1 Householdに分離し、同一Lane上へ12 m間隔の固有RoadAccessPointを与える。これによりMotor scenarioでspawn位置競合によるwalking fallbackをbenchmark結果へ混入させない。

回帰条件として、`foot-dispatch`はmeasurement中の最大active Pedestrian数がPerson数と一致すること、`motor-dispatch`は最大active Vehicle数がPerson数と一致し、active Pedestrianが0であることを必須とする。1件でもMotor dispatchがwalkingへfallbackした場合はbenchmark自体を失敗させる。

100,000 Person同時dispatchはCIの通常回帰には含めない。大規模rush-hour workloadは専用負荷試験として扱い、標準benchmark jobの時間・メモリbudgetを不必要に膨らませない。

## 実行条件

GitHub Actions `.github/workflows/benchmarks.yml` の`population-1k-10k-100k` scenario jobから実行する。

- runner OS: Ubuntu 24.04
- .NET SDK: repository `global.json`
- Simulation tick rate: 30 Hz
- default CI warmup: 10 ticks
- default CI measurement: 50 ticks
- seed: 15015
- spatial cell size: 64 m

実行コマンド:

```bash
dotnet run \
  --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj \
  --configuration Release \
  -- --population --warmup 10 --ticks 50
```

CSV列:

```text
scenario,persons,households,ticks,average_ms,p50_ms,p95_ms,p99_ms,max_ms,allocated_bytes_per_tick,managed_bytes,max_active_pedestrians,max_active_vehicles
```

## Historical idle baseline

以下は従来のHome-only `idle` scenarioをGitHub Actions run `33301355867`で計測したhistorical baseline。dispatch scenario導入後も、この表は過去測定値として書き換えず比較基準に残す。

| Person | Household | Average | p50 | p95 | p99 | Max | Allocated / tick | Managed bytes |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 | 250 | 0.2332 ms | 0.2260 ms | 0.2782 ms | 0.2984 ms | 0.2984 ms | 64 B | 639,568 B |
| 10,000 | 2,500 | 0.5294 ms | 0.5325 ms | 0.5846 ms | 0.6388 ms | 0.6388 ms | 64 B | 5,579,016 B |
| 100,000 | 25,000 | 3.1623 ms | 2.9659 ms | 4.1998 ms | 6.1704 ms | 6.1704 ms | 64 B | 54,000,200 B |

30 Hzのtick budgetは約33.33 ms。historical 100,000 Person idle baselineは平均で約9.5%、p99で約18.5%を使用した。

managed memoryは100,000 Personで約54.0 MB、単純換算で約540 bytes / Person。ただしこの値にはHousehold、Dictionary / List capacity、SimulationWorld基礎stateなども含むためPerson object単体のサイズではない。

## Regression interpretation

- `idle`はplanner / Need / store traversalの基礎性能を監視する。
- `foot-dispatch`はendpoint候補解決、walking route、Pedestrian生成、Pedestrian tickの統合costを監視する。
- `motor-dispatch`はMotor access index、candidate pair評価、Road route、Vehicle生成、Vehicle tickの統合costを監視する。
- measurement最初のtickに一斉dispatchが含まれるため、`max_ms` / p99はdispatch spikeの退行検知に特に重要。
- scenario間の絶対値を同一workloadとして比較しない。それぞれ別のperformance contractである。

Population publishはこのSimulation benchmarkへ含めない。Server publishは`PopulationStatistics`固定長集計とID指定`PersonDebug`のServer/Protocol tests、およびPopulation Browser E2Eで別途検証する。
