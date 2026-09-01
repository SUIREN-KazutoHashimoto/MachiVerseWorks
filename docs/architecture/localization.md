# Localization / Internationalization Architecture

MachiVerseWorks は初期段階では日本語を主言語として開発しますが、将来の多言語対応で Simulation / Protocol / Save Data / UI の契約を壊さないよう、最初から言語境界を定義します。

この文書は翻訳作業そのものではなく、国際化（i18n）のアーキテクチャを定めます。Localization機能の実装計画・進捗は [`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md) の **View Phase 5 — Localization** を正本とします。

## 1. 基本原則

- シミュレーション状態は言語に依存させない。
- Protocol は原則として翻訳済みの表示文字列を送らない。
- ユーザー向け文言の最終的なローカライズは Web Client が担当する。
- 保存データには表示文言ではなく、安定した ID / code / enum / raw value を保存する。
- 数値、日時、単位、複数形などを文字列結合で組み立てない。
- Locale は BCP 47 language tag（例: `ja-JP`, `en-US`）で扱う。

初期 default locale は `ja-JP` とします。

## 2. 責務境界

### Simulation Core

Simulation Core は locale を知らない設計を基本とします。

例:

```text
energyShortage
population = 50000
speedMetersPerSecond = 13.8
stationId = 42
```

次のような表示用日本語を Simulation の状態として保持しません。

```text
"電力が不足しています"
"人口 50,000人"
"時速49.7km"
```

固有名詞やユーザーが入力した文字列はデータとして保持できますが、システムが生成するカテゴリ名・状態名とは区別します。

### Server

Server は通信、認証、検証、Simulation lifecycle を担当しますが、通常の UI 文言の翻訳は行いません。

クライアントへ通知が必要な場合は、安定した code と構造化された parameter を送ります。

```text
code: "simulation.power.shortage"
parameters:
  deficitMw: 125.4
```

Server の運用ログや診断ログは UI localization の対象外として扱えます。

### Protocol

Protocol の契約に locale 固有の文言を埋め込みません。

推奨:

```text
errorCode
messageCode
eventCode
parameters
entityId
raw numeric values
```

避ける:

```text
errorMessageJa
errorMessageEn
localizedDisplayName
```

Protocol version と翻訳 resource version は独立して扱えるようにします。

### Web Client

Web Client が locale 選択、resource lookup、message formatting、数値・日時・単位の地域形式を担当します。

将来 i18n library を採用する場合も、この責務境界は変更しません。

## 3. Locale resource

Web Client の locale resource は `src/web/locales/` を入口とします。

初期段階では `ja-JP` のみを supported locale とし、実際の UI 実装を開始した時点からユーザー向け固定文言を resource key 経由にします。

key は表示文そのものではなく、意味が安定する名前を使用します。

良い例:

```text
menu.settings.title
simulation.status.paused
error.connection.timeout
inspector.agent.age
```

避ける例:

```text
設定
接続がタイムアウトしました
text_001
```

## 4. Message parameter

翻訳文へ値を埋め込む場合は named parameter を使用します。

```text
population.current = {count}
power.shortage = {deficit} MW不足
```

語順は言語ごとに異なるため、次のようなコード側の文字列結合は避けます。

```text
"人口 " + count + " 人"
```

複数形や grammatical variation が必要になった場合は、採用する i18n engine の plural / select 機能を利用します。

## 5. 数値・日時・単位

内部値は表示形式から分離します。

例:

- Simulation time: 数値または構造化時刻
- 距離: meter を基準とした数値
- 速度: m/s など正規化された内部値
- 電力: W / kW / MW 等へ変換可能な raw value

表示時は Web Client 側で `Intl.NumberFormat`, `Intl.DateTimeFormat` など locale-aware API を利用する方針とします。

小数点、桁区切り、日付順序、12/24時間表記を保存データや Protocol の文字列へ固定しません。

## 6. Fallback

将来複数 locale を追加した場合の基本 fallback は次とします。

```text
requested locale
  -> language-only locale（存在する場合）
  -> ja-JP
  -> resource key を診断表示
```

missing translation を空文字や無言の失敗にしないよう、Development build では検出可能にします。

## 7. Layout

将来の翻訳では日本語より文字列が長くなる場合があります。

UI は次を前提にします。

- 固定文字数を前提に幅を決めない。
- button / label の長さに余裕を持つ。
- text truncation を使う場合は意味が失われない導線を用意する。
- CSS の `left` / `right` への過度な依存を避け、可能な範囲で logical properties を使用する。
- 将来 RTL locale を追加しても全面改修にならない構造を意識する。

RTL 対応そのものは初期実装の必須要件ではありません。

## 8. 固有名詞と生成名

次の文字列は区別します。

1. システム UI 文言
2. システムが生成するカテゴリ・状態名
3. システムが生成する固有名詞
4. ユーザー入力文字列

1 と 2 は localization resource を使用します。

3 は生成ルール自体を locale-aware にする可能性があるため、単純な翻訳 resource と分離します。

4 は翻訳せず、そのままデータとして扱います。

## 9. 翻訳ファイルに入れないもの

- Protocol message type
- enum / command / event の内部 ID
- save data schema key
- database / serialized property name
- metrics key
- telemetry key
- source code identifier

表示層だけで翻訳します。

## 10. 将来の実装段階

多言語対応を実際に開始するときは、少なくとも次を追加します。

1. Web Client の i18n service
2. `ja-JP` resource の正本化
3. locale selector
4. browser locale detection
5. locale persistence
6. missing-key validation
7. resource schema / type safety
8. 最初の追加 locale

具体的なライブラリ選定は Web Client の実装開始時に行い、この文書では固定しません。これらをTaskへ分解するときは [`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md) を更新します。
