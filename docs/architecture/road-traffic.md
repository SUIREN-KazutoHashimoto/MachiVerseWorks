# Road Traffic Architecture

Phase 13 Road Traffic Simulation を Simulation Core から Save / Protocol / Server / Web へ通す実装境界を記録する。

## State ownership

Vehicle の authoritative state は `SimulationWorld` に属する。

- `VehicleStore` が Vehicle の mutable state、stable ID、deterministic iteration order を保持する。
- `RoadTrafficTopology` が Road Network snapshot から Lane geometry / travel progress を派生する。
- `LaneOccupancyIndex` が Lane ごとの順序付き occupancy と Vehicle location lookup を保持する。
- `IntersectionControlStore` は交差点 entry permission の別責務であり、Phase 14 の policy を提供する。

Server / Protocol / Web Client は Vehicle state の正本にならない。

## Derived topology

Road Traffic は Road Network の mutable store を複製して別正本にしない。

Road topology が変更された場合、`RoadTrafficTopology` は Road Network snapshot から再構築される。Vehicle route は stable `LaneId` / `RoadSegmentId` / `LaneConnectionId` を参照する。

geometry 上の交差だけから Lane transition を生成せず、Phase 11 / 12 の明示 topology を再利用する。

## Tick ordering

`SimulationWorld.Step()` の主要順序は次の通り。

1. Agent update
2. Population trip planning
3. Vehicle update
4. Pedestrian update
5. Population trip completion observation
6. Simulation time commit

Vehicle update 内では、intersection intent を先に収集し、その tick の entry permission を確定してから Vehicle を stable ID 順に更新する。

各 Vehicle update は occupancy から一旦自身を外し、前走車と target Lane occupancy を評価して移動後に再登録する。これにより自身を leader と誤認せず、更新後 overlap を検出できる。

## Lane occupancy index

`LaneOccupancyIndex` は `Dictionary<LaneId, SortedSet<Entry>>` と `Dictionary<VehicleId, Location>` を組み合わせる。

Lane 内 entry は progress、次に Vehicle ID で順序付ける。leader lookup、前後 gap validation、remove / reinsert を全 Vehicle 全件走査から分離する。

Vehicle count が増加しても hot path に Agent ごとの Task や LINQ pipeline を導入しない。

## Vehicle state and pose

`VehicleStore` の mutable state は Route step index、step progress、speed、movement state、derived pose を保持する。

pose は current `RouteLaneStep` と `RoadTrafficTopology` から更新する。Protocol / Server のために別の自由座標 state を持たない。

snapshot は `VehicleSnapshot` として immutable に公開する。

## Car-following boundary

前走 Vehicle が存在する場合、bumper gap から minimum gap を差し引いた free gap を用いて advance と target speed を制限する。

acceleration / comfortable deceleration は Vehicle performance に従う。Road Traffic の最小 model は driver personality や高度な追越し判断を持たない。

## Lane change / transition

同一 RoadSegment 上で Route が別 Lane を要求する場合を最小 lane change とする。

次 Lane への進入前に occupancy gap を検証する。RoadSegment 間 transition では Route step の `ExitConnectionId` と intersection entry permission を利用する。

Phase 14 の signal / priority policy は `IntersectionControlStore` 側に閉じ込め、VehicleStore は「この Vehicle がこの connection へ今 tick 進入可能か」という結果だけを利用する。

## Checkpoint / Save boundary

`SimulationWorld.CreateCheckpoint()` は Vehicle store から next Vehicle ID、dimensions / performance、Route steps、route step index / progress、speed、movement state を immutable checkpoint へ materialize する。

restore は Road Network を先に復元し、`RoadTrafficTopology` を構築してから Vehicle state と occupancy を再構築する。

Save serializer は checkpoint と Save format 6+ の変換を担当し、VehicleStore を直接 serialize しない。

## Publish boundary

Server は `SimulationRuntime.CapturePublishSnapshot()` の lock 内で Vehicle snapshot array を取得し、その後の network processing は immutable published snapshot に対して行う。

`VehicleSnapshotMessagePlanner` は connection ごとの known Vehicle ID と current subscription result を比較し、spawn / update / remove を計画する。Simulation lock を WebSocket send の間保持しない。

## Web boundary

Web Client は `VehicleStore` に protocol message を適用し、`WorldView` が interpolation 後の pose を `THREE.InstancedMesh` へ反映する。

Phase 13 E2E は dedicated Server fixture を使い、交差点制御なしの複数独立 Lane で Road Network snapshot、Vehicle spawn、複数 update と位置変化、全 fixture Vehicle の `Arrived`、VehicleStore と `THREE.InstancedMesh` の count 一致を確認する。

## Performance boundary

Phase 13 benchmark は 1,000 / 10,000 / 100,000 Vehicle を 100 Vehicle / Lane へ決定的に分散する。

setup cost を tick 測定へ混ぜないため、Road topology を作成した checkpoint に Vehicle checkpoint を注入して restore した world を測定対象とする。

測定対象は full Simulation tick、Lane occupancy leader lookup の全 Vehicle sweep、full Vehicle snapshot materialization の3つとする。

baseline と runner 条件は [`../development/road-traffic-benchmark.md`](../development/road-traffic-benchmark.md) を正本とする。
