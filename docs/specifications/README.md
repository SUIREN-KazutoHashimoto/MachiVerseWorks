# Specifications

都市シミュレーションとしての**現行仕様**を分野別に管理します。

このディレクトリでは What / Why を中心に記述し、実装方法の詳細は `docs/architecture/` へ分離します。将来実装のTask状態は仕様書へ混ぜず、Simulation側は[`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)、read-only View側は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)、管理・編集UIは[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)を正本とします。

SimulationとViewのread-only境界、Observation Gateway、cache方針は[`../architecture/observation-gateway.md`](../architecture/observation-gateway.md)を参照してください。

## Foundation / Client / Server

- [`world-coordinate-system.md`](world-coordinate-system.md): ネイティブ3D座標、単位、境界、renderer / audio写像
- [`simulation-core-poc.md`](simulation-core-poc.md): 現行Simulation Core基盤仕様
- [`headless-server-poc.md`](headless-server-poc.md): Headless Server / 3D subscription基盤
- [`server-administration-console.md`](server-administration-console.md): Server Administration commandとauthoritative control境界
- [`remote-mcp-administration.md`](remote-mcp-administration.md): Remote MCPの認証・scope・Tool・制限・HTTPS deployment契約
- [`web-client-poc.md`](web-client-poc.md): read-only Web View + Audio Client Foundationの3D基盤
- [`save-data.md`](save-data.md): checkpoint state、validation、migration、Save Data互換性

## World / City Foundation

- [`world-environment-terrain.md`](world-environment-terrain.md): deterministic Global Environment、Detailed 3D Terrain、GeographicFeature、自然地名、Save / Protocol境界

## City / Mobility / Population

- [`building-poi.md`](building-poi.md): Building / POIのstable ID、3D state、参照整合性
- [`road-network.md`](road-network.md): Road / Lane / access topology
- [`routing.md`](routing.md): Road / Lane由来の3D routing、cost、constraint、determinism、cache
- [`road-traffic.md`](road-traffic.md): Vehicle state、Lane occupancy、car-following、Route progress、Save / publish
- [`intersection-signal-control.md`](intersection-signal-control.md): movement / conflict、priority / yield、fixed signal、queue、downstream blocking
- [`population-daily-activity.md`](population-daily-activity.md): Household / Person、Need / Activity、daily schedule、Trip Request
- [`pedestrian-simulation.md`](pedestrian-simulation.md): walking graph、route、fixed-tick歩行、crossing / occupancy
- [`railway-infrastructure.md`](railway-infrastructure.md): Track / connection / block / Station / Platform / Depotの静的authoritative topology
- [`railway-operations.md`](railway-operations.md): Formation / Route / Timetable / Service / Train、Block / Platform ownership、delay、Depot lifecycleの動的運行state
- [`multimodal-transit.md`](multimodal-transit.md): 徒歩 / Bus / Taxi / Railwayの共通Journey、dispatch、Passenger、Save / Protocol

## Economy / Logistics / Infrastructure

- [`economy.md`](economy.md): Company / Employment / Household economyとeconomic state
- [`logistics-freight.md`](logistics-freight.md): Freight demand、shipment、物流routing / dispatchと既存交通domainの連携
- [`power-infrastructure.md`](power-infrastructure.md): Power network、supply / demand、service / outageと都市entityへの供給
- [`water-sewer-infrastructure.md`](water-sewer-infrastructure.md): Water / Sewer network、flow、service / outageと都市entityへの接続
- [`gas-infrastructure.md`](gas-infrastructure.md): Gas network、supply / demand、service / outageと都市entityへの接続
- [`optical-communication.md`](optical-communication.md): OpticalNode / FiberCable / OLT・ONU / backhaul、帯域・輻輳・簡易latency・障害復旧
- [`radio-spectrum.md`](radio-spectrum.md): 方式非依存Radio Site / Antenna / Tx / Rx / Emission、周波数・伝搬・遮蔽・干渉・SINR

Railway InfrastructureとRailway Operationsは責務を分離します。前者はTrack / Station等のtopology正本、後者はそのstable IDを参照して動くTrain / Service stateです。Multimodal Transitはさらにその上でRoad Traffic / Railway Operationsを再利用し、mode間Journeyを所有します。Optical CommunicationはPower InfrastructureとBuilding / Establishmentのstable IDを再利用し、Radio & Spectrum Foundationへgeneric backhaul境界を提供します。

Protocolの現行version、binary layout、互換性ルールは[`../architecture/protocol.md`](../architecture/protocol.md)を正本とします。Save formatの現行versionとmigrationは[`save-data.md`](save-data.md)を正本とし、索引READMEでは番号を重複管理しません。

過去Phaseの歴史的資料を現行契約と異なる形で残す必要がある場合は `docs/archive/` へ移します。
