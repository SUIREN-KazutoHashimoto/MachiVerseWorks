# Routing Foundation 仕様

## 1. 目的

Phase 12では、Road / Lane topologyを正本として車両系システムから再利用できる共有routing基盤を定義する。

このPhaseで扱うのは経路探索そのものであり、車両生成、lane changing、信号、渋滞、駐車、公共交通の運行判断は後続Phaseの責務とする。

## 2. 公開契約

Routingの公開契約は`MachiVerseWorks.Simulation`に置く。

- `RouteRequest`
  - `Origin`: native 3D world coordinate
  - `Destination`: native 3D world coordinate
  - `CostMetric`: `Distance`または`EstimatedTravelTime`
  - `Constraints`: 任意の一時的な通行制約
- `RouteConstraints`
  - `ClosedLaneIds`: 一時的に通行不能とするLane stable ID
  - `ClosedConnectionIds`: 一時的に通行不能とするLaneConnection stable ID
- `RouteResult`
  - 選択したcost metricとcost
  - 総距離
  - 速度上限から算出した推定所要時間
  - immutableな`RouteLaneStep`列
- `RouteLaneStep`
  - `LaneId`
  - `RoadSegmentId`
  - RoadSegmentのstart→endを0..1とした開始／終了offset
  - step距離と推定所要時間
  - 次Laneへ移るための`LaneConnectionId`

IDは既存Road Networkのstable IDをそのまま使用し、routing専用に同一実体へ別IDを付与しない。

## 3. 起点・終点のresolve

起点・終点のworld coordinateは、openなLaneのRoadSegment中心線へ3D射影し、射影点までの3D距離が最小のLaneへresolveする。

同距離のLaneが複数ある場合は`LaneId`の昇順をtie-breakに使用する。閉鎖Laneは候補から除外する。

segment offsetはRoadSegmentの`StartNodeId`側を0、`EndNodeId`側を1とする。`LaneDirection.Reverse`でもoffsetの定義は反転させず、走行方向だけを反転する。

## 4. Routing graph

Routing graphはRoad Network snapshotから派生する。

- graph上の走行単位はLaneとする。
- Laneの向きは`LaneDirection`を正とする。
- Lane間遷移は明示的な`LaneConnectionSnapshot`だけを使用する。
- 交差して見えるRoadSegment同士をgeometryだけで接続しない。
- `TurnMovement`は接続の意味を表す。接続が存在しないturnは許可されない。
- 一時的なturn restrictionは`ClosedConnectionIds`で表現する。
- 一時的な通行止めは`ClosedLaneIds`で表現する。

このためone-way、turn restriction、grade-separated crossingは同一のdirected topologyとして扱える。

## 5. Cost

### 5.1 Distance

`Distance`はnative 3D座標上のLane centerline長をmeterとして使用する。起点Laneと終点Laneはsegment offsetに応じた部分距離だけを加算する。

### 5.2 EstimatedTravelTime

`EstimatedTravelTime`は各stepについて次で算出する。

```text
stepTimeSeconds = stepDistanceMeters / Lane.SpeedLimitMetersPerSecond
```

信号待ち、混雑、加減速、turn penaltyはPhase 12のcostへ含めない。後続Phaseでcost providerを拡張できるよう、公開契約ではcost metricを明示する。

## 6. 探索とdeterminism

最短路探索は非負costのdirected graphに対するDijkstra法を基準とする。

同一cost候補では次のstable ID順を用いて結果を固定する。

1. `LaneConnectionId`
2. predecessor `LaneId`
3. queue上の`LaneId`

同一Road Network、同一`RouteRequest`、同一constraint集合からは同一Lane sequenceを返さなければならない。

起点と終点が同一Laneにresolveされ、LaneDirectionに沿って直接到達できる場合は、そのLane内の部分routeを最短routeとして返す。終点がLaneDirectionの後方にある場合は逆走せず、明示LaneConnectionによるloopが存在するときだけ到達可能とする。

## 7. 3D制約

RoutingはXY平面だけで接続判定しない。

- endpoint resolveはX/Y/Zを含む3D距離で行う。
- graph接続はstable RoadNode / LaneConnection topologyを正とする。
- 地上道路、高架道路、地下道路が同じXYを通過しても、明示Connectionがなければ相互に遷移できない。

## 8. Route cache

unconstrainedな`RouteRequest`はprocess-local LRU cacheの対象とする。

- key: Origin / Destinationの3D coordinate bit patternと`RoutingCostMetric`
- entry capacity: 最大1,024 entries
- retained route size budget: cache全体で最大100,000 Lane steps
- single-route policy: 100,000 Lane stepsを超えるrouteはcacheしない
- eviction: entry数またはLane step総数が上限を超える間、least recently used entryから削除する
- topology mutation: cache全消去とderived routing graphの再構築を要求する
- `ClosedLaneIds`または`ClosedConnectionIds`を含むrequest: cache対象外

entry数だけでなく保持Lane step総数にも上限を設けるのは、100,000 Lane級の長距離routeを多数cacheした場合に`RouteResult`保持量がentry数以上に増幅することを防ぐためである。

constraint付きrequestをcacheしないのは、事故・工事・運行規制など短寿命の動的状態を通常route cacheへ混在させないためである。

## 9. Topology mutation

Road node、Road segment、Lane、LaneConnectionの作成・更新・削除が成功した場合、routing graphとroute cacheをdirtyにする。

失敗してstateが変化しなかったmutationではrouting cacheを無効化しない。

RoadAccessPointはPhase 12のLane graphを変更しないため、RoadAccessPoint単独の変更ではrouting graphを無効化しない。

## 10. エラー

次はrouting errorとして扱う。

- Road NetworkにLaneが存在しない。
- constraintが存在しないstable IDを参照する。
- open Laneへresolveできない。
- directed topology上で到達不能。

到達不能時にgeometry上の近傍Roadへ暗黙jumpするfallbackは行わない。

## 11. 非目標

Phase 12では次を実装しない。

- congestion-aware dynamic cost
- traffic signal delay
- turn penalty model
- lane changing policy
- vehicle movement / collision
- parking / garage routing
- transit schedule routing
- pedestrian routeとの統合cost

これらはRoute contractを利用する後続Phaseで扱う。
