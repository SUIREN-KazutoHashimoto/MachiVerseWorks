# Locale Resources

Web Client の user-facing text を配置する翻訳 resource の正規入口です。

- default locale: `ja-JP`
- supported locale: `ja-JP`
- i18n library: なし（Phase 5 では小さな `Localizer` を使用）
- `manifest.json` が default / supported locale の正本
- `ja-JP.json` が Phase 5 の最小 UI / Protocol error resource

Web Client は起動時に `manifest.json` の `defaultLocale` を読み、対応する resource を選択します。Protocol の error code は stable numeric code のまま受け取り、Client側で `error.protocol.<code>` に解決します。

## 重要な境界

- Protocol の code / enum を翻訳 key に置き換えない。
- save data に翻訳済み文字列を保存しない。
- user-entered text は翻訳対象にしない。
- number / date / unit formatting は locale-aware API で行う。

詳細:

- `../../../docs/architecture/localization.md`
- `../../../docs/development/localization-guidelines.md`
- `../../../docs/decisions/ADR-0002-localization-boundary.md`
