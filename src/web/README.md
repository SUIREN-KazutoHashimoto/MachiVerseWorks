# MachiVerseWorks Web Client

Vite + TypeScript + Three.js で構成する MachiVerseWorks のブラウザClientです。

Phase 5 では次の最小経路を実装しています。

```text
WebSocket -> Protocol decoder -> EntityStore -> interpolation -> Three.js
                                              \\-> Audio presentation boundary
```

## 起動

Serverを既定設定で起動した後、このディレクトリで次を実行します。

```bash
npm ci
npm run dev
```

既定のWebSocket URLは `ws://127.0.0.1:5080/ws` です。別のServerへ接続する場合は Vite environment variable で指定します。

```bash
VITE_SERVER_URL=ws://127.0.0.1:5080/ws npm run dev
```

URLは `ws://` または `wss://` のみ受理します。

## 操作

- 左ドラッグ: camera移動
- マウスホイール: zoom
- `音声を有効化`: browserのuser gesture要件を満たして `AudioContext` を resume

Cameraから計算した矩形にpaddingを加えて `SubscribeArea` を送信します。切断時はClient entity stateを破棄し、自動再接続後に新しい接続のspawn snapshotから再構築します。

## 検証

```bash
npm run lint
npm run typecheck
npm test
npm run build
```

Protocol codec / EntityStore / audio voice budget・Ambient Zone policy は Node.js のbuilt-in test runnerで検証します。

音声assetは将来 `public/audio/` 以下へ配置し、codeからfile pathを指定せず `audio/manifest.json` のstable cue IDを利用します。
