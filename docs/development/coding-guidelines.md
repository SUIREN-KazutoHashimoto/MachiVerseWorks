# Coding Guidelines

MachiVerseWorks の実装で共通して守るコーディング方針を定めます。

この文書は細かな好みを固定するためではなく、Simulation Core、Gateway、Server Host、Protocol、View、Management の責務と、大規模リアルタイムシミュレーションに必要な性能特性を長期的に維持するための基準です。

## 1. 基本原則

- 正しさ、責務分離、検証可能性を優先する。
- 最適化は計測結果に基づいて行う。
- hot path と通常コードを同じ基準で過剰最適化しない。
- 一時的に動かすためだけの責務越境を避ける。
- 公開契約、保存形式、Protocol の変更は互換性への影響を明示する。
- 仕様変更では関連する `docs/specifications/` または `docs/architecture/` も更新する。

## 2. C# 共通

### 2.1 命名

- 型、public member、enum: `PascalCase`
- local variable、parameter、private field: `camelCase`
- private field は `_camelCase` を基本とする。
- boolean は `is` / `has` / `can` / `should` など意味が分かる名前を優先する。
- 単位を持つ値は、曖昧な場合に名前へ単位を含める。例: `speedMetersPerSecond`, `timeoutMilliseconds`。

### 2.2 Nullable

Nullable reference types は常時有効とする。

- `!` による抑制は、成立条件をコード上で説明できる場合だけ使用する。
- nullable を曖昧な未初期化表現として使わず、状態遷移を型や明示的な state で表現する。

### 2.3 class / struct / record

- 振る舞いと identity を持つオブジェクトは `class` を基本とする。
- 小さく、値として扱えるデータは `readonly struct` / `record struct` を検討する。
- 大きな mutable struct は避ける。
- `record` は DTO や値契約に有効だが、Simulation の高頻度 mutable state に安易に使わない。

### 2.4 ID

Agent、Vehicle、Building、Station などの ID は安定性を重視する。

- ID を表示順や配列の詰め直し都合で再採番しない。
- 異なる種類の ID を同じ整数として混同しやすい箇所では strongly typed ID を検討する。
- Protocol / Save Data で公開した ID の意味を変更しない。

## 3. Simulation Core

### 3.1 状態所有

状態には明確な owner を持たせる。

- Agent state は Agent/World store が正本を持つ。
- Traffic state は交通系 store/system が正本を持つ。
- Activity、ETA、schedule、classification、semantic event等は対応するSimulation domainを意味的正本とする。
- Gateway、View、Management、Server HostがSimulation semantic stateを推測して別の正本を作らない。

### 3.2 Tick

Simulation tick は再現性と観測可能性を重視する。

基本形:

```text
commands
→ simulation update
→ state transition commit
→ semantic observation source capture
```

- I/O 待ちを Simulation tick に持ち込まない。
- async network operation を tick 内部へ混ぜない。
- tick 中に外部から mutable state を直接変更させない。
- Gatewayのsubscription / cache / delivery状態をSimulation ruleやfidelityの判定条件に使用しない。

### 3.3 allocation

高頻度処理では allocation を意識する。

避ける例:

- Agent ごとの一時 object
- 毎 tick の LINQ chain
- 毎 tick の不要な collection 作成
- boxing が大量発生する抽象化
- Agent ごとの `Task`

通常コードでは、必要性がない限り可読性を犠牲にして allocation を消さない。

### 3.4 LINQ

LINQ 自体は禁止しない。

- 設定処理、起動時処理、管理画面用処理など低頻度領域では使用してよい。
- tick、routing、traffic、entity iteration など hot path では allocation と走査回数を確認する。
- 性能問題が確認されていない LINQ を推測だけで手書き loop に置き換えない。

### 3.5 Span / Memory / ArrayPool

- `Span<T>` / `ReadOnlySpan<T>` はコピー削減や境界明確化に有効な場合に使う。
- `Memory<T>` は async 境界など `Span<T>` を保持できない場合に検討する。
- `ArrayPool<T>` は allocation 削減効果が計測できる長寿命または大容量 buffer で検討する。
- pool から借りた buffer は ownership と返却地点を明確にする。
- `unsafe` / pointer / native memory は profiling で必要性を確認した後に限定導入する。

## 4. 並列処理

並列化は CPU core 数を使うための手段であり、目的ではありません。

