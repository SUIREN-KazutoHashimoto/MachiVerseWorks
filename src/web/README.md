# MachiVerseWorks Web Client

Vite + TypeScript + Three.jsで構成するMachiVerseWorksのブラウザ3D Clientです。都市世界の正本はServerにあり、Webは受信snapshotの表示・補間・debug UI・audioを担当します。

## Data flow

```text
WebSocket / Protocol 2.8
  ├─ Agent / Pedestrian / Vehicle stores -> interpolation -> WorldView
  ├─ Road Network -----------------------> static road geometry
  ├─ Intersection Control ---------------> traffic debug/render state
  ├─ Population --------------------------> statistics / Person inspector
  ├─ Railway Infrastructure --------------> revision-aware static 3D layer
  ├─ Railway Operations ------------------> Train layer / railway debug
  └─ Multimodal Transit ------------------> Transit debug / realtime state
                                             \
                                              -> Web Audio
```

Protocol wire layoutはC# object graphへ依存せずTypeScript側にもstable contractとして実装します。現在のnegotiated currentはProtocol 2.8です。

## 起動

Serverを既定設定で起動した後、このディレクトリで次を実行します。

```bash
npm ci
npm run dev
```

既定のWebSocket URLは`ws://127.0.0.1:5080/ws`です。別Serverへ接続する場合:

```bash
VITE_SERVER_URL=ws://127.0.0.1:5080/ws npm run dev
```

URLは`ws://`または`wss://`だけを受理します。

## Camera / subscription

- 左ドラッグ: cameraの水平移動
- マウスホイール: zoom
- `音声を有効化`: browser user gesture要件を満たして`AudioContext`をresume

Cameraのnear/farを含む8つのfrustum cornerから3D AABBを計算し、paddingを加えて`SubscribeVolume`を送信します。固定高度bandや`SubscribeArea`は使用しません。

Serverから`subscriptionVolumeTooLarge`を受けた場合はzoom-inして再購読します。Reconnect時はClient側のdynamic/static/debug stateを破棄し、HelloAck後の新しいsnapshotから再構築します。

## Rendering / debug

Simulation `(X,Y,Z)`はThree.js / Web Audio境界で`(X,Z,Y)`へ明示変換します。

- AgentはInstancedMesh、Pedestrian / Vehicleは専用storeから描画する
- RoadとRailway Infrastructureはstatic/revision-aware geometryとして扱う
- Railway OperationsのTrainはProtocol 2.7がFormation寸法を送らないため固定サイズのdebug proxy meshで描画する
- Railway debugの次到着表示は`plannedArrivalTick + service.delayTicks`というschedule projectionであり、Phase 19 Multimodal Transitの`estimatedArrivalTick`とは別契約である
- Multimodal Transit debugはLine / Stop / Pattern、Bus / Taxi位置、arrival estimateを表示する

## 検証

```bash
npm run lint
npm run typecheck
npm test
npm run build
```

実Server / WebSocket / headless browserを接続するE2Eは`.github/workflows/e2e.yml`へ集約されています。Protocol 2.8までの主要経路を同workflowのmatrixで継続検証します。

詳細は[`../../docs/architecture/web-client.md`](../../docs/architecture/web-client.md)を参照してください。
