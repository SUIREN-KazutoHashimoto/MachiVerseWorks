# View Visual Regression

このディレクトリでは、View の Browser E2E で使用する承認済み Golden と実 Runtime 統合 Golden 契約を管理します。

Visual Regression はスクリーンショットだけを正しさの根拠にせず、renderer diagnostics、構造 assertion、実 Runtime の主要レイヤー契約を組み合わせて回帰を検出します。正本となる View の画像は FHD（`1920x1080`）、device pixel ratio 1 で取得します。

## Renderer Golden

Renderer Golden は決定論的 fixture に対する pixel regression です。Simulation の通常 bootstrap とは分離し、個別 renderer の見た目を固定します。

- `golden/view-physical-world.png`: Physical World Rendering。Terrain、Water、GeographicFeature、自然地名を確認します。
- `golden/view-settlement-structure.png`: Settlement / Structure Rendering。Settlement、District、Parcel、Building、POI、Label、Road Sign を確認します。

`run-view-phase03-e2e.sh` の Renderer Golden 区間では `Simulation__DefaultWorldBootstrap__Enabled=false` を明示し、通常 Runtime の都市生成が fixture に混ざらないようにします。

## Actual Runtime Integrated Golden

Renderer Golden の後、View Phase 3 E2E は Server を通常設定で再起動し、`Simulation -> Server/Gateway protocol -> Application -> WorldView` の実経路を通ったユーザー画面を検証します。View 側から Agent / Building / Settlement / Road / Train を注入しません。

通常起動は `Simulation:DefaultWorldBootstrap` により fresh world に Regional Generation を materialize し、道路・建物・人口・経済・初期移動主体・列車運行を成立させます。保存済みワールドや明示的 fixture が既に状態を持つ場合は bootstrap で上書きしません。

`golden/view-runtime-integrated.json` は通常ユーザー画面の正式な統合 Golden 契約です。次の主要可視状態を required E2E で検証します。

- generic debug Agent が 0 件であること
- Terrain snapshot が存在すること
- Settlement / Building が存在すること
- Road Network が存在すること
- Pedestrian / Vehicle が存在すること
- Train が存在すること
- 通常画面で Debug Overlay が表示されていないこと
- CI の固定日本語フォントが利用可能であること

`?visualTest=runtime` は通常描画を差し替えず、実際に受信した `Application.state` と描画レイヤーから diagnostics を読み取る seam だけを追加します。CI は `.artifacts/view-phase03-e2e/runtime-user-view/actual/` に FHD スクリーンショットを保存し、`summary.json` と `diagnostics/` に統合 Golden 判定値を記録します。

現在の Runtime capture は次の 3 枚です。

- `runtime-default.png`: Application 起動後の通常ユーザー View。
- `runtime-agent-cloud.png`: 既存 Artifact 名との互換性を維持した capture。runtime visual seam では都市全体へフォーカスします。
- `runtime-worst-grounding.png`: 既存 Artifact 名との互換性を維持した capture。同じく都市レイヤーを観測します。

Runtime は Simulation tick によって動的に変化するため、Renderer fixture と同じ pixel-perfect baseline ではなく、固定 Browser・固定 Font・FHD screenshot と主要レイヤーの構造 Golden を組み合わせます。これにより建物・道路・交通主体・Debug Overlay などの欠落を false negative にしない一方、tick 差だけで全画面 pixel comparison が不安定になることを避けます。

## Debug Overlay

通常ユーザー画面では Performance / Logistics / Power / Water-Sewer / Gas / Optical / Radio の Debug Overlay を非表示にします。診断が必要な場合だけ URL に `?debug=1` を付けて明示的に表示します。Runtime Integrated Golden は通常 URL で `visibleDebugOverlayCount=0` を要求します。

## 固定実行環境

Required Visual Regression は `ubuntu-24.04` 上で実行し、Visual 対象ジョブだけ Chrome for Testing `152.0.7977.75` を `scripts/install-visual-browser.sh` から取得して使用します。描画には Chrome 同梱の SwiftShader を使用し、runner が CDP の `Browser.getVersion` を検証して diagnostics に記録します。

日本語を含む Canvas / DOM ラベルを tofu glyph のまま承認しないよう、Visual 対象ジョブでは `fonts-noto-cjk` `1:20230817+repack1-3` を明示インストールし、Visual E2E 専用 fontconfig で `sans-serif` を `Noto Sans CJK JP` へ固定します。CI は package version と `fc-match sans-serif` の結果を検証し、使用した font family / package version を diagnostics に記録します。

Golden 更新時も、可能な限り同じ固定 Browser・Font 環境を使用してください。CI と異なる Chrome / Chromium や generic font mapping で生成した画像をそのまま Golden として登録しません。

## 失敗時 Artifact

各 View E2E Artifact には、該当する場合に次のファイルを保存します。

- `expected/<name>.png`
- `actual/<name>.png`
- `diff/<name>.png`
- `comparison/<name>.json`
- `diagnostics/<name>.json`
- Runtime `summary.json`
- Browser HTML / Chrome log / Server log

Renderer diagnostics には fixture の構造・描画 metric、canvas dimensions、device pixel ratio、Browser version、font family / package version を記録します。Runtime diagnostics には Terrain / Water sample、Settlement / Building / Road、Pedestrian / Vehicle / Train、generic Agent、可視 Debug Overlay、日本語 Font readiness を記録します。

## Golden Image の更新

CI を通す目的だけで Golden Image を更新してはいけません。まず変更内容と CI の `actual` / `diff` Artifact を確認し、見た目の変更が意図したものかを判断します。

固定 Chrome for Testing を用意する場合は次のようにします。

```bash
export MVW_VISUAL_BROWSER_VERSION=152.0.7977.75
export MVW_VISUAL_BROWSER="$(bash scripts/install-visual-browser.sh .artifacts/visual-browser)"
```

意図した Renderer 変更として承認した場合だけ、対象 E2E を Golden 更新モードで実行します。

```bash
MVW_UPDATE_VISUAL_GOLDEN=1 bash scripts/run-view-phase03-e2e.sh
MVW_UPDATE_VISUAL_GOLDEN=1 bash scripts/run-view-phase04-e2e.sh
```

生成された PNG を目視確認してから通常の source change と同様にコミットします。Runtime Integrated Golden の構造条件を変更する場合も、同じく actual screenshot と diagnostics を確認してから `view-runtime-integrated.json` を更新します。

## 差分閾値

共通 pixel comparator は、既定で各 channel `8/255` を noise threshold とし、画像全体に対する changed-pixel ratio の上限を `0.1%` とします。

Settlement / Structure シーンは背景面積が大きいため、`run-view-phase04-e2e.sh` では既定上限を `0.01%` に厳格化します。FHD では約 207 pixel を超える有意差で失敗するため、Settlement や Building の透明化・背景色化のように件数 assertion だけでは検出できない回帰も検出します。

調査時のみ `MVW_VISUAL_CHANNEL_THRESHOLD` と `MVW_VISUAL_MAX_CHANGED_RATIO` で上書きできます。正本 baseline の閾値を変更する場合は、検出能力が低下しないことを確認してレビュー対象にします。
