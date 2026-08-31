# Architecture

MachiVerseWorks の技術アーキテクチャを管理します。

主な対象:

- Simulation Core と Server の責務分離
- Server / Browser 間プロトコル
- Simulation tick と snapshot 配信
- spatial interest management
- threading / job system
- data model / memory layout
- save / load
- localization / internationalization boundary
- Browser presentation / audio boundary

主要文書:

- [`overview.md`](overview.md): 全体アーキテクチャ
- [`simulation-core.md`](simulation-core.md): Simulation Core 最小 PoC の state ownership と hot path
- [`road-network.md`](road-network.md): Road Network topology / spatial index / access boundary
- [`routing.md`](routing.md): Road / Lane derived routing graph、deterministic search、LRU cache、invalidation
- [`road-traffic.md`](road-traffic.md): Vehicle state ownership、Lane occupancy、tick、Save / publish / Web境界
- [`intersection-signal-control.md`](intersection-signal-control.md): movement conflict、entry arbitration、fixed signal、publish / Web debug boundary
- [`population-daily-activity.md`](population-daily-activity.md): Person / Household state ownership、daily planner、Trip dispatch、Save / Protocol / Web境界
- [`pedestrian-simulation.md`](pedestrian-simulation.md): derived walking graph、routing、tick、crossing / occupancy、Server / Web boundary
- [`railway-infrastructure.md`](railway-infrastructure.md): Railway authoritative topology、Save Format 8、Protocol 2.6、static publish、Web 3D boundary
- [`railway-operations.md`](railway-operations.md): Train / Service / Timetable、block / platform、Save Format 9、Protocol 2.7
- [`multimodal-transit.md`](multimodal-transit.md): 共通Journey、Bus / Taxi Road Traffic再利用、Passenger、Save Format 10、Protocol 2.8
- [`server-administration-console.md`](server-administration-console.md): stdin / AdminCommandQueue / authoritative runtime boundary、read-model invalidation、Remote Admin再利用境界
- [`economy.md`](economy.md): Company / Employment / Household economy、economic tick、Save Format 11、Protocol 2.10、Web debug / benchmark境界
- [`protocol.md`](protocol.md): Server / Web Client 間 binary protocol の versioning と wire layout
- [`headless-server.md`](headless-server.md): Headless Server の lifecycle、WebSocket、command queue、snapshot publish
- [`web-client.md`](web-client.md): Web Clientのconnection、EntityStore、subscription、rendering
- [`audio.md`](audio.md): AudioEngine、mixer、positional audio、Ambient Zone、voice virtualization
- [`persistence.md`](persistence.md): Simulation checkpoint、Save Data serializer、validation、format evolution
- [`localization.md`](localization.md): 多言語対応を見越した言語境界

仕様上の振る舞いそのものは `docs/specifications/` に記述します。
