# View Phase 4 rendering baseline

`view-phase04-rendering-baseline.json` は View Phase 4 の Settlement / Structure renderer に対する、version管理された回帰baselineである。

## 実行

```bash
./scripts/run-view-phase04-e2e.sh
```

このコマンドは static fixture と Simulation Phase 31 evolution fixture を実Chrome / Chromium + Three.jsで描画し、`.artifacts/view-phase04-e2e/rendering-baseline.txt` に実測値を出力した後、`scripts/check-view-phase04-rendering-baseline.mjs` で本baselineと比較する。

## 比較ルール

entity countやfixture由来のsemantic値は環境に依存しないため `exact` で固定する。具体的には Settlement / Parcel / Building / label / Road Sign の個数、Phase 31 evolutionの`currentYear`とSettlement数を完全一致で検証する。

Three.js / GPU / browser実装差の影響を受け得るrender statsは `minimum` として扱い、draw call / geometryがゼロへ退行していないことをgateする。環境依存値を不用意に完全一致へ変更しない。

## baseline更新ルール

baselineを変更する場合は、rendererまたはfixtureの意図的な変更と同じPRで行い、PR本文に「何が変わったか」「なぜ新しい値が正しいか」を記録する。テスト失敗だけを解消する目的でbaselineを追従更新しない。

fixtureのsemantic構造を変更した場合は、対応するBrowser E2E assertionsも同時に更新する。render statsの下限を下げる場合は、描画primitiveが失われていないことを別assertionで説明できる状態にする。
