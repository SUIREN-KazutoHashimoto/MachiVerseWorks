# MachiVerseWorks.Server

Phase 4 の最小 headless server を提供します。

- `appsettings.json` から listen address / port と Simulation 設定を読む
- `/health` で server / simulation の稼働状態を確認できる
- Simulation tick を hosted service で実行する
- `/ws` で binary WebSocket Protocol を受け付ける
- Browser WebSocketは`Server:AllowedWebSocketOrigins`のallowlistでOriginを検証する
- Origin headerを持たないnon-browser Clientは許可する
- `Hello` / `HelloAck` で handshake する
- `SubscribeArea` を bounded channel 経由で処理する
- subscription 範囲の Agent spawn / update / remove を snapshot publish 周期で配信する

ローカル起動:

```bash
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

既定では `127.0.0.1:5080` を listen し、Vite開発用の `http://127.0.0.1:5173` と `http://localhost:5173` をBrowser Originとして許可します。別OriginからWeb Clientを配信する場合はallowlistへ明示的に追加してください。
