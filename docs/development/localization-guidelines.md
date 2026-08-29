# Localization Development Guidelines

この文書は MachiVerseWorks の多言語対応を将来追加しやすくするため、実装時に守るルールを定めます。

現時点では日本語のみで開発して構いません。ただし、後から翻訳対応するときに Simulation / Protocol / Save Data の互換性を壊さないことを優先します。

## 1. ユーザー向け文字列

Web Client の実装開始後、ユーザーへ表示する固定文言は原則として locale resource key 経由で参照します。

例:

```text
menu.settings.title
inspector.vehicle.speed
simulation.status.running
```

プロトタイプ段階で一時的に hard-code した場合も、本実装へ残す前に resource 化します。

## 2. key 命名

key は画面上の日本語ではなく、意味で命名します。

```text
<area>.<feature>.<meaning>
```

例:

```text
common.ok
common.cancel
menu.settings.title
error.network.disconnected
traffic.vehicle.state.waitingSignal
```

番号だけの key や表示文そのものを key にしません。

## 3. 値の埋め込み

値の埋め込みは named parameter を使います。

良い例:

```text
"現在の人口: {count}"
```

避ける例:

```ts
"現在の人口: " + count
```

単語単位の翻訳結果をコードで連結して文章を作らないでください。言語ごとに語順や文法が変わるためです。

## 4. Protocol / API

Server からユーザー向けメッセージを返す必要がある場合は、可能な限り次の形式を使用します。

```text
code + structured parameters
```

例:

```json
{
  "code": "command.road.invalidConnection",
  "parameters": {
    "roadId": 12
  }
}
```

翻訳済み日本語を Protocol の正式契約にしません。

ユーザー入力文字列や外部から取得した固有名詞は、この制約の対象外です。

## 5. 保存データ

保存データへ表示ラベルを永続化しないでください。

保存するもの:

- stable ID
- enum / code
- raw numeric value
- user-entered text

保存しないもの:

- 翻訳済み状態名
- locale 固有の日付文字列
- locale 固有の数値表記

## 6. Formatting

数値・日時・単位は locale-aware formatter を経由します。

Web Client では Web 標準の `Intl` API を第一候補とします。

同じ値でも locale によって次が変わることを前提にしてください。

- `1,234.56` / `1.234,56`
- YYYY/MM/DD / MM/DD/YYYY / DD/MM/YYYY
- 12時間 / 24時間
- unit placement

## 7. UI レイアウト

UI review では、日本語だけでなく「翻訳後に1.5〜2倍程度長くなる可能性」を考慮します。

- 固定幅 label を乱用しない。
- button の text overflow を確認する。
- table column が文字列長だけで崩れないようにする。
- tooltip / aria-label も将来 resource 化できる構造にする。

## 8. Accessibility

画面上に見える文字だけを localization 対象と考えないでください。

将来 resource 化する対象:

- aria-label
- alt text
- tooltip
- dialog title
- validation message
- keyboard shortcut description

## 9. テスト

多言語対応を有効化した後は、CI で次を検証できるようにします。

- default locale の missing key がない。
- resource key の重複がない。
- placeholder 名が locale 間で一致している。
- locale manifest と実ファイルが一致している。

unused key 検出は false positive が多い場合があるため、導入時に運用を判断します。

## 10. 現在の運用

初期セットアップ中は次だけを必須とします。

1. Simulation / Protocol / Save Data を言語非依存に保つ。
2. Web Client の locale resource 置き場を `src/web/locales/` に固定する。
3. default locale を `ja-JP` とする。
4. i18n library はまだ固定しない。

Web Client の実装開始時に、この文書を基準として実際の i18n service を構築します。
