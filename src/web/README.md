# MachiVerseWorks Web Client

Vite + TypeScript + Three.jsで構成するMachiVerseWorksのブラウザ3D Clientです。

現在の最小経路:

```text
WebSocket -> Protocol 2.x decoder -> EntityStore (XYZ) -> interpolation (XYZ) -> Three.js
                                                              \\-> Web Audio
```

## 起動

Serverを既定設定で起動した後、このディレクトリで次を実行します。

```bash
npm ci
npm run dev
```

既定のWebSocket URLは`ws://127.0.0.1:5080/ws`です。別のServerへ接続する場合はVite environment variableで指定します。

```bash
VITE_SERVER_URL=ws://127.0.0.1:5080/ws npm run dev
```

URLは`ws://`または`wss://`のみ受理します。

## 操作

- 左ドラッグ: cameraの水平移動
- マウスホイール: zoom
- `音声を有効化`: browserのuser gesture要件を満たして`AudioContext`をresume

Cameraのnear/farを含む8つのfrustum cornerから3D AABBを計算し、paddingを加えて`SubscribeVolume`を送信します。固定高度bandや`SubscribeArea`は使用しません。切断時はClient entity stateを破棄し、自動再接続後に新しい接続のspawn snapshotから再構築します。

Simulation `(X,Y,Z)`はThree.js / Web Audio境界で`(X,Z,Y)`へ明示変換します。

## 検証

```bash
npm run lint
npm run typecheck
npm test
npm run build
```

Protocol codec / EntityStore / 3D camera subscription / renderer mapping / audio voice budget・Ambient Zone policyはNode.js testで検証します。Phase 6 E2Eでは実Server / Browser / `WorldView.render()`まで接続します。

音声assetは将来`public/audio/`以下へ配置し、codeからfile pathを指定せず`audio/manifest.json`のstable cue IDを利用します。
