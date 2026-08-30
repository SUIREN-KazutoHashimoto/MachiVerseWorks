# Phase 13 Road Traffic Benchmark Baseline

Phase 13 の Vehicle tick / Lane occupancy / snapshot cost を継続計測するための基準を記録する。

## Runner

`RoadTrafficBenchmarkRunner` を次のコマンドで実行する。

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --road-traffic --warmup 5 --ticks 20
```

対象 Vehicle 数は 1,000 / 10,000 / 100,000。

各 scenario は 100 Vehicle / Lane に決定的に分散し、Road topology を作成した checkpoint に Vehicle checkpoint を設定して restore する。setup / restore cost は測定対象へ含めない。

測定項目:

- `tick`: `SimulationWorld.Step()` 1回の wall-clock latency と current-thread allocation
- `occupancy`: 全 Vehicle について `LaneOccupancyIndex.TryGetLeader()` を1回ずつ行う full sweep latency
- `snapshot`: `CreateAllVehicleSnapshots()` 1回の wall-clock latency と current-thread allocation

## Baseline

Phase 13 closeout candidate の GitHub Actions 実測値を、この文書へ記録してから P13-016 を完了とする。

## Interpretation policy

GitHub-hosted runner は hardware 共有条件が変化し得るため、単一 run の微小差を性能回帰と断定しない。

次を中心に比較する。

- Vehicle 数に対する scaling curve
- tick / occupancy / snapshot の桁違いの latency 悪化
- tick / snapshot allocation の構造的増加
- 100,000 Vehicle case が CI timeout 内で完走するか

## Automation

`.github/workflows/phase13-benchmark.yml` が Road Traffic / benchmark 変更時に runner を実行し、CSV artifact を14日保持する。
