# Web Client Architecture

## 目的

Browser ClientはHeadless Serverから受け取るProtocol 2.x snapshotを描画可能なClient stateへ変換するpresentation層です。Simulationの権威はServerに残し、Clientは予測・Simulation更新を行いません。

## Data flow

```text
WebSocket
  -> MachiVerseConnection
  -> Protocol decoder (XYZ)
  -> EntityStore (XYZ previous/current)
  -> interpolation (XYZ)
  -> WorldView / InstancedMesh
                       \\-> Web Audio
```

`MachiVerseConnection`はconnection lifecycle、Hello/HelloAck、binary frame受信、切断検知、最小reconnectを所有します。Protocol wire layoutはC# projectへ依存せず、`src/web/src/protocol.ts`に同じstable contractを実装します。

`EntityStore`はClient側のsnapshot stateだけを所有します。spawn/update/removeを順序通り反映し、update時にXYZのprevious/current positionと受信間隔を保存します。描画は1 snapshot分遅延させ、受信間隔を基準に3軸positionを線形補間します。

## Coordinate mapping

Simulationの正本座標は`(X,Y,Z)`で、Zが高度です。Three.js / Web Audio境界では次の1箇所の規則で明示変換します。

```text
Simulation (X, Y, Z) -> Three.js / Web Audio (X, Z, Y)
```

Simulation内部の軸定義をrenderer固有座標へ合わせません。Agent、listener、positional audio、Ambient Zoneは同じ変換を使用します。

## Camera / subscription

Three.jsの`OrthographicCamera`は傾斜した3D cameraとして扱います。subscriptionは2D visible rectangleや固定高度bandから作りません。

`WorldView.getSubscriptionVolume()`はcameraのnear/farを含む8つのfrustum cornerをworld座標へunprojectし、Simulation座標へ写像した3D AABBへ20% paddingを追加して`SubscribeVolume`を生成します。したがってcameraのpan、zoom、高度、向き、clip rangeが変わればXYZ全軸のsubscriptionも追従します。

Drag/zoom中のcommand floodを避けるため、subscription再評価は設定された周期で行い、前回volumeと実質的に同じ場合は送信しません。Reconnect後は最新のdesired subscriptionをHelloAck後に再送します。

Server側は`MaximumSubscriptionCellCount`で外部入力を制限します。既定cameraの16:9最小zoomでfull frustum volumeを受理できるよう、現在の既定budgetは262,144 cellsです。

## Rendering

Agentは個別Meshを常設せず`THREE.InstancedMesh`で描画します。必要capacityは2倍ずつ拡張します。各instanceのtranslationは`(sim.X, sim.Z + halfSize, sim.Y)`とし、高度差を実mesh transformへ反映します。

Browser E2Eではhelperの戻り値だけでなく、`WorldView.render()`後の`InstancedMesh` matrixを観測して同一XY・異なるZが異なるThree.js Yへ配置されることを確認します。

## Localization

`locales/manifest.json`の`defaultLocale`がClient起動時localeの正本です。Protocol errorはnumeric codeのまま受信し、`error.protocol.<code>` resourceへClient側で変換します。

## Reconnect

WebSocket close時はClient EntityStoreを破棄します。1秒から最大5秒までの指数backoffで再接続し、新しいServer connectionのspawn snapshotからstateを再構築します。古いconnectionのentity stateを新しいsessionへ持ち越しません。
