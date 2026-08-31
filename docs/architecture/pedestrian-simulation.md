# Pedestrian Simulation Architecture

Phase 16の歩行者実装におけるstate ownership、derived network、tick処理、Server / Web境界を記述する。仕様上の振る舞いは[`../specifications/pedestrian-simulation.md`](../specifications/pedestrian-simulation.md)を正本とする。

## State ownership

`SimulationWorld`が歩行者のauthoritative ownerである。

- `RoadNetworkStore`: 道路正本
- `PedestrianNetworkStore`: Road Networkから派生したwalking graphとmanual crossing permission
- `IntersectionControlStore`: Vehicle movement / fixed-signal phase / per-tick entry grantを持つderived control layer
- `PedestrianStore`: Pedestrian stable ID、TripRequest、route progress、movement state
- `PedestrianSpatialIndex`: Pedestrian positionの3D subscription query用index

walking graphのNode / Edge / Crossing topology自体はSaveへ直接保存せず、Road Networkから決定的に再構築する。一方、`SetPedestrianCrossingOpen`で変更されるmanual crossing permissionはauthoritative mutable stateなのでcheckpoint / Saveへ保存する。Intersection control由来のautomatic permissionはRoad topology、TickCount、Vehicle entry grantから再導出できるため保存しない。checkpoint restoreではRoadを先に復元してwalking graphを再構築し、manual crossing permissionを適用してからPedestrian routeとprogressを復元する。

## Derived network lifecycle

RoadNode / RoadSegment / RoadAccessPoint変更時にderived networkをdirty化する。Pedestrianが存在する状態でこれらを変更すると保存済みroute legが無効化されるため、mutation自体を拒否する。

Lane / LaneConnectionはPedestrian graphのgeometry正本ではないため、walking graph invalidation対象にしない。ただしIntersection controlはLane / LaneConnectionから再構築され、crossingのautomatic gateはそのcontroller snapshotをtickごとに参照する。

## Stable ID mapping

通常のRoadNode ID `1..2^63-1`はその値をPedestrianNodeへそのまま対応させ、通常のRoadAccessPoint IDは上位bit domainへ写像する。これにより既存Save / derived IDとの互換性を維持する。

RoadNode / RoadAccessPointが`2^63`以上のfull `ulong` IDを使う場合は、source domainごとにstable hashを下位63bitへ写像し、同一snapshot内で既使用IDと衝突した場合はstable source-ID順にdeterministic probingする。RoadNode domainとAccessPoint domainは上位bitで分離したままなので相互衝突しない。同じRoad Networkからは入力列挙順に依存せず同じPedestrianNode IDを得る。

PedestrianEdge / Crossingはdomain byteと参照stable ID列から決定的hashを生成する。hash collision時に異なるentityが同じIDになった場合はsilent overwriteせず例外とする。

## Routing

`PedestrianNetworkStore.FindRoute`はDijkstra法を使用する。

- cost: 3D Euclidean edge length
- adjacency: stable edge ID順
- equal cost: predecessor edge / node stable IDでtie-break
- Building / POIが複数のFoot accessを持つ場合、全accessをmulti-source / multi-target候補として最短の組を選ぶ

stable IDが最小の入口だけを固定採用しないため、孤立した入口が先に作成されていても別の接続済み入口からrouteを構築できる。

通常tickでroute探索は行わず、Pedestrian生成またはcheckpoint restore時だけrouteを構築する。

## Tick hot path

`PedestrianStore`は作成時のstable ID順を保持する`orderedIds`を使い、tickごとの全Pedestrian配列化・再sortを行わない。occupancy dictionaryもtick間で再利用する。

1. 各Pedestrianを`(edgeId, canonicalBin)`へ登録する。
2. `canonicalBin`はedgeのstable node IDが小さい側を共通原点とし、逆方向routeでは`edgeLength - progress`へ正規化する。
3. 同一bin競合時は最小Pedestrian IDをownerにする。
4. `SimulationWorld`が次tickのIntersection controller stateからcrossing control gateを更新する。
5. Pedestrianをstable ID順に進める。
6. effective crossing permissionまたは移動先occupancyで停止判定する。
7. 移動後position / velocity / route progressと`PedestrianSpatialIndex`を更新する。

双方向routeでも同じ物理位置が同じoccupancy binへ写像される。全組合せ距離計算を行わないため、occupancy bookkeepingはPedestrian数に対して概ねO(n)である。Route遷移は1 tick内に通過したedge数に比例する。

## Spatial subscription

PedestrianはAgentとは別の`PedestrianSpatialIndex`へ位置を登録する。生成・tick移動・削除・checkpoint restoreに合わせてindexを同期する。

`CreatePedestrianSnapshot(WorldVolume)`は全Pedestrianを走査せず、まずvolumeに重なるcellから候補IDだけを取得し、最後に厳密な`WorldVolume.Contains`で絞り込む。これによりsubscription costは表示範囲外のPedestrian数へ直接比例しない。

## Crossing control boundary

`PedestrianNetworkStore`が保持するboolは**manual gate**であり、`SetPedestrianCrossingOpen`とSave/Restoreのownershipを持つ。`SimulationWorld`はwalking graph rebuild時に`PedestrianCrossingId -> RoadNodeId`をcacheし、tickごとに対応する`IntersectionControllerSnapshot`から**automatic control gate**を派生する。

```text
effective open
  = PedestrianNetworkStore manual permission
  AND SimulationWorld intersection-control permission
```

Automatic gateは次の通り。

- FixedSignal: 全movementがRedかつ`EntryGrantedThisTick == false`のall-red windowだけopen。
- Unsignalized: そのtickにVehicle entry grantが1件も無い場合だけopen。Vehicle grantをPedestrianより優先する。
- controller無し: automatic gateはopenとしてmanual permissionだけに委ねる。

PedestrianStore自身はIntersectionControlStoreへ依存せず、`Func<PedestrianCrossingId, bool>`としてeffective判定を受け取る。これによりmovement engineとtraffic-control implementationを直接結合しない。

`SetPedestrianCrossingOpen(false)`はautomatic stateに関係なく強制closeできる。`true`はmanual gateを解除するだけで、Green / YellowやVehicle entry grant中のunsafe crossingを強制openできない。

Crossing判定はincident edge間を切り替えるnode上のpoint transitionで行う。横断開始後のbody envelopeを信号phase跨ぎで保持する連続占有modelではない。

## Checkpoint / Save

`SimulationCheckpoint`はPedestrianの次ID、Pedestrian checkpoint配列、Pedestrian **manual** crossing permission配列を持つ。Format 3/4のlegacy SaveではPedestrian stateを空としてmigrationし、format 5ではPedestrian stateとmanual crossing permissionを保存する。

Restore順序:

1. Simulation config / time validation
2. Agent / Building / POI validation
3. Road topology validation
4. Pedestrian / crossing checkpoint validation
5. Road store restore
6. derived walking graph rebuild
7. manual crossing permission復元
8. Trip endpointの全Foot access候補からroute再計算
9. 保存されたleg index / progress / movement stateを適用し、Pedestrian spatial indexへ登録
10. 次tickでIntersection control permissionを再導出

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
