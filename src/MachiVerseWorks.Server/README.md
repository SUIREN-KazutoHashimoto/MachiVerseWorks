# MachiVerseWorks.Server

Headless ServerはASP.NET Core / Kestrel上で1つの`SimulationWorld`をserver-authoritativeに実行し、HTTP health endpointとProtocol 2.x binary WebSocketを提供します。現在のServer Protocol上限は[`ProtocolVersion.Current`](../MachiVerseWorks.Protocol/ProtocolVersion.cs)の **2.16** です。

Serverはread-onlyな**Observation Gateway**と、authoritative mutationを扱う**Administration / Management command boundary**を分離します。

## Runtime boundary

- `appsettings.json`からlisten address / port、Simulation、Server policyを読む
- `/health`でserver / simulationの稼働状態を確認する
- hosted serviceでfixed-tick Simulationを実行する
- `/ws`で`Hello` / `HelloAck` negotiation後のbinary Protocolを処理する
- Browser WebSocketは`Server:AllowedWebSocketOrigins` allowlistでOriginを検証する
- `SubscribeVolume` / Inspect等のObservation Requestをread-only境界で処理する
- Observation read modelのspatial filtering / revision cache / request deduplication / snapshot deliveryをServer側で行う
- `Server:MaximumSubscriptionCellCount`でobservation subscription cell数を制限する
- Save起動時は復元した`SimulationWorld.Config`をscheduler / HelloAck / subscription guardの正本にする
- stdin管理Consoleはtransport-independentな`AdminCommand`境界を経由し、Simulation tickと同じauthoritative lockで直列化する
- Remote MCPは明示設定時だけ`/mcp`を公開し、同じ`AdminCommandQueue` / executorを再利用する
- 将来のManagement Clientはauthoritative command境界を利用し、read-only View moduleとは分離する

既定では`127.0.0.1:5080`をlistenし、Vite開発用の`http://127.0.0.1:5173`と`http://localhost:5173`をBrowser Originとして許可します。

## Observation Gateway

Observation GatewayはSimulationが公開したdetached read modelをView等のread-only clientへ配送します。

許可する責務:

- observation subscription
- explicit inspection
- spatial filtering
- snapshot / delta planning
- revision-driven static delivery
- Entity / Spatial / Static read-model cache
-同一requestのdeduplication
- 再利用可能なencoded payload cache
- reconnect / resync
- slow client isolation

禁止する責務:

- Simulation state mutation
- Activity / ETA / classification / schedule等の意味的再計算
- current observationからsemantic futureを予測すること
- Camera / LOD / View cacheをSimulation workload / fidelityへ反映すること

cacheはwall-clock TTLだけではなく、Simulation由来のtick / revision / generation markerを基準にstale判定します。

詳細は[`../../docs/architecture/observation-gateway.md`](../../docs/architecture/observation-gateway.md)を参照してください。

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

`world save`はSimulation lock中にcheckpointだけをcaptureし、serializationとfile I/Oはlock外で行います。`world load`はfile I/Oとdeserializeを先に終えてからworldをatomicに差し替えます。world差し替えやtopology mutationはread-model revisionを進め、Observation Gatewayの関連cache / connection-local delivery stateを再同期可能な状態へ移します。

詳細は[`../../docs/specifications/server-administration-console.md`](../../docs/specifications/server-administration-console.md)と[`../../docs/decisions/ADR-0005-server-administration-boundary.md`](../../docs/decisions/ADR-0005-server-administration-boundary.md)を参照してください。

## Remote MCP administration

Remote MCPは既定で無効です。有効化すると公式C# MCP SDKのStreamable HTTP transportを`/mcp`へ登録し、Bearer credentialごとに`read` / `write` / `destructive`のToolを分離します。Remote MCP adapterは`SimulationRuntime`を直接変更せず、authoritative操作を既存`AdminCommandParser` / `AdminCommandQueue` / `AdminCommandExecutorV2`へ渡します。

開発・閉域環境での最小設定例:

