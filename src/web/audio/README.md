# Audio Assets

`manifest.json` は Web Client の音声 cue ID と再生metadataの正本です。Application / Simulation event handler はasset pathではなくstable cue IDだけを参照します。

実音源は `src/web/public/audio/` 以下へ配置し、manifestの `source` は `/audio/` からの相対pathとして記述します。Phase 5 では音声基盤のみを実装するため、実音源とcue定義はまだ空です。

Cueの想定例:

```json
{
  "ambient.station.platform": {
    "source": "ambient/station-platform.ogg",
    "category": "ambient",
    "spatial": false,
    "loop": true,
    "gain": 0.8
  }
}
```

Server / Protocolはこのcue IDやasset pathを送信しません。将来のworld eventをClient presentation層でcueへ解決します。
