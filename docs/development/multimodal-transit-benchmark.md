# Phase 19 Multimodal Transit Benchmark Baseline

Phase 19のJourney planning / transfer continuation / Taxi dispatchについて、継続的な回帰検知に使うbaselineを記録する。

## 対象

Benchmark class: `MultimodalTransitBenchmarks`

`Scale=25 / 100`で次をShortRun計測する。

- `JourneyPlanning`: 共通Bus stop/service-pattern graphからaccess/egressとstop間edgeを探索し、Journeyを生成する。
- `DispatchNearestTaxi`: Scale台のidle Taxiからpickupに最も近い車両をstable ID tie-break付きで選ぶ。各invocation前に同一checkpointへ復元する。
- `TransferCheckpointContinuation`: transferを含むPassenger checkpointからworldを復元し、32 fixed tick進めてPassenger snapshotを取得する。

## Baseline

PR #132の`Phase 19 Multimodal Transit Benchmark` ShortRun結果を初回baselineとする。CIで計測完了後、この節へrun ID / head commit / runner環境 / Mean / Allocatedを固定してからPRをReady for reviewにする。

## Interpretation

このbenchmarkは性能保証ではなくregression baselineである。GitHub-hosted runnerのhardware差より、Scale増加時のオーダー、allocationの構造的増加、桁違いのlatency悪化を重視する。

## Automation

`.github/workflows/benchmarks.yml`の`journey-transfer-dispatch` jobが`*MultimodalTransitBenchmarks*`をShortRunで実行し、`benchmark-multimodal-transit` artifactを14日保持する。
