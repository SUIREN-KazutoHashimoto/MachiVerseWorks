# Specifications

都市シミュレーションとしての**現行仕様**を分野別に管理します。

現在の基盤仕様:

- [`world-coordinate-system.md`](world-coordinate-system.md): ネイティブ3D座標、単位、境界、renderer/audio写像の正本
- [`simulation-core-poc.md`](simulation-core-poc.md): 現行Simulation Core基盤仕様
- [`building-poi.md`](building-poi.md): Building / POIのstable ID、3D state、参照整合性
- [`road-network.md`](road-network.md): Road / Lane / access topology
- [`routing.md`](routing.md): Road / Lane由来の3D routing、cost、constraint、determinism、cache
- [`road-traffic.md`](road-traffic.md): Vehicle state、Lane occupancy、car-following、Route progress、Save / publish
- [`intersection-signal-control.md`](intersection-signal-control.md): movement / conflict、priority / yield、fixed signal、queue、downstream blocking
- [`population-daily-activity.md`](population-daily-activity.md): Household / Person、Need / Activity、daily schedule、Trip Request
- [`pedestrian-simulation.md`](pedestrian-simulation.md): walking graph、route、fixed-tick歩行、crossing / occupancy
- [`railway-infrastructure.md`](railway-infrastructure.md): Track / connection / block / Station / Platform / Depotの**静的authoritative topology**
- [`railway-operations.md`](railway-operations.md): Formation / Route / Timetable / Service / Train、Block / Platform ownership、delay、Depot lifecycleの**動的運行state**
- [`multimodal-transit.md`](multimodal-transit.md): 徒歩 / Bus / Taxi / Railwayの共通Journey、dispatch、Passenger、Save / Protocol
- [`optical-communication.md`](optical-communication.md): OpticalNode / FiberCable / OLT・ONU / backhaul、帯域・輻輳・簡易latency・障害復旧
- [`headless-server-poc.md`](headless-server-poc.md): Headless Server / 3D subscription基盤
- [`web-client-poc.md`](web-client-poc.md): Web Client + Audio Client Foundationの3D基盤
- [`save-data.md`](save-data.md): Save Format 11、checkpoint state、validation、migration

Railway InfrastructureとRailway Operationsは責務を分離する。前者はTrack/Station等のtopology正本、後者はそのstable IDを参照して動くTrain/Service stateである。Multimodal Transitはさらにその上でRoad Traffic / Railway Operationsを再利用し、mode間Journeyを所有する。Optical CommunicationはPower InfrastructureとBuilding / Establishmentのstable IDを再利用し、Phase 27のRadio Infrastructureへgeneric backhaul境界を提供する。

Protocolのbinary layoutと互換性ルールは[`../architecture/protocol.md`](../architecture/protocol.md)を正本とします。current Protocolは2.15で、Protocol 1.x / `SubscribeArea` / 2D座標を現行契約として扱いません。

このディレクトリではWhat / Whyを中心に記述し、実装方法の詳細は`docs/architecture/`へ分離します。過去Phaseの歴史的資料を現行契約と異なる形で残す必要がある場合は`docs/archive/`へ移します。
