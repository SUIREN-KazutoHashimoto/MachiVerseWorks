# Architecture

MachiVerseWorks の現行技術アーキテクチャを管理します。

仕様上の振る舞い（What / Why）は `docs/specifications/`、ここでは責務分離・state ownership・data flow・実装境界（How）を中心に記述します。

## Core / Platform

- [`overview.md`](overview.md): 全体アーキテクチャと主要コンポーネント境界
- [`simulation-core.md`](simulation-core.md): Simulation Coreのstate ownershipとhot path
- [`protocol.md`](protocol.md): Server / Web Client間binary protocolのversioningとwire layout
- [`headless-server.md`](headless-server.md): Headless Serverのlifecycle、WebSocket、command queue、snapshot publish
- [`server-administration-console.md`](server-administration-console.md): Admin command queue、authoritative runtime境界、Remote Administration再利用境界
- [`web-client.md`](web-client.md): Web Clientのconnection、EntityStore、subscription、rendering
- [`audio.md`](audio.md): AudioEngine、mixer、positional audio、Ambient Zone、voice virtualization
- [`persistence.md`](persistence.md): Simulation checkpoint、Save Data serializer、validation、format evolution
- [`localization.md`](localization.md): 多言語対応を見越した言語境界

## Mobility / Population / Economy

- [`road-network.md`](road-network.md): Road Network topology、spatial index、access boundary
- [`routing.md`](routing.md): Road / Lane derived routing graph、deterministic search、cache、invalidation
- [`road-traffic.md`](road-traffic.md): Vehicle state ownership、Lane occupancy、tick、Save / publish / Web境界
- [`intersection-signal-control.md`](intersection-signal-control.md): movement conflict、entry arbitration、signal、publish / Web debug境界
- [`population-daily-activity.md`](population-daily-activity.md): Person / Household state ownership、daily planner、Trip dispatch
- [`pedestrian-simulation.md`](pedestrian-simulation.md): derived walking graph、routing、tick、crossing / occupancy、Server / Web境界
- [`railway-infrastructure.md`](railway-infrastructure.md): Railway authoritative topology、static publish、Web 3D境界
- [`railway-operations.md`](railway-operations.md): Train / Service / Timetable、block / platform ownership、動的運行state
- [`multimodal-transit.md`](multimodal-transit.md): 共通Journey、Bus / Taxi / Railway再利用、Passenger境界
- [`economy.md`](economy.md): Company / Employment / Household economy、economic tick、publish / benchmark境界
- [`logistics-freight.md`](logistics-freight.md): Freight demand、shipment、物流routing / dispatchと既存交通domainの連携

## Urban Infrastructure

- [`power-infrastructure.md`](power-infrastructure.md): Power topology、supply / demand、service state、他domainへの電力供給境界
- [`water-sewer-infrastructure.md`](water-sewer-infrastructure.md): Water / Sewer topology、flow / service state、都市entityとの接続境界
- [`gas-infrastructure.md`](gas-infrastructure.md): Gas topology、supply / demand、service state、都市entityとの接続境界
- [`optical-communication.md`](optical-communication.md): Optical topology、capacity-aware routing、power dependency、backhaul境界

現行仕様との対応を変更した場合は、関連する `docs/specifications/` と必要な ADR も同時に同期します。
