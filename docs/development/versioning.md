# バージョン運用

MachiVerseWorks のアプリケーションバージョンと、互換性用versionの責務を定義します。

## 1. アプリケーションバージョン

通常開発開始後は、リポジトリルートの `VERSION` を**唯一の正本**とします。

```text
A.B.C
```

`VERSION` には余分な接頭辞や説明を入れず、例として `1.4.12` のような値だけを保存します。

初期セットアップ期間は `VERSION` を作成しません。通常開発へ移行することが明示された時点で初期値を決定して作成します。

## 2. カウント規則

- `A`: `main` 向け PR を作成するときに `+1` し、`B = 0`, `C = 0` にする。
- `B`: `develop` 向け PR を作成するときに `+1` し、`C = 0` にする。
- `C`: 通常の開発コミットを作成するときに `+1` する。

例:

```text
1.4.12
  ↓ 通常コミット
1.4.13
  ↓ develop 向け PR
1.5.0
  ↓ 通常コミット
1.5.1
  ↓ main 向け PR
2.0.0
```

PR 作成に伴う A / B のversion更新コミットでは、同じ操作で C を別途加算しません。GitHub がPRマージ時に生成する merge commit も管理上のコミットとして扱い、C を加算しません。

## 3. 各コンポーネントへの反映

同じアプリケーションversionを複数ファイルへ手入力しません。

将来の実装では次を基本とします。

- C# build: ルート `VERSION` を MSBuild から読み取り、Assembly / Package metadataへ反映する。
- Server: health / metadata / log など必要な場所で build version を公開する。
- Web Client: build時にルート `VERSION` を読み取り、画面表示やdiagnosticsへ反映する。
- Release workflow: tag / Release名 / artifact metadata は `VERSION` を参照する。

`package.json` や個別 `.csproj` の値をアプリケーションversionの別正本にしません。

## 4. 独立して管理するversion

次はアプリケーションversionと意味が異なるため、`VERSION` と連動させません。

### Protocol version

Client / Server 間のwire互換性を表します。

アプリケーションversionが進んでもProtocolが変わらない場合があります。逆にProtocolのbreaking changeは明示的にProtocol versionを更新します。

### Save format version

保存データの読み書き互換性を表します。

アプリケーションversionとは独立して更新し、migration / rejection判断に使用します。

## 5. CI

初期セットアップ中は `VERSION` が存在しない状態を許可します。

`VERSION` が追加された後はCIで最低限次を検証します。

- `A.B.C` の3整数形式であること
- 前後に不要な文字や空白行を持たないこと

通常開発へ移行するときは、CIの必須ファイル一覧にも `VERSION` を追加します。

## 6. 禁止事項

- ServerとWeb Clientで別々にアプリケーションversionを手更新しない。
- Protocol versionをアプリケーションversionで代用しない。
- Save format versionをアプリケーションversionで代用しない。
- Git tagだけをversionの正本にしない。
- build日時を公式versionの代わりにしない。
