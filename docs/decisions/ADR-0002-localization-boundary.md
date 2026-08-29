# ADR-0002: Localization Boundary

## Status

Accepted

## Context

MachiVerseWorks は日本語を主言語として開発を開始するが、将来は複数言語へ対応する可能性がある。

旧来の単一言語アプリケーションでよくあるように、Server や Protocol が表示用文字列を直接生成すると、後から locale を追加するときに通信契約、保存データ、ログ、UI の責務が混在しやすい。

MachiVerseWorks は Simulation Server と Browser Client を明確に分離するため、多言語対応もこの境界に沿って設計する必要がある。

## Decision

ユーザー向けの localization は原則として Web Client の責務とする。

- Simulation Core は locale を知らない。
- Server は通常の UI 文言を翻訳しない。
- Protocol は翻訳済み文字列ではなく stable code と structured parameter を送る。
- Save Data は locale-independent な ID / enum / code / raw value を保存する。
- Web Client が locale resource lookup と表示 formatting を担当する。
- default locale は `ja-JP` とする。
- locale tag は BCP 47 形式を使用する。
- i18n library の具体的な選定は Web Client 実装開始時まで固定しない。

ユーザー入力文字列、固有名詞、外部コンテンツなど、翻訳対象ではない文字列はそのままデータとして扱う。

## Consequences

### Positive

- locale 追加で Protocol version を変更する必要が減る。
- Server と Simulation が UI 言語へ依存しない。
- Save Data を別 locale で開いても互換性を保ちやすい。
- Browser ごとに異なる locale を同じ Server へ接続できる。
- 数値・日時・単位の表示責務を Client に集約できる。

### Negative

- Client は message code と resource の対応を管理する必要がある。
- missing translation を検出する仕組みが必要になる。
- Server 由来のエラーでも、Client 側に対応 resource が必要になる。
- Debug log と user-facing message を明確に区別する必要がある。

## Notes

多言語対応そのものは現時点の実装対象ではない。

この ADR の目的は、初期設計の時点で locale 依存データを Simulation / Protocol / Save Data に混入させないことにある。

詳細は `docs/architecture/localization.md` を参照する。
