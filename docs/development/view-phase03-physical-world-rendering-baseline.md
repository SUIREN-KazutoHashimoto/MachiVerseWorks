# View Phase 3 Physical World Rendering baseline

View Phase 3 では、Simulation Phase 29 が公開する authoritative な Physical World observation を対象として、Browser 側の再現可能な rendering baseline を記録する。

## シナリオ

CI シナリオは `scripts/run-view-phase03-e2e.sh` とし、次の条件を使用する。

- Simulation seed は固定値 `29027` を使用する。
- Protocol `2.17` を使用する。
- 広域の `WorldEnvironmentSnapshot` subscription を使用する。
- 実際の Browser 上で `WorldView`、`WorldEnvironmentStore`、`PhysicalWorldRenderer` を使用する。
- Server から配送された Terrain / SurfaceWater / GeographicFeature / NaturalToponym observation をそのまま描画入力として使用する。

このシナリオで View は terrain の分類、GeographicFeature の推定、地名生成を行わない。意味情報は Simulation が提供し、View は presentation primitive への mapping だけを担当する。

## Baseline 出力

成功した CI run は `.artifacts/view-phase03-e2e/rendering-baseline.txt` を artifact として保存し、次の値を記録する。

- `frame_time_ms`: 計測対象 frame の Browser render call 所要時間。
- `draw_calls`: 計測対象 frame の Three.js renderer draw call 数。
- `geometries`: Three.js が保持する geometry resource 数。
- `textures`: Three.js が保持する texture resource 数。
- `geometry_bytes`: Physical World geometry model が所有する typed-array の byte 数。
- `terrain_triangles`: primary terrain の三角形数。
- `water_samples`: 水域表現として描画した配送済み SurfaceWater sample 数。
- `feature_segments`: 配送済み GeographicFeature の line segment 数。
- `toponym_labels`: label として表現した配送済み自然地名数。

runner hardware や headless WebGL の timing は変動するため、artifact の値自体は固定 threshold の performance gate にしない。一方で構造的な regression は gate 対象とし、E2E では terrain が空でないこと、GeographicFeature geometry が存在すること、authoritative な地名数と label 数が一致すること、draw/resource metrics が正であること、旧 `GridHelper` が存在しないことを確認する。

## Multi-surface 境界

`terrain-geometry.ts` は terrain column を role と layer で識別される observed surface の集合として表現する。Phase 29 が現在配送するのは primary surface sample だが、境界自体は個別に配送された `cavity-boundary`、`overhang`、追加 layer を受け取れる。

View は不足している surface を生成・推定しない。将来 Simulation / Gateway がそれらの observation を公開した場合も、現在の rendering contract を置き換えずに追加できる構造とする。

## Resource 更新方針

`WorldEnvironmentSnapshot` は tick ごとに再配送される場合があるが、terrain / feature / toponym / subscription volume が同一なら rendering revision は進めない。最新 authoritative snapshot は保持しつつ、terrain geometry、material、CanvasTexture 等の GPU resource は描画内容が変化した場合だけ再構築する。

SurfaceWater marker は authoritative terrain sample と同じ semantic 座標を使用し、意味的な水位は推定しない。terrain と完全共面になることによる z-fighting を避けるため、描画時のみ小さな presentation offset を加える。
