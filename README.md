<p align="center">
  <img src="assets/brand/machiverseworks-icon.png" alt="MachiVerseWorks icon" width="132">
</p>

<h1 align="center">MachiVerseWorks</h1>

<p align="center">
  <strong>City Simulation Project</strong><br>
  C#製ヘッドレス・シミュレーションサーバーとブラウザ3Dクライアントで構成する、大規模リアルタイム都市シミュレーション。
</p>

<p align="center">
  <a href="https://github.com/SUIREN-KazutoHashimoto/MachiVerseWorks/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/SUIREN-KazutoHashimoto/MachiVerseWorks/actions/workflows/ci.yml/badge.svg?branch=develop"></a>
  <a href="LICENSE"><img alt="License: Apache-2.0" src="https://img.shields.io/badge/license-Apache--2.0-blue.svg"></a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4.svg">
  <img alt="Status: Pre-alpha" src="https://img.shields.io/badge/status-pre--alpha-blue.svg">
</p>

<p align="center">
  <img src="assets/brand/machiverseworks-social-preview.png" alt="MachiVerseWorks — City Simulation Project" width="100%">
</p>

## MachiVerseWorks とは

MachiVerseWorks は、市民・道路交通・公共交通・物流・産業・電力などの都市活動を、サーバー側で継続的にシミュレーションする都市シミュレーションプロジェクトです。

旧 Machi-Sim で得られたドメイン・設計・性能面の知見を引き継ぎつつ、ブラウザ単体実装からシミュレーション本体を分離。クライアントは必要な空間範囲だけを受信・描画し、より大規模な都市と多数の Agent を扱える構成を目指します。

> [!NOTE]
> 現在は **pre-alpha** 段階です。実装・API・Protocol・Save format・仕様は開発の進行に伴って変更される可能性があります。

## Architecture

```text
┌──────────────────────────────┐
│      Browser 3D Client       │
│  TypeScript / Three.js       │
└──────────────┬───────────────┘
               │ WebSocket / Binary Protocol
┌──────────────▼───────────────┐
│     MachiVerseWorks.Server   │
│ connection / command / I/O   │
└──────────────┬───────────────┘
               │
┌──────────────▼───────────────┐
│  MachiVerseWorks.Simulation  │
│ authoritative world state    │
└──────────────────────────────┘

       MachiVerseWorks.Protocol
       = Client / Server contract
```

| Component | Responsibility |
| --- | --- |
| **Simulation** | 都市状態の正本、tick、Agent・交通・経済などのシミュレーション |
| **Server** | 実行ライフサイクル、接続、command受付、snapshot配信 |
| **Protocol** | Client / Server間の安定した契約とバイナリメッセージ |
| **Web Client** | 3D描画、入力、補間、UI、ローカライズ |

設計の詳細は [`docs/architecture/overview.md`](docs/architecture/overview.md)、採用理由は [`ADR-0001`](docs/decisions/ADR-0001-csharp-headless-simulation-server.md) を参照してください。

## Design Principles

- **Server authoritative** — Web Clientにシミュレーションの正本を持たせない
- **Spatial subscription** — クライアントには必要な範囲だけを配信する
- **Separated clocks** — Simulation tick / snapshot publish / render frame を分離する
- **Measure first** — 最適化は profiler・benchmark・実測値に基づいて行う
- **Stable data contracts** — Protocol / Save Dataに表示言語やUI文字列を混ぜない
- **Small, completable tasks** — 巨大な目標ではなく、完了判定できるTask ID単位で進める

## Roadmap

進捗と実装予定は [`ROADMAP.md`](ROADMAP.md) を正本として管理します。

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0 | Repository foundation | ✅ 完了 |
| 1 | 開発プロジェクト骨格 | ⏭️ 次 |
| 2 | Simulation Core 最小 PoC | ⏳ 待機 |
| 3 | Protocol 最小実装 | ⏳ 待機 |
| 4 | Headless Server 最小実装 | ⏳ 待機 |
| 5 | Web Client 最小実装 | ⏳ 待機 |
| 6 | End-to-End PoC | ⏳ 待機 |

大きな機能名を長期間残すのではなく、**単独で実装・検証・完了できる小さなTask**へ分解して進めます。

## Repository

```text
MachiVerseWorks/
├─ src/
│  ├─ MachiVerseWorks.Simulation/
│  ├─ MachiVerseWorks.Server/
│  ├─ MachiVerseWorks.Protocol/
│  └─ web/
├─ tests/
├─ benchmarks/
├─ assets/
│  ├─ originals/        # 加工前のブランド原本
│  └─ brand/            # README / docs向けブランド画像
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
├─ global.json
├─ ROADMAP.md
├─ AGENTS.md
└─ README.md
```

## Development

.NET SDK はルートの [`global.json`](global.json) を正本として固定し、現在は **.NET 10** 系を採用しています。Web Client は **TypeScript + Three.js** を前提に設計しています。

主要な開発ドキュメント:

- [AGENTS.md — 開発・エージェント運用ルール](AGENTS.md)
- [Coding Guidelines](docs/development/coding-guidelines.md)
- [Performance Guidelines](docs/development/performance.md)
- [CI / GitHub Actions](docs/development/ci.md)
- [Git workflow](docs/development/git-workflow.md)
- [Versioning](docs/development/versioning.md)

## Documentation

| Directory | Purpose |
| --- | --- |
| [`docs/product/`](docs/product/) | プロジェクトの目的・概念・用語 |
| [`docs/architecture/`](docs/architecture/) | システム構成と技術設計 — **How** |
| [`docs/specifications/`](docs/specifications/) | シミュレーションの振る舞い — **What / Why** |
| [`docs/development/`](docs/development/) | 開発・テスト・Git・CI・version運用 |
| [`docs/decisions/`](docs/decisions/) | Architecture Decision Record |
| [`docs/archive/`](docs/archive/) | Legacy資料・廃止済み設計・実験記録 |

ドキュメント全体の索引は [`docs/README.md`](docs/README.md) を参照してください。

## Legacy

旧ブラウザ単体版は [`Machi-Sim_Legacy`](https://github.com/SUIREN-KazutoHashimoto/Machi-Sim_Legacy) として保存しています。

旧実装をそのまま移植するのではなく、必要なドメイン仕様・設計知見を選別し、新しいServer-authoritative architectureに合わせて再設計します。移行方針は [`docs/archive/legacy-machi-sim/README.md`](docs/archive/legacy-machi-sim/README.md) に記録しています。

## Contributing / Security

- 貢献方法: [`CONTRIBUTING.md`](CONTRIBUTING.md)
- 行動規範: [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)
- 脆弱性報告: [`SECURITY.md`](SECURITY.md)

## License

MachiVerseWorks は **Apache License 2.0** の下で提供します。

- [`LICENSE`](LICENSE)
- [`NOTICE`](NOTICE)
- [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt)
