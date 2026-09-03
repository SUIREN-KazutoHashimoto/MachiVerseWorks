# View Runtime Visual E2E

この試験は、Renderer 単体 fixture ではなく、ユーザーが通常起動時に見る経路をそのまま観測するための View E2E です。

```text
Simulation
  -> Server / Gateway protocol
  -> MachiVerseConnection
  -> Application.state
  -> WorldView
  -> Browser screenshot / diagnostics
```

`run-view-phase03-e2e.sh` は既存の Physical World Golden 比較を完了した後、Server を通常 Agent 生成設定で再起動し、実アプリの `/` を `?visualTest=runtime` 付きで開きます。テスト側から Agent / Building / Settlement は注入しません。

初期導入時は既知の表示不具合を Golden として固定しないため observation-only とし、`runtime-default.png`、`runtime-agent-cloud.png`、`runtime-worst-grounding.png` と高度差 diagnostics を Artifact へ保存します。通常Viewの表示を修正し、実ランタイム画像をレビューした後に Golden 比較へ昇格させます。
