# Power Infrastructure Benchmark

## Purpose

Phase 23 adds a regression benchmark for the cost of a Power tick as PowerNode / PowerLine / PowerLoad counts grow. The benchmark is intended to detect algorithmic or allocation regressions in the standard capacity solver; it is not a guarantee that every scenario will meet a realtime budget.

## Scenario

`PowerBenchmarks` creates:

- one Generator bus
- one 100-node distribution chain
- one high-capacity Generator
- 1,000 or 5,000 Building-backed Load nodes
- one PowerLine from the distribution network to each Load

Each benchmark iteration runs `SimulationWorld.Step()` and then captures `PowerStatistics`.

The topology deliberately has enough nodes and edges to exercise graph construction and maximum-flow dispatch while remaining deterministic and reproducible in CI.

## Cases

| Load count | Purpose |
| ---: | --- |
| 1,000 | routine regression baseline |
| 5,000 | larger topology / allocation boundary |

## Local command

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj -c Release -- --filter '*PowerBenchmarks*'
```

A dry smoke run can be executed with:

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj -c Release -- --job Dry --filter '*PowerBenchmarks*'
```

## Interpretation

The Phase 23 standard solver rebuilds a deterministic flow graph each Power tick. Expected cost therefore grows with PowerNode / PowerLine / Generator / PowerLoad graph size. The 5,000-Load case is the initial large-topology regression boundary.

The benchmark should be compared only against runs from a comparable runner and runtime. A future solver that changes physical fidelity or asymptotic behavior should retain this scenario, or add an equivalent baseline, so the performance trade-off is visible rather than hidden.

## Memory boundary

The benchmark uses `[MemoryDiagnoser]` so graph-building and snapshot-related allocations remain observable. Large increases in per-tick allocation should be treated as regressions even when elapsed time remains acceptable.

## CI evidence

The repository benchmark workflow executes all BenchmarkDotNet classes in its smoke coverage, so `PowerBenchmarks` participates automatically once added to the benchmark project. Dedicated Phase 23 benchmark workflow coverage can filter `*PowerBenchmarks*` when a narrower regression run is desired.
