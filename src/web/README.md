# Web Client

ブラウザベースの 3D クライアントを配置する予定です。

表示、入力、snapshot decode、interpolation を担当し、Simulation の正本は持ちません。

## Localization

将来の多言語対応を見越し、ユーザー向け表示文言の localization は Web Client の責務とします。

- default locale: `ja-JP`
- locale resource 入口: `src/web/locales/`
- Server / Protocol は原則として翻訳済み UI 文言を送らない
- 数値、日時、単位の表示は Client 側で locale-aware に format する

詳細は `docs/architecture/localization.md` を参照してください。
