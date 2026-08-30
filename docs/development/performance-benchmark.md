# Performance Benchmark Foundation

Phase 7で導入した性能計測基盤と、Phase 9の3D Simulation回帰計測条件・closeout実測結果をまとめます。一般的な性能判断ルールは [`performance.md`](performance.md) を正本とします。

## 1. BenchmarkDotNet共通設定

`benchmarks/MachiVerseWorks.Benchmarks/PerformanceBenchmarkConfig.cs` を共通設定とし、BenchmarkDotNet `0.15.8` を使用します。

共通で有効にするもの:

- `MemoryDiagnoser`: allocationとGen0 / Gen1 / Gen2 collectionを記録
- GitHub Markdown exporter: 人がレビューしやすいsummary
- Full JSON exporter: 後処理・比較用の構造化結果
- raw CSV measurements exporter: iteration単位の生データ

通常計測:

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --filter '*'
```

特定benchmarkだけ測る場合は `--filter '*SnapshotBenchmarks*'` のように絞ります。

短時間の構成確認:

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --job Dry --filter '*'
```

`Dry`はbuild・起動・benchmark実行・exportが成立することを確かめるsmoke用です。性能baselineとしては扱いません。

独自tick runner:

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --warmup 60 --ticks 200
```

## 2. 結果保存形式

既定では `BenchmarkDotNet.Artifacts/` に保存します。環境変数 `MACHIVERSE_BENCHMARK_ARTIFACTS` を指定すると保存先を変更できます。

`results/` 配下に次を残します。

- Markdown summary
- Full JSON result
- raw CSV measurements

GitHub Actionsでは`.github/workflows/benchmarks.yml`を性能検証の正規入口とします。`benchmarkdotnet-smoke` jobは`.artifacts/benchmarks/smoke`を`benchmark-smoke`として14日間保存し、shared runnerの絶対時間へ厳しいthresholdを設けず、BenchmarkDotNet基盤が正常に動くことをgatingします。

Phase 9の2D→3D比較も同じ`Benchmarks` workflowの`phase9-2d-to-3d-regression` jobから`scripts/run-phase9-regression-benchmark.sh`を実行します。同一runner内で3D化直前commitと現在commitを別worktreeとして連続測定し、`.artifacts/benchmarks/phase9-regression/`を`benchmark-phase9-regression`として14日間保存します。

## 3. Phase 9 3D benchmark scenarios

Phase 9では、2D互換入口ではなく実際にZ値を分散させた入力で性能回帰を観測します。

### Snapshot

`SnapshotBenchmarks` は固定seedで1,000 / 10,000 / 100,000 Agentを3D volumeへ生成し、中央の固定3D subscription volumeに対する `SimulationWorld.CreateSnapshot` を測定します。

ここには次が含まれます。

- 3D spatial query
- exact volume filtering
- `AgentSnapshot`配列生成

### Spatial query

`SpatialQueryBenchmarks` は `SpatialIndex.Query(WorldVolume)` 自体を分離して測ります。10,000 / 100,000 AgentをX/Y/Zへ分散し、異なるquery volumeを組み合わせます。

`SparseSpatialQueryBenchmarks` は±1,000,000 metreの巨大volumeを使い、3D化後のadaptive queryが空cellを体積分列挙せずoccupied cell側へ切り替わる経路を継続計測します。

### Tick

独自tick runnerはAgentの初期位置と速度の両方にZ成分を設定します。これにより3軸位置更新と3D Spatial Index cell更新を含むtick costを測定します。

### Protocol

`ProtocolCodecBenchmarks` はZ / VelocityZが非0の代表的な `AgentUpdateMessage` のencode / decodeを個別に測ります。

Network send時間はmicrobenchmarkへ混ぜず、Server側の配信統計で観測します。

## 4. 2D契約からの構造的回帰

Phase 9のwire contractは意図的に情報量が増えます。実行時間とは別に、次のpayload増加は仕様上の固定costです。

| Message | Phase 8まで | Phase 9 | 増加 |
| --- | ---: | ---: | ---: |
| 旧 `SubscribeArea` payload → `SubscribeVolume` | 32 bytes | 48 bytes | +16 bytes / +50% |
| AgentSpawn / AgentUpdate payload | 48 bytes | 64 bytes | +16 bytes / +33.3% |

Spatial cell keyも `(X,Y)` から `(X,Y,Z)` へ増えるため、cell dictionaryのkeyとhash計算costは増加します。Save Dataは各Agentへ `z` と `velocityZ` を追加するため、JSON byte数も増えます。

## 5. Phase 9 closeout実測 — 2D → 3D

### 測定条件

| 項目 | 値 |
| --- | --- |
| 測定日 | 2026-08-29 |
| 2D baseline | `2ada7e8736c7d93038f3291fd7db154f58db09e0` |
| 3D head | `d01b4f25176f0dbb8c5362c60138f4d86278a63c` |
| Actions比較checkout | PR merge candidate `18f9ee38635086036b1186e6841cf671a2136c5c` |
| OS | Ubuntu 24.04.4 LTS / x86_64 |
| CPU | AMD EPYC 7763 / 4 logical CPU |
| .NET SDK | 10.0.400 |
| .NET Runtime | 10.0.11 |
| BenchmarkDotNet | ShortRun: 1 launch / 3 warmup / 3 iteration |
| tick runner | warmup 60 / measurement 200 ticks |

比較は同一GitHub-hosted runner内でbaseline worktree→current worktreeの順に実行しています。CPU reported frequencyはbaseline 2.45GHz / current 2.61GHzであり、短時間benchmarkの絶対値はノイズを含むため、小さい差は有意な改善・悪化とは断定しません。

### Tick

| Agent | 2D avg | 3D avg | 差 | 2D p99 | 3D p99 | 差 | 2D alloc/tick | 3D alloc/tick | 差 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 | 0.2201 ms | 0.2034 ms | -7.6% | 0.2441 ms | 0.2276 ms | -6.8% | 91.52 B | 72.16 B | -21.2% |
| 10,000 | 0.2320 ms | 0.2540 ms | +9.5% | 1.4068 ms | 1.5076 ms | +7.2% | 596.16 B | 693.44 B | +16.3% |
| 100,000 | 0.9321 ms | 1.1942 ms | +28.1% | 1.0034 ms | 1.3878 ms | +38.3% | 2,615.12 B | 3,314.88 B | +26.8% |

100,000 AgentではXYZ更新と3D cell処理の追加costが観測されました。ただし30 ticks/secのtick budgetは約33.3msで、3D p99 1.3878msは約4.2%です。現時点では基盤として十分余裕があり、この差だけを理由にSoA・並列化などの複雑化を先行導入しません。

### Spatial query

| Agent | Half extent | 2D mean | 3D mean | 差 | Allocation |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 10,000 | 256 | 1.044 μs | 1.047 μs | +0.3% | 328 B → 328 B |
| 10,000 | 1,024 | 13.278 μs | 13.391 μs | +0.9% | 4,264 B → 4,264 B |
| 100,000 | 256 | 1.461 μs | 1.468 μs | +0.5% | 2,192 B → 2,192 B |
| 100,000 | 1,024 | 67.902 μs | 68.644 μs | +1.1% | 65,800 B → 65,800 B |

通常queryは測定ノイズ程度の差で、allocation増加もありません。巨大疎volumeは3D側でoccupied-cell adaptive pathを追加し、別benchmarkと回帰testで監視します。

### Snapshot

| Agent | 2D mean | 3D mean | 差 | Allocation |
| ---: | ---: | ---: | ---: |
| 1,000 | 1.648 μs | 1.646 μs | -0.1% | 296 B → 296 B |
| 10,000 | 16.246 μs | 16.281 μs | +0.2% | 6,344 B → 6,344 B |
| 100,000 | 48.304 μs | 48.843 μs | +1.1% | 51,600 B → 51,600 B |

Snapshot materializationはほぼ横ばいで、allocationも同一です。

### Protocol codec

| Method | 2D mean | 3D mean | 差 | Allocation |
| --- | ---: | ---: | ---: | ---: |
| Encode | 17.35 ns | 17.15 ns | -1.2% | 104 B → 104 B |
| Decode | 22.55 ns | 22.66 ns | +0.5% | 112 B → 112 B |

Agent state payload自体は48→64 bytesへ増えていますが、codec CPU時間とmanaged allocationはこのShortRunでは実質横ばいでした。network帯域についてはpayloadの+16 bytes/Agentを固定costとして別途扱います。

### 評価

Phase 9の3D化による明確な実行時回帰は100,000 Agent tickで確認しましたが、絶対時間はtick budgetに十分収まり、Spatial Query / Snapshot / Protocolには大きな回帰がありません。3D化で不可避なstate量増加と実装上の不要なhot-path回帰を分離して観測できる状態になったため、Phase 9の性能基盤完了条件を満たします。

## 6. Server snapshot delivery metrics

`/metrics/e2e` と各snapshot deliveryのDebug structured logで次を記録します。

- connection ID
- Agent count
- message count
- bytes
- encode time
- send time

開発時に配信統計ログを有効化する例:

```bash
Logging__LogLevel__MachiVerseWorks.Server.SnapshotPublishService=Debug \
  dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

