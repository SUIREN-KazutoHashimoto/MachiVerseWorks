# CI / GitHub Actions

MachiVerseWorksのGitHub Actions運用方針です。

## 1. CI

`.github/workflows/ci.yml`を通常の必須チェックの中心として使用します。

現在のjob:

- `repository`: 必須file、Markdown local link / heading anchor、.NET SDK、`VERSION`形式、localization manifest
- `detect components`: .NET / Web実装有無とrequired E2Eの必要性を検出
- `dotnet`: restore / build / test
- `web`: npm install / lint / typecheck / test / build
- `dependency review`: Pull Requestのdependency差分を検査し、High以上の既知vulnerabilityでfail
- `required e2e`: 対象変更で必要なE2E matrixを実行
- `ci-gate`: 上記required jobを集約する最終判定

Branch protection / Rulesetのrequired checkは **`CI / ci-gate`** を正本とします。

### Repository validation

- 必須Repository fileが存在し空でないこと。Roadmapは`roadmap/SIMULATION_ROADMAP.md`、`roadmap/GATEWAY_ROADMAP.md`、`roadmap/VIEW_ROADMAP.md`、`roadmap/MANAGEMENT_ROADMAP.md`の4ファイルを必須とする
- `scripts/check-markdown-links.py`でMarkdown local file linkとheading anchorを検証
- `global.json` SDK policy
- `VERSION`が`A.B.C`形式で、余分な文字や空白行を持たないこと
- `src/view/locales/manifest.json`のlocale/default整合

`VERSION`はRelease versionの正本であり、通常PRではbase branchとの比較やbranch別incrementを行いません。Release時のversion決定は[`versioning.md`](versioning.md)を正本とします。

MarkdownやRoadmapを移動・改名した場合は、CIのMarkdown link validationを必ず通し、旧相対リンクを残さない。Roadmap責務を変更した場合は4 Roadmapと`roadmap/README.md`の依存索引も同期します。

### .NET

baselineは.NET 10.x / Release。`MachiVerseWorks.slnx`を優先し、無ければ`.sln`を使います。

