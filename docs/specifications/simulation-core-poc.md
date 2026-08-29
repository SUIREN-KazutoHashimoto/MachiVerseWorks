# Simulation Core 最小 PoC 仕様

## 目的

Phase 2 では都市機能そのものではなく、多数 Agent を deterministic に step できる最小 Simulation Core を成立させる。

## 時間

- tick rate は `SimulationConfig.TickRate` で保持する。
- 初期既定値は 30 ticks/sec とする。
- `Step()` 1回につき tick counter を1増やす。
- Simulation time は wall clock ではなく tick rate から算出する。

## Seed と再現性

- Simulation seed は `SimulationConfig.Seed` で保持する。
- 同一 seed、同一生成順、同一入力、同一 tick 回数では同一状態を得る。
- Agent の自動生成で使う乱数は Simulation Core 内部の deterministic PRNG から供給する。

## Agent

最小 Agent state は次を持つ。

- 安定した `AgentId`
- 2D world position
- 2D velocity
- active state

`AgentId` は単調増加し、削除や内部 slot の都合で再採番しない。

1 tick の最小更新は `position += velocity * tickDuration` とする。

## Spatial Index

- World を固定サイズの正方形 cell に分割する。
- cell 座標は `floor(worldCoordinate / cellSize)` で求める。
- 負座標でも同じ規則を使う。
- Agent 生成時に cell へ登録し、移動で cell を跨いだ場合は所属を更新する。
- 矩形 query は境界を含む。

## Snapshot

Client 配信用の最小 snapshot は次を持つ。

- `AgentId`
- position
- velocity
- snapshot 時点の tick count

Snapshot は値としてコピーし、Simulation 内部の mutable state への参照を外部へ渡さない。

指定矩形の snapshot は spatial index で候補を絞った後、実座標で境界判定する。

## Phase 2 で扱わないもの

- 経路探索
- 衝突回避
- Agent 種別
- 建物・道路・交通機関
- 並列 tick
- network protocol
- 永続化

これらは後続 Phase で必要な責務へ分離して追加する。
