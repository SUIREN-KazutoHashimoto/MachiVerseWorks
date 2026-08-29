# Locale Resources

Web Client の locale resource を配置するディレクトリです。

MachiVerseWorks は当面日本語のみで開発しますが、将来 locale を追加するときに UI 実装を作り直さなくて済むよう、このディレクトリを翻訳 resource の正規入口とします。

## 現在の状態

- default locale: `ja-JP`
- supported locale: `ja-JP` のみ
- i18n library: 未選定
- 翻訳 resource: Web Client 実装開始時に追加

`manifest.json` は locale の識別情報だけを先に定義します。

## 将来の想定構成

```text
locales/
├─ manifest.json
├─ ja-JP.json
├─ en-US.json
└─ ...
```

実際の resource format は Web Client の i18n library 選定時に確定します。

## 重要な境界

- Protocol の code / enum を翻訳 key に置き換えない。
- save data に翻訳済み文字列を保存しない。
- user-entered text は翻訳対象にしない。
- number / date / unit formatting は locale-aware API で行う。

詳細は次を参照してください。

- `docs/architecture/localization.md`
- `docs/development/localization-guidelines.md`
- `docs/decisions/ADR-0002-localization-boundary.md`
