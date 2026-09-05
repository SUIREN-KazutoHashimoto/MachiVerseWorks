# GitHub Repository 設定

MachiVerseWorks のGitHub側設定の基準です。コードやworkflowから変更できない設定を、Repository Settingsで構成するときの正本として使用します。

## 1. Branch / Ruleset

`main` と `develop` の両方を保護します。

現在のRuleset `Protect main and develop` では次を適用しています。

- Pull Requestを経由しない通常変更を禁止する。
- required status checkとして `CI / ci-gate` を指定する。`ci-gate` は通常CI・対象PRのE2E・Dependency Review（High以上でfail）を集約する。
- required status checkはstrict modeとし、merge前にbranchを最新baseへ追従させる。
- unresolved review conversation / review thread がある場合のmergeを禁止する。
- required approval数は0とする。
- unattributed changesに対する追加approval設定を有効にする。GitHubが追加approvalを要求する状態を表示した場合は、その要求を満たすまでmergeしない。
- Copilot Code Reviewをpush時に自動実行し、draft Pull Requestもreview対象とする。
- force pushを禁止する。
- branch deletionを禁止する。
- merge方式はmerge commitのみ許可する。
- bypass actorは設定しない。

`main` はリリース系統なので、運用上は `develop -> main` PR だけを使用します。

### 自動レビューの運用

Copilot / Codex等の自動レビューでinline threadが作成された場合、指摘を修正しただけではmerge条件を満たさないことがあります。

対応後は次を確認します。

1. 指摘内容へ必要な修正を行う。
2. fresh CIを確認する。
3. 対応済みのreview threadをResolveする。
4. 新しいpushで追加reviewが発生していないか確認する。
5. `ci-gate`成功・branch最新・未解決thread 0件を確認してmergeする。

Benchmark workflowの失敗は現時点ではRuleset requiredではありません。性能へ影響する変更では調査・説明が必要ですが、required checkとして扱うのは`ci-gate`です。

## 2. Pull Request / Merge

RepositoryのPull Requests設定は次を基準とし、現在この設定を適用済みです。

| 設定 | 値 |
| --- | --- |
| Allow merge commits | ON |
| Allow squash merging | OFF |
| Allow rebase merging | OFF |
| Always suggest updating pull request branches | ON |
| Automatically delete head branches | ON |
| Allow auto-merge | OFF |

merge commitを採用する理由は、個々の開発コミットとPR境界を保持し、並行開発の統合履歴を追跡しやすくするためです。Application `VERSION`はPR単位の履歴番号ではなくRelease versionとして別管理します。

## 3. Security

公開Repositoryとして、利用可能な範囲で次を有効にします。現在、主要項目は有効化済みです。

- Private vulnerability reporting
- Dependency graph
- Automatic dependency submission
- Dependabot alerts
- Dependabot malware alerts
- Dependabot security updates
- Secret Protection
- Push protection for secrets
- Code scanning（GitHub CodeQL Default setup）

CodeQLはGitHub側のDefault setupを正本とします。Repository内にAdvanced setup用の `codeql.yml` は置きません。GitHub Actions workflowもCodeQLの解析対象になります。

`Grouped security updates` と `AI findings` は現段階では必須にせず、依存関係や運用状況に応じて再評価します。

Security機能の提供条件や名称がGitHub側で変わる場合は、その時点のUIに従って同等機能を有効にします。

## 4. Repository metadata

- Issues: ON
- Discussions: 必要に応じてON。現在の公開コミュニケーション用途ではONでよい。
- Wiki: docsをRepository内で正本管理するため原則OFF。
- Projects: `roadmap/SIMULATION_ROADMAP.md`、`roadmap/GATEWAY_ROADMAP.md`、`roadmap/VIEW_ROADMAP.md`、`roadmap/MANAGEMENT_ROADMAP.md`を領域別の進捗正本とする間は必須ではない。
- Pages: Web Clientのdeploy方針が決まるまでOFF。

Roadmapの責務は次で固定します。

- Simulation: authoritative state / rule / semantic observation source / authoritative command contract
- Gateway: read-only Observation Request / subscription / cache / delivery / Protocol adaptation / resync
- View: 完全read-onlyな観測・描画
- Management: World / City / Serverを変更する操作UI

Analyticsが必要になった場合は4 Roadmapへ無理に混在させず、別Listener / clientとして設計します。

Gateway Roadmapの分離は責務と進捗管理の分離であり、別repository / process / deploy unitを必須としません。

Topicsは実装が始まった時点で、実態に合うものだけ追加します。候補は `csharp`, `dotnet`, `threejs`, `city-simulation`, `simulation` です。

## 5. 設定変更時の確認

GitHub設定を変更したら、少なくとも次を確認します。

1. `main` / `develop` のrulesetまたはbranch protectionが有効である。
2. PRで `CI / ci-gate` がrequired checkとして認識され、Dependency Review失敗も `ci-gate` の失敗へ反映される。
3. branch最新化がrequiredとして機能する。
4. unresolved review threadがあるPRをmergeできないことを確認する。
5. Copilot Code Reviewのpush時自動reviewが意図した通り動作する。
6. direct push / force push / branch deletionが意図した通り制限される。
7. merge画面でmerge commitだけが標準方式として使用できる。
8. merge後に短命branchが自動削除される。
9. Securityページで有効化した機能が表示される。
