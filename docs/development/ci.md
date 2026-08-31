# CI / GitHub Actions

MachiVerseWorksのGitHub Actions運用方針です。

## 1. CI

`.github/workflows/ci.yml`を通常の必須チェックの中心として使用します。

現在のjob:

- `repository`: 必須file、Markdown local link、.NET SDK、`VERSION`、localization manifest
- `detect components`: .NET / Web実装有無を検出
- `dotnet`: restore / build / test
- `web`: npm install / lint / typecheck / test / build
- `ci-gate`: component jobを集約する最終判定

Branch protection / Rulesetのrequired checkは **`CI / ci-gate`** を正本とする。

### Repository validation

- 必須Repository fileが存在し空でないこと
- `scripts/check-markdown-links.py`でMarkdown local linkを検証
- `global.json` SDK policy
- `VERSION`の`A.B.C`形式とPR baseからのversion increase
- `src/web/locales/manifest.json`のlocale/default整合

### .NET

baselineは.NET 10.x / Release。`MachiVerseWorks.slnx`を優先し、無ければ`.sln`を使う。

```text
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

### Web Client

baselineはNode.js 24 + npm。`package-lock.json`必須。

```text
npm ci
npm run lint --if-present
npm run typecheck --if-present
npm test --if-present
npm run build
```

## 2. Benchmarks

`.github/workflows/benchmarks.yml`が性能回帰の正規入口。Phaseごとにworkflowを増やさず、機能名をmatrixへ追加する。

現在の主なjob:

- `road-network-10k-100k`
- `routing-small-medium-large`
- `queued-intersections`
- `pedestrians-1k-10k`
- `railway-10k-100k`
- `railway-operations-100-1000`
- `journey-transfer-dispatch`
- `vehicles-1k-10k-100k`
- `population-1k-10k-100k`
- `benchmarkdotnet-smoke`
- `snapshot-readmodel`
- `phase9-2d-to-3d-regression`
- `legacy-tick`（manual only）

PRと`develop` merge後に対象path変更で実行し、feature branch push単独では二重実行しない。artifactは原則14日保持する。

Railway Infrastructureのscenario / baselineは[`railway-infrastructure-benchmark.md`](railway-infrastructure-benchmark.md)を参照する。

## 3. End-to-end

`.github/workflows/e2e.yml`がServer / Protocol / Web接続E2Eの正規入口。Phase 6 / 11 / 13 / 14 / 16 / 17 / 18 / 19の既存scriptをmatrixから呼び出す。

新規E2Eは原則としてPhase専用workflowを増やさず、このmatrixへ機能名 / script / artifactを追加する。

## 4. CodeQL

GitHub Code Securityの **Default setup** を正本とする。RepositoryにAdvanced setup用`.github/workflows/codeql.yml`は置かない。

custom query等、Default setupで表現できない要件が生じた場合だけAdvanced setupを再検討する。

## 5. Dependency Review

`.github/workflows/dependency-review.yml`はPull Requestのdependency変更を確認する。現時点ではHigh以上の既知vulnerabilityをmerge blockerとする。

license allow/deny policyは`THIRD_PARTY_NOTICES.txt`運用と合わせて定義する。

## 6. Dependabot

`.github/dependabot.yml`は現在、実際に存在する3 ecosystemを週次で監視する。

| Ecosystem | Directory | Schedule | Group |
| --- | --- | --- | --- |
| GitHub Actions | `/` | Monday 09:00 JST | `github-actions` |
| NuGet | `/` | Monday 09:10 JST | `nuget` |
| npm | `/src/web` | Monday 09:20 JST | `npm` |

各ecosystemは`open-pull-requests-limit: 5`で、同ecosystem内updateをgroup化する。

したがって「NuGet / npmはmanifest追加後に有効化する」という旧記述は現行設定ではない。`Directory.Packages.props` / project filesと`src/web/package.json` / lockfileを現在のdependency sourceとして扱う。

GitHub Code Security側のDependabot alerts / security updates等とは、version update PRを作るDependabot configurationを区別する。

## 7. Release / Deploy

Release workflowはまだ固定しない。Server binary、Web hosting、configuration、container等の配布単位が確定した時点で設計する。

## 8. Branch protection

`main`と`develop`は[`repository-settings.md`](repository-settings.md)の基準で保護する。required checkは`CI / ci-gate`を正本とし、CodeQL / Dependency ReviewをRuleset requiredへ追加するかは運用実績を見て再評価する。
