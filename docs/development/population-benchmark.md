# Phase 15 Population Benchmark

Phase 15 Population & Daily Activity のplanner / fixed tick / managed memory baselineを記録する。

## 対象

`PopulationBenchmarkRunner`で次のPerson数を同一scenarioで計測する。

- 1,000 Person / 250 Household
- 10,000 Person / 2,500 Household
- 100,000 Person / 25,000 Household

各Householdは4 Person、全Personは`Home` activity windowを1日中持つ。交通entity生成やroute searchの変動を混ぜず、Population Store走査、Need更新、daily plannerの基礎コストを観測する。

交通を伴うPopulation Trip Request -> Pedestrian / Vehicle -> arrivalはSimulation integration testで別に検証する。

## 実行条件

GitHub Actions `Phase 15 Population Benchmark` run `33301355867`。

- runner OS: Ubuntu 24.04
- runner image: `ubuntu-24.04`
- .NET SDK: repository `global.json`、run時 10.0.400
- Simulation tick rate: 30 Hz
- warmup: 10 ticks
- measurement: 50 ticks
- seed: 15015
- spatial cell size: 64 m

実行コマンド:

```bash
dotnet run \
  --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj \
  --configuration Release \
  -- --population --warmup 10 --ticks 50
```

## Baseline result

| Person | Household | Average | p50 | p95 | p99 | Max | Allocated / tick | Managed bytes |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 | 250 | 0.2332 ms | 0.2260 ms | 0.2782 ms | 0.2984 ms | 0.2984 ms | 64 B | 639,568 B |
| 10,000 | 2,500 | 0.5294 ms | 0.5325 ms | 0.5846 ms | 0.6388 ms | 0.6388 ms | 64 B | 5,579,016 B |
| 100,000 | 25,000 | 3.1623 ms | 2.9659 ms | 4.1998 ms | 6.1704 ms | 6.1704 ms | 64 B | 54,000,200 B |

30 Hzのtick budgetは約33.33 ms。100,000 Personのbaselineは平均で約9.5%、p99で約18.5%を使用した。

managed memoryは100,000 Personで約54.0 MB、単純換算で約540 bytes / Person。ただしこの値にはHousehold、Dictionary / List capacity、SimulationWorld基礎stateなども含むためPerson object単体のサイズではない。

## 判定

Phase 15 baselineとして次を確認した。

- 1,000 / 10,000 / 100,000 Personの3段階で計測できる。
- 100,000 Personでもplanner / tick traversalは30 Hz budget内に十分収まる。
- measurement loopのmanaged allocationは64 B / tickでPerson数に比例して増えていない。
- managed memoryはPerson数にほぼ比例して増加しており、100,000 Person時でもPhase 15の観測用途として扱える範囲にある。

## 注意点

このbenchmarkはPopulation plannerの基礎baselineであり、100,000 Personが同時にTripを開始するrush-hour workloadを表さない。Road Routing、Vehicle、Pedestrian、Protocol publishはそれぞれ既存benchmarkまたは後続の統合benchmarkで評価する。

Population側では全Person詳細を毎publishで複製せず、Protocol 2.5の`PopulationStatistics`を固定長集計として配信し、個別`PersonDebug`はID指定時だけ生成する。
