# Road Traffic Simulation Specification

Phase 13 Road Traffic Simulation の現行仕様を定義する。

## Scope

Road Traffic は、Phase 12 Routing が返す Lane sequence を Vehicle が固定 Simulation tick で走行するための正本 domain である。

Phase 13 の責務は次の通り。

- Vehicle の stable ID、寸法、性能、移動状態
- Route / Lane progress と 3D pose
- Lane occupancy と前走車追従
- Route に含まれる最小 Lane change / Lane transition
- traffic invariant と基礎 metrics
- checkpoint / Save Data
- Protocol / Server subscription / Web Client 描画

交差点の競合・信号現示は Phase 14、Person が移動する理由と mode dispatch は Phase 15、Freight 固有状態は Phase 21 の責務とする。

## Vehicle identity and state

Vehicle は `VehicleId` を持つ。ID は `ulong` の stable ID とし、生成済み Vehicle 間で再利用しない。

Vehicle の正本状態は Simulation Core に属し、最低限次を保持する。

- `VehicleDimensions`: length / width / height
- `VehiclePerformance`: maximum speed / maximum acceleration / comfortable deceleration / minimum gap / time headway
- Route steps
- current route step index
- current route progress
- speed
- `VehicleMovementState`

`VehicleMovementState` は次を使用する。

- `Driving`: Route 上を通常走行中
- `WaitingForTraffic`: 前走車、次 Lane occupancy、交差点 permission 等により進行できない
- `ChangingLane`: 同一 RoadSegment 上の別 Lane へ移る transition 中
- `Arrived`: Route 終端へ到達済み

`Arrived` Vehicle の速度は 0 とし、明示 remove されるまで stable state として保持できる。

## Route contract

Vehicle は Phase 12 の `RouteLaneStep` sequence を直接参照する。

各 step は `LaneId`、`RoadSegmentId`、start / end segment offset、step distance、estimated travel time、次 step へ進むための任意 `LaneConnectionId` を含む。

Vehicle は Route topology を生成し直さない。Road Network / Routing の stable ID と direction contract を再利用する。

## Fixed-tick movement

Vehicle update は `SimulationWorld.Step()` の固定 tick 内で行う。

1. 現在 Lane の speed limit と Vehicle performance から target speed を決める。
2. Lane occupancy から前走 Vehicle を取得する。
3. minimum gap / time headway を満たすよう target speed と advance distance を制限する。
4. acceleration / comfortable deceleration の範囲で speed を target へ近づける。
5. Route progress を進める。
6. step 終端では次 Lane の occupancy と entry permission を確認して transition する。
7. Route 終端へ到達したら `Arrived` へ遷移する。
8. 更新後に pose と invariant を検証する。

3D position と forward vector は RoadSegment geometry、Lane direction、segment offset から導出する。Vehicle が独立した任意 3D 座標を正本として Road geometry から乖離することは許可しない。

## Lane occupancy and car following

Lane occupancy は Lane ごとの順序付き index と Vehicle location lookup を持つ。

- 前走車検索を全 Vehicle の全件走査で実装しない。
- spawn / restore / Lane transition 時に必要 gap を満たさない occupancy は拒否する。
- tick 後に Vehicle 同士が重なる状態を許可しない。
- leader の速度と bumper gap を利用し、minimum gap を侵害する advance を抑制する。

traffic density が上昇した場合、Vehicle は `WaitingForTraffic` または低速状態へ自然に遷移できる。

## Lane transition

次 Route step が同一 RoadSegment 上の別 Lane の場合、最小 lane-change transition として扱う。

- Route が要求している Lane への変更だけを行う。
- target Lane の occupancy が安全でない場合は進入しない。
- 任意の追越し最適化、複数 Lane の自由選択、driver personality は Phase 13 の対象外とする。

RoadSegment 間 transition は `LaneConnectionId` がある場合、その明示 connection を使用する。geometry が交差しているだけの Lane へ暗黙に移らない。

## Intersection permission seam

Phase 13 の Vehicle transition は intersection entry permission を問い合わせる seam を持つ。

Phase 14 ではこの seam に movement conflict、priority / yield、fixed signal、downstream blocking を接続する。Phase 13 単独の Road Traffic contract は交差点制御 policy 自体を所有しない。

## Traffic invariants

少なくとも次を invariant とする。

- Vehicle ID は 0 ではなく重複しない。
- Route は空ではなく、Road/Lane topology と整合する。
- route step index / progress は有効範囲内である。
- speed、寸法、performance 値は finite かつ各 contract の有効範囲内である。
- Vehicle は current Lane geometry 上の pose を持つ。
- Lane direction に逆行する progress を生成しない。
- occupancy 上で Vehicle が重ならない。
- 到達不能な次 Lane へ強制 transition しない。

## Persistence

Vehicle state は Simulation checkpoint と Save Data に含める。

Save format 6 で next Vehicle ID、Vehicle ID、dimensions / performance、Route steps、route step index / progress、speed、movement state を保存する。

restore 後も stable ID、Route progress、speed、state を保持し、同一条件で継続可能であることを要求する。

## Protocol and Server

Vehicle wire contract は Protocol 2.3 で `VehicleSpawn` / `VehicleUpdate` / `VehicleRemove` として追加された。

Server は published Simulation snapshot から client subscription volume 内の Vehicle だけを選択し、connection ごとの既知 ID と比較して spawn / update / remove を生成する。

Simulation の mutable Vehicle store を network task から直接参照し続けない。

## Web Client

Web Client は Vehicle message を client-side store へ適用し、snapshot 間を補間して `THREE.InstancedMesh` で描画する。

- Simulation state の正本にはならない。
- Vehicle の 3D position / forward / dimensions を描画へ反映する。
- subscription 外へ出た Vehicle は remove message に従って client store から除去する。

## Metrics

Road Traffic は最低限 Vehicle count、active Vehicle count、total Lane kilometers、density (vehicles/km)、average speed、queue length を計測可能にする。

これらは Simulation behavior の観測値であり、Web Client が独自に再計算した値を正本としない。

## Validation evidence

Phase 13 closeout では次を独立して検証する。

- Simulation / Persistence / Protocol / Server / Web の既存 automated tests
- 交差点制御へ依存しない複数 Vehicle fixture の実 Server → Browser E2E
- 1,000 / 10,000 / 100,000 Vehicle の tick / occupancy leader lookup / full snapshot benchmark

benchmark の手順と baseline は [`../development/road-traffic-benchmark.md`](../development/road-traffic-benchmark.md) を正本とする。
