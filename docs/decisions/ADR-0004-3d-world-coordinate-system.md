# ADR-0004: Simulation World の正本座標をネイティブ3D化する

- Status: Accepted
- Date: 2026-08-29

## Context

MachiVerseWorks の旧PoCはSimulationの `X / Y` を水平2D座標として扱っていた。今後、地下・高架・多層建物・立体交通を実装するには、rendererだけでなくSimulation、Spatial Index、Protocol、Save Dataまで同一の高さ情報を正本として保持する必要がある。

2D APIを残して`Z = 0`へ暗黙変換すると、呼び出し側が高度を指定し忘れてもコンパイル時に検出できず、将来の立体シミュレーションで高度欠落を再導入する危険がある。

## Decision

Simulation World をネイティブ3Dのみの契約へ移行する。

- 正本座標は `X / Y / Z`。`Z`は上向きを正とする高度。
- 単位は全軸とも metre。
- `WorldPoint` / `WorldVector` / `SpatialCell` は3軸必須。
- 2引数constructor、`WorldRect`、2D型aliasなどの互換APIは提供しない。
- 空間範囲、snapshot、subscription、spatial queryは `WorldVolume` のみを使う。
- Spatial Gridは3軸とも共通の`SpatialCellSize`で分割する。
- Three.js / Web Audio境界では `(sim.X, sim.Z, sim.Y)` へ明示変換する。
- 自動生成Agentの`VelocityZ = 0`は生成ポリシーであり、座標モデルは常に3Dとする。
- Protocolは3D必須wire contractとして2.0、Save Dataは3D必須schemaとしてformat 2を使用する。
- 旧2D protocol/save/APIへの暗黙fallbackは行わない。

## Consequences

### 利点

- 高度の指定漏れをAPI境界で防ぎやすい。
- 地下・地上・高架が同じ水平座標に存在しても正本状態とSpatial Indexで区別できる。
- 後続機能が2D互換層を意識せず、最初から立体空間を前提に設計できる。
- Simulationからrenderer、audio、saveまで3D契約を一貫してテストできる。

### コスト

- 旧2D呼び出しは明示的にZまたはZ範囲を指定するよう移行が必要。
- Spatial Indexのcell数は高さ方向にも増えるため、subscription上限を3次元で評価する必要がある。
- Protocol payloadとSave Dataは増加し、性能回帰をbenchmarkで継続確認する必要がある。
