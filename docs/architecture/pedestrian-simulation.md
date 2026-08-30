# Pedestrian Simulation Architecture

Phase 16の歩行者実装におけるstate ownership、derived network、tick処理、Server / Web境界を記述する。仕様上の振る舞いは[`../specifications/pedestrian-simulation.md`](../specifications/pedestrian-simulation.md)を正本とする。

## State ownership

`SimulationWorld`が歩行者のauthoritative ownerである。

- `RoadNetworkStore`: 道路正本
- `PedestrianNetworkStore`: Road Networkから派生したwalking graph
- `PedestrianStore`: Pedestrian stable ID、TripRequest、route progress、movement state

`PedestrianNetworkStore`はSaveへ直接保存しない。Road Networkから決定的に再構築できるため、checkpoint restoreではRoadを先に復元し、その後walking graphを再構築してPedestrian routeを再計算する。保存するのはrouteの`legIndex` / `progressMeters`等、再構築後に継続に必要なstateである。

## Derived network lifecycle

RoadNode / RoadSegment / RoadAccessPoint変更時にderived networkをdirty化する。Pedestrianが存在する状態でこれらを変更すると保存済みroute legが無効化されるため、mutation自体を拒否する。

Lane / LaneConnectionはPedestrian graphのgeometry正本ではないため、Phase 16ではwalking graph invalidation対象にしない。

## Stable ID mapping

RoadNodeはstable ID値をPedestrianNodeへ対応させる。RoadAccessPoint由来nodeは上位bit domainを分離してRoadNodeと衝突させない。

PedestrianEdge / Crossingはdomain byteと参照stable ID列から決定的hashを生成する。hash collision時に異なるentityが同じIDになった場合はsilent overwriteせず例外とする。

## Routing

`PedestrianNetworkStore.FindRoute`はDijkstra法を使用する。

- cost: 3D Euclidean edge length
- adjacency: stable edge ID順
- equal cost: predecessor edge / node stable IDでtie-break

通常tickでroute探索は行わず、Pedestrian生成またはcheckpoint restore時だけrouteを構築する。

## Tick hot path

`PedestrianStore.Step`はPedestrian ID順に処理する。

1. 各Pedestrianを`(edgeId, floor(progress / 0.75m))`へ登録する。
2. 同一bin競合時は最小Pedestrian IDをownerにする。
3. Pedestrianをstable ID順に進める。
4. crossing permissionまたは移動先occupancyで停止判定する。
5. 移動後position / velocity / route progressを更新する。

全組合せ距離計算を行わないため、occupancy bookkeepingはPedestrian数に対して概ねO(n)である。Route遷移は1 tick内に通過したedge数に比例する。

## Crossing boundary

Crossing permissionは`PedestrianNetworkStore`にbool stateとして保持し、`SimulationWorld.SetPedestrianCrossingOpen`から更新する。Phase 16ではTraffic Signal modelへの強い依存を置かず、後続のSignal実装がこのAPIへ許可状態を供給できる境界とする。

## Checkpoint / Save

`SimulationCheckpoint`はPedestrianの次IDとPedestrian checkpoint配列を持つ。Format 3/4のlegacy SaveではPedestrian stateを空としてmigrationし、format 5では必須collectionとして扱う。

Restore順序:

1. Simulation config / time validation
2. Agent / Building / POI validation
3. Road topology validation
4. Pedestrian checkpoint validation
5. Road store restore
6. derived walking graph rebuild
7. Trip endpointからroute再計算
8. 保存されたleg index / progress / movement stateを適用

## Protocol / Server

Protocol 2.2でPedestrian messageを追加する。Serverの`ClientSubscriptionState`はAgentとPedestrianの既知ID集合を別々に持つ。

snapshot publish時は同じ3D `SubscribeVolume`に対して:

- 新規可視: `PedestrianSpawn`
- 継続可視: `PedestrianUpdate`
- 範囲外 / 削除: `PedestrianRemove`

を生成する。Protocol 2.1以下へPedestrian messageは送らない。

## Web Client

`PedestrianStore`が受信snapshotのprevious/current positionと受信時刻を保持する。描画時刻に合わせて線形補間し、`WorldView`の専用`PedestrianRenderer`へpacked position bufferを書き出す。

描画は`THREE.InstancedMesh`を利用し、PedestrianごとのMesh object生成を避ける。Simulation `(X,Y,Z)`は既存契約どおりThree.js `(X,Z,Y)`へ写像する。
