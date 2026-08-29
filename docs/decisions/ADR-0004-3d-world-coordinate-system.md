# ADR-0004: Simulation World の正本座標を3D化する

- Status: Accepted
- Date: 2026-08-29

## Context

MachiVerseWorks は既存PoCでSimulationの `X / Y` を水平2D座標として扱い、Web Client側でThree.jsの `X / Z` 平面へ写像している。

今後、地下・高架・多層建物・立体的な交通を実装するには、rendererだけではなくSimulation、Spatial Index、Protocol、Save Dataまで同じ高さ情報を正本として保持する必要がある。一方で、Phase 9で重力や地形衝突まで導入すると座標基盤と物理仕様が密結合になる。

## Decision

Simulation World の正本座標を `X / Y / Z` とする。

- `X / Y` は既存の水平2軸の意味を維持する。
- `Z` は高度で、正方向を上とする。
- 単位は全軸とも metre とする。
- `WorldPoint` / `WorldVector` は全成分の有限値を要求する。
- Spatial Grid は3軸とも共通の `SpatialCellSize` で3D cellへ分割する。
- 3D範囲検索は境界包含の `WorldVolume` を正規APIとする。
- 既存2D APIは移行用互換入口として `Z = 0` に固定して残す。
- Three.js では `(sim.X, sim.Z, sim.Y)` へ写像する。
- 自動生成Agentの `VelocityZ` は0とし、Phase 9だけでは飛行・重力等の物理ルールを導入しない。
- Protocolの3D wire contractは互換性のない変更としてmajor versionを更新する。
- Save Dataも3D stateを必須化し、Save format versionを更新する。

## Consequences

### 利点

- 地下・高架・多層構造を後続機能から共通の座標契約で扱える。
- Simulationからrendererまで高さ情報の欠落箇所を明確にテストできる。
- 既存の水平 `X / Y` の意味を維持できるため、2D PoCからの移行範囲を限定できる。
- 物理挙動を後続Taskへ分離でき、Phase 9の責務を座標・配信・保存基盤へ限定できる。

### コスト

- Spatial Indexのcell数は高さ方向にも増えるため、subscription volumeのcell上限を3次元で評価する必要がある。
- Protocol payloadとSave Dataは大きくなり、benchmarkで回帰を計測する必要がある。
- Three.js/Web Audio境界ではSimulation軸からrenderer軸への明示的な変換が必要になる。
