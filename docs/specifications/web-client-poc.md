# Web Client 基盤仕様

## Goal

Phase 5で成立させたBrowser Clientを基礎とし、Phase 9以降はHeadless Serverから3D subscription範囲内の観測データを受信・補間・描画する **read-only View Client** として扱います。Audio Client Foundationも同じXYZ座標を使用します。

ViewはSimulationのauthoritative stateを変更せず、意味的な判定・推測・集計・予定生成を行いません。Simulationが公開したObservationだけを忠実に表示します。観測境界の設計は[`../architecture/observation-gateway.md`](../architecture/observation-gateway.md)、現行Protocol versionと追加domainのbinary契約は[`../architecture/protocol.md`](../architecture/protocol.md)、View実装計画は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)を正本とします。

World / City / Serverを変更する操作UIは[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)で管理し、Viewへmutation責務を持ち込みません。

## Connection

- Server URLは`VITE_SERVER_URL`で上書きできる。
- 未指定時は`ws://127.0.0.1:5080/ws`を使用する。
- 接続直後にClientが対応するcurrent Protocolの`Hello`を送る。
- `HelloAck`後だけ`SubscribeVolume`等のObservation Requestを送る。
- binary frameのみ処理する。
- 切断時はClient entity / debug / observation stateを破棄する。
- 自動再接続は1秒開始、最大5秒の指数backoffとする。
- Protocol 1.x / `SubscribeArea`へのfallbackは行わない。

`SubscribeVolume`、明示Inspector request等は表示対象を要求するためのObservation Requestであり、Simulation mutation commandとして扱いません。

## Entity state / rendering

- `AgentSpawn`はClient EntityStoreへXYZ position / XYZ velocityを追加する。
- `AgentUpdate`はprevious/currentのXYZ snapshotを更新する。
- `AgentRemove`はClient stateと関連audio emitterを削除する。
- Positionはsnapshot受信間隔を基準にprevious/current間で3軸線形補間する。
- 補間結果は描画専用であり、Simulationのauthoritative stateや意味的stateとして扱わない。
- Agent描画は`THREE.InstancedMesh`の最小box形状とする。
- Simulation `(X,Y,Z)`はThree.js `(X,Z,Y)`へ明示変換する。
- Cameraはdragで水平pan、wheelでzoomできる。
- subscriptionはOrthographicCameraのnear/farを含む8つのfrustum cornerから3D AABBを算出し、paddingを加えた`SubscribeVolume`として送る。
- 固定の高度slabでsubscriptionを切らず、cameraの高度・向き・clip rangeへ追従する。
- Camera / View cache / Rendering LODをSimulationのauthoritative stateやworkloadの判定条件に使用しない。
- Clientはposition・時刻・destination等からActivity / ETA / classification / event等を推測しない。表示に必要な意味はSimulationのObservation contractから受け取る。

## UI / localization

- default localeはlocale manifestから選択する。
- 現在のdefault resourceは`ja-JP`とする。
- 接続state、negotiated Protocol version、各read-only observation/debug stateを必要に応じて表示する。
- Protocol errorのuser-facing textはlocale resourceから解決する。
- read-only Inspector / observation overlay / localization拡張はView Roadmapで管理する。
- World / City編集、Simulation運転、Server設定、Save / Load、Addon操作等のmutation UIはManagement Roadmapで管理する。
- Dashboard、統計分析、trend、heatmap等の分析機能はView / Managementの必須責務に含めず、将来のAnalytics Listener / analysis clientとして別途設計する。

## Inspector observation

Object Inspectorで表示するCurrent / Recent Past / Planned Futureは、Viewが生成せずSimulation側のObservation contractを正本とします。

- Current: Simulationが公開した現在状態を表示する。
- Recent Past: Simulationが公開したrecent state / event、または意味を付加しない観測履歴を表示する。
- Planned Future: Simulationが公開したschedule / planned stateを表示する。
- Relations: Simulationが公開したstable ID参照を使い、View独自の関係推測を行わない。

Viewが保持してよい短期履歴は描画補間・再描画・受信cache等のpresentation用途に限定し、そこから新しいsemantic eventを生成しません。

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

- Simulation stateへのmutation
- World / City editor、Simulation運転、Server管理、Save / Load操作
- View側でのsemantic state / event / ETA / schedule生成
- Dashboard / statistics analysis / trend / heatmap等の分析
- 実際の音源asset
- BGM selection UI
- 保存されるvolume設定
- production deployment

将来Task化する場合は、authoritative state / semantics / observation contractをSimulation Roadmap、純粋な描画・観測をView Roadmap、mutation / administration UIをManagement Roadmapへ分割します。分析系は必要になった時点でAnalyticsを独立した責務として設計します。
