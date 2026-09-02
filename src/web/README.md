# MachiVerseWorks Web View

Vite + TypeScript + Three.jsで構成するMachiVerseWorksのread-onlyブラウザ3D Viewです。都市世界の正本と意味的処理はSimulationにあり、WebはObservation Gatewayから受信したsnapshot / inspection resultの表示・補間・Camera・Selection・Inspector・audioを担当します。

- Simulation側のsemantic observation source: [`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)
- Observation Request / subscription / delivery / reconnect等のGateway側計画: [`../../roadmap/GATEWAY_ROADMAP.md`](../../roadmap/GATEWAY_ROADMAP.md)
- View側の将来実装・改善計画: [`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)
- World / City / Server操作UI: [`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)
- Observation Gateway設計: [`../../docs/architecture/observation-gateway.md`](../../docs/architecture/observation-gateway.md)

## Read-only invariant

- Web ViewはSimulation stateを変更するcommandを送信しない。
- `SubscribeVolume` / Inspect系requestはObservation Requestでありmutationではない。
- Activity、ETA、分類、予定、semantic event、分析結果等をWeb側で意味的に推測・再計算しない。
- Camera / Selection / FPS / LOD / View cache / Client接続数でSimulation結果を変えない。
- Management ClientがView componentを再利用する場合も、command clientはView moduleとは分離する。

## Data flow

```text
WebSocket / Protocol 2.16
  ├─ Agent / Pedestrian / Vehicle stores -> interpolation -> WorldView
  ├─ Road Network -----------------------> static road geometry
  ├─ Intersection Control ---------------> read-only render state
  ├─ Population --------------------------> observation / Person inspector
  ├─ Railway Infrastructure --------------> revision-aware static 3D layer
  ├─ Railway Operations ------------------> Train layer / observation
  ├─ Multimodal Transit ------------------> realtime observation
  ├─ Economy / Logistics -----------------> domain observation state
  ├─ Power / Water-Sewer / Gas ----------> infrastructure observation state
  ├─ Optical -----------------------------> communication observation state
  └─ Radio / Spectrum --------------------> radio observation state
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

## Camera / Observation subscription

- 左ドラッグ: cameraの水平移動
- マウスホイール: zoom
- `音声を有効化`: browser user gesture要件を満たして`AudioContext`をresume

Cameraのnear / farを含む8つのfrustum cornerから3D AABBを計算し、paddingを加えて`SubscribeVolume`を送信します。固定高度bandや`SubscribeArea`は使用しません。

Serverから`subscriptionVolumeTooLarge`を受けた場合はzoom-inして再購読します。Reconnect時はClient側のdynamic / static / observation stateを破棄し、HelloAck後の新しいsnapshotから再構築します。

World scale Camera、Rendering LOD、Client-local rendering / asset cacheはView Roadmapで管理します。Observation subscription / shared Server cache / delivery / reconnect / resyncはGateway Roadmapで管理し、CameraやLODをSimulation workload / fidelityの判定条件に使用しません。

## Rendering / observation

Simulation `(X,Y,Z)`はThree.js / Web Audio境界で`(X,Z,Y)`へ明示変換します。

- AgentはInstancedMesh、Pedestrian / Vehicleは専用storeから描画する
- RoadとRailway Infrastructureはstatic / revision-aware geometryとして扱う
- Railway OperationsのTrainはProtocol 2.7がFormation寸法を送らないため固定サイズのdebug proxy meshで描画する。production化時にSimulation側semantic sourceとGateway delivery contractへ必要なauthoritative情報を追加し、View側で編成形状を推測しない
- Railwayの次到着等はSimulationが公開しGatewayが配送するschedule semanticsを表示し、View側で意味的ETAを再計算しない
- Multimodal TransitはLine / Stop / Pattern、Bus / Taxi位置、arrival estimate等の提供値を表示する
- Economy / Logistics / Power / Water-Sewer / Gas / Optical / Radio-Spectrumは各Protocol decoderからread-only stateへ反映する

現在のdebug表示はSimulation stateを観測するための表示であり、Web側状態はauthoritative stateではありません。production向け描画、Selection / Inspector、Current / Recent / Planned表示、Historical ViewはView Roadmapで計画します。それらを届けるgeneric inspection / temporal / historical deliveryはGateway Roadmapで管理します。

Population / Economy / Traffic等の分析・trend・heatmapをWeb Viewで生成しません。必要な分析は将来Analytics Listener / analysis clientとして分離します。

Editor、pause / resume、Server configuration、Save / Load、Addon install / enable等のmutation / administration操作はManagement Roadmapで管理します。

## Localization

Web Viewのlocale resourceは`locales/`を入口とし、default localeはmanifestの`ja-JP`です。Localization architectureは[`../../docs/architecture/localization.md`](../../docs/architecture/localization.md)、read-only Viewの実装計画はView RoadmapのLocalization Phaseを参照してください。

Managementが同じi18n基盤を再利用しても、Management固有UIのTaskはManagement Roadmapで管理します。

## 検証

```bash
npm run lint
npm run typecheck
npm test
npm run build
```

実Server / WebSocket / headless browserを接続するE2Eは`.github/workflows/e2e.yml`へ集約されています。Protocol 2.16までの実装済み主要domainを対応するE2E / unit testで継続検証します。

View / Gateway基盤の拡張では、View未接続 / 接続中、Camera / Selection / LOD / cache差でSimulation state digestが一致するinvariance testを追加します。

詳細は[`../../docs/architecture/web-client.md`](../../docs/architecture/web-client.md)を参照してください。