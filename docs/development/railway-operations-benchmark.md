# Phase 18 Railway Operations Benchmark Baseline

Phase 18のTrain / Service運行処理について、fixed tick・route traversal / block arbitration・publish snapshotの継続的な回帰検知に使う基準値を記録する。

## 対象

Benchmark class: `RailwayOperationsBenchmarks`

fixtureは4 TrackSegment / 4 Block、2 Station / 2 Platform、origin / destination Depotを1本の連続RailwayRouteで接続する。1 Formation / 1 Timetableを共有し、`TrainCount`と同数のService / Trainを生成する。各Serviceはstable ID順にplanned start tickをずらし、測定前に60 tick進める。

測定method:

- `FixedTickOperations`: `SimulationWorld.Step()`を1 tick進める。Service activation、route上の3D移動、加減速、Block予約・所有権遷移、Platform look-ahead / 競合、dwell、delay、Depot lifecycleを含む。
- `CreateOperationsSnapshot`: Formation / Route / Timetable / Service / Trainを含むRailway Operations全体snapshotを生成する。
- `CreateTrainSnapshot`: publish用途のTrain mutable-state snapshotを生成する。

このfixtureは全Trainが同じRoute / Block列を共有するため、Block所有権競合と待機時のper-Train処理を継続的に含む。一方で、独立した多数路線やnetwork-wide journey planningを再現するものではない。

## Baseline

GitHub Actions `Phase 18 Railway Operations Benchmark` run `33318887363`、head commit `26e4321e39dec5f4a40fe1def79c5d04f1cf4809`で取得したShortRun baseline。

環境:

- Ubuntu 24.04.4 LTS
- AMD EPYC 9V74 2.60 GHz
- 2 physical / 4 logical cores
- .NET SDK 10.0.400
- .NET runtime 10.0.11
- BenchmarkDotNet 0.15.8
- ShortRun: 1 launch / 3 warmup / 3 measurement iterations

| Method | TrainCount | Mean | Allocated |
| --- | ---: | ---: | ---: |
| FixedTickOperations | 100 | 1.968 us | 64 B |
| CreateOperationsSnapshot | 100 | 9.852 us | 37,928 B |
| CreateTrainSnapshot | 100 | 3.134 us | 20,896 B |
| FixedTickOperations | 1,000 | 23.324 us | 64 B |
| CreateOperationsSnapshot | 1,000 | 99.089 us | 361,928 B |
| CreateTrainSnapshot | 1,000 | 30.663 us | 208,096 B |

## Interpretation

100→1,000 Trainで、full operations snapshotとTrain snapshotは概ねTrain数に比例して増加している。1,000 Train caseでもfixed tick平均は23.324 usで、30 Hzの1 tick budget 33.333 msに対して十分小さい値だった。

`FixedTickOperations`のmanaged allocationは両caseとも64 B / operationで、Train数に比例するtick allocationはこのbaselineでは観測されなかった。一方、snapshotは配列をmaterializeするため1,000 Trainでoperations snapshot約362 KB、Train snapshot約208 KBを割り当てる。publish頻度やvisible filteringを拡張するときはこのallocationを回帰監視対象とする。

この値は性能保証ではなくregression baselineである。GitHub-hosted runnerのhardware条件は変化し得るため、微小な単一run差よりも、allocationの構造的増加、桁違いのlatency悪化、Train数に対するスケーリング曲線の変化を重視する。

## Related E2E evidence

`Phase 18 Railway Operations E2E` run `33318887371`では、実Server→WebSocket→headless browserでProtocol 2.7をnegotiationし、2 Train / 2 Serviceについて3D移動、Platform assignment、dwell、delayを観測した。最終delayは276 / 717 tickで、両Service / TrainがCompletedとなりDepotへ戻ることを確認した。

## Automation

`.github/workflows/phase18-benchmark.yml`がSimulationまたはbenchmark変更時に`*RailwayOperationsBenchmarks*`をShortRunで実行し、BenchmarkDotNet artifactを14日保持する。`.github/workflows/phase18-e2e.yml`はServer→Browserの1運行周期を検証し、E2E artifactを7日保持する。
