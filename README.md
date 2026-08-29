# MachiVerseWorks

[![CI](https://github.com/SUIREN-KazutoHashimoto/MachiVerseWorks/actions/workflows/ci.yml/badge.svg)](https://github.com/SUIREN-KazutoHashimoto/MachiVerseWorks/actions/workflows/ci.yml)

MachiVerseWorks は、C# 製のヘッドレス・シミュレーションサーバーとブラウザベースの 3D クライアントで構成する、大規模リアルタイム都市シミュレーションです。

市民、道路交通、公共交通、物流、産業、電力などの都市活動をサーバー側で継続的にシミュレーションし、クライアント側では必要な範囲のデータを受信して可視化します。

旧 Machi-Sim で得られたドメイン・設計・性能面の知見を引き継ぎつつ、ブラウザ単体実装からシミュレーション本体を分離し、より大規模な都市と多数の Agent を扱える構成へ再設計します。

> [!NOTE]
> 現在はリポジトリの初期セットアップ段階です。実装・API・プロトコル・仕様は今後変更される可能性があります。

## 基本アーキテクチャ

```text
Browser 3D Client
        ↑↓
Protocol / WebSocket
        ↑↓
C# Headless Server
        ↓
Simulation Core
```

基本方針は次の通りです。

- `MachiVerseWorks.Simulation` が都市シミュレーションの正本を持つ
- `MachiVerseWorks.Server` が実行ループ、クライアント接続、command、snapshot 配信を担当する
- `MachiVerseWorks.Protocol` がクライアント・サーバー間の契約を定義する
- Web クライアントは表示・入力・補間を担当し、シミュレーション状態を直接所有しない
- 高頻度データはバイナリ転送を前提とし、クライアントには必要な空間範囲だけを配信する
- Simulation tick、snapshot publish、render frame を分離する
- シミュレーション仕様と実装設計をドキュメント上でも分離する

詳細は [`docs/architecture/overview.md`](docs/architecture/overview.md)、採用理由は [`ADR-0001`](docs/decisions/ADR-0001-csharp-headless-simulation-server.md) を参照してください。

## リポジトリ構成

```text
MachiVerseWorks/
├─ src/
│  ├─ MachiVerseWorks.Simulation/
│  ├─ MachiVerseWorks.Server/
│  ├─ MachiVerseWorks.Protocol/
│  └─ web/
├─ tests/
├─ benchmarks/
├─ docs/
│  ├─ product/
│  ├─ architecture/
│  ├─ specifications/
│  ├─ development/
│  ├─ decisions/
│  └─ archive/
├─ scripts/
├─ tools/
├─ .github/
├─ AGENT.md
├─ AGENTS.md
├─ CONTRIBUTING.md
├─ SECURITY.md
├─ CODE_OF_CONDUCT.md
├─ LICENSE
├─ NOTICE
├─ THIRD_PARTY_NOTICES.txt
└─ README.md
```

各ディレクトリの役割は、それぞれの `README.md` と [`docs/README.md`](docs/README.md) を参照してください。

## ドキュメント管理方針

MachiVerseWorks では、旧実装のように巨大な横断設計書へ情報を集約せず、役割ごとに小さな正本ドキュメントを管理します。

- `docs/product/`: プロジェクトの目的、概念、用語
- `docs/architecture/`: システム構成と技術設計（How）
- `docs/specifications/`: シミュレーションの振る舞いと仕様（What / Why）
- `docs/development/`: 開発・テスト・Git・バージョン運用
- `docs/decisions/`: ADR（Architecture Decision Record）
- `docs/archive/`: 廃止済み設計・旧資料・実験記録

新しい設計判断は、必要に応じて ADR として理由を残します。

## 開発方針

- サーバー / Simulation Core: C# / .NET
- Web クライアント: TypeScript + Three.js を想定
- Simulation Core は HTTP / WebSocket / ASP.NET Core などの通信層へ依存させない
- ネットワーク配信と描画更新は Simulation tick と分離する
- 大規模 Agent 処理はデータ指向・割り当て抑制・並列処理を前提に設計する
- 最適化は計測結果に基づいて行い、可読性や仕様の正しさより先に複雑化しない

具体的な開発ルールは [`AGENT.md`](AGENT.md)、開発フローは [`docs/development/git-workflow.md`](docs/development/git-workflow.md)、CI は [`docs/development/ci.md`](docs/development/ci.md) を参照してください。

## 旧 Machi-Sim

旧ブラウザ単体版は Legacy 実装として保存されています。

- [Machi-Sim_Legacy](https://github.com/SUIREN-KazutoHashimoto/Machi-Sim_Legacy)
- [Legacy からの移行メモ](docs/archive/legacy-machi-sim/README.md)

旧repoのコードや巨大な仕様書をそのまま正本としてコピーせず、必要なドメイン仕様・設計知見だけを新アーキテクチャに合わせて書き直して引き継ぎます。

## Contributing / Security

- 貢献方法: [`CONTRIBUTING.md`](CONTRIBUTING.md)
- 行動規範: [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)
- 脆弱性報告: [`SECURITY.md`](SECURITY.md)

## ライセンス

Apache License 2.0 の下で提供します。

- ライセンス全文: [`LICENSE`](LICENSE)
- プロジェクト帰属表示: [`NOTICE`](NOTICE)
- 第三者ソフトウェア表示: [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt)