## 7. Web Client development overlay

Vite development modeでは右上に `Performance (DEV)` overlayを表示します。production buildでは表示しません。

表示するrolling 240-sample metrics:

- snapshot decode time: average / p95 / maximum
- animation frame interval: average / p95 / maximum

sample総数とdecode総bytesは内部metricsとして維持し、E2Eの計測にも引き続き利用します。

## 8. 性能改善候補

ServerからClientへのAgent message batchingは引き続き最優先候補です。Phase 9では1 Agentあたりのstate payloadが16 bytes増えるため、frame数削減に加えてbatchingによるheader overhead削減の価値も相対的に高くなります。

3D `SpatialIndex.Query` は通常volumeでは2D baselineとほぼ同じ性能を維持しています。巨大な疎volumeではoccupied-cell走査へadaptiveに切り替え、外部Client subscriptionは`MaximumSubscriptionCellCount`で上限を設けます。今後は実際の都市高度分布とcamera frustumに基づき、必要ならoccupied-cell構造やquery strategyを再評価します。

100,000 Agent tickは2D baselineより約28%遅くなったため、都市機能追加後も継続観測します。ただし現時点のp99は30Hz budgetの約4.2%であり、測定根拠なしに複雑な最適化は導入しません。

## 9. 完了判定

3D化後のtick / spatial query / snapshot / protocolを再現可能な条件で測定し、2D baselineとの同一runner比較をrepository内へ記録しました。3D化で増えた固定costと実行時回帰を区別してレビューできるため、P9-013の完了条件を満たします。
