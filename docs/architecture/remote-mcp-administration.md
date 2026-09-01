# Remote MCP Administration Architecture

## 目的

Phase 27では、既存Headless ServerへRemote Model Context Protocol（MCP）境界を追加する。ただし、authoritativeなAdministration経路を二重化しないことを最優先とする。

基本data flowは次のとおり。

```text
MCP Client
  -> HTTPS reverse proxy / tunnel
  -> Streamable HTTP /mcp
  -> RemoteMcpSecurityMiddleware
  -> MCP Tool adapter
  -> RemoteMcpAdminGateway
  -> AdminCommandParser
  -> AdminCommandQueue
  -> AdminCommandExecutorV2
  -> SimulationRuntime
```

MCP Toolは`SimulationRuntime`を直接受け取らず、Simulation内部Storeにも直接アクセスしない。readとmutationはPhase 20 Administration Consoleと同じvalidation、authoritative ordering、state ownershipを再利用する。

## Host境界

MCP Serverは`MachiVerseWorks.Server`内へ組み込み、公式`ModelContextProtocol.AspNetCore` packageを利用して`/mcp`へStreamable HTTP endpointを公開する。

`Server:Mcp:Enabled`の既定値は`false`とする。無効時はMCP serviceを登録せず、`/mcp`もmapしない。Remote MCP公開はdeployment側の明示的なopt-inである。

Phase 27のToolはserver-to-client samplingやelicitation等のsession-owned機能を使用しないため、HTTP transportはstatelessとして構成する。

## 認証・認可境界

pre-shared bearer credentialを3段階のscopeへ分離する。

| Credential | Claims | 主なTool |
| --- | --- | --- |
| read | `read` | status / version / diagnostics / logs / Entity inspect |
| write | `read`, `write` | readに加えてpause / step / resume / save / Entity add・update |
| destructive | `read`, `write`, `destructive` | 上記に加えて許可済みEntity remove |

設定tokenは32文字以上かつ相互に異なる値とする。runtimeでは比較用SHA-256 hashのみ保持し、raw tokenをlogへ出力しない。

MCP SDKのauthorization filterをToolへ適用し、scope不足のToolは`tools/list`から除外する。Clientが非表示Tool名を直接`tools/call`しても同じpolicyで拒否する。

## Browser Origin境界

Browser以外のMCP Clientは通常`Origin`を送信しないため、その場合はCORS処理を必要としない。

Browserから別Originへ接続する場合、`Authorization` headerによりpreflightが発生する。`RemoteMcpSecurityMiddleware`は次の順序で処理する。

1. `/mcp` requestかを判定する。
2. `Origin`が存在する場合は`AllowedOrigins`完全一致を検証する。
3. 許可済み`OPTIONS` preflightへCORS response headerを返し、Bearer認証前に`204`で終了する。
4. 通常requestではrequest size、Bearer認証、rate/concurrency制限、timeoutを適用する。
5. 許可済みOriginへ`Access-Control-Allow-Origin`を完全一致値で返す。

wildcard Originは使用しない。

## Tool surface

Remote MCP surfaceはlocal Administration Consoleより意図的に狭く保つ。

read Tool:

- `server_status`
- `server_version`
- `simulation_status`
- `diagnostics_metrics`
- `logs_query`
- `entity_query`

write Tool:

- `simulation_pause`
- `simulation_step`
- `simulation_resume`
- `simulation_save`
- `entity_write`

Destructive Tool:

- `entity_remove`

公開しない操作:

- Server shutdown / `stop` / `exit`
- `world load`
- generic Administration command execution
- shell / process execution
- 任意file path read/write
- Client disconnect control
- remote全Entity列挙

MCPから受け取る可変argumentは1要素ずつquoted Administration tokenへ変換する。Entity種別とoperationは固定allowlistを通過したものだけcommandへmappingする。

## Entity inspect境界

大規模worldで`<entity> list`を呼ぶと、Administration executorがsnapshot生成・sort・formattingを全件分実行する可能性がある。この処理をRemote read credentialから繰り返せる状態はresource isolation上不適切である。

