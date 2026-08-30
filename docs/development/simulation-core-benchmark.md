# Simulation Core Phase 2 Benchmark

Phase 2 — Simulation Core 最小 PoC の初回性能 baseline を記録します。

この結果は GitHub-hosted runner 上の共有実行環境で取得した比較用 baseline であり、特定ハードウェア上の性能保証や CI の合否基準ではありません。

## 測定条件

| 項目 | 値 |
| --- | --- |
| 測定日 | 2026-08-29 |
| commit | `40dbbb290843f13a68a052beb718ed6e13d4d70b` |
| branch | `feature/phase-2-simulation-core` |
| build | Release / `net10.0` |
| OS | Ubuntu 24.04.4 LTS / x86_64 |
| CPU | AMD EPYC 7763 / 4 logical CPU |
| Memory | 15 GiB |
| .NET SDK | 10.0.400 |
| .NET Runtime | 10.0.11 |
| tick rate | 30 ticks/sec |
| seed | 1234 |
| spatial cell size | 64 |
| spawn area | X/Y ともに -5,000 〜 5,000 |
| warmup | 60 ticks |
| measurement | 200 ticks |

Benchmark は Agent の最小状態更新 `position += velocity * tickDuration` と、cell 境界を跨いだ場合の spatial membership 更新を測定します。

## 結果

| Agent | Average | p50 | p95 | p99 | Max | ticks/sec | allocation/tick |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 | 0.2096 ms | 0.2053 ms | 0.2307 ms | 0.2455 ms | 0.2508 ms | 4,770.42 | 91.52 B |
| 10,000 | 0.6764 ms | 1.0589 ms | 1.1037 ms | 1.4021 ms | 2.9444 ms | 1,478.42 | 596.16 B |
| 100,000 | 0.8909 ms | 0.8829 ms | 0.9527 ms | 1.0237 ms | 1.0998 ms | 1,122.40 | 2,615.12 B |

## 初回評価

30 ticks/sec の tick budget は約 33.3 ms です。この測定では 100,000 Agent の p99 が 1.0237 ms で、最小 PoC の更新処理は初期 budget に十分収まっています。

一方、この値だけを将来の都市シミュレーション全体の余裕とはみなしません。Phase 2 には routing、traffic、economy、network serialization、snapshot delivery などの実処理が含まれていないためです。

10,000 Agent の分布は 1,000 / 100,000 Agent と単純比例していません。共有 runner の scheduling、tiered JIT、CPU frequency などの影響を受ける単発 baseline なので、絶対値や局所的な順位ではなく、同じ harness を継続して比較するための初回値として扱います。

allocation/tick は Agent 数とともに増加していますが、100,000 Agent でも約 2.6 KiB/tick です。現時点では測定根拠なしに SoA、pooling、並列化を先行導入せず、Phase 7 の profiling と benchmark 拡張で改善対象を選定します。

## 再実行

GitHub Actionsの`.github/workflows/benchmarks.yml`を手動実行すると`legacy-tick` jobで同じtick harnessを再実行できます。ローカルでは次を実行します。

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --warmup 60 --ticks 200
```

比較時は commit、OS、CPU、RAM、.NET version、seed、Agent 数、warmup、measurement ticks を併記します。
