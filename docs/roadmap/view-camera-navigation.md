# View Camera Navigation

View Phase 2 `Camera & Observation Navigation` のCamera操作契約を補足する。Issue #296で旧リポジトリ相当の自由3D視点へ戻したため、以後のView PhaseはこのCamera modelを前提とする。

## Camera model

- 基本Cameraは `THREE.PerspectiveCamera` とする。
- 基準FOVは55度とする。
- Simulation座標 `(X, Y, Z)` とThree.js座標 `(X, Z, Y)` の既存変換を維持する。
- 描画far planeとObservation subscription depthは別のPresentation / Delivery stateとして管理する。

## Free camera controls

- 左ドラッグ: yaw / pitchによる視線操作
- `W / S`: Camera視線方向へ前進 / 後退
- `A / D`: Camera基準の左右移動
- `E` または `Space`: 上昇
- `Q` または `Ctrl`: 下降
- `Shift`: 高速移動
- Mouse wheel: 自由視点時の移動速度を変更
- pitchは上下反転直前でclampする
- Cameraには最低高度を設ける

移動速度はView-local stateでありSimulation stateへ影響しない。基準値は旧実装に合わせ `40 m/s`、Shift倍率 `4`、速度範囲 `2..800 m/s` とする。

## Focus / follow / jump

既存の `ViewNavigationTarget` と `jump` / `focus` / `follow` APIを維持する。

- `jump` / `focus` は対象のstable observation positionをCamera前方へ配置する。
- 旧Orthographic Camera向け `preferredZoom` は互換hintとしてPerspective距離へ変換する。
- `follow` は対象stable positionの周囲をorbitできる。
- follow中のMouse wheelはfollow distanceを変更する。
- follow解除時は現在のCamera姿勢を自由視点へ引き継ぐ。
- Vehicle / Trainのheading-aware orbitや一人称Cameraは、対象Observation contractが利用可能になった時点で同じcontroller境界へ追加する。

## Observation subscription

Perspective Cameraではvisual far plane全体をそのまま購読しない。

- Camera frustumのcorner rayからSimulation座標AABBを生成する。
- `maximumObservationDistance` でfrustum depthをboundedにする。
- 現行baselineは3 kmとする。
- Gatewayがsubscription sizeを拒否した場合はCamera FOVを変えずObservation depthだけを段階的に縮小する。
- Camera位置・向き・subscription範囲を変更してもSimulation workload / fidelity / resultの意味を変更しない。

## Regression coverage

最低限、以下をunit / CIで固定する。

- yaw / pitchとpitch clamp
- WASD / 上下移動 / Shift高速移動
- Mouse wheelによるfree speed / follow distance変更
- 最低高度
- stable targetへのjump / focus / follow
- follow解除後の姿勢継承
- Perspective subscription volumeのfinite / bounded性
- Camera translationに対するSubscriptionの追従
- default Server cell budget内に収まるbaseline

Lighting、shadow、material、fog、asset品質はCamera navigationとは別のRendering責務として扱う。
