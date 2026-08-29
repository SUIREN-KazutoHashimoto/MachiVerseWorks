# CI / GitHub Actions

MachiVerseWorks の GitHub Actions 運用方針です。

## 1. CI

`.github/workflows/ci.yml` を通常の必須チェックの中心として使用します。

現在の job:

- `repository`: リポジトリ必須ファイルと localization manifest を検証
- `detect components`: .NET / Web Client の実装有無を検出
- `dotnet`: C# project が存在する場合だけ restore / build / test
- `web`: `src/web/package.json` が存在する場合だけ npm install / lint / typecheck / test / build

初期セットアップ中は source project がまだ存在しないため、`repository` と component detection のみ実行されます。

### .NET

C# project が追加された時点から CI が自動的に有効になります。

CI baseline:

- .NET 10.x
- Release configuration
- `MachiVerseWorks.slnx` を優先し、無ければ `MachiVerseWorks.sln` を使用
- project が存在するのに solution が無い場合は CI failure

標準処理:

```text
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

テスト結果は短期間の Actions artifact として保存します。

### Web Client

`src/web/package.json` が追加された時点から Web CI が有効になります。

CI baseline:

- Node.js 24
- npm
- `package-lock.json` 必須
- `npm ci`
- `npm run lint --if-present`
- `npm run typecheck --if-present`
- `npm test --if-present`
- `npm run build`

`dist/` が生成された場合は短期間の Actions artifact として保存します。

## 2. CodeQL

`.github/workflows/codeql.yml` で CodeQL advanced setup を使用します。

対象:

- C#
- JavaScript / TypeScript

実際に対象 source が存在する language job だけを実行します。

C# は build mode `none` を使用し、通常の build correctness は `CI` workflow 側で検証します。

## 3. Dependency Review

`.github/workflows/dependency-review.yml` は Pull Request で新規・更新依存関係を確認します。

現時点では High 以上の既知 vulnerability を merge blocker とします。

ライセンス allow / deny policy は実際の NuGet / npm dependency が導入されてから、`THIRD_PARTY_NOTICES.txt` の運用と合わせて定義します。

## 4. Dependabot

`.github/dependabot.yml` では、まず GitHub Actions 自身の更新だけを週次で確認します。

NuGet と npm の Dependabot 設定は、実際の package manifest が追加されたタイミングで有効化します。

## 5. Release / Deploy

Release workflow はまだ作成しません。

MachiVerseWorks は旧ブラウザ単体版と異なり、将来的に次の配布物を持つ可能性があります。

- Simulation Server
- Web Client
- server configuration / protocol metadata
- OS 別 binary
- container image

配布単位、対応 OS、Web Client hosting、GitHub Release asset の形式が決まる前に release workflow を固定すると後から壊しやすいため、最初の実行可能 PoC と配布方針が確定した時点で追加します。

## 6. Branch protection

通常開発へ移行した後は、少なくとも `develop` で `CI / repository` と実装済み component の build job を required check にすることを推奨します。

`main` は release 系統として扱い、`develop -> main` PR では同じ CI を通します。
