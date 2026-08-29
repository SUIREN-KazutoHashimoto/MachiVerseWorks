# Headless Server 最小 PoC 仕様

## 目的

Headless Server は Simulation Core と Protocol を実プロセスへ接続し、Browser Client が接続できる server-authoritative runtime を提供します。Phase 9 以降は Simulation World の正本状態をフルネイティブ3Dとして扱います。

## 起動と設定

Server は `MachiVerseWorks.Server` を単独実行できます。

```bash
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

既定値は `src/MachiVerseWorks.Server/appsettings.json` から読みます。

- listen address: `127.0.0.1`
- port: `5080`
- Simulation tick rate: `30 Hz`
- snapshot publish rate: `10 Hz`
- maximum subscription cells: `65,536`
- allowed Browser WebSocket origins: `http://127.0.0.1:5173`, `http://localhost:5173`
- initial Agent count: `1,000`
- default spawn volume: `X/Y = -500..500`, `Z = -64..64`

listen address / port、tick rate、snapshot rate、maximum subscription cell count、WebSocket Origin allowlist、seed、spatial cell size、initial Agent count、3D spawn volume は configuration provider から上書きできます。

spawn volume の設定キーは `Simulation:SpawnVolume:MinX/MinY/MinZ/MaxX/MaxY/MaxZ` です。

`Server:AllowedWebSocketOrigins` は `;` 区切りのscalar値として扱い、上位configuration providerは値全体を置き換えます。空文字列はBrowser Originを1件も許可しない設定です。

## HTTP endpoint

### `GET /health`

Server が request を処理できる場合に HTTP 200 を返します。

response には最低限、次の観測値を含めます。

- `status`
- current Simulation tick
- active Agent count
- active WebSocket connection count

## WebSocket endpoint

### `/ws`

binary WebSocket message のみを受け付けます。payload は MachiVerseWorks Protocol 2.0 frame です。

Browser WebSocket requestのように`Origin` headerを持つrequestは、Originをscheme / host / port単位で正規化した値が`Server:AllowedWebSocketOrigins`に含まれる場合だけupgradeします。未許可または不正なOriginはWebSocket upgrade前にHTTP 403で拒否します。

`Origin` headerを持たないnon-browser Clientは許可します。Origin検証はauthentication / authorizationの代替ではなく、BrowserからローカルServerへ意図しないcross-origin接続を作らせないための接続元制約です。

接続直後は `Hello` が必須です。compatible な Protocol version なら Server は `HelloAck` を返し、現在の Protocol version と Simulation tick rate を通知します。

compatible でない version、不正 frame、未知 message type、接続状態に対して不正な request は stable Protocol error code で通知します。

## Subscription

handshake 完了後、Client は `SubscribeVolume` を送信できます。

subscription は connection ごとに1つ保持し、新しい `SubscribeVolume` はその connection の既存3D volumeを置き換えます。Client command は network receive path から直接 Simulation state を変更せず、bounded command channel を経由して Server 側 state へ反映します。

`SubscribeVolume` は `minX / minY / minZ / maxX / maxY / maxZ` の6境界を持ちます。全値は有限値で、各軸について `max >= min` を要求します。2D rectangle への暗黙変換は行いません。

Server は subscription を受理する前に、volume の両端が spatial grid の対応座標範囲へ収まることと、対象セル数 `cellsX × cellsY × cellsZ` が `Server:MaximumSubscriptionCellCount` 以下であることを検証します。既定上限は `65,536` cellsです。条件を満たさない場合は `InvalidRequest` を返し、Simulation query は実行しません。

## Snapshot 配信

snapshot publish 周期は Simulation tick 周期から分離します。

各 publish では connection の subscription volumeだけを Simulation Core から snapshot として取得します。connectionごとに同時実行するsnapshot deliveryは1件までとし、前回deliveryがまだ完了している最中に次のpublish周期が来た場合、そのconnectionの新しい周期はqueueせずdropします。別connectionのdeliveryは独立してscheduleするため、1台のslow Clientが他Clientへの配信を停止させません。

各Protocol messageのWebSocket sendには5秒のtimeoutを適用します。timeoutしたconnectionはabortしてregistryから削除します。timeout用のcancellation stateはdelivery単位で再利用し、Agent/message単位のtimer allocationは行いません。

- Client がまだ知らない Agent: `AgentSpawn`
- Client が既に知っている Agent: `AgentUpdate`
- 前回はvolume内だったが現在 snapshot に存在しない Agent: `AgentRemove`

`AgentSpawn` / `AgentUpdate` は位置 `X/Y/Z` と速度 `VelocityX/VelocityY/VelocityZ` を欠落なく配信します。

subscription 変更時も前回の known Agent ID を次の snapshot 比較まで保持します。これにより新volumeから外れた旧Agentは `AgentRemove` され、Client側に残留しません。

Simulation の mutable state 自体は network layer へ公開しません。

## Lifecycle

Server 起動時に Simulation runtime と hosted services を開始します。Simulation tick loop は network I/O と別の hosted service で実行します。

application shutdown 時は cancellation を各長寿命処理へ伝播し、tick loop / command processor / snapshot publisher / WebSocket session を終了させます。snapshot publisherはshutdown時に既存のin-flight delivery完了を回収します。

## 現段階の範囲外

- TLS 終端と authentication / authorization
- snapshot batching / compression
- delta encoding の高度化
- send queue / bandwidth 制御の本格的 backpressure policy
- multi-process / distributed Simulation
- production observability / metrics / tracing
