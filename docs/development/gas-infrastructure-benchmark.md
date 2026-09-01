# Gas Infrastructure Benchmark

## 目的

Phase 25のPipeline Gas capacity solverとDelivered Gasのinventory / Shipment処理について、規模増加時のtick・snapshot負荷を継続取得する。Delivered GasはPhase 22 Logisticsの正本実装を再利用しつつ、`CommodityKind.Gas`とGasServicePointを含む専用scenarioで統合負荷も測定する。

## Pipeline Gas scenario

`GasBenchmarks.StepAndSnapshotStatistics`を使用する。

| LoadCount | Gas nodes | Gas pipelines | Service points |
| ---: | ---: | ---: | ---: |
| 1,000 | 1,002 | 1,001 | 1,000 |
| 5,000 | 5,002 | 5,001 | 5,000 |

各loadはBuilding、Gas service node、GasPipeline、Piped GasServicePointを持ち、共通distributionへ接続する。Sourceからdistributionまでのupstream pipelineには全loadを収容できるcapacityを設定する。

測定対象は1 tickの`SimulationWorld.Step()`と`CreateGasStatistics()`であり、`MemoryDiagnoser`でallocationも記録する。

## Delivered Gas scenario

`DeliveredGasBenchmarks`は100 / 1,000 consumer inventoryを作成し、すべてを`CommodityKind.Gas`とDelivered GasServicePointへ接続する。consumer inventoryを空で開始し、既存Logisticsのreorder ruleによって同数のShipmentを生成した状態をbenchmark baselineとする。

測定対象は次の2つ。

- `Tick`: Gas service state、Economy、Logistics / Freightを含む1 tick
- `Snapshots`: Gas snapshotとLogistics snapshotの組み合わせ

Shipmentの積載・道路輸送・配送処理そのものはPhase 22 Logisticsの正本を利用するため、別実装をbenchmark用に複製しない。

## CI

Benchmarks workflowの`gas-loads-1k-5k` jobは`*GasBenchmarks*` filterでPipeline GasとDelivered Gasの両benchmark classを実行し、Markdown / JSON / CSV artifactを14日保存する。特定の絶対時間をhard gateにはせず、同一workflowの継続artifactを性能回帰のbaselineとする。
