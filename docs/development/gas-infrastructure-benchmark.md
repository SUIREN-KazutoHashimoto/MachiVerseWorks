# Gas Infrastructure Benchmark

## 目的

Phase 25の標準Pipeline Gas capacity solverについて、大規模Gas node / pipeline / service pointを含むtick処理とstatistics snapshotの基準値を継続取得する。Delivered Gasは既存Logisticsのinventory / shipment benchmarkを再利用し、Gas側では追加の供給状態変換が既存tickを阻害しないことをCIで確認する。

## BenchmarkDotNet scenario

`GasBenchmarks.StepAndSnapshotStatistics`を使用する。

| LoadCount | Gas nodes | Gas pipelines | Service points |
| ---: | ---: | ---: | ---: |
| 1,000 | 1,002 | 1,001 | 1,000 |
| 5,000 | 5,002 | 5,001 | 5,000 |

各loadはBuilding、Gas service node、GasPipeline、Piped GasServicePointを持ち、共通distributionへ接続する。Sourceからdistributionまでのupstream pipelineには全loadを収容できるcapacityを設定する。

測定対象は1 tickの`SimulationWorld.Step()`と`CreateGasStatistics()`であり、`MemoryDiagnoser`でallocationも記録する。

## Delivered Gas coverage

Delivered Gasのorder / inventory / shipment / road freightはPhase 22のLogistics modelを直接利用するため、専用の重複benchmarkは作らない。既存`LogisticsBenchmarks`に加えてPhase 25 E2Eで`CommodityKind.Gas`のconsumer inventoryがreorder・shipment・deliveryによって回復し、Gas service stateへ反映されることを確認する。

## CI

Benchmarks workflowに`gas-loads-1k-5k` jobを追加し、short jobのMarkdown / JSON / CSV artifactを14日保存する。特定の絶対時間をhard gateにはせず、同一workflowの継続artifactを性能回帰のbaselineとする。
