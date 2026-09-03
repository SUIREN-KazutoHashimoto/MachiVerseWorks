# Remote MCP Administration 仕様

## 対象範囲

Phase 27では、稼働中のMachiVerseWorks ServerをModel Context Protocol（MCP）のStreamable HTTP経由で安全に参照・管理するRemote Administration境界を提供する。

公開endpointは`/mcp`とする。MCPは既定で無効であり、有効化した場合も匿名アクセスは提供しない。

Remote MCPはPhase 20で確立したAdministration境界を再利用する。MCP adapterが`SimulationRuntime`やSimulation内部Storeを直接変更してはならない。

## 設定

設定は`Server:Mcp`配下に置き、通常のASP.NET Core configuration mappingを利用する。本番credentialは設定ファイルへ書き込まず、`Server__Mcp__ReadToken`のような環境変数またはsecret管理機構から注入する。

| Key | 既定値 | 契約 |
| --- | --- | --- |
| `Enabled` | `false` | MCP serviceと`/mcp`を明示的に有効化する |
| `ReadToken` | 未設定 | read credential。32文字以上 |
| `WriteToken` | 未設定 | read + write credential。32文字以上 |
| `DestructiveToken` | 未設定 | read + write + destructive credential。32文字以上 |
| `AllowedOrigins` | 空 | `Origin`を送信するClient向けのHTTP(S) Origin完全一致allowlist。`;`区切り |
| `MaxRequestBytes` | `262144` | MCP request body上限 |
| `MaxConcurrentRequests` | `8` | MCP全体の同時処理request上限 |
| `RequestsPerMinute` | `120` | credential単位の固定window rate limit |
| `RequestTimeoutMilliseconds` | `30000` | request timeout / cancellation上限 |
| `MaxResultBytes` | `65536` | Tool resultの最大サイズ |
| `MaxLogEntries` | `512` | memory上のlog tail保持件数 |
| `MaxQueryItems` | `200` | log query等の1回あたり最大件数 |
| `SaveDirectory` | `data/mcp-saves` | MCP save先としてServerが管理するroot |

MCPを有効化する場合、最低1つのcredentialを設定する。複数scopeのtokenを設定する場合は相互に異なる値でなければならない。

## 認証・認可

MCP requestは`Authorization: Bearer <token>`を使用する。

権限は次の3段階に分離する。

- read: read-only Toolのみ
- write: readに加えて運転操作および許可済みmutation
- destructive: writeに加えて明示的に許可したremove操作

認可はTool実行時だけでなくMCP discoveryにも適用する。read credentialからwrite/destructive Toolを`tools/list`で見せず、非表示Tool名を直接`tools/call`しても認可を迂回できないことを契約とする。

## Browser / CORS

`Origin` headerを送らない通常のMCP ClientはCORS処理を必要としない。

Browserから別Originの`/mcp`へ接続する場合、`Authorization`を伴うrequestの前にCORS preflightが発生する。`Server:Mcp:AllowedOrigins`に完全一致するOriginについてのみ、認証前の`OPTIONS` preflightを許可し、必要なMCP headerとHTTP methodを返す。

許可されていないOriginはpreflightを含めて`403`とする。`Access-Control-Allow-Origin: *`は使用しない。

## Read Tool

### `server_status`

既存Administrationの`status`を呼び出し、authoritative tick / pause stateと主要countを返す。

### `server_version`

既存`version` commandを通じて実行中application versionを返す。

### `simulation_status`

`simulation status`を通じてtick、pause state、tick rateを返す。

### `diagnostics_metrics`

既存のbounded `E2eMetrics` snapshotを返す。serialization後の結果が`MaxResultBytes`を超える場合、途中でJSONを切断せずstableな`result_too_large` rejectionを返す。

### `logs_query`

Remote MCP境界が明示的に生成したsanitized eventだけを保持するmemory上のbounded log tailを検索する。一般`ILogger`出力はこのtailへ自動転送しない。

- `limit`: 1から`MaxQueryItems`
- `contains`: categoryまたはmessageへのcase-insensitive filter

任意log fileの読み取りは提供しない。結果が`MaxResultBytes`を超える場合はentry数を減らして再serializationし、常に完全なJSONを返す。

### `entity_query`

allowlist済みEntity typeとstable IDを受け取り、既存Administrationの`show`へmappingする。

