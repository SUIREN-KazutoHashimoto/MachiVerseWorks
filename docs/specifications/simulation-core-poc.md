# Simulation Core 基盤仕様

## 目的

Phase 2で成立させた最小Simulation Coreを基礎とし、Phase 9以降は多数Agentをdeterministicにstepできる**ネイティブ3D**の正本Worldとして扱う。

座標契約の詳細は [`world-coordinate-system.md`](world-coordinate-system.md) を正本とする。

## 時間

- tick rateは`SimulationConfig.TickRate`で保持する。
- 既定値は30 ticks/secとする。
- `Step()` 1回につきtick counterを1増やす。
- Simulation timeはwall clockではなくtick rateから算出する。

## Seedと再現性

- Simulation seedは`SimulationConfig.Seed`で保持する。
- 同一seed、同一生成順、同一入力、同一tick回数では同一状態を得る。
- Agent自動生成で使う乱数はSimulation Core内部のdeterministic PRNGから供給する。
- 失敗した生成commandはPRNG stateを含むSimulation stateを変更しない。

## Agent

最小Agent stateは次を持つ。

- 安定した`AgentId`
- XYZ world position
- XYZ velocity
- active state

`AgentId`は単調増加し、削除や内部slotの都合で再採番しない。

1 tickの最小更新は全3軸で`position += velocity * tickDuration`とする。自動生成Agentの`VelocityZ = 0`は生成ポリシーであり、Z軸を省略する互換表現ではない。

## Spatial Index

- Worldを固定サイズの3次元cellへ分割する。
- cell座標はX/Y/Z各軸で`floor(worldCoordinate / cellSize)`により求める。
- 負座標でも同じ規則を使う。
- Agent生成時に3D cellへ登録し、いずれかの軸でcellを跨いだ場合は所属を更新する。
- 空間queryは`WorldVolume(minX, minY, minZ, maxX, maxY, maxZ)`のみを使用し、境界を含む。
- query volumeが覆うcell数がoccupied cell数より大きい場合はoccupied cell側を走査し、巨大な疎volumeで空cellを立方体規模に総当たりしない。

## Snapshot

Client配信用の最小snapshotは次を持つ。

- `AgentId`
- XYZ position
- XYZ velocity
- snapshot時点のtick count

Snapshotは値としてコピーし、Simulation内部のmutable stateへの参照を外部へ渡さない。

指定`WorldVolume`のsnapshotはSpatial Indexで候補を絞った後、実座標を`WorldVolume.Contains`で判定する。

## 現時点で扱わないもの

- 経路探索
- 衝突回避
- Agent種別
- 建物・道路・交通機関固有のルール
- 重力・terrain collision・ground snapping
- 並列tick

これらは後続Phaseで必要な責務へ分離して追加する。