```text
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

### Web Client

baselineは`src/view/.node-version`で固定したNode.js + npm。`package-lock.json`必須です。

```text
npm ci
npm run lint
npm run typecheck
npm test
npm run build
```

## 2. GitHub Actions supply-chain policy

Repository内のthird-party / GitHub-maintained Actionはmutableな`@vN` tagだけを実行参照にせず、review済みの**full 40-character commit SHA**へ固定します。可読性のため同じ行末にmajor version commentを残します。

```yaml
uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7
```

対象workflowは`.github/workflows/`配下の全workflowです。特に通常運用の中心は次です。

- `.github/workflows/ci.yml`
- `.github/workflows/benchmarks.yml`
- `.github/workflows/e2e.yml`
- domain別benchmark / validation workflow

Action更新時はDependabotのGitHub Actions PR、release notes、upstream tagが指すcommitをreviewし、SHAとversion commentを同時に更新します。major tagを直接実行参照へ戻しません。

SHA pinはAction repositoryの内容差し替えリスクを下げるためのsupply-chain controlであり、Dependabotによる継続更新を止める意図ではありません。

## 3. Benchmarks

`.github/workflows/benchmarks.yml`が中央性能回帰の正規入口です。Phaseごとに中央workflowを増やさず、機能名をmatrixへ追加します。

現在の主なBenchmarkDotNet matrix:

- `road-network-10k-100k`
- `routing-small-medium-large`
- `queued-intersections`
- `pedestrians-1k-10k`
- `railway-10k-100k`
- `railway-operations-100-1000`
- `journey-transfer-dispatch`
- `logistics-inventory-100-1000`
- `power-loads-1k-5k`
- `water-sewer-loads-1k-5k`
- `gas-loads-1k-5k`
- `persistent-regional-evolution-world-scale`

scenario / auxiliary job:

- `vehicles-1k-10k-100k`
- `population-1k-10k-100k`
- `benchmarkdotnet-smoke`
- `snapshot-readmodel`
- `phase9-2d-to-3d-regression`

旧`legacy-tick` benchmarkは中央workflowから分離し、必要時は **Legacy Tick Benchmark**（`.github/workflows/legacy-tick-benchmark.yml`）を手動実行します。

中央`Benchmarks`およびdomain別benchmark workflowは、現時点ではRulesetのrequired status checkではありません。したがって**advisory / non-blocking**として扱います。ただし性能へ影響する変更で赤になった場合は無視せず、原因調査・再現性・既知ノイズ・未解決事項をPRへ明記します。

Benchmarkをmerge blockerへ昇格する場合は、workflowの安定性を確認したうえでRulesetとこの文書を同時に更新します。

PRと`develop` merge後に対象path変更で実行し、feature branch push単独では二重実行しません。artifactは原則14日保持します。

個別domainのbaselineや再現条件は`docs/development/*-benchmark.md`を参照し、workflow matrixを変更した場合はこの文書も同期します。

## 4. End-to-end

`.github/workflows/e2e.yml`がServer / Protocol / Web接続E2Eの正規入口です。実装済み主要domainを1つのmatrixで継続検証します。

主なmatrix:

- core / road network / road traffic / intersection
- population / pedestrian
- railway infrastructure / railway operations / multimodal transit
- administration console
- economy / logistics
- power / water-sewer / gas
- optical communication
- remote MCP administration
- radio / spectrum
- world environment
- view physical world / settlement structure

Protocol番号の正本は[`../architecture/protocol.md`](../architecture/protocol.md)、workflowの実行対象の正本は[`.github/workflows/e2e.yml`](../../.github/workflows/e2e.yml)とします。

新規E2Eは原則としてPhase専用workflowを増やさず、このmatrixへ機能名 / script / artifactを追加します。

## 5. CodeQL

GitHub Code Securityの **Default setup** を正本とします。RepositoryにAdvanced setup用`.github/workflows/codeql.yml`は置きません。

custom query等、Default setupで表現できない要件が生じた場合だけAdvanced setupを再検討します。

## 6. Dependency Review

Dependency Reviewは独立した`dependency-review.yml`ではなく、**`.github/workflows/ci.yml`内の`dependency review` job**として実行します。

Pull Requestのdependency差分を確認し、現時点ではHigh以上の既知vulnerabilityをfail条件とします。この結果は`ci-gate`へ集約されるため、対象PRではmerge blockerになります。

license allow/deny policyは`THIRD_PARTY_NOTICES.txt`運用と合わせて定義します。

## 7. Dependabot

`.github/dependabot.yml`は現在、実際に存在する3 ecosystemを週次で監視します。

| Ecosystem | Directory | Schedule | Group |
| --- | --- | --- | --- |
| GitHub Actions | `/` | Monday 09:00 JST | `github-actions` |
| NuGet | `/` | Monday 09:10 JST | `nuget` |
| npm | `/src/view` | Monday 09:20 JST | `npm` |

各ecosystemは`open-pull-requests-limit: 5`で、同ecosystem内updateをgroup化します。

GitHub Actions update PRはfull SHA pinを更新します。NuGet / npmは`Directory.Packages.props` / project filesと`src/view/package.json` / lockfileをdependency sourceとして扱います。

GitHub Code Security側のDependabot alerts / security updates等とは、version update PRを作るDependabot configurationを区別します。

## 8. Release / Deploy

Release workflowはまだ固定しません。Server binary、Web hosting、configuration、container等の配布単位が確定した時点で設計します。

Release workflowを追加するときは、ルート`VERSION`、tag、GitHub Release、artifact metadataの一致を検証します。通常PRへversion increment制約を戻しません。

## 9. Branch protection

`main`と`develop`は[`repository-settings.md`](repository-settings.md)の基準で保護します。required checkは`CI / ci-gate`を正本とし、Benchmarkは現時点ではrequiredへ含めません。