Remote MCPからの無制限な`list`は提供しない。大規模worldでread credentialのみから全Entity snapshot生成・sort・formattingを誘発できないよう、Remote MCPのEntity queryは1 Entityのinspectに限定する。

対象にはAgent、Building、POI、Road Infrastructure、Railway Infrastructure / Operations、Vehicleのread、Formation、Rail Route、Timetable、Service、Trainを含む。

## Write Tool

### `simulation_pause`

`simulation pause`へmappingする。

### `simulation_step`

`simulation step <count>`へmappingし、`count`は1〜10000に制限する。

### `simulation_resume`

`simulation resume`へmappingする。

### `simulation_save`

任意pathではなく安全なslot名のみを受け取る。実pathは`<SaveDirectory>/<slot>.mvw`としてServer側で生成し、非上書きの`world save-new`へmappingする。既存slotが存在する場合は上書きせずstable `conflict`を返す。

slotは1〜64文字のASCII英数字、`.`, `_`, `-`のみとし、`.`と`..`は禁止する。`world load`はRemote MCPへ公開しない。

SaveDirectory作成・アクセスに失敗した場合はSDK例外をそのまま返さず、stable code `io_error`へ変換する。

### `entity_write`

allowlist済みEntity、operation、最大32個のbounded argumentを受け取る。各argumentは1つのquoted Administration tokenへ変換してから既存Parserへ渡す。

operation allowlistはEntity単位ではなくoperation単位で定義する。

- `add`: Administration側にcreate/addが存在するEntityのみ
- `update`: Administration側にupdateが存在するEntityのみ

Formation / Rail Route / Timetable / Service / Trainは現行Administration境界では`add`のみのため、Remote MCPから`update`を許可しない。Vehicle spawnおよびconnection controlはgeneric mutation allowlistへ含めない。

## Destructive Tool

### `simulation_save_overwrite`

`destructive` scopeと`confirm=true`を必須とし、`<SaveDirectory>/<slot>.mvw`へ`world save`で明示的に上書き保存する。通常の`simulation_save`から既存slotの破壊的更新を分離する。

### `entity_remove`

`destructive` scopeと`confirm=true`の両方を必須とする。remove allowlistはAdministration側にremoveが実装されているEntityだけで構成し、`entity_write`と同じargument数・長さ・control character制約を適用する。

## Queue cancellation

Remote request timeoutまたはClient disconnectによりrequestのcancellation tokenがcancelされた場合、まだ`AdminCommandQueue`で実行待ちのcommandは実行しない。

特にmutationについて、Clientへtimeoutを返した後から遅延実行される状態を禁止する。これによりClient retryによる二重create/removeを防止する。

すでにexecutorがcommand実行を開始した後のatomic operationを途中でrollbackする契約ではない。cancellationは「実行開始前のqueued commandを破棄する」境界として扱う。

## Stable MCP result

Administration-backed Toolは次のstructured resultを返す。

```json
{
  "success": true,
  "code": "ok",
  "message": "..."
}
```

stable codeには`ok`, `invalid_syntax`, `unknown_command`, `invalid_argument`, `not_found`, `conflict`, `invalid_state`, `queue_full`, `io_error`, `internal_error`を含む。MCP固有のcodeとして`confirmation_required`, `result_too_large`を使用する。

validationで拒否したTool callはMCP transport errorへ変換せず、`success=false`とstable codeを持つstructured resultとして返す。

## セキュリティ要件

Remote MCPから次の能力を公開してはならない。

- 任意shell / executable / process実行
- 任意Administration command実行
- Server shutdown
- 任意filesystem read/write path
- authoritative world load / replacement
- `AdminCommandQueue` / `AdminCommandExecutorV2`を迂回するmutation
- 制限なしのEntity全件列挙

Bearer tokenのraw値をServer log、MCP result、source-controlled default configurationへ記録しない。

## Deployment要件

Client-facing MCP URLはHTTPSとする。Cloudflare等のreverse proxyでTLS terminationする場合、Kestrel originをprivate networkまたはfirewallで保護し、untrusted networkから直接到達できない構成とする。

reverse proxyは`Authorization`およびMCP protocol headerを保持し、`/mcp`をcacheしてはならない。proxy側のbody size / timeout制限はapplication側と同等以上に厳しく設定する。