```bash
export Server__Mcp__Enabled=true
export Server__Mcp__ReadToken='replace-with-at-least-32-random-characters'
export Server__Mcp__WriteToken='replace-with-a-different-32-char-token'
export Server__Mcp__DestructiveToken='replace-with-another-distinct-token'
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

Browser由来のMCP Clientだけを許可する場合はexact Originを設定します。通常のnon-browser MCP Clientは`Origin`を送らないため、この設定は必須ではありません。

```bash
export Server__Mcp__AllowedOrigins='https://admin.example.com;https://ops.example.com'
```

Internet越しではClientからKestrelのHTTP originを直接公開せず、Cloudflare Tunnel等のtrusted reverse proxyでTLSを終端し、公開URLをHTTPS endpointにします。origin側はloopback / private network / firewallで保護し、proxyは認証・Protocol headerを転送し、`/mcp`をcacheしない設定にします。Bearer tokenは`appsettings.json`へ書かず、環境変数またはsecret storeから注入してください。

主なRemote MCP制限は`Server:Mcp`配下の`MaxRequestBytes`、`MaxConcurrentRequests`、`RequestsPerMinute`、`RequestTimeoutMilliseconds`、`MaxResultBytes`、`MaxLogEntries`、`MaxQueryItems`で調整できます。`simulation_save`は任意pathを受け取らず、`SaveDirectory`配下のsafe slotだけを使用します。server shutdown、`world load`、任意shell / process、任意Administration commandはRemote MCPへ公開しません。

詳細契約は[`../../docs/specifications/remote-mcp-administration.md`](../../docs/specifications/remote-mcp-administration.md)、実装境界は[`../../docs/architecture/remote-mcp-administration.md`](../../docs/architecture/remote-mcp-administration.md)、判断理由は[`../../docs/decisions/ADR-0006-remote-mcp-through-administration-boundary.md`](../../docs/decisions/ADR-0006-remote-mcp-through-administration-boundary.md)を参照してください。

## Published observation domains

negotiated minorに応じて次のread model / messageを配信します。message ID / binary layout / compatibilityの正本は[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)です。

| Minimum Protocol | Domain |
| --- | --- |
| 2.0 | Agent |
| 2.1 | Road Network |
| 2.2 | Pedestrian |
| 2.3 | Vehicle |
| 2.4 | Intersection Control |
| 2.5 | Population statistics / Person debug |
| 2.6 | Railway Infrastructure |
| 2.7 | Railway Operations |
| 2.8 | Multimodal Transit |
| 2.10 | Economy |
| 2.11 | Logistics / Freight |
| 2.12 | Power |
| 2.13 | Water / Sewer |
| 2.14 | Gas |
| 2.15 | Optical Communication |
| 2.16 | Radio / Spectrum |

Protocol 2.9は新しいServer→Client domain snapshotではなく、Client→Serverのread-only `ClearPersonInspection` Observation Requestを追加します。

1回のpublishではSimulation lock下でdetached / immutable read modelをcaptureし、可能な処理はlock外でcache / filtering / message planning / encoding / network I/Oを行います。各domainのspatial filtering、world-wide delivery、payload上限はdomainごとの現行contractに従います。

Road snapshotと一部dynamic snapshotが1 MiBのsingle-frame上限を超える場合は、対応codec / publisherの契約に従い送信前にstructured Errorへ変換します。Railway Infrastructureはentity境界でchunkできます。

Protocol 1.xの`SubscribeArea`や2D rectangle互換経路は提供しません。

## Roadmap boundary

- authoritative state / rule / Observation contract / command / Protocol / Save / Administration境界: [`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)
- read-only Browser View / Camera / Selection / Inspector / Historical viewing / Rendering LOD: [`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)
- editor / runtime control / configuration / Save / Addon管理UI: [`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)
- Dashboard分析・trend・heatmap等: 将来Analytics Listener / analysis clientとして別設計

## ローカル起動

```bash
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

詳細なlifecycle / Observation Gateway / revision / cache / oversize policyは[`../../docs/architecture/headless-server.md`](../../docs/architecture/headless-server.md)、wire contractは[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)を参照してください。
