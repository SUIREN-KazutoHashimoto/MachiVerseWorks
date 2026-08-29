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

GitHub Actionsの`Phase 11 Road Network Benchmark`は同じsuiteを10,000 / 100,000 Segmentで実行し、Markdown / JSON / CSV measurementをartifactとして保存する。Phase 11 closeoutではこのworkflowの成功結果を基準値として扱い、後続Routing / Traffic実装で同じbenchmarkを再実行して退行を比較する。

## 評価上の注意

`CreateFullTopologySnapshot`は全件のimmutable snapshot生成を目的としており、allocationがSegment数に比例する。一方、`QuerySpatialVolume`と`LookupStableSegment`はsubscription / routing前段で頻繁に利用されるため、全件走査へ退行していないことを重視する。
