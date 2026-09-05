# VQ-0 User-facing Visual Baseline

このディレクトリは、Legacy Visual Parity の比較を繰り返し実行するための **User-facing Golden** 契約を管理します。

## Legacy参照の固定

Legacy側の参照正本は、archive済み `Machi-Sim_Legacy` の `v1.1.9` / commit `5715ca26d1a7525d89a93c35540f926a720e5386` に固定します。

このcommitでは、Legacyのproduction Viewとして少なくとも次が実装・記録されています。

- Perspective Camera、Fog、Directional Light、Hemisphere Light、PCF Soft Shadow
- 道路階層、Building / POI、Signal / Crosswalk
- Railway / Station / Train
- Vehicle等の都市活動
- GPU Instancingとdistance LOD

VQ-0のCIはLegacy repository自体をclone / executeしません。参照commitをprovenanceとして固定し、新版側の再現可能な5構図を同一条件で蓄積します。Legacyとの直接的な合否判定はVQ-7で行います。

## Technical Goldenとの違い

既存の `../golden/` は Technical Golden です。

- `view-physical-world.png` / `view-settlement-structure.png`: 決定論的fixtureに対するrenderer pixel regression
- `view-runtime-integrated.json`: 通常Runtimeで主要layerが存在することを確認するstructural contract

これらが成功しても、Legacyと同等以上のユーザー向けVisual品質を意味しません。

User-facing Goldenは、通常のDefault World Bootstrapを固定Seedで起動し、実際の `Simulation -> Server/Gateway -> Application -> WorldView` 経路から5つの代表構図を取得します。構図・実行条件・Legacy比較項目の正本は `manifest.json` です。

## 代表構図

1. `world-overview` — 広域のSettlement silhouette、Terrain / Water、都市の存在感
2. `dense-urban` — Building mass、Skyline、都市密度
3. `road-interchange` — Road hierarchy、Intersection / grade-separationへ発展する構図
4. `railway` — Railway / Trainの存在感
5. `street-activity` — Vehicle / Pedestrian等の近距離活動

Camera targetは固定座標をハードコードせず、固定Seedから得られたauthoritative observationのstable ID / geometryを決定論的に選択します。Camera移動後は新しいRoad subscription snapshotを受信し、安定時間が経過するまで次の構図へ進みません。これにより前構図の遅延snapshotを次構図のanchor選択に使うraceを防ぎます。

## 固定実行条件

- Simulation Seed: `29027`
- Simulation Tick Rate: `30`
- Capture Tick: `60`（通常Bootstrap完了後にこのtickまで進めてPause）
- Snapshot Rate: `2`
- Default World Bootstrap: enabled
- Viewport: `1920x1080`
- Device Pixel Ratio: `1`
- Browser: Chrome for Testing `152.0.7977.75`
- Renderer: SwiftShader
- Font: `Noto Sans CJK JP` / `fonts-noto-cjk 1:20230817+repack1-3`

5構図はすべて同じPause済みSimulation stateから撮影します。Vehicle / Pedestrian / Trainを含むauthoritative World stateは構図間で進行しません。通常起動で非表示となるDebug Overlayに加え、Person / Railway / Transit / Economy / Performance等の診断専用UIもcapture seamで非表示を強制します。通常ユーザー向けstatus chrome、Camera hint、identityは残すため、UIがWorld観測を過度に妨げていないかも画像から確認できます。

## Golden保存場所

承認済み画像は隣接する `../user-facing-golden/` に保存します。

```text
src/view/tests/visual/user-facing-golden/
  world-overview.png
  dense-urban.png
  road-interchange.png
  railway.png
  street-activity.png
```

E2E artifactは `.artifacts/view-phase03-e2e/user-facing/` に `actual/`, `expected/`, `diff/`, `comparison/`, `diagnostics/`, `summary.json` を出力します。

## 更新方法

User-facing Goldenだけを更新するときは、固定Browser / Font環境で次を実行します。

```bash
MVW_UPDATE_USER_FACING_GOLDEN=1 bash scripts/run-view-phase03-e2e.sh
```

この変数はTechnical Goldenを更新しません。`actual` と `diff` を目視し、意図したVisual変更であることを確認した場合だけ生成画像をコミットします。CIを通すためだけのGolden更新は禁止します。

## 差分判定

Simulation stateは固定tickで停止させるため、動的Entityの時間進行を理由に差分を許容しません。channel thresholdはTechnical Goldenと同じ`8/255`を維持し、changed-pixel ratioはブラウザのrasterization差を吸収するため既定`0.5%`まで許容します。大きな構図崩れ・layer消失・UI占有増加はこの範囲を超えるため回帰として扱います。

このpixel baselineは**Legacy parityの合否そのものではありません**。VQ-1〜VQ-6では同じ5構図に対して意図した改善をレビューし、VQ-7で固定したLegacy参照とUser-facing Goldenを正式なParity Gateへ昇格します。
