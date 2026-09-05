# Source

実行コードを責務単位のcomponent directoryへ配置します。

- `simulation/`: 通信層に依存しない authoritative Simulation Core
- `server/`: headless Server host。Observation Gateway と管理command境界をホストする実行・network boundary
- `protocol/`: client-server Protocol contract
- `persistence/`: Simulation checkpoint / Save Data mapping
- `view/`: read-only browser 3D View

`Gateway` は `server/` 内のread-only Observation責務です。Serverホスト全体をGatewayとは扱わず、authoritative mutationを伴う管理commandはGateway責務から分離します。

component directoryはrepository分離を意味しません。モノレポ内で責務と依存方向を明示し、必要になった場合に独立process / deploy unitへ切り出しやすい境界を維持します。
