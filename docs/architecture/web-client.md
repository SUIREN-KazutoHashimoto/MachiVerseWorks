# Web Client Architecture

## 目的

Phase 5 の Browser Client は、Headless Serverから受け取るProtocol snapshotを描画可能なClient stateへ変換する最小presentation層です。Simulationの権威はServerに残し、Clientは予測・Simulation更新を行いません。

## Data flow

```text
WebSocket
  -> MachiVerseConnection
  -> Protocol decoder
  -> EntityStore
  -> interpolation
  -> WorldView / InstancedMesh
```

`MachiVerseConnection` は connection lifecycle、Hello/HelloAck、binary frame受信、切断検知、最小reconnectを所有します。Protocol wire layoutはC# projectへ依存せず、`src/web/src/protocol.ts` に同じstable contractを実装します。

`EntityStore` はClient側のsnapshot stateだけを所有します。spawn/update/removeを順序通り反映し、update時に previous/current position と受信間隔を保存します。描画は1 snapshot分遅延させ、受信間隔を基準にpositionを線形補間します。

## Camera / subscription

Three.jsのOrthographicCameraをworld上面から見る構成にし、Simulation `(X,Y)` をThree.js `(X,Z)` に対応させます。Cameraの中心・aspect・zoomからvisible rectangleを求め、20% paddingを加えた範囲を `SubscribeArea` として送信します。

Drag/zoom中のcommand floodを避けるため、subscription再評価は200ms周期で行い、前回矩形と実質的に同じ場合は送信しません。Reconnect後は最新のdesired subscriptionをHelloAck後に再送します。

## Rendering

Agentは個別Meshを常設せず `THREE.InstancedMesh` で描画します。必要capacityは2倍ずつ拡張します。Phase 6で10,000/100,000 AgentのE2E計測を行うため、Phase 5の時点でAgent数に比例したdraw callを作らない構成にします。

## Localization

`locales/manifest.json` の `defaultLocale` がClient起動時localeの正本です。Phase 5では `ja-JP.json` と小さな `Localizer` を使います。Protocol errorはnumeric codeのまま受信し、`error.protocol.<code>` resourceへClient側で変換します。

## Reconnect

WebSocket close時はClient EntityStoreを破棄します。1秒から最大5秒までの指数backoffで再接続し、新しいServer connectionのspawn snapshotからstateを再構築します。古いconnectionのentity stateを新しいsessionへ持ち越しません。
