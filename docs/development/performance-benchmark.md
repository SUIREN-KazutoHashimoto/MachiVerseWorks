# Phase 7 Performance Benchmark Foundation

Phase 7で導入した性能計測基盤の実行方法、保存形式、観測点、最初の改善候補をまとめます。一般的な性能判断ルールは [`performance.md`](performance.md) を正本とします。

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

Phase 2の独自tick runnerは既存baseline再現用に残しています。

```bash
dotnet run --project benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj --configuration Release -- --warmup 60 --ticks 200
```

## 3. Phase 7 benchmark scenarios

### Snapshot

`SnapshotBenchmarks` は固定seedで1,000 / 10,000 / 100,000 Agentを生成し、中央の固定subscription範囲に対する `SimulationWorld.CreateSnapshot` を測定します。

ここには次が含まれます。

- spatial query
- exact area filtering
- `AgentSnapshot`配列生成

### Spatial query

`SpatialQueryBenchmarks` は `SpatialIndex.Query` 自体を分離して測ります。10,000 / 100,000 Agentと、異なるquery範囲を組み合わせます。

Snapshot全体とSpatialIndex単体を分けることで、候補列挙とsnapshot materializationのどちらが支配的か比較できます。

### Protocol

`ProtocolCodecBenchmarks` は代表的な `AgentUpdateMessage` のencode / decodeを個別に測ります。

Network send時間はmicrobenchmarkへ混ぜず、Server側の配信統計で観測します。

## 4. Server snapshot delivery metrics

Phase 6の `/metrics/e2e` は互換維持し、Phase 7では各snapshot deliveryについてDebug structured logも出力します。

記録項目:

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

これにより累積値だけでなく、配信単位の変動を時系列で追跡できます。

## 5. Web Client development overlay

Vite development modeでは右上に `Performance (DEV)` overlayを表示します。production buildでは表示しません。

表示するrolling 240-sample metrics:

- snapshot decode time: average / p95 / maximum
- animation frame interval: average / p95 / maximum

sample総数とdecode総bytesは内部metricsとして維持し、Phase 6 E2Eの計測にも引き続き利用します。

起動:

```bash
cd src/web
npm run dev
```

FrameはSimulation tick timeではなく、ブラウザの連続する `requestAnimationFrame` timestamp間隔です。

## 6. 最初の性能改善候補

最優先候補は **ServerからClientへのAgent message batching** とします。

Phase 6の100,000 Agent近傍配信では、1回の近傍snapshotで約1,075 Agentに対して約1,078 message、約68.9 KBを送信し、encode約0.58 msに対してsend約6.08 msでした。この構成ではAgentごとにbinary frameを生成し、messageごとに `WebSocket.SendAsync` を呼び出しています。

したがって最初に検証すべき仮説は「payload byte数そのものより、message/frame数とSendAsync呼び出し回数がServer送信costを押し上げている」です。

次の最適化フェーズでは、複数Agent updateを1 frameへbatchする案を現行方式と同条件で比較し、最低限次を確認してから採用します。

- send time
- encode time
- bytes / snapshot
- message / frame count
- allocation / GC
- Client decode time
- reconnect / removeを含む正しさ

次点の候補は `SnapshotMessagePlanner` の毎snapshot sort・HashSet・List allocationと、Web Clientのvisible Agent全件同期です。これらはBenchmarkDotNet結果とbrowser profilerで支配率を確認してから着手します。

## 7. 完了判定

Phase 7では「速くした」ことではなく、「継続して測り、結果を保存し、次の最適化対象を根拠付きで選べる基盤がある」ことを完了条件とします。
