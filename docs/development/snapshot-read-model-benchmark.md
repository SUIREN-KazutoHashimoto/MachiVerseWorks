# Snapshot Read Model Benchmark

ServerのClient向けsnapshot queryをauthoritative Simulation lockから分離したread modelの回帰benchmarkです。

## 対象

`PublishedReadModelBenchmarks.QueryPublishedStateForConcurrentClients`は、10,000 Agentを含む1つのimmutable publish snapshotに対して、10 Client / 100 Clientの異なるsubscription volumeを並列queryします。

BenchmarkDotNetのmeanに加えてp95 / p99とallocationを記録します。ClientごとのqueryはSimulationWorldのmutation lockへ戻らず、同一publish cycleでcaptureされた1つのTickCountとread stateを共有します。

## 実行

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj \
  --configuration Release -- \
  --filter '*PublishedReadModelBenchmarks*' --job short
```

PRでは`Snapshot Read Model Benchmark` workflowが同じ10 / 100 Client条件を実行し、BenchmarkDotNetのmarkdown / csv / json artifactを14日保存します。

## 判定

- 10 Clientと100 Clientの両方でbenchmarkが完走すること。
- p95 / p99がartifactへ出力され、後続変更と比較できること。
- query中に`SimulationRuntime._gate`や`SimulationWorld.Step()`へアクセスしないこと。
- subscription volumeが異なっても、同じatomic publish snapshotのTickCountを共有しながら正しい範囲だけを返すこと。
