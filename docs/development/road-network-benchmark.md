# Road Network Benchmark

Phase 11では10,000 / 100,000 RoadSegment規模について、Road Networkの基準性能を独立BenchmarkDotNet suiteで追跡する。

## 対象

`RoadNetworkBenchmarks`は各RoadSegmentを独立した2つのRoadNodeで構築し、4段階の高度を混在させる。次の3操作を同じrunner条件で測定する。

- `QuerySpatialVolume`: 3D `WorldVolume`からRoad Network subsetを取得する。
- `CreateFullTopologySnapshot`: 全Road topology snapshotをstable ID順に生成する。
- `LookupStableSegment`: stable `RoadSegmentId`から単一Segmentを取得する。

## 実行

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj \
  --configuration Release -- \
  --filter '*RoadNetworkBenchmarks*' --job short
```

GitHub Actionsの`.github/workflows/benchmarks.yml`は`road-network-10k-100k` jobで同じsuiteを10,000 / 100,000 Segmentに対して実行し、`benchmark-road-network` artifactとしてMarkdown / JSON / CSV measurementを保存する。後続Routing / Traffic実装では同じbenchmarkを再実行して退行を比較する。

## Phase 11基準値

2026-08-30のGitHub Actions ShortRunで取得した基準値を以下に記録する。runnerはUbuntu 24.04.4、AMD EPYC 7763、.NET SDK 10.0.400 / Runtime 10.0.11、BenchmarkDotNet 0.15.8で、1 launch・3 warmup・3 iterationで実行した。

| RoadSegment数 | 操作 | Mean | Allocated / op |
| ---: | --- | ---: | ---: |
| 10,000 | `QuerySpatialVolume` | 440.737 us | 502,696 B |
| 10,000 | `CreateFullTopologySnapshot` | 1.992 ms | 3,227,055 B |
| 10,000 | `LookupStableSegment` | 3.557 ns | 0 B |
| 100,000 | `QuerySpatialVolume` | 3.816 ms | 2,411,172 B |
| 100,000 | `CreateFullTopologySnapshot` | 22.556 ms | 32,683,328 B |
| 100,000 | `LookupStableSegment` | 3.575 ns | 0 B |

この値はPhase 11 closeout候補の性能基準であり、SLAや固定上限ではない。GitHub hosted runnerの揺らぎを考慮し、後続Phaseでは単発値ではなく同一suite・同等runner条件での傾向を比較する。

10,000→100,000 Segmentでstable ID lookupはほぼ一定で、spatial queryは約8.7倍、全件snapshotは約11.3倍となった。全件snapshotは意図どおり件数に応じてallocationが増える一方、stable lookupにはmanaged allocationがない。

## 評価上の注意

`CreateFullTopologySnapshot`は全件のimmutable snapshot生成を目的としており、allocationがSegment数に比例する。一方、`QuerySpatialVolume`と`LookupStableSegment`はsubscription / routing前段で頻繁に利用されるため、全件走査へ退行していないことを重視する。
