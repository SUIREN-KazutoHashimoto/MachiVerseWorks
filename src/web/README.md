# MachiVerseWorks Web Client

Vite + TypeScript + Three.jsで構成するMachiVerseWorksのブラウザ3D Clientです。都市世界の正本はServerにあり、Webは受信snapshotの表示・補間・debug UI・audioを担当します。

View側の将来実装・改善計画は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)を正本とします。

## Data flow

```text
WebSocket / Protocol 2.16
  ├─ Agent / Pedestrian / Vehicle stores -> interpolation -> WorldView
  ├─ Road Network -----------------------> static road geometry
  ├─ Intersection Control ---------------> traffic debug/render state
  ├─ Population --------------------------> statistics / Person inspector / explicit clear
  ├─ Railway Infrastructure --------------> revision-aware static 3D layer
  ├─ Railway Operations ------------------> Train layer / railway debug
  ├─ Multimodal Transit ------------------> Transit debug / realtime state
  ├─ Economy / Logistics -----------------> domain debug state
  ├─ Power / Water-Sewer / Gas ----------> infrastructure debug state
  ├─ Optical -----------------------------> communication debug state
  └─ Radio / Spectrum --------------------> radio debug / coverage state
                                             \
                                              -> Web Audio
```

Protocol wire layoutはC# object graphへ依存せずTypeScript側にもstable contractとして実装します。現在のnegotiation versionは[`WEB_CURRENT_PROTOCOL_VERSION`](src/person-inspection-protocol.ts)の **Protocol 2.16** です。C#側のcurrentは[`../MachiVerseWorks.Protocol/ProtocolVersion.cs`](../MachiVerseWorks.Protocol/ProtocolVersion.cs)、binary契約は[`../../docs/architecture/protocol.md`](../../docs/architecture/protocol.md)を正本とします。

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

World scale Camera、Rendering LOD、View streaming / cache等の拡張はView Roadmapで管理し、CameraやLODをSimulation workloadの判定条件に使用しません。

## Rendering / debug

Simulation `(X,Y,Z)`はThree.js / Web Audio境界で`(X,Z,Y)`へ明示変換します。

- AgentはInstancedMesh、Pedestrian / Vehicleは専用storeから描画する
- RoadとRailway Infrastructureはstatic/revision-aware geometryとして扱う
- Railway OperationsのTrainはProtocol 2.7がFormation寸法を送らないため固定サイズのdebug proxy meshで描画する
- Railway debugの次到着表示は`plannedArrivalTick + service.delayTicks`というschedule projectionであり、Multimodal Transitの`estimatedArrivalTick`とは別契約である
- Multimodal Transit debugはLine / Stop / Pattern、Bus / Taxi位置、arrival estimateを表示する
- Economy / Logistics / Power / Water-Sewer / Gas / Optical / Radio-Spectrumは各Protocol decoderからdebug stateへ反映する

これらのdebug表示はSimulation stateを観測するための表示であり、Web側状態はauthoritative stateではありません。本格的なoverlay / Inspector / Dashboard /管理UIは[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)で計画します。

## Localization

Web Clientのlocale resourceは`locales/`を入口とし、default localeはmanifestの`ja-JP`です。Localization architectureは[`../../docs/architecture/localization.md`](../../docs/architecture/localization.md)、実装計画はView RoadmapのLocalization Phaseを参照してください。

## 検証

```bash
npm run lint
npm run typecheck
npm test
npm run build
```

実Server / WebSocket / headless browserを接続するE2Eは`.github/workflows/e2e.yml`へ集約されています。Protocol 2.16までの実装済み主要domainを対応するE2E / unit testで継続検証します。

詳細は[`../../docs/architecture/web-client.md`](../../docs/architecture/web-client.md)を参照してください。
