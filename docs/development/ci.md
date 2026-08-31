# CI / GitHub Actions

MachiVerseWorksのGitHub Actions運用方針です。

## 1. CI

`.github/workflows/ci.yml`を通常の必須チェックの中心として使用します。

現在のjob:

- `repository`: 必須file、Markdown local link / heading anchor、.NET SDK、`VERSION`、localization manifest
- `detect components`: .NET / Web実装有無を検出
- `dotnet`: restore / build / test
- `web`: npm install / lint / typecheck / test / build
- `ci-gate`: component jobを集約する最終判定

Branch protection / Rulesetのrequired checkは **`CI / ci-gate`** を正本とする。

### Repository validation

- 必須Repository fileが存在し空でないこと
- `scripts/check-markdown-links.py`でMarkdown local file linkとheading anchorを検証
- `global.json` SDK policy
- `VERSION`の`A.B.C`形式
- `develop`向けPRはbase `A.B.C`から厳密に`A.(B+1).0`へ更新
- `main`向けPRはbase `A.B.C`から厳密に`(A+1).0.0`へ更新
- その他のPR targetはbaseより大きい`VERSION`を要求
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

## 2. GitHub Actions supply-chain policy

Repository内のthird-party / GitHub-maintained Actionはmutableな`@vN` tagだけを実行参照にせず、review済みの**full 40-character commit SHA**へ固定する。可読性のため同じ行末にmajor version commentを残す。

```yaml
uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7
```

対象workflow:

- `.github/workflows/ci.yml`
- `.github/workflows/benchmarks.yml`
- `.github/workflows/e2e.yml`
- `.github/workflows/dependency-review.yml`

Action更新時はDependabotのGitHub Actions PR、release notes、upstream tagが指すcommitをreviewし、SHAとversion commentを同時に更新する。major tagを直接実行参照へ戻さない。

SHA pinはAction repositoryの内容差し替えリスクを下げるためのsupply-chain controlであり、Dependabotによる継続更新を止める意図ではない。

## 3. Benchmarks

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

Population scenarioは`idle`に加えて`foot-dispatch` / `motor-dispatch`を同じrunnerから出力し、詳細は[`population-benchmark.md`](population-benchmark.md)を参照する。

Railway Infrastructureのscenario / baselineは[`railway-infrastructure-benchmark.md`](railway-infrastructure-benchmark.md)を参照する。

## 4. End-to-end

`.github/workflows/e2e.yml`がServer / Protocol / Web接続E2Eの正規入口。Phase 6 / 11 / 13 / 14 / **15** / 16 / 17 / 18 / 19のscriptをmatrixから呼び出す。

Phase 15 Population E2Eは実Server + WebSocket + headless Browserで次を検証する。

- Protocol 2.9接続と`PopulationStatistics`表示
- Person inspectorによる`PersonDebug`表示
- client reconnect後のinspection再送 / UI復元
- 別WebSocketによるProtocol 2.5 `Hello -> InspectPerson -> PersonDebug`互換

新規E2Eは原則としてPhase専用workflowを増やさず、このmatrixへ機能名 / script / artifactを追加する。

## 5. CodeQL

GitHub Code Securityの **Default setup** を正本とする。RepositoryにAdvanced setup用`.github/workflows/codeql.yml`は置かない。

custom query等、Default setupで表現できない要件が生じた場合だけAdvanced setupを再検討する。

## 6. Dependency Review

`.github/workflows/dependency-review.yml`はPull Requestのdependency変更を確認する。現時点ではHigh以上の既知vulnerabilityをmerge blockerとする。

license allow/deny policyは`THIRD_PARTY_NOTICES.txt`運用と合わせて定義する。

## 7. Dependabot

`.github/dependabot.yml`は現在、実際に存在する3 ecosystemを週次で監視する。

| Ecosystem | Directory | Schedule | Group |
| --- | --- | --- | --- |
| GitHub Actions | `/` | Monday 09:00 JST | `github-actions` |
| NuGet | `/` | Monday 09:10 JST | `nuget` |
| npm | `/src/web` | Monday 09:20 JST | `npm` |

各ecosystemは`open-pull-requests-limit: 5`で、同ecosystem内updateをgroup化する。

GitHub Actions update PRはfull SHA pinを更新する。NuGet / npmは`Directory.Packages.props` / project filesと`src/web/package.json` / lockfileをdependency sourceとして扱う。

GitHub Code Security側のDependabot alerts / security updates等とは、version update PRを作るDependabot configurationを区別する。

## 8. Release / Deploy

Release workflowはまだ固定しない。Server binary、Web hosting、configuration、container等の配布単位が確定した時点で設計する。

## 9. Branch protection

`main`と`develop`は[`repository-settings.md`](repository-settings.md)の基準で保護する。required checkは`CI / ci-gate`を正本とし、CodeQL / Dependency ReviewをRuleset requiredへ追加するかは運用実績を見て再評価する。
