# MachiVerseWorks.Simulation

都市シミュレーションのauthoritative stateとdeterministic fixed-tick進行を所有するC# class libraryです。

## Current scope

現在のSimulation基盤は次のdomainを所有します。

- Agent / 3D spatial index
- Building / POI
- Road Network / Lane / Road Access Point
- Road / Lane Routing
- Vehicle / Lane occupancy / car-following
- Intersection movement / fixed signal control
- Pedestrian network / walking / crossing
- Household / Person / Need / daily activity / Trip dispatch
- Railway Infrastructure: Track / Block / Station / Platform / Depot
- Railway Operations: Formation / Route / Timetable / Service / Train
- Multimodal Transit: Walk / Bus / Taxi / Railway Journey、Passenger、dispatch

stable ID、fixed tick、seeded deterministic state、3D snapshot、checkpoint境界を提供します。Web表示用の文字列やnetwork session stateは正本にしません。

## Boundary

SimulationはHTTP、WebSocket、ASP.NET Core、DOM、Three.js、Save JSON schemaを知りません。

- network contract: `MachiVerseWorks.Protocol`
- runtime / connection: `MachiVerseWorks.Server`
- Save Data mapping: `MachiVerseWorks.Persistence`
- presentation: `src/web`

外部へmutable Storeを露出せず、snapshot / checkpoint / command APIを境界にします。derived graph/indexは必要に応じて再構築し、authoritative stateと区別します。

現行仕様は[`../../docs/specifications/README.md`](../../docs/specifications/README.md)、state ownershipとhot path設計は[`../../docs/architecture/README.md`](../../docs/architecture/README.md)を参照してください。
