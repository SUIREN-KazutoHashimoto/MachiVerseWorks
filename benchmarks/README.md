# Benchmarks

Simulation Core、snapshot生成、spatial query、Protocolなどの性能評価コードを配置します。

通常テストと性能benchmarkを混在させず、性能変更は比較可能な計測結果を残します。

Phase 7以降のmicrobenchmarkはBenchmarkDotNetを標準とします。共通設定は `MachiVerseWorks.Benchmarks/PerformanceBenchmarkConfig.cs` に集約し、MemoryDiagnoserとMarkdown / JSON / raw CSV exporterを有効にしています。

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --filter '*'
```

CI相当の短いsmokeは次で実行できます。

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --job Dry --filter '*'
```

Phase 2のtick baselineを再現する従来runnerも互換維持しています。

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --warmup 60 --ticks 200
```

詳細は [`../docs/development/performance-benchmark.md`](../docs/development/performance-benchmark.md) を参照してください。
