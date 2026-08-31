# MachiVerseWorks.Server

Headless ServerはASP.NET Core / Kestrel上で1つの`SimulationWorld`をserver-authoritativeに実行し、HTTP health endpointとProtocol 2.x binary WebSocketを提供します。

## Runtime boundary

- `appsettings.json`からlisten address / port、Simulation、Server policyを読む
- `/health`でserver / simulationの稼働状態を確認する
- hosted serviceでfixed-tick Simulationを実行する
- `/ws`で`Hello` / `HelloAck` negotiation後のbinary Protocolを処理する
- Browser WebSocketは`Server:AllowedWebSocketOrigins` allowlistでOriginを検証する
- 3D `SubscribeVolume`をbounded command queue経由で処理する
- `Server:MaximumSubscriptionCellCount`でsubscription cell数を制限する
- Save起動時は復元した`SimulationWorld.Config`をscheduler / HelloAck / subscription guardの正本にする
- stdin管理Consoleはtransport-independentな`AdminCommand`境界を経由し、Simulation tickと同じauthoritative lockで直列化する

既定では`127.0.0.1:5080`をlistenし、Vite開発用の`http://127.0.0.1:5173`と`http://localhost:5173`をBrowser Originとして許可します。

## Administration console

`Server:Console:Enabled`は既定で`true`です。`false`にするとstdin readerを起動しません。Consoleは信頼済みローカル運用者向けであり、認証境界ではありません。

代表的なコマンド:

```text
help
status
version
simulation pause
simulation step 1
simulation resume
agent list
agent show 1
agent add 10 20 0 --vx=1 --vy=0 --vz=0
building list
poi list
road node list
connection list
world save "saves/city one.json"
world load "saves/city one.json"
exit
```

数値はInvariant Cultureで解釈し、IDは正の10進`ulong`です。引用符付きtokenを使うと空白を含むpathを渡せます。不正なcommandや参照整合性エラーはstructured resultとして処理し、Server processを停止させません。

`world save`はSimulation lock中にcheckpointだけをcaptureし、serializationとfile I/Oはlock外で行います。`world load`はfile I/Oとdeserializeを先に終えてからworldをatomicに差し替えます。world差し替えやtopology mutationはRoad/Railway read-model revisionを進め、接続中Clientへ新しいread modelを再配信できる状態にします。

詳細は[`../../docs/specifications/server-administration-console.md`](../../docs/specifications/server-administration-console.md)と[`../../docs/decisions/ADR-0005-server-administration-boundary.md`](../../docs/decisions/ADR-0005-server-administration-boundary.md)を参照してください。

## Published domains

negotiated minorに応じて次のread model / messageを配信します。

- Agent (2.0)
- Road Network (2.1、revision付きread model)
- Pedestrian (2.2)
- Vehicle (2.3)
- Intersection Control (2.4)
- Population statistics / Person debug (2.5、専用publish/inspect boundary)
- Railway Infrastructure (2.6、revision付きread model、必要ならmulti-frame)
- Railway Operations (2.7、visible Train + related Service / Timetable)
- Multimodal Transit (2.8、Line / Stop / Pattern / realtime Bus・Taxi / arrival estimate、現行はworld-wide delivery)

1回のtraffic snapshot publishではSimulation lock下でimmutable read modelをcaptureし、lock外でmessage planning / encoding / network I/Oを行います。Agent / Road / Pedestrian / Vehicle / Intersection / RailwayはClient別`SubscribeVolume`でfilterしますが、Multimodal Transitは現行2.8ではvolume filterせずsnapshot全体を配信します。slow Clientはconnection単位のdelivery task / timeoutで隔離します。

Road snapshotとRailway Operations snapshotが1 MiBのsingle-frame上限を超える場合は、送信前に検出してsubscription-localなstructured Errorへ変換します。Railway Infrastructureはentity境界でchunkできます。

Protocol 1.xの`SubscribeArea`や2D rectangle互換経路は提供しません。

## ローカル起動

```bash
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

詳細なlifecycle、publish read model、revision、oversize policyは[`../../docs/architecture/headless-server.md`](../../docs/architecture/headless-server.md)、wire contractは[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)を参照してください。
