# MachiVerseWorks.Server

Headless ServerはASP.NET Core / Kestrel上でSimulation Worldをserver-authoritativeに実行します。

- `appsettings.json`からlisten address / portとSimulation設定を読む
- `/health`でserver / simulationの稼働状態を確認できる
- Simulation tickをhosted serviceで実行する
- `/ws`でProtocol 2.x binary WebSocketを受け付ける
- Browser WebSocketは`Server:AllowedWebSocketOrigins`のallowlistでOriginを検証する
- Origin headerを持たないnon-browser Clientは許可する
- `Hello` / `HelloAck`でhandshakeする
- 3D `SubscribeVolume`をbounded channel経由で処理する
- subscription volume内のAgent spawn / update / removeをsnapshot publish周期で配信する
- subscriptionはXYZ cell数で`Server:MaximumSubscriptionCellCount`を検証する

Protocol 1.xの`SubscribeArea`や2D rectangle互換経路は提供しません。

ローカル起動:

```bash
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

既定では`127.0.0.1:5080`をlistenし、Vite開発用の`http://127.0.0.1:5173`と`http://localhost:5173`をBrowser Originとして許可します。

`Server:MaximumSubscriptionCellCount`の既定値は262,144 cellsです。Web Clientの既定OrthographicCameraが最小zoomで持つfull 3D frustum AABBを受理できる値であり、外部Clientからの無制限なvolumeは引き続き拒否します。Simulation内部のSpatial Indexは巨大な疎volumeでoccupied cell走査へ切り替えます。

`Server:AllowedWebSocketOrigins`は`;`区切りのscalar値です。configuration providerの優先順位に従って値全体が置換されます。空文字列にするとBrowser Originを1件も許可せず、Origin headerを持たないnon-browser Clientだけを許可します。
