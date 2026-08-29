# Headless Server Architecture

## 概要

Phase 4 の Server は ASP.NET Core / Kestrel 上で HTTP health endpoint と binary WebSocket endpoint を提供し、1つの `SimulationWorld` を server-authoritative な正本として所有します。

```text
Kestrel
├─ GET /health
└─ /ws
   └─ WebSocketSessionHandler
      ├─ ClientConnectionRegistry
      └─ bounded ClientCommandQueue
             │
             ▼
      ClientCommandProcessor

SimulationTickService ──► SimulationRuntime ◄── SnapshotPublishService
                               │                        │
                               ▼                        ▼
                         SimulationWorld        subscription snapshot
                                                        │
                                                        ▼
                                              spawn/update/remove
```

## State ownership

`SimulationRuntime` が `SimulationWorld` を所有します。WebSocket session、connection registry、Protocol message は Simulation の mutable store を直接所有しません。

Phase 4 では tick service と snapshot publisher が別 hosted service で動くため、`SimulationRuntime` が lock boundary を提供します。これにより `Step()` と `CreateSnapshot()` が同時に内部 store を操作しません。

snapshot は detached value の配列として lock の外へ出た後に Protocol message へ変換します。network send を Simulation lock 中に実行しないことが重要です。

## Simulation tick lifecycle

`SimulationTickService` は `BackgroundService` と `PeriodicTimer` を使って設定された tick rate で `SimulationRuntime.Step()` を呼びます。

network receive / send を tick loop 内へ持ち込まず、application stopping token により graceful shutdown します。

## Client command boundary

network receive path から Simulation / connection state を同期的に横断して変更し続けないため、Client command は bounded `Channel<ClientCommand>` へ投入します。

Phase 4 で扱う command は `SubscribeArea` のみです。channel capacity は有限とし、producer が server の処理速度を恒常的に上回った場合に無制限な backlog を作らない構成にしています。

`SubscribeArea` は command queue へ投入する前に server policy で検証します。矩形の両端が `SpatialGrid` の対応範囲へ変換できることに加え、走査対象セル数を `Server:MaximumSubscriptionCellCount` 以下に制限します。既定値は 4096 cells です。これにより極端な座標で hosted service を停止させたり、巨大矩形で spatial query に過大な走査を要求したりできないようにします。

## Connection state

`ClientConnectionRegistry` が active connection を管理します。各 connection は次を保持します。

- WebSocket
- handshake 完了状態
- negotiated Protocol version
- current subscription area
- subscription revision
- Client が既に認識している Agent ID set

connection 切断時は registry から削除し、subscription / known Agent state も connection と一緒に破棄します。

## WebSocket session lifetime

`/ws` request は `AcceptWebSocketAsync()` 後も session handler 完了まで await します。request aborted token と application stopping token を link し、client disconnect と server shutdown の両方を長寿命 session へ伝播します。

受信は binary message のみ許可し、Protocol の最大 frame size を超える message は拒否します。Protocol decode 後、handshake 前は `Hello` のみ許可します。

## Handshake

1. Client が `Hello` frame を送信する。
2. Server が frame header の Protocol version を検証する。
3. compatible なら negotiated version を connection state に保存する。
4. Server が `HelloAck` と current Simulation tick rate を返す。

handshake 後に frame version が途中で変化した場合は connection を拒否します。

## Snapshot publisher

`SnapshotPublishService` は Simulation tick とは別の `PeriodicTimer` で動きます。

publish ごとに connection の subscription を capture し、その矩形だけ `SimulationRuntime.CreateSnapshot()` で取得します。`SnapshotMessagePlanner` は前回 Client が認識していた Agent ID set と比較して次を生成します。

- new ID → `AgentSpawn`
- existing ID → `AgentUpdate`
- disappeared ID → `AgentRemove`

subscription を変更しても known Agent ID set は次の publish まで保持します。新範囲の snapshot と比較することで、旧範囲にだけ存在した Agent へ `AgentRemove` を送信できます。

subscription revision を使い、publish 中に Client が別範囲へ subscription を変更した場合、古い publish 結果で known Agent ID set を上書きしません。

## Send serialization

同一 WebSocket へ handshake/error response と snapshot publisher が同時 send しないよう、connection 単位で send を直列化します。Protocol serialization は send lock の前に行い、WebSocket I/O の ownership だけを排他します。

connection disposal と snapshot send が競合する可能性があるため、send の lifetime を参照カウントします。dispose request 後は新しい send を拒否し、既に開始済みの send がすべて終了してから send semaphore を破棄します。registry snapshot に残った古い参照からの送信拒否は publisher 側で接続終了として処理します。

## Logging

Server の structured logging は source-generated `LoggerMessage` を使用します。長寿命 service や connection hot path で不要な logging argument allocation を避けます。

## Graceful shutdown

application stop では hosted service の cancellation token と WebSocket session の linked token を cancel します。integration test では server stop 後に Simulation tick が増えないことを確認します。

## 現段階の制約

Phase 4 は End-to-End PoC 用の最小実装です。snapshot は Agent ごとの frame を送信しており、batching / compression / bandwidth budget はまだ導入していません。これらは実際の snapshot bytes / encode time / send time を Phase 6 / 7 で計測した後に最適化します。
