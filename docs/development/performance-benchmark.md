# Performance Benchmark Foundation

Phase 7で導入した性能計測基盤と、Phase 9の3D Simulation回帰計測条件をまとめます。一般的な性能判断ルールは [`performance.md`](performance.md) を正本とします。

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

## 2. 結果保存形式

既定では `BenchmarkDotNet.Artifacts/` に保存します。環境変数 `MACHIVERSE_BENCHMARK_ARTIFACTS` を指定すると保存先を変更できます。

`results/` 配下に次を残します。

- Markdown summary
- Full JSON result
- raw CSV measurements

GitHub Actionsの `Phase 7 benchmark` workflowは `.artifacts/phase7-benchmarks` を14日間のActions artifactとして保存します。shared runnerの絶対時間は環境変動が大きいため、workflowは厳しい性能thresholdを設けず、BenchmarkDotNet基盤が正常に動くことだけをgatingします。

比較用の正式baselineを取る場合は、同一hardware・同一OS・同一runtime・同一commit条件を記録して通常Jobで実行します。

独自tick runner:

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --warmup 60 --ticks 200
```

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

Snapshot全体とSpatialIndex単体を分けることで、3D cell候補列挙とsnapshot materializationのどちらが支配的か比較できます。

### Tick

独自tick runnerはAgentの初期位置と速度の両方にZ成分を設定します。これにより3軸位置更新と3D Spatial Index cell更新を含むtick costを測定します。

### Protocol

`ProtocolCodecBenchmarks` はZ / VelocityZが非0の代表的な `AgentUpdateMessage` のencode / decodeを個別に測ります。

Network send時間はmicrobenchmarkへ混ぜず、Server側の配信統計で観測します。

## 4. 2D契約からの構造的回帰

Phase 9のwire contractは意図的に情報量が増えます。実行時間とは別に、次のpayload増加は仕様上の固定costです。

| Message | Phase 8まで | Phase 9 | 増加 |
| --- | ---: | ---: | ---: |
| SubscribeArea payload | 32 bytes | 48 bytes | +16 bytes / +50% |
| AgentSpawn / AgentUpdate payload | 48 bytes | 64 bytes | +16 bytes / +33.3% |

Spatial cell keyも `(X,Y)` から `(X,Y,Z)` へ増えるため、cell dictionaryのkeyとhash計算costは増加します。Save Dataは各Agentへ `z` と `velocityZ` を追加するため、JSON byte数も増えます。

CPU時間・allocationの比較はshared runnerの別run同士を直接性能判定に使わず、Phase 8以前のcommitとPhase 9 commitを同一hardware / OS / .NET runtimeでそれぞれ通常Job実行して比較します。最低限、次を比較対象にします。

- 1,000 / 10,000 / 100,000 Agent tick average / p95 / p99 / allocation
- Spatial query mean / allocated bytes
- Snapshot mean / allocated bytes
- Protocol encode / decode mean / allocated bytes
- Server snapshot delivery bytes / encode time / send time
- Web decode time / animation frame interval

性能回帰を検知した場合も、3D化で不可避なpayload増加と、実装上の不要なCPU / allocation増加を分離して判断します。

## 5. Server snapshot delivery metrics

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

## 6. Web Client development overlay

Vite development modeでは右上に `Performance (DEV)` overlayを表示します。production buildでは表示しません。

表示するrolling 240-sample metrics:

- snapshot decode time: average / p95 / maximum
- animation frame interval: average / p95 / maximum

sample総数とdecode総bytesは内部metricsとして維持し、E2Eの計測にも引き続き利用します。

## 7. 性能改善候補

ServerからClientへのAgent message batchingは引き続き最優先候補です。Phase 9では1 Agentあたりのstate payloadが16 bytes増えるため、frame数削減に加えてbatchingによるheader overhead削減の価値も相対的に高くなります。

次点は3D `SpatialIndex.Query` のcell走査数です。subscription volumeが高さ方向へ広がり過ぎると候補cell数が積で増えるため、Serverの `MaximumSubscriptionCellCount` とClientの高度購読範囲をbenchmark結果に合わせて調整します。

## 8. 完了判定

性能改善そのものではなく、3D化後のtick / spatial query / snapshot / protocolを再現可能な条件で測定でき、2Dから増えた固定costと実行時回帰を区別してレビューできることを完了条件とします。
