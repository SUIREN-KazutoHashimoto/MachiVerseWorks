# Web Client 基盤仕様

## Goal

Phase 5で成立させたBrowser Clientを基礎とし、Phase 9以降はHeadless Serverから3D subscription範囲内Agentを受信・補間・描画するClientとして扱います。Audio Client Foundationも同じXYZ座標を使用します。

この文書は基盤として維持する挙動を定義します。現行Protocol versionと追加domainのbinary契約は[`../architecture/protocol.md`](../architecture/protocol.md)、現在および将来のView実装計画は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)を正本とします。

## Connection

- Server URLは`VITE_SERVER_URL`で上書きできる。
- 未指定時は`ws://127.0.0.1:5080/ws`を使用する。
- 接続直後にClientが対応するcurrent Protocolの`Hello`を送る。
- `HelloAck`後だけ`SubscribeVolume`を送る。
- binary frameのみ処理する。
- 切断時はClient entity / debug stateを破棄する。
- 自動再接続は1秒開始、最大5秒の指数backoffとする。
- Protocol 1.x / `SubscribeArea`へのfallbackは行わない。

## Entity state / rendering

- `AgentSpawn`はClient EntityStoreへXYZ position / XYZ velocityを追加する。
- `AgentUpdate`はprevious/currentのXYZ snapshotを更新する。
- `AgentRemove`はClient stateと関連audio emitterを削除する。
- Positionはsnapshot受信間隔を基準にprevious/current間で3軸線形補間する。
- Agent描画は`THREE.InstancedMesh`の最小box形状とする。
- Simulation `(X,Y,Z)`はThree.js `(X,Z,Y)`へ明示変換する。
- Cameraはdragで水平pan、wheelでzoomできる。
- subscriptionはOrthographicCameraのnear/farを含む8つのfrustum cornerから3D AABBを算出し、paddingを加えた`SubscribeVolume`として送る。
- 固定の高度slabでsubscriptionを切らず、cameraの高度・向き・clip rangeへ追従する。
- Camera / View cache / Rendering LODをSimulationのauthoritative stateやworkloadの判定条件に使用しない。

## UI / localization

- default localeはlocale manifestから選択する。
- 現在のdefault resourceは`ja-JP`とする。
- 接続state、negotiated Protocol version、各debug stateを必要に応じて表示する。
- Protocol errorのuser-facing textはlocale resourceから解決する。
- 本格的なInspector / Dashboard / overlay / management UI / localization拡張はView Roadmapで管理する。

## Audio foundation

- AudioContextはuser gesture後にのみresumeする。
- Master / Music / UI / Ambient / World / Voice busを持つ。
- Cue IDからmanifestを通じてassetを解決し、callerへasset pathを露出しない。
- Short SFXをAudioBuffer cacheできる。
- non-positional / positional play APIを持つ。
- Simulation `(X,Y,Z)`をWeb Audio `(X,Z,Y)`へ明示変換する。
- Camera transformをWeb Audio listenerへ同期する。
- Positional looping emitterはvoice budgetでvirtualizeする。
- Entity position/removeをemitterへ反映できる。
- Global ambient / Ambient Zone / overlap / fade / external parameterを3D位置で扱う。
- Web Audio API非対応でもClient通信・描画は継続する。
- Server / Protocolは音声asset pathを送らない。

## Out of scope

- 実際の音源asset
- semantic world event Protocol message
- BGM selection UI
- 保存されるvolume設定
- production deployment

これらを将来Task化する場合は責務に応じてSimulation / View Roadmapへ分割します。
