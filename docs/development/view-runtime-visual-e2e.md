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

`run-view-phase03-e2e.sh` は既存の Physical World Golden 比較を完了した後、Server を通常設定で再起動し、実アプリの `/` を `?visualTest=runtime` 付きで開きます。テスト側から Agent / Building / Settlement は注入しません。

通常ユーザー起動では、旧デバッグ用途の generic 3D Agent population を自動生成しません。`Simulation:InitialAgentCount` の通常設定は `0` とし、明示的な stress / protocol E2E だけが必要な Agent 数と 3D SpawnVolume を指定します。Runtime Visual E2E は Terrain snapshot の受信後も generic Agent が 0 件である状態を正常なユーザー起動契約として確認するため、以前の cyan Agent cloud が再導入された場合は readiness を満たしません。

初期導入時は既知の表示不具合を Golden として固定しないため observation-only とし、`runtime-default.png`、`runtime-agent-cloud.png`、`runtime-worst-grounding.png` と diagnostics を Artifact へ保存します。generic Agent が 0 件の場合、後者2つは Artifact 形状の互換性を維持するため通常カメラの no-op checkpoint として保存されます。通常View全体の表示問題を修正し、実ランタイム画像をレビューした後に Golden 比較へ昇格させます。
