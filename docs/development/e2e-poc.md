# Phase 6 End-to-End PoC

Phase 6 では、Simulation Core、Headless Server、Protocol、Web Client を実際に接続し、Agent 数を増やしても Client が subscription 範囲だけを受信・描画できることを確認します。

## 検証対象

| Scenario | Server Agent | Browser subscription | 主な確認内容 |
| --- | ---: | --- | --- |
| 基本接続 | 1,000 | 全域 → camera移動 → 近傍 | 接続、全Agent表示、subscription更新、remove、再接続後のstate復元 |
| 中規模 | 10,000 | camera近傍 | Server全体では10,000 Agentをstepし、Browserは近傍だけを保持・描画 |
| 大規模 | 100,000 | camera近傍 | Server全体では100,000 Agentをstepし、Protocol配信を近傍に限定 |

Browser smoke test は production code の `MachiVerseConnection`、`EntityStore`、`WorldView`、Protocol decoder を直接使用します。専用の模擬Protocol実装へ置き換えません。

## 実行方法

Repository root で次を実行します。

```bash
bash scripts/run-phase6-e2e.sh
```

スクリプトは `.NET` restore/build、Web Client の `npm ci` / build、Vite、Headless Server、headless Chrome/Chromium を順に使用します。Chrome または Chromium が `PATH` に必要です。

Browserの完了待ちはChrome DevTools ProtocolをNode.jsから実時間で監視します。Chromeのvirtual timeはWebSocket相手のServer時間と一致しないため使用しません。

実行結果は `.artifacts/phase6-e2e/` に保存します。

- `browser-*.html`: Browser smoke test の最終DOMとClient計測値
- `server-metrics-*.json`: Serverのsnapshot送信計測値
- `server-*.log`: 各Agent数のServer log
- `vite.log` / `chrome.log`: Web/Browser側の診断log

GitHub Actions の `Phase 6 E2E` workflow でも同じスクリプトを実行するため、ローカルとCIで検証手順を分けません。

## Server 計測

`GET /metrics/e2e` は Phase 6 のPoC用に次を返します。

- snapshot delivery回数
- Protocol message数
- snapshot配信bytes
- `ProtocolCodec.Serialize` に費やした時間
- `WebSocket.SendAsync` に費やした時間
- 直近subscription内のAgent数とmessage数

計測は比較可能な観測点を作るための軽量な累積値であり、本番監視基盤ではありません。

## Client 計測

Web Client のstatus panelへ次を表示します。

- Protocol frame decode時間の平均 / 最大
- animation frame間隔の平均 / 最大

Browser E2E artifactにも同じ計測値を残します。絶対値はrunner、GPU、ブラウザ、負荷で変わるため、このPhaseでは固定閾値を性能合否条件にしません。

## PoCで確認する結果

Phase 6 の成功条件は次です。

1. Browserが実ServerとHello/HelloAckを完了する。
2. 1,000 Agent構成で全域subscriptionから1,000 AgentをClientへ復元できる。
3. camera由来subscriptionを範囲外へ移すと既知Agentがremoveされる。
4. subscriptionを保持したまま再接続するとClient stateを再構築できる。
5. 10,000 / 100,000 Agent構成でも、Clientへ配信・保持するAgent数がServer全体数より少ない。
6. Serverのbytes / encode / sendと、Clientのdecode / frameを記録できる。

実行時の具体的な計測値は `.artifacts/phase6-e2e/` を正とします。

## 既知のボトルネック

Phase 6 はEnd-to-End成立確認を目的とし、次の最適化はPhase 7で扱います。

- 現在は Agent 1件につき `AgentSpawn` / `AgentUpdate` を1つのWebSocket frameとして送るため、Agent数よりもmessage数と`SendAsync`回数が先に効きやすい。
- `SnapshotMessagePlanner` はsnapshotごとにsort、`HashSet`、message `List` を生成するため、subscription内Agentが増えるとCPUとallocationが増える。
- Clientは受信Agentを `Map` に保持し、frameごとに全可視Agentの補間位置を書き出すため、可視Agent数がframe timeへ直接影響する。
- `InstancedMesh` は個別Meshより効率的だが、capacity拡張時にはGPU resourceの作り直しが発生する。
- Phase 6 metrics はプロセス内累積値であり、histogram、percentile、接続別seriesは持たない。

これらを計測可能な状態にしたうえで、Phase 7 の batching、差分配信、buffer再利用、可視範囲処理の最適化へ進みます。
