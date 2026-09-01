# ADR-0006: Remote MCPをAdministration境界経由で実行する

## Status

Accepted

## Context

Phase 20では、command parsing、validation、bounded command queue、authoritative mutationを直列実行するexecutorからなるAdministration境界を導入した。この境界が`SimulationRuntime`へ対する管理操作の正本である。

Phase 27では、AI assistantや運用ToolからRemote MCP経由でServerを参照・管理できる必要がある。

MCP ToolからSimulationへ直接接続するadapterは実装量を減らせる一方、次の問題を生む。

- authoritative mutation pathが二重化する
- Administration側のvalidationを複製する必要がある
- localとremoteで操作仕様が乖離しやすい
- deterministicなmutation orderingを弱める
- 将来の権限・監査・制限を2系統で維持する必要がある

またRemote accessでは、認証、least privilege、destructive operationのconfirmation、request resource制限、reverse proxy deployment、AIからの誤ったTool invocationを明示的に考慮する必要がある。

## Decision

MCPはtransportおよびTool adaptation layerとしてのみ扱う。

Administration相当のauthoritative read / mutationは、固定allowlistに基づくAdministration commandへ変換し、`AdminCommandParser`、`AdminCommandQueue`、`AdminCommandExecutorV2`を経由して実行する。

MCP Toolは`SimulationRuntime`へ直接依存せず、generic Administration command executorも公開しない。

権限はread / write / destructiveの3scopeへ分離し、MCP discoveryとinvocationの両方で認可する。destructiveなEntity removeにはscopeに加えて明示的な`confirm=true`を要求する。

MCPは既定で無効とし、公式C# MCP ASP.NET Core SDKによるStreamable HTTPを`/mcp`へ公開する。Remote deploymentではapplication自身またはtrusted reverse proxy / tunnelでHTTPSを終端し、plaintext originをuntrusted networkへ公開しない。

Remote surfaceからServer shutdown、world load、任意shell / process実行、任意filesystem access、generic Administration commandを除外する。

大規模Entityの全件列挙はRemote MCP read surfaceへ公開しない。`entity_query`はstable IDによる単一Entity inspectへ限定し、高volume queryが必要になった場合は共有Administration/read-model境界自体へbounded query contractを追加する。

Create / update / remove capabilityはoperation別allowlistで管理し、Administration側に実装されていないoperationをMCP capabilityとして公開しない。

Remote requestがtimeoutまたはcancelされた場合、そのcancellation tokenを`AdminCommandRequest`へ伝播し、まだqueue待ちのcommandはexecutorへ渡さない。Clientへtimeoutを返した後からmutationだけが遅延適用される状態を許可しない。

## Consequences

### Positive

- Local ConsoleとRemote MCPが1つのauthoritative command / validation pathを共有できる
- Simulation mutation orderingを既存bounded queueで直列化できる
- Simulation domain projectへMCP依存を持ち込まない
- Remote security reviewの対象を明示的な小さいTool / operation allowlistへ限定できる
- read / write / destructive capabilityをcaller scopeに応じてdiscovery段階から制御できる
- Administration validationの改善がRemote MCPにも適用される
- timeoutしたqueued mutationの遅延実行を防止できる
- read credentialからの無制限Entity全件formattingを防止できる

### Negative

- MCP resultはPhase 20 Administrationのtext-oriented result形状に影響される
- structured MCP argumentとAdministration tokenの間にtranslation / quotingが必要になる
- 大量データexportには適さない
- pre-shared bearer credentialのrotationとsecret管理をdeployment側で行う必要がある
- operation allowlistをAdministration capability変更時に同期する必要がある

## Follow-up

将来よりrichなstructured Administration responseやbounded Entity検索が必要になった場合、MCPだけに専用のSimulation accessを追加せず、共有Administration / read-model境界そのものを拡張する。

OAuth / OIDCが必要になった場合はcredential authentication layerを置き換えてよいが、read / write / destructive policyおよびTool→Administration mappingは維持する。