- Agent ごとに `Task.Run` しない。
- subsystem ごとに永久固定 thread を割り当てる設計を前提にしない。
- range/chunk 単位の job を基本とする。
- `Parallel.For` 等は state ownership と write conflict が明確な処理で使用する。
- shared mutable state への細かい `lock` の乱用を避ける。
- parallel phase と commit phase を分けられる場合は分ける。
- 並列化後も deterministic behavior が必要な箇所を明示する。

## 5. async / Task / Channel

- `async` は主に network、file、database 等の I/O 境界で使用する。
- CPU-bound Simulation 処理を async 化しただけで高速化したと考えない。
- `CancellationToken` を長時間処理と I/O API の適切な境界へ渡す。
- `Channel<T>` は Server 内の producer/consumer 境界や command queue 等で有効な場合に検討する。
- unbounded queue は backlog が無制限に増えないか確認する。

## 6. Gateway / Server Host

- Gatewayはread-only Observation Request / subscription / filtering / cache / delivery / reconnectを担当し、semantic stateを生成しない。
- Gatewayのcache hit / miss / rebuildで同一authoritative revisionのObservation semanticsを変えない。
- Network処理からSimulation mutable Storeを長時間直接参照せず、detached source / snapshotを使う。
- Observation routeからauthoritative mutation routeへ到達させない。
- Server HostはGatewayとAdministration / Management command boundaryを別責務としてhostする。
- slow client、timeout、cancellation、queue backlogはconnection / request単位で隔離し、Simulation tickへ無制限backpressureを波及させない。

## 7. 例外とエラー

- 例外は異常系に使用し、通常の高頻度 state transition を例外で表現しない。
- public boundary では invalid input を検証する。
- Protocol では user-facing message ではなく stable error/message code と structured parameter を返す。
- catch して握りつぶさない。無視する場合は理由を明確にする。

## 8. Logging

- structured logging を基本とする。
- hot path で Agent ごとの常時ログを出さない。
- Debug/Trace logging でも大量出力による性能影響を確認する。
- user-facing localization と運用ログを混同しない。
- secret、token、個人情報をログへ出さない。

## 9. Protocol / Save Data

- internal class layout をそのまま Protocol に公開しない。
- binary layout は明示的な field order / width / endianness を持たせる。
- Protocol version と application version を独立して扱える構造を維持する。
- domain semantic payloadの意味・field / unitはSimulation、Observation control / delivery envelope / adaptationはGatewayというRoadmap ownershipを維持する。
- read-only Observation Requestとauthoritative mutation commandを明示的に区別する。
- Save Data は locale-independent な ID / enum / code / raw value を保存する。
- 互換性を壊す変更では migration または versioning 方針を明示する。

## 10. View / Management Client

### View

- Simulation state の正本を持たない。
- Gatewayから受け取ったauthoritative observationを表示用 state へ変換し、render frame と Simulation tick を分離する。
- semantic state、予定、ETA、分析結果等を推測・補完・再計算しない。
- UI の固定表示文言は localization resource 経由を前提にする。
- network decode と rendering object の lifecycle を明確にする。
- 描画最適化は entity 数、draw call、frame time を計測して行う。

### Management

- mutationはserver-authoritative command境界からだけ実行する。
- command pending / resultとGatewayから再観測したauthoritative World stateを混同しない。
- read-only View componentを再利用しても、command clientをView / Gateway moduleへ注入しない。
- optimistic previewはauthoritative stateと型・state ownershipを分離する。

## 11. コメントとドキュメント

コメントは「何をしているか」より「なぜそうする必要があるか」を優先する。

特に次は理由を残す。

- 一見不要に見える guard
- protocol compatibility workaround
- unusual memory layout
- ordering requirement
- performance optimization
- deterministic behavior のための制約

重要な設計判断はコメントだけで終わらせず ADR を使用する。

## 12. Analyzer / build

共通設定は `.editorconfig` と `Directory.Build.props` を正本とする。

- Nullable: enabled
- .NET analyzers: enabled
- code style analysis: build でも有効
- CI: warnings as errors

警告を抑制する場合は、警告全体を無効化する前に局所修正または局所 suppression を優先する。

## 13. Review checklist

実装レビューでは最低限次を確認する。

- 状態 owner は明確か
- Simulation / Gateway / Server Host / Protocol / View / Managementの責務境界を越えていないか
- Gateway / Viewがsemantic stateを推測・再計算していないか
- Observation routeからmutation APIへ到達していないか
- hot path に不要な allocation / full scan がないか
- concurrent write の可能性はないか
- Protocol / Save Data / localization の契約を壊していないか
- test / benchmark / documentation の更新が必要ではないか