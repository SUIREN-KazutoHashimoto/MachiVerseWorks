# Water & Sewer Infrastructure Benchmark

## 目的

Phase 24の標準capacity solverについて、大規模Water / Sewer node・pipe・service pointを含むtick処理とstatistics snapshotの基準値を継続取得する。

## BenchmarkDotNet scenario

`WaterSewerBenchmarks.StepAndSnapshotStatistics`を使用する。

| LoadCount | Water nodes | Water pipes | Sewer nodes | Sewer pipes | Service points |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 | 1,002 | 1,001 | 1,002 | 1,001 | 1,000 |
| 5,000 | 5,002 | 5,001 | 5,002 | 5,001 | 5,000 |

各loadはBuilding、Water service node、Sewer service node、WaterPipe、SewerPipe、WaterSewerServicePointを1つずつ持つ。全loadが共通のWater distribution / Sewer collectionへ接続される。

測定対象は1 tickの`SimulationWorld.Step()`と`CreateWaterSewerStatistics()`であり、`MemoryDiagnoser`でallocationも記録する。

## CI

Benchmarks workflowに`water-sewer-loads-1k-5k` jobを追加し、short jobのMarkdown / JSON / CSV artifactを14日保存する。Phase 24では特定の絶対時間をhard gateにせず、実行環境差を避けながらartifactをbaselineとして残す。
