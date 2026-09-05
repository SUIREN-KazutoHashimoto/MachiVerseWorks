# バージョン運用

MachiVerseWorks のアプリケーションバージョンと、互換性用versionの責務を定義します。

## 1. アプリケーションバージョン

リポジトリルートの `VERSION` を**公開成果物のRelease versionの唯一の正本**とします。

```text
A.B.C
```

`VERSION` には余分な接頭辞や説明を入れず、例として `0.72.0` や `1.0.0` のような値だけを保存します。

`VERSION` はGit commit数、PR数、branch種別を表す番号ではありません。通常開発では既存値を維持し、Releaseとして公開するversionを決めたときだけ変更します。

## 2. 更新タイミング

通常のfeature / fix / perf / refactor / docs作業、worker branch、`develop`向けPull Requestでは、原則として`VERSION`を変更しません。

複数PRを`develop`へ統合しても、Releaseを決めるまでは同じ`VERSION`を維持できます。

Releaseを作成するときは、公開する成果物に付与したいversionへ明示的に更新します。

例:

```text
VERSION = 0.71.0
  ↓ feature / fix / refactor PRを複数merge
VERSION = 0.71.0
  ↓ 次のReleaseを0.72.0にすると決定
VERSION = 0.72.0
  ↓ develop -> main
Release 0.72.0
```

`develop -> main`というbranch操作そのものはversion番号を決定しません。Release PRには、公開したいversionが既に`VERSION`へ設定されている状態で含めます。

A / B / Cのどこを変更するかは、そのReleaseの互換性・規模・公開方針に応じて決定します。Git branch名やPR種別から機械的に`A+1`、`B+1`、`C+1`を要求しません。

一度公開済みのversionを別内容のReleaseへ再利用しません。

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

通常CIでは`VERSION`について次だけを検証します。

- Repository rootに`VERSION`が存在すること
- `A.B.C` の3整数形式であること
- 前後に不要な文字や空白行を持たないこと

通常PRではbase branchとのversion比較を行いません。`develop`向けPR、`main`向けPR、その他branch向けPRのいずれでも、branch種別を理由としたversion incrementを要求しません。

将来Release workflowを追加する場合は、そのworkflow側でtag / GitHub Release / artifact metadataと`VERSION`の一致を検証します。通常CIへPRごとのversion increment規則を再導入しません。

## 6. 禁止事項

- 通常PRやmerge commitのたびに機械的に`VERSION`を更新しない。
- branch種別だけを理由にversion番号を決めない。
- ServerとWeb Clientで別々にアプリケーションversionを手更新しない。
- Protocol versionをアプリケーションversionで代用しない。
- Save format versionをアプリケーションversionで代用しない。
- Git tagだけをversionの正本にしない。
- build日時を公式versionの代わりにしない。
- 公開済みversionを別内容のReleaseへ再利用しない。
