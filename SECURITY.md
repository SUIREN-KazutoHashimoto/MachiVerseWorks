# Security Policy

## Supported Versions

セキュリティ修正は原則として最新の `main` を対象にし、必要に応じて最新の `develop` でも先行対応します。過去リリースすべてへのバックポートは保証しません。

初期セットアップ期間中はリリース版が存在しないため、最新の既定ブランチを対象とします。

## Reporting a Vulnerability

セキュリティ上の問題を発見した場合は、公開 Issue へ脆弱性の詳細、認証情報、秘密鍵、悪用可能な payload を投稿しないでください。

GitHub の Private vulnerability reporting が利用できる場合は、リポジトリの Security ページから非公開で報告してください。

Private vulnerability reporting を利用できない場合は、機密情報や具体的な悪用手順を含めず、公開 Issue で `Security contact request` として連絡してください。安全な連絡方法を確保した後に詳細を共有してください。

報告には可能な範囲で次を含めてください。

- 影響を受けるバージョンまたはコミット
- 影響するコンポーネント（Server / Protocol / Web Client など）
- 問題の概要
- 想定される影響
- 再現条件
- 修正案がある場合はその概要

## Security-sensitive Areas

MachiVerseWorks では特に次をセキュリティ境界として扱います。

- Server の外部公開 API / WebSocket
- Client から送信される command と入力検証
- save / load データや設定ファイルの読み込み
- Protocol の長さ・型・version 検証
- ファイルパス、URL、外部リソースの取り扱い
- CI / Release に使用する token、secret、署名情報

Simulation Core はネットワーク入力を無条件に信頼せず、Server / Protocol 境界で検証された command を受け取る設計を基本とします。

## Disclosure

修正が公開されるまで、脆弱性の具体的な再現手順や悪用可能な情報の公開は避けてください。確認後は影響範囲を評価し、修正と必要な告知を行います。
