# Headless Server Architecture

## 概要

Headless Server は ASP.NET Core / Kestrel 上で HTTP health endpoint と binary WebSocket endpoint を提供し、1つの `SimulationWorld` を server-authoritative な正本として所有します。Phase 9 以降、位置・速度・subscription・snapshot はフルネイティブ3Dです。

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
                         SimulationWorld        3D volume snapshot
                                                        │
                                                        ▼
                                              spawn/update/remove
```

## State ownership

`SimulationRuntime` が `SimulationWorld` を所有します。WebSocket session、connection registry、Protocol message は Simulation の mutable store を直接所有しません。

Phase 4 から継続して tick service と snapshot publisher は別 hosted service で動作し、`SimulationRuntime` が lock boundary を提供します。`Step()` と `CreateSnapshot(WorldVolume)` を同時に内部 store へ適用しません。

snapshot は detached value の配列として lock の外へ出た後に Protocol message へ変換します。network send を Simulation lock 中に実行しません。

## Simulation tick lifecycle

`SimulationTickService` は `BackgroundService` と `PeriodicTimer` を使って設定された tick rate で `SimulationRuntime.Step()` を呼びます。

network receive / send を tick loop 内へ持ち込まず、application stopping token により graceful shutdown します。

## Client command boundary

network receive path から Simulation / connection state を同期的に横断して変更し続けないため、Client command は bounded `Channel<ClientCommand>` へ投入します。

現在のsubscription commandは `SubscribeVolume` です。2Dの `SubscribeArea` / rectangle互換入口は持ちません。

`SubscribeVolume` は command queue へ投入する前に server policy で検証します。`minX/minY/minZ/maxX/maxY/maxZ` は有限かつ各軸で `max >= min` を要求し、volume両端が `SpatialGrid` の対応範囲へ変換できることを確認します。

走査対象セル数は `cellsX × cellsY × cellsZ` で数え、`Server:MaximumSubscriptionCellCount` 以下に制限します。既定値は `65,536` cellsです。Web Clientの既定高度範囲と16:9 viewportの最小zoomを収めつつ、極端に巨大なvolumeによる過大なspatial queryを防ぎます。

## Connection state

`ClientConnectionRegistry` が active connection を管理します。各 connection は次を保持します。

- WebSocket
- handshake 完了状態
- negotiated Protocol version
- current `WorldVolume` subscription
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

Phase 9 の current Protocol は `2.0` です。handshake 後に frame version が途中で変化した場合は connection を拒否します。

## Snapshot publisher

`SnapshotPublishService` は Simulation tick とは別の `PeriodicTimer` で動きます。

publish ごとに connection の3D subscription volumeを captureし、そのvolumeだけ `SimulationRuntime.CreateSnapshot()` で取得します。`SnapshotMessagePlanner` は前回 Client が認識していた Agent ID set と比較して次を生成します。

- new ID → `AgentSpawn`
- existing ID → `AgentUpdate`
- disappeared ID → `AgentRemove`

`AgentSpawn` / `AgentUpdate` は Simulation の `X/Y/Z` と `VelocityX/VelocityY/VelocityZ` を Protocol 2.0へそのまま保持します。

subscription を変更しても known Agent ID set は次の publish まで保持します。新volumeの snapshot と比較することで、旧volumeにだけ存在した Agent へ `AgentRemove` を送信できます。

subscription revision を使い、publish 中に Client が別volumeへ subscription を変更した場合、古い publish 結果で known Agent ID set を上書きしません。

## Snapshot delivery isolation

snapshot publisherはconnectionごとに最大1件のdelivery taskだけをin-flightとして保持します。同じconnectionが配送中なら次のpublish周期はqueueせずdropします。異なるconnectionのdeliveryは独立taskとして進むため、slow Clientのnetwork backpressureを他Clientへ伝播させません。

各deliveryではlinked `CancellationTokenSource` を1つだけ作り、各message sendの直前に5秒timeoutを再設定します。5秒以内に1messageを送信できないconnectionはabortしてregistryから除外します。

shutdown時は新しいscheduleを停止した後、既存in-flight taskを回収してからpublisherを終了します。

## Send serialization

同一 WebSocket へ handshake/error response と snapshot publisher が同時 send しないよう、connection 単位で send を直列化します。Protocol serialization は send lock の前に行い、WebSocket I/O の ownership だけを排他します。

## Logging

Server の structured logging は source-generated `LoggerMessage` を使用します。

## Graceful shutdown

application stop では hosted service の cancellation token と WebSocket session の linked token を cancel します。integration test では server stop 後に Simulation tick が増えないことを確認します。

## 現段階の制約

snapshot は Agent ごとの frame を送信しており、batching / compression / bandwidth budget はまだ導入していません。これらは計測結果を基に後続最適化します。
