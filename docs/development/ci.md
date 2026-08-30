# CI / GitHub Actions

MachiVerseWorks の GitHub Actions 運用方針です。

## 1. CI

`.github/workflows/ci.yml` を通常の必須チェックの中心として使用します。

現在の job:

- `repository`: 必須ファイル、Markdown local link、.NET SDK、任意の `VERSION`、localization manifest を検証
- `detect components`: .NET / Web Client の実装有無を検出
- `dotnet`: C# project が存在する場合だけ restore / build / test
- `web`: `src/web/package.json` が存在する場合だけ npm install / lint / typecheck / test / build
- `ci-gate`: 上記jobの結果を集約し、実装有無に関係なく常に1つの最終判定を返す

Branch protection / Ruleset の required check には **`CI / ci-gate`** を指定します。component jobを直接requiredにすると未実装時のskipと相性が悪いため、固定gateを正本とします。

### Repository validation

`repository` jobでは次を検証します。

- 必須Repositoryファイルが存在し空でないこと
- `scripts/check-markdown-links.py` によるMarkdownのRepository内リンク切れ検出
- `global.json` のSDK policy
- `VERSION` が存在する場合の `A.B.C` 形式
- `src/web/locales/manifest.json` のlocale形式とdefault locale整合性

`VERSION` は初期セットアップ中は存在しなくてよく、通常開発開始時に必須化します。

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

## 2. Benchmarks

`.github/workflows/benchmarks.yml` は性能回帰の正規入口です。Phaseごとにworkflowを増やさず、機能名をmatrixへ追加します。

- `benchmarkdotnet`: Road Network、Routing、Intersection Control、Pedestrian、Railway Infrastructure、Railway Operations、Multimodal Transit
- `scenario`: Road Traffic、Population
- `benchmarkdotnet-smoke`: BenchmarkDotNet全suiteのDry実行
- `snapshot-readmodel`: publish read model latency / allocation
- `phase9-2d-to-3d-regression`: 2D→3D比較を同一runnerで実行
- `legacy-tick`: 初期Simulation tick baselineの手動再実行

PRと`develop`へのmerge後に実行し、feature branchへのpush単独では起動しません。これにより同じ変更に対するpush / pull_requestの二重計測を避けます。

## 3. End-to-end

`.github/workflows/e2e.yml` はServer / Protocol / Web Clientを接続するE2Eの正規入口です。Phase 6 / 11 / 13 / 14 / 16 / 17 / 18 / 19の既存スクリプトをmatrixから呼び出します。

`src/**`、`scripts/**`、E2E fixture、共通.NET build設定、solution、`global.json`の変更で起動します。PRと`develop`へのmerge後に実行し、feature branchへのpush単独では起動しません。

新しいE2Eは原則として新規workflowを作らず、このmatrixへ機能名・script・artifactを追加します。

## 4. CodeQL

CodeQL は GitHub Code Security の **Default setup** を正本として使用します。

Repository内にAdvanced setup用の `.github/workflows/codeql.yml` は置きません。Default setupが対象言語とGitHub Actions workflowを解析し、通常のbuild correctnessは `CI` workflow側で検証します。

将来、custom query、特殊なbuild手順、独自matrixなどDefault setupで表現できない要件が生じた場合のみAdvanced setupへの移行を再検討します。

## 5. Dependency Review

`.github/workflows/dependency-review.yml` は Pull Request で新規・更新依存関係を確認します。

現時点では High 以上の既知 vulnerability を merge blocker とします。

ライセンス allow / deny policy は実際の NuGet / npm dependency が導入されてから、`THIRD_PARTY_NOTICES.txt` の運用と合わせて定義します。

## 6. Dependabot

`.github/dependabot.yml` では、まず GitHub Actions 自身の更新だけを週次で確認します。

NuGet と npm の Dependabot 設定は、実際の package manifest が追加されたタイミングで有効化します。

GitHub Code Security 側では Dependabot alerts / malware alerts / security updates を有効化しています。

## 7. Release / Deploy

Release workflow はまだ作成しません。

MachiVerseWorks は旧ブラウザ単体版と異なり、将来的に次の配布物を持つ可能性があります。

- Simulation Server
- Web Client
- server configuration / protocol metadata
- OS 別 binary
- container image

配布単位、対応 OS、Web Client hosting、GitHub Release asset の形式が決まる前に release workflow を固定すると後から壊しやすいため、最初の実行可能 PoC と配布方針が確定した時点で追加します。

## 8. Branch protection

`main` と `develop` は `docs/development/repository-settings.md` の基準で保護します。

required check は `CI / ci-gate` とし、CodeQL / Dependency Reviewは現時点ではRulesetのrequired conditionへ追加しません。実装状況と運用実績を見て必要になった時点で再評価します。