このためPhase 27の`entity_query`はstable IDを必須とし、`show`相当の1 Entity inspectだけを公開する。大量データ取得は将来の専用bounded query/read-model境界で扱い、MCP adapterで全件結果を後段truncateする設計は採用しない。

## Mutation allowlist境界

Create / update / removeの可否を単一`MutableEntities`集合で表現せず、operationごとにallowlistを分離する。

Administration側が`add`のみ提供するFormation、Rail Route、Timetable、Service、TrainはRemote MCPでも`add`だけを許可する。update/removeをTool contract上許可してからexecutorで`invalid_argument`へ落とす構造を避け、remote capabilityとauthoritative implementationを一致させる。

## Save境界

`simulation_save`はpathではなくslot名を受け取る。slotは1〜64文字のASCII英数字、`.`, `_`, `-`に限定し、`.`と`..`を禁止する。

実pathは常に`Server:Mcp:SaveDirectory`配下へ生成し、既存`world save` commandへ渡す。Directory作成・アクセスの失敗はMCP adapter内でstable `io_error`へ変換する。

Remote MCPから`world load`は公開しない。authoritative world置換はPhase 27のremote trust boundaryより広いconfirmation / failure surfaceを持つためである。

## Bounded diagnostics / logs

MCP有効時だけbounded memory `ILoggerProvider`を登録する。`logs_query`はこのtailのみ検索し、filesystem上のlog fileへアクセスしない。

log resultが`MaxResultBytes`を超える場合、serialized JSON文字列を途中sliceしない。entry数を削減して再serializationし、常にvalid JSONを返す。

`diagnostics_metrics`もserialization後のサイズを確認し、完全なresultが上限を超える場合はstable rejectionを返す。

## Request isolation

`/mcp`境界では次を適用する。

- request body最大サイズ
- MCP全体の同時request数
- credential単位のrequests-per-minute
- request timeout / cancellation
- `Origin`存在時のexact allowlist
- Tool input / result size上限
- bounded log retention

slow / malformed ClientがSimulation全体へ無制限のresource負荷を波及させないことを目的とする。

## Queue cancellation

`RemoteMcpAdminGateway`はMCP requestのcancellation tokenを`AdminCommandRequest`へ保持させる。

`AdminCommandQueue`はrequestをexecutorへ渡す直前にtokenを確認し、すでにcancel済みならcompletionをcancelしてcommandを破棄する。これによりrequest timeout後にqueued mutationだけが遅れて適用される事象を防止する。

executorがすでに実行を開始したcommandへ汎用rollbackを提供するものではない。Phase 27で保証するのは「実行開始前のcancel済みqueue itemを実行しない」ことである。

## Reverse proxy / Cloudflare契約

Remote Clientは`https://server.example/mcp`のようなHTTPS URLへ接続する。TLSをtrusted reverse proxyまたはCloudflare Tunnelで終端する場合、Kestrel originはprivate networkまたはfirewallで保護する。

Deployment要件:

1. 外部へ公開するのはproxy / tunnelのHTTPS endpointだけとする。
2. Kestrel originへuntrusted networkから直接到達できないようにする。
3. Bearer tokenは環境変数またはsecret管理から注入する。
4. proxy側のrequest body / timeout制限をapplication側と同等以上に厳しくする。
5. `/mcp` responseをcacheしない。
6. `Authorization`, `Content-Type`, `Accept`, `MCP-Protocol-Version`およびMCP response headerを保持する。
7. Browser MCP Clientを許可する場合は必要なOriginだけを設定する。

Cloudflare Access等を外側の追加認証として利用してよいが、Phase 27のread/write/destructive scope認可を置き換えるものではない。

## Failure isolation

MCPは独立したmutation pathを作らず、既存のbounded `AdminCommandQueue`による直列化を維持する。不正request、timeout、scope不足、oversize input、unsupported operationはSimulation process停止や権限昇格へ波及させず、HTTP statusまたはstable Tool resultとして閉じ込める。
