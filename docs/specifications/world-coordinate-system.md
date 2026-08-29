# Simulation World 3D Coordinate Contract

## 目的

Simulation World の位置・速度・空間検索を3次元の正本状態として扱い、道路・鉄道・建物・地下・高架などの後続実装が高さ情報を失わない共通契約を定める。

## 正本座標系

Simulation は `X / Y / Z` の3軸だけを持つ。

- `X`: 水平面の第1軸
- `Y`: 水平面の第2軸
- `Z`: 高度。正方向を上とする
- 1 world unit = 1 metre

`WorldPoint`、`WorldVector`、`SpatialCell`、範囲指定はすべて3軸必須とし、2D専用型・2引数constructor・`Z = 0`へ暗黙変換する互換APIは提供しない。水平面だけを扱う呼び出し側も、明示的にZ座標またはZ範囲を指定する。

Phase 9 は座標基盤の契約を定めるものであり、重力、落下、地面への吸着、道路・線路・建物ごとの高度制約は導入しない。自動生成 Agent の `VelocityZ = 0` は生成規則であり、2D互換表現ではない。明示的に指定した3軸速度はtickで高度を変化させる。

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

空間範囲は `WorldVolume(minX, minY, minZ, maxX, maxY, maxZ)` のみで表す。全軸で `max >= min` を要求し、境界は最小・最大とも包含する。Snapshot、subscription、spatial queryはすべて`WorldVolume`を使用する。

## Renderer への写像

Simulation と Three.js の軸は次のように明示的に写像する。

| Simulation | Three.js | 意味 |
| --- | --- | --- |
| `X` | `X` | 水平第1軸 |
| `Y` | `Z` | 水平第2軸 |
| `Z` | `Y` | 高度 |

renderer 座標は `(sim.X, sim.Z, sim.Y)` とする。Simulation内部にはThree.js固有の軸定義を持ち込まない。Web Audio の3D emitterも同じ写像を使い、高度差を距離計算とPanner位置へ反映する。

## Protocol / Save Data

Protocol と Save Data は位置 `X / Y / Z`、速度 `VelocityX / VelocityY / VelocityZ` を必須項目として保持する。3D化はbreaking changeとしてProtocol 2.0とSave format 2で表現し、2D wire/save schemaへの暗黙fallbackは行わない。
