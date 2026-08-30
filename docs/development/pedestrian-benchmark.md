# Pedestrian Benchmark

Phase 16の歩行者hot pathについて、1,000 / 10,000 Pedestrianでfixed-tick処理とwalking route探索を継続計測する。

## 対象

`PedestrianBenchmarks`は同一の長距離walkable segmentへ多数のPedestrianを配置し、次を計測する。

- `FixedTickWithOccupancy`: occupancy bin、crossing判定、route progress、position / velocity更新を含む`SimulationWorld.Step()`
- `FindWalkingRoute`: Building→Buildingのwalking route探索

距離を十分長くしてbenchmark中に先頭Pedestrianが到着しないようにし、到着後の空処理だけを測定しない。

## Scale

BenchmarkDotNet parameter:

- 1,000 Pedestrian
- 10,000 Pedestrian

occupancy constraintは`(PedestrianEdgeId, bin)`dictionaryを使用し、全組合せ距離比較を行わない。したがってfixed-tick側ではPedestrian数に対する概ね線形な増加を期待する。

## 実行

```bash
dotnet restore MachiVerseWorks.slnx
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release --no-restore -- --filter '*PedestrianBenchmarks*' --job short
```

CIでは`.github/workflows/phase16-benchmark.yml`が同じfilterを実行し、BenchmarkDotNet artifactを14日間保存する。

## 判定

Phase 16では固定の絶対ms閾値をportableなCI gateにはしない。hardware差を避けつつ、次をregression signalとして確認する。

- 10,000 Pedestrianでbenchmarkが完走すること。
- fixed-tick処理がall-pairs由来の急激な二次増加を示さないこと。
- allocationや処理時間が将来の変更で大幅に悪化した場合、BenchmarkDotNet結果を比較して原因を調査すること。
