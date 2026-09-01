# GitHub Repository 設定

MachiVerseWorks のGitHub側設定の基準です。コードやworkflowから変更できない設定を、Repository Settingsで構成するときの正本として使用します。

## 1. Branch / Ruleset

`main` と `develop` の両方を保護します。

現在のRuleset `Protect main and develop` では次を適用しています。

- Pull Requestを経由しない通常変更を禁止する。
- required status checkとして `CI / ci-gate` を指定する。
- merge前にbranchを最新状態へ更新する設定を有効にする。
- unresolved conversation がある場合のmergeを禁止する。
- force pushを禁止する。
- branch deletionを禁止する。
- required approval数は0とし、レビュー運用が必要になった時点で追加する。
- merge方式はmerge commitのみ許可する。
- bypass actorは設定しない。

`main` はリリース系統なので、運用上は `develop -> main` PR だけを使用します。

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

merge commitを採用する理由は、個々のコミットと `A.B.C` version推移を保持するためです。

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
- Projects: `roadmap/SIMULATION_ROADMAP.md` と `roadmap/VIEW_ROADMAP.md` を領域別の進捗正本とする間は必須ではない。
- Pages: Web Clientのdeploy方針が決まるまでOFF。

Topicsは実装が始まった時点で、実態に合うものだけ追加します。候補は `csharp`, `dotnet`, `threejs`, `city-simulation`, `simulation` です。

## 5. 設定変更時の確認

GitHub設定を変更したら、少なくとも次を確認します。

1. `main` / `develop` のrulesetまたはbranch protectionが有効である。
2. PRで `CI / ci-gate` がrequired checkとして認識される。
3. direct push / force push / branch deletionが意図した通り制限される。
4. merge画面でmerge commitだけが標準方式として使用できる。
5. merge後に短命branchが自動削除される。
6. Securityページで有効化した機能が表示される。
