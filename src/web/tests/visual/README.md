# View Visual Regression

このディレクトリでは、View の Browser E2E で使用する承認済み Golden Image を管理します。

Visual Regression は、構造・数値 assertion を既に持つ決定論的な View fixture と同じシーンで実行します。スクリーンショットだけを正しさの根拠にせず、renderer diagnostics と構造検証を組み合わせて回帰を検出します。

正本となる View Golden Image は FHD（`1920x1080`）、device pixel ratio 1 で取得します。CI では手動確認したものと同じ解像度の表示を比較します。

## 対象シーン

- `view-physical-world.png`: View Phase 3 の Physical World Rendering。Terrain、Water、GeographicFeature、自然地名を確認します。
- `view-integrated-world-overview.png`: Terrain 上へ Settlement / Building / Agent を同時配置した統合Worldの広域確認です。
- `view-terrain-closeup.png`: 統合Worldの地形起伏を中距離から確認します。
- `view-city-center.png`: Settlement / Parcel / Building と Terrain の位置関係を都市中心部の距離で確認します。
- `view-agent-grounding.png`: Terrain sample 上に配置した Agent を近距離で確認します。画像比較に加えて Agent Z と authoritative terrain sample Z の差が 1µm 以下であることを数値 assertion します。
- `view-settlement-structure.png`: View Phase 4 の Settlement / Structure Rendering。Settlement、District、Parcel、Building、POI、Label、Road Sign を確認します。

Phase 3 の追加4 checkpoint は既存 `WorldEnvironment` の決定論的 Server fixture を利用し、Visual E2E のときだけテスト用 Settlement / Building / Agent を同じ `WorldView.scene` に重ねます。通常の View / Simulation state は変更しません。`physical-world` checkpoint は既存 Golden と互換を保つため従来どおり空の `EntityStore` で実行します。

## 固定実行環境

Visual Regression の required E2E は `ubuntu-24.04` 上で実行し、Visual 対象ジョブだけ Chrome for Testing `152.0.7977.75` を `scripts/install-visual-browser.sh` から取得して使用します。描画には Chrome 同梱の SwiftShader を使用し、`run-headless-visual-e2e.mjs` が CDP の `Browser.getVersion` を検証して diagnostics に記録します。

ラベル描画の generic `sans-serif` が runner image のフォント構成に依存しないよう、Visual 対象ジョブでは `fonts-dejavu-core` `2.37-8` を明示インストールし、Visual E2E 専用の fontconfig で `sans-serif` を `DejaVu Sans` へ固定します。CI は package version と `fc-match sans-serif` の結果を検証し、使用した font family / package version も diagnostics に記録します。

Golden 更新時も、可能な限り同じ固定ブラウザー・フォント環境を使用してください。CI と異なる Chrome / Chromium や generic font mapping で生成した画像をそのまま Golden として登録しません。

## 失敗時 Artifact

各 View E2E Artifact には、該当する場合に次のファイルを保存します。

- `expected/<name>.png`
- `actual/<name>.png`
- `diff/<name>.png`
- `comparison/<name>.json`
- `diagnostics/<name>.json`
- Browser HTML と Chrome log

`view-physical-world` Artifact は、比較前に5 checkpointすべてを撮影します。最初の差分で失敗しても `actual/` にはCamera Tour全体が残るため、Golden追加・差分調査を1回のCIで行えます。

`diagnostics/<name>.json` には既存 fixture の構造・描画 metric、canvas dimensions、device pixel ratio、使用した Browser version、Visual E2E の font family / package version を記録します。統合checkpointでは Agentの `z` / `groundZ` / `groundingDeltaMeters` と Building件数も保存します。

CDP command は1回あたり30秒で timeout し、DevTools WebSocket が `close` / `error` になった場合は保留中 command を即座に reject します。renderer target の異常終了時に GitHub Actions の matrix job timeout まで停止し続けないようにします。

## Golden Image の更新

CI を通す目的だけで Golden Image を更新してはいけません。まず View の変更内容と CI の `actual` / `diff` Artifact を確認し、見た目の変更が意図したものかを判断します。

固定 Chrome for Testing を用意する場合は次のようにします。

```bash
export MVW_VISUAL_BROWSER_VERSION=152.0.7977.75
export MVW_VISUAL_BROWSER="$(bash scripts/install-visual-browser.sh .artifacts/visual-browser)"
```

ローカル環境のフォント構成が CI と一致しない場合は、ローカルで生成した画像を Golden に採用せず、固定 CI 環境で生成された `actual` Artifact をレビューします。

意図した変更として承認した場合だけ、対象 E2E を Golden 更新モードで実行します。

```bash
MVW_UPDATE_VISUAL_GOLDEN=1 bash scripts/run-view-phase03-e2e.sh
MVW_UPDATE_VISUAL_GOLDEN=1 bash scripts/run-view-phase04-e2e.sh
```

生成された PNG を再度目視確認してから、通常の source change と同様にコミットします。CI は `MVW_UPDATE_VISUAL_GOLDEN` を設定しないため、Golden の欠落や意図しない差分で自動更新されることはありません。

## 差分閾値

共通 comparator は、既定で各 channel `8/255` を noise threshold とし、画像全体に対する changed-pixel ratio の上限を `0.1%` とします。

Phase 3 の `view-city-center.png` は `0.05%`、`view-agent-grounding.png` は `0.02%` を上限とし、近距離の接地・位置ずれを広域画像より厳しく検出します。

Phase 4 の Settlement / Structure シーンは背景面積が大きく、全画面 `0.1%` では主要オブジェクトの消失を見逃す可能性があるため、`run-view-phase04-e2e.sh` では既定上限を **`0.01%`** に厳格化します。FHD では約 207 pixel を超える有意差で失敗するため、Settlement や Building の透明化・背景色化のように件数 assertion だけでは検出できない回帰も Visual Regression で検出します。

調査時のみ `MVW_VISUAL_CHANNEL_THRESHOLD` と `MVW_VISUAL_MAX_CHANGED_RATIO` で上書きできます。正本 baseline の閾値を変更する場合は、検出能力が低下しないことを確認してレビュー対象にします。
