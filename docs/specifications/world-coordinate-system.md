# Simulation World 3D Coordinate Contract

## 目的

Simulation World の位置・速度・空間検索を3次元の正本状態として扱い、道路・鉄道・建物・地下・高架などの後続実装が高さ情報を失わない共通契約を定める。

## 正本座標系

Simulation は `X / Y / Z` の3軸を持つ。

- `X`: 水平面の第1軸
- `Y`: 水平面の第2軸
- `Z`: 高度。正方向を上とする
- 1 world unit = 1 metre

既存の2D `X / Y` の意味は変更しない。2D互換入口から生成した位置・速度は `Z = 0` として扱う。

Phase 9 は座標基盤の契約を定めるものであり、重力、落下、地面への吸着、道路・線路・建物ごとの高度制約は導入しない。自動生成 Agent の速度も `VelocityZ = 0` とし、明示的に指定した3軸速度のみが高度を変化させる。

## 値の制約

`WorldPoint` と `WorldVector` の全成分は有限値でなければならない。`NaN` と正負の infinity は拒否する。

Spatial Grid へ登録できる位置は、各軸について `floor(coordinate / cellSize)` が `Int32` の範囲へ収まる必要がある。Phase 9 ではこれを実装上の空間境界とし、別の固定 world size は設けない。

## Spatial Grid

`SpatialCell` は `(X, Y, Z)` の整数 cell index を持つ。各軸の cell index は同じ `SpatialCellSize` で算出する。

```text
cellX = floor(worldX / cellSize)
cellY = floor(worldY / cellSize)
cellZ = floor(worldZ / cellSize)
```

負座標も floor により対称に cell 分割する。

## Volume

3D範囲は `WorldVolume(minX, minY, minZ, maxX, maxY, maxZ)` で表す。全軸で `max >= min` を要求し、境界は最小・最大とも包含する。

従来の `WorldRect` は2D互換入口として残し、3D APIへ渡す場合は `Z = 0` の平面 volume として扱う。本番の購読・snapshot経路は `WorldVolume` を使用する。

## Renderer への写像

Simulation と Three.js の軸は次のように明示的に写像する。

| Simulation | Three.js | 意味 |
| --- | --- | --- |
| `X` | `X` | 水平第1軸 |
| `Y` | `Z` | 水平第2軸 |
| `Z` | `Y` | 高度 |

すなわち renderer 座標は `(sim.X, sim.Z, sim.Y)` とする。この写像は既存Web ClientがSimulation `X / Y`をThree.jsの水平 `X / Z`へ描画していた意味を維持しながら高度を追加するための境界であり、Simulation内部へThree.js固有の軸定義を持ち込まない。

Web Audio の3D emitterも同じ renderer 空間へ写像し、高度差を距離計算とPanner位置へ反映する。

## Protocol / Save Data

Protocol と Save Data は位置 `X / Y / Z`、速度 `VelocityX / VelocityY / VelocityZ` を欠落なく保持する。3D化によってwire layoutとSave formatが変わるため、それぞれの独立versionを更新する。
