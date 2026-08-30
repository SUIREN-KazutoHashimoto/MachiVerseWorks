# Specifications

都市シミュレーションとしての**現行仕様**を分野別に管理します。

想定する主な分野:

- Agent / citizen
- Building / POI
- city generation
- road / traffic
- pedestrian
- public transit
- rail
- logistics / industry
- power
- economy

現在の基盤仕様:

- [`world-coordinate-system.md`](world-coordinate-system.md): ネイティブ3D座標、単位、境界、renderer/audio写像の正本
- [`simulation-core-poc.md`](simulation-core-poc.md): 現行Simulation Core基盤仕様
- [`building-poi.md`](building-poi.md): Building / POIのstable ID、3D state、参照整合性仕様
- [`road-network.md`](road-network.md): Road / Lane / access topology仕様
- [`routing.md`](routing.md): Road / Lane由来の3D routing、cost、constraint、determinism、cache仕様
- [`pedestrian-simulation.md`](pedestrian-simulation.md): Road Network派生walking graph、route、fixed-tick歩行、crossing / occupancy仕様
- [`headless-server-poc.md`](headless-server-poc.md): Headless Server / 3D subscription基盤仕様
- [`web-client-poc.md`](web-client-poc.md): Web Client + Audio Client Foundationの3D基盤仕様
- [`save-data.md`](save-data.md): versioned Save Data、checkpoint state、validation仕様

Protocolのbinary layoutと互換性ルールは[`../architecture/protocol.md`](../architecture/protocol.md)を正本とします。Protocol 1.x / `SubscribeArea` / 2D座標を現行契約として扱いません。

このディレクトリではWhat / Whyを中心に記述し、実装方法の詳細は`docs/architecture/`へ分離します。過去Phaseの歴史的資料を現行契約と異なる形で残す必要がある場合は`docs/archive/`へ移します。
