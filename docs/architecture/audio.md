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

Phase 5では基盤だけを実装し、実音源と実cueはまだ追加しません。

## Positional audio / virtualization

Three.js cameraのworld transformをWeb Audio `AudioListener` へ同期します。Positional cueは`PannerNode`を使い、既定値はmanifestで管理します。

多数AgentにAudioNodeを常設しません。`AudioEmitterRegistry`相当のvirtual emitter stateだけを保持し、listenerからの距離とpriorityを評価してworld voice budget内のemitterだけを実voiceへ昇格します。既定world budgetは64です。Entityと結び付いたemitterはAgent position更新で移動し、remove時に解放します。

## Ambient

環境音はglobal layerと矩形Ambient Zoneに分けます。Zoneはpriority、edge fade distance、複数layerを持てます。重複Zoneは最高priorityを基準に低priority側へweightを減衰させ、同一layerはgainを合成します。

外部parameter (`rain`, `crowd` など) を0..1で渡せるため、将来Simulation stateやweather stateに応じてambient mixを変更できます。Layerの増減はAudioEngine側でfade/crossfadeします。
