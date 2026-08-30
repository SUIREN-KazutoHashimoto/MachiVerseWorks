# Phase 14 Intersection Control Benchmark Baseline

Phase 14 のintersection entry arbitration / queue処理を、既存Simulation benchmarkと独立して継続計測するための基準値を記録する。

## 対象

Benchmark class: `IntersectionControlBenchmarks`

各intersectionは4方向・4 movementの固定cycle Signalを持ち、各movementについて下流Lane入口を1台のVehicleで塞ぎ、incoming Vehicleをstop lineで待機させる。Global setupで5 simulation seconds進めてqueueを形成してから測定する。

1 intersectionあたり:

- 4 signal movements
- 4 downstream blocker Vehicles
- 4 queued incoming Vehicles

したがって500 intersection caseは2,000 movement、4,000 Vehicleを含む。

測定method:

- `QueuedIntersectionTick`: `SimulationWorld.Step()`でentry intent収集、signal permission、downstream blocking、queue処理、Vehicle updateを実行し、TrafficMetricsを取得する。
- `ControllerSnapshot`: 同じworldから全intersection controller / movement state snapshotを生成する。

## Baseline

GitHub Actions `Phase 14 Intersection Benchmark` run `33296276808`、head commit `547a1f8048ad5fffdc97780dfe1c171ff5b88b2f` で取得したShortRun baseline。

環境:

- Ubuntu 24.04.4 LTS
- AMD EPYC 7763 2.45 GHz
- 2 physical / 4 logical cores
- .NET SDK 10.0.400
- .NET runtime 10.0.11
- BenchmarkDotNet 0.15.8
- ShortRun: 1 launch / 3 warmup / 3 measurement iterations

| Method | Intersections | Mean | Allocated |
| --- | ---: | ---: | ---: |
| QueuedIntersectionTick | 10 | 28.574 us | 36.6 KB |
| ControllerSnapshot | 10 | 4.491 us | 19.35 KB |
| QueuedIntersectionTick | 100 | 286.242 us | 358.61 KB |
| ControllerSnapshot | 100 | 45.836 us | 193.02 KB |
| QueuedIntersectionTick | 500 | 1,510.798 us | 1,768.16 KB |
| ControllerSnapshot | 500 | 235.539 us | 964.9 KB |

## Interpretation

このbaselineでは、10→100→500 intersectionでqueued tickとcontroller snapshotの双方が概ねintersection数に比例して増加している。500 intersection / 4,000 Vehicleの継続queue caseでも1 Simulation tickの測定meanは約1.51 msだった。

この値は性能保証ではなく regression baseline とする。GitHub-hosted runnerはhardware共有条件が変化し得るため、将来の変更は単一runの微小差ではなく、allocationの構造的増加、桁違いのlatency悪化、スケーリング曲線の変化を中心に判断する。

## Automation

`.github/workflows/phase14-benchmark.yml` がSimulationまたはbenchmark変更時に `*IntersectionControlBenchmarks*` をShortRunで実行し、BenchmarkDotNet artifactを14日保持する。
