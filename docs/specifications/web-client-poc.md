# Web Client 最小 PoC 仕様

## Goal

Phase 5ではBrowserからHeadless Serverへ接続し、subscription範囲内Agentを受信・補間・描画できる最小Clientを完成させます。同時に将来の効果音・3D音・環境音を追加できるAudio Client Foundationを用意します。

## Connection

- Server URLは `VITE_SERVER_URL` で上書きできる。
- 未指定時は `ws://127.0.0.1:5080/ws` を使用する。
- 接続直後にProtocol 1.0 `Hello` を送る。
- `HelloAck` 後だけsubscriptionを送る。
- binary frameのみ処理する。
- 切断時はClient entity stateを破棄する。
- 自動再接続は1秒開始、最大5秒の指数backoffとする。

## Entity state / rendering

- `AgentSpawn` はClient EntityStoreへ追加する。
- `AgentUpdate` はprevious/current snapshotを更新する。
- `AgentRemove` はClient stateと関連audio emitterを削除する。
- Positionはsnapshot受信間隔を基準にprevious/current間で線形補間する。
- Agent描画はInstancedMeshの最小box形状とする。
- Cameraはdragでpan、wheelでzoomできる。
- Camera visible areaに20% marginを足した矩形をsubscriptionとして送る。

## UI / localization

- default localeはlocale manifestから選択する。
- Phase 5 resourceは`ja-JP`のみ。
- 接続state、Protocol version、Agent count、Audio stateを表示する。
- Protocol errorのuser-facing textはlocale resourceから解決する。

## Audio foundation

- AudioContextはuser gesture後にのみresumeする。
- Master / Music / UI / Ambient / World / Voice busを持つ。
- Cue IDからmanifestを通じてassetを解決し、callerへasset pathを露出しない。
- Short SFXをAudioBuffer cacheできる。
- non-positional / positional play APIを持つ。
- Camera transformをWeb Audio listenerへ同期する。
- Positional looping emitterはvoice budgetでvirtualizeする。
- Entity position/removeをemitterへ反映できる。
- Global ambient / Ambient Zone / overlap / fade / external parameterを扱える。
- Web Audio API非対応でもClient通信・描画は継続する。
- Server / Protocolは音声asset pathを送らない。

## Out of scope

- 実際の音源asset
- semantic world event Protocol message
- BGM selection UI
- 保存されるvolume設定
- production deployment
- E2E performance acceptance
