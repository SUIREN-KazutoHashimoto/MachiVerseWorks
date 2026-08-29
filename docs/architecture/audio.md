# Audio Client Architecture

## 境界

音声はSimulationやrenderingから独立したClient presentation systemです。Server / Protocolは音声file pathや`PlaySound`命令を送信しません。将来、列車ドア開閉などのsemantic world eventをProtocolで表現し、Client側のresolverがstable audio cue IDへ変換します。

```text
semantic world event / client state
              |
          cue resolver
              |
        stable cue ID
              |
          AudioEngine
     /       |        \
  mixer   positional  ambient
```

## AudioEngine lifecycle

Browserのautoplay policyに合わせ、`AudioContext` はuser gestureで `unlock()` されるまで開始しません。Stateは `locked / running / suspended / unavailable` でUIへ公開します。Web Audio APIが存在しない場合は `unavailable` とし、描画・通信機能は継続します。

Mixer graphは次のcategory busを持ちます。

```text
Master
|- Music
|- UI
|- Ambient
|- World
`- Voice
```

Master mute / master volume / category volumeはAudioEngineのAPIで制御します。

## Asset ownership

`src/web/audio/manifest.json` がcue metadataの正本です。Applicationはfile pathを受け取らず `audio.play("train.door.open")` のようなcue IDだけを使います。実assetは `src/web/public/audio/` から配信します。Short SFXはdecode後の`AudioBuffer`をcue ID単位でcacheします。

## Positional audio / virtualization

Three.js cameraのworld transformをWeb Audio `AudioListener` へ同期します。Positional cueは`PannerNode`を使い、Simulation `(X,Y,Z)` はWeb Audio `(X,Z,Y)`へ明示変換します。

多数AgentにAudioNodeを常設しません。virtual emitter stateだけを保持し、listenerからの距離とpriorityを評価してworld voice budget内のemitterだけを実voiceへ昇格します。既定world budgetは64です。

Entity-linked emitterは次の2つのindexを同期して管理します。

- `emitterId -> VirtualEmitter`
- `entityId -> emitterId set`

Agent position更新時は`entityId` indexから該当emitterだけを更新し、全emitter scanを行いません。1 entityに複数emitterを関連付け可能です。register/replace/remove時にindexを同時更新し、Entity削除時は紐付くemitterだけを解放します。Applicationはlinked emitterが存在しないAgentについて位置objectの生成とAudioEngine位置更新を省略します。

位置のfinite validationはpublic boundaryで維持しますが、正常系でXYZ成分ごとのdiagnostic文字列を組み立てないよう、failure時だけエラーメッセージを生成します。

## Ambient

環境音はglobal layerと3D Ambient Zoneに分けます。Zoneはpriority、edge fade distance、複数layerを持てます。重複Zoneは最高priorityを基準に低priority側へweightを減衰させ、同一layerはgainを合成します。

外部parameter (`rain`, `crowd` など) を0..1で渡せるため、将来Simulation stateやweather stateに応じてambient mixを変更できます。Layerの増減はAudioEngine側でfade/crossfadeします。

`AmbientSystem`のmix反映はtransaction的に扱います。複数layerの適用途中で1件が失敗した場合、すでに成功したlayerを更新前の状態へrollbackしてから失敗を返します。rollback自体が失敗した場合も、実際に残り得るlayerをtracking stateから落とさず、次回updateでreconcile可能な状態を維持します。これにより部分成功した未追跡のambient voiceを残しません。
