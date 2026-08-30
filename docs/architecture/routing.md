# Routing Foundation アーキテクチャ

## 1. 責務境界

Routing Foundationは`MachiVerseWorks.Simulation`に閉じる。

```text
SimulationWorld
  ├─ RoadNetworkStore          Road / Lane topologyの正本
  └─ RoadRouter               derived routing graph / search / cache
       └─ RouteResult          immutable public result
```

`RoadRouter`はRoad Networkの正本を所有しない。Road topology mutationは従来どおり`RoadNetworkStore`へ行い、成功時に`SimulationWorld`が`RoadRouter.Invalidate()`を呼ぶ。

## 2. Rebuild lifecycle

`RoadRouter`は初期状態とtopology mutation後をdirtyとして扱う。

`FindRoadRoute`時にdirtyなら`RoadNetworkStore.CreateSnapshot()`を一度だけ取得してderived graphを再構築する。dirtyでなければsnapshotを再生成しない。

再構築時に保持する主なデータは次のとおり。

- Lane stable ID → Lane geometry / speed / direction
- Lane stable ID → outgoing `LaneConnectionSnapshot[]`
- LaneConnection stable ID → connection
- stable ID順のLane配列

outgoing connectionはstable ID順に並べて保持し、search hot pathでのsortを避ける。

## 3. Endpoint resolver

Endpoint resolverはderived Lane配列を走査し、RoadSegment centerlineへ3D projectionする。

Phase 12では専用nearest-Lane spatial indexを導入せず全Lane走査を基準実装とする。これは探索実装を単純化し、3D correctnessとstable tie-breakを先に固定するためである。

large graph benchmarkでendpoint resolveが支配的になった場合は、後続の性能改善として3D Lane spatial indexを導入する。indexを導入してもnearest判定とtie-breakの契約は変更しない。

## 4. Search state

Dijkstraのstateは「Lane出口まで走行済み」を表す。

- 起点Lane: resolved offsetからLane出口までの部分costを初期costとする。
- 中間Lane: Lane全長のcostを加算する。
- 終点Lane: Lane入口からresolved destination offsetまでの部分costをgoal costとして扱う。

終点Laneへ入った時点でgoal候補を評価することで、終点より先のLane出口まで余分に走行するcostを入れない。

起点と終点が同一Laneで順方向に到達可能ならDijkstraを省略する。

## 5. Deterministic queue / predecessor

priority queueのpriorityは`(cost, LaneId)`とする。

同一Laneへ同一costで到達する候補は、`LaneConnectionId`、predecessor `LaneId`の順で比較する。goal候補も同じ規則で比較する。

zero-length segmentを含んでもsettled Laneを再更新しないため、zero-cost cycleでpredecessor chainが循環しない。

## 6. Result materialization

探索中はLane stable IDとpredecessorだけを保持する。goal確定後にpredecessor chainを逆順に辿り、Lane sequenceとtransition sequenceを生成する。

各`RouteLaneStep`は次をmaterializeする。

- Lane / RoadSegment stable ID
- start / end segment offset
- 3D distance
- `distance / speedLimit`による推定時間
- 次stepへ進むLaneConnection stable ID

`RouteResult`は入力配列をcopyし、read-only viewだけを公開する。callerへmutable routing internalsは公開しない。

## 7. Cache

`RoadRouter`内部にdictionary + linked-list LRUを持ち、entry数と保持routeサイズの二重上限を設ける。

- 最大1,024 entries
- cache全体で最大100,000 Lane steps
- 100,000 Lane stepsを超える単一routeはcacheしない
- insert / replace後にどちらかの上限を超える間、LRU末尾からevictする

cache hit時はendpoint resolveとDijkstraを行わず、既存immutable `RouteResult`を返す。

cache keyはunconstrained requestのOrigin / Destination各XYZの`double` bit patternとcost metricである。Road topologyはkeyへ埋め込まず、mutation時の全cache invalidationをgeneration boundaryとする。

Lane step総数にも上限を設けることで、large graphの長距離Routeを多数保持したときにentry上限だけでは抑えられない`RouteResult`メモリ増幅を防ぐ。

constraint付きrouteをcacheしないため、closure set hashingやcollision handlingをhot pathへ持ち込まない。

## 8. Invalidation

次の成功したcommandで`RoadRouter.Invalidate()`する。

- RoadNode create / update / remove
- RoadSegment create / update / remove
- Lane create / update / remove
- LaneConnection create / update / remove

`false`を返すnot-found update/removeやexceptionでrollbackされたmutationではinvalidateしない。

checkpoint restoreは新しい`SimulationWorld`とdirtyな`RoadRouter`を生成するため、restore前worldのcacheを引き継がない。

## 9. Allocation / performance policy

- graph rebuildはcold pathとしてLINQ / dictionary constructionを許容する。
- endpoint resolverはrebuild済み配列を直接走査し、routeごとのsortを行わない。
- search missはDictionary / HashSet / PriorityQueueをroute単位で確保する基準実装とする。
- cache hitはgraph snapshot、endpoint scan、search container allocationを行わない。
- large route cacheはLane step総数上限で長寿命保持量を制御する。

Phase 12 benchmarkではsmall / medium / large graphについてsearch missとcache hitを別々に測定し、探索時間とallocationの基準値を残す。

## 10. 拡張点

後続Phaseでは公開Route contractを維持しつつ、次を内部差し替え可能とする。

- nearest-Lane 3D spatial index
- pooled search workspace
- A* / hierarchical routing
- congestion / signal / turn penalty cost provider
- closure generationを含むconstraint-aware cache

最適化でstable ID tie-break、3D topology、LaneDirection、明示LaneConnectionというcorrectness契約を変更しない。
