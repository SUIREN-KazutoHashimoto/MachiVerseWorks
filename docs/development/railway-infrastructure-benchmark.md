# Railway Infrastructure Benchmark

Railway Infrastructureの性能確認はPhase専用workflowではなく、`.github/workflows/benchmarks.yml`の **`railway-10k-100k`** jobを正規入口とする。

## Scenario

BenchmarkDotNetの`RailwayInfrastructureBenchmarks`を`ShortRun`で実行し、TrackSegment数10,000 / 100,000の2規模を測る。

対象method:

- `QuerySpatialVolume`: 3D spatial volume query
- `CreateFullTopologySnapshot`: 全Railway topology snapshot作成
- `ValidateConnectivity`: weakly-connected Track component diagnostic

CI command相当:

```bash
dotnet run \
  --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj \
  --configuration Release \
  --no-restore \
  -- --filter '*RailwayInfrastructureBenchmarks*' --job short
```

結果は`benchmark-railway-infrastructure` artifactとして保存し、Markdown / JSON / CSV exportを含む。workflow artifact retentionは14日。

## Reference baseline

PR #152のBenchmarks #14、`railway-10k-100k`成功runをreference baselineとする。GitHub-hosted Ubuntu runner / .NET 10.0.11 / SDK 10.0.400 / BenchmarkDotNet 0.15.8、ShortRun（Launch 1、Warmup 3、Iteration 3）の結果:

| Method | TrackSegmentCount | Mean | Allocated / op |
| --- | ---: | ---: | ---: |
| QuerySpatialVolume | 10,000 | 589.6 µs | 252.1 KB |
| CreateFullTopologySnapshot | 10,000 | 3.371 ms | 3,995.48 KB |
| ValidateConnectivity | 10,000 | 1.865 ms | 1,988.79 KB |
| QuerySpatialVolume | 100,000 | 4.995 ms | 252.1 KB |
| CreateFullTopologySnapshot | 100,000 | 33.450 ms | 40,185.7 KB |
| ValidateConnectivity | 100,000 | 24.368 ms | 19,414.25 KB |

## Interpretation

この表は**hosted runner上のreference measurement**であり、hard pass/fail thresholdではない。CPU割当・runner image・runtime updateで絶対値は変動するため、性能回帰は同workflow / 同条件でのtrend、allocation増加、order-of-growthの変化を中心に判断する。

現baselineでは:

- Spatial queryは10k→100kで約8.5倍のmeanだが、返却範囲依存のallocationは約252 KBで同水準
- Full snapshotはentity数に応じてtime / allocationがほぼlinearに増える
- Connectivity validationも100kで数十ms規模に収まるが、full graph用allocationを伴う

大きな実装変更で比較が必要な場合は、PRの`railway-10k-100k`artifactを保存し、このreferenceと同じmethod / parameterを比較する。

## Related contracts

- [`../specifications/railway-infrastructure.md`](../specifications/railway-infrastructure.md): topology / connectivity semantics
- [`../architecture/railway-infrastructure.md`](../architecture/railway-infrastructure.md): store / publish / chunk architecture
- [`ci.md`](ci.md): consolidated benchmark workflow運用
