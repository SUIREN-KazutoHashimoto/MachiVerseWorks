<p align="center">
  <img src="assets/brand/machiverseworks-icon.png" alt="MachiVerseWorks icon" width="132">
</p>

<h1 align="center">MachiVerseWorks</h1>

<p align="center">
  <strong>City Simulation Project</strong><br>
  C#製ヘッドレス・シミュレーションサーバーとread-onlyブラウザ3D Viewで構成する、大規模リアルタイム都市シミュレーション。
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

旧 Machi-Sim で得られたドメイン・設計・性能面の知見を引き継ぎつつ、ブラウザ単体実装からシミュレーション本体を分離しています。Simulationがauthoritative worldと意味的処理を所有し、Gatewayがread-only observationのsubscription / cache / deliveryを担当し、ViewはGateway経由で必要な3D空間・Entity情報を読み取って忠実に表示します。World / City / Serverを変更する操作はView / Gatewayから分離し、Management Clientからserver-authoritative command境界を利用する構成です。

> [!NOTE]
> 現在は **pre-alpha** 段階です。実装・API・Protocol・Save format・仕様は開発の進行に伴って変更される可能性があります。

## Current baseline

- Application version: ルート[`VERSION`](VERSION)を正本とする
- Protocol: **2.16**（正本: [`ProtocolVersion.Current`](src/MachiVerseWorks.Protocol/ProtocolVersion.cs)）
- Save format: **11**（正本: [`SaveFormatVersion.Current`](src/MachiVerseWorks.Persistence/SaveFormatVersion.cs)）
- Simulation Phase 28 Radio & Spectrum Foundation: ✅ 完了
- Simulation Phase 29 World & Physical Environment Generation: ▶️ 次
- Gateway Roadmap: Phase 1 Observation Boundary Foundationから開始し、Simulation Phase 29 / View Phase 1と並行可能
- View Roadmap: Phase 1 Read-Only View Foundationから開始し、各描画Taskは依存するSimulation semantic source / Gateway delivery contractの完成に追従
- Management Roadmap: Simulation Phase 36のcommand境界に合わせてManagement Clientを実装予定

詳細なPhase / Task状態は、Simulation側を[`roadmap/SIMULATION_ROADMAP.md`](roadmap/SIMULATION_ROADMAP.md)、Gateway側を[`roadmap/GATEWAY_ROADMAP.md`](roadmap/GATEWAY_ROADMAP.md)、read-only View側を[`roadmap/VIEW_ROADMAP.md`](roadmap/VIEW_ROADMAP.md)、管理・編集UIを[`roadmap/MANAGEMENT_ROADMAP.md`](roadmap/MANAGEMENT_ROADMAP.md)で管理し、READMEへ全Task一覧は複製しません。

## Architecture

```text
                         ┌──────────────────────────────┐
                         │      Browser Read-Only View  │
                         │ TypeScript / Three.js        │
                         └──────────────▲───────────────┘
                                        │ Observation
                                        │ Protocol 2.16 / WebSocket
                               ┌────────┴────────┐
                               │     Gateway      │
                               │ subscribe/cache  │
                               │ delivery/resync  │
                               └────────▲─────────┘
                                        │ detached semantic source
┌──────────────────────────────┐        │
│      Management Client       │        │
│ edit / operation / admin UI  │        │
└──────────────┬───────────────┘        │
               │ command                │
               ▼                        │
        ┌───────────────────────────────┴─┐
        │      MachiVerseWorks.Server     │
        │ Gateway / Command adapters      │
        └────────────────┬────────────────┘
                         │
        ┌────────────────▼────────────────┐
        │   MachiVerseWorks.Simulation    │
        │ authoritative world / semantics │
        └────────────────▲────────────────┘
                         │ checkpoint mapping
        ┌────────────────┴────────────────┐
        │  MachiVerseWorks.Persistence    │
        │ Save Format 11 / validation     │
        └─────────────────────────────────┘

MachiVerseWorks.Protocol = Observation / Command wire contract
```

| Component | Responsibility |
| --- | --- |
| **Simulation** | authoritative world、rule、意味的state、schedule、semantic observation source、Agent / Road / Traffic / Population / Transit / Economy / Infrastructure / Communication |
| **Persistence** | Simulation checkpointとversioned Save Dataのmapping、外部Save validation |
| **Gateway** | read-only Observation Request、subscription、snapshot / delta delivery、cache / deduplication、Protocol adaptation、reconnect / resync |
| **Server / Command Boundary** | Administration / Management commandのvalidation・dispatch・authoritative mutation |
| **Protocol** | Client / Server間のstable ID・version negotiation・Observation / Command binary layout |
| **View** | read-only 3D描画、Camera、Selection、Inspector、補間、Historical viewing、localization |
| **Management** | build / edit / remove、運転control、Server設定、Save / Load等の明示的な管理UI |

Gatewayは現時点では`MachiVerseWorks.Server`内の責務として実装し、Roadmap上の分離は独立した責務・進捗管理を意味します。将来必要になればprocess / deploy unitを分離できる境界へ育てます。

設計の詳細は[`docs/architecture/overview.md`](docs/architecture/overview.md)、SimulationとGateway / Viewの境界・cache設計は[`docs/architecture/observation-gateway.md`](docs/architecture/observation-gateway.md)、Protocolのbinary正本は[`docs/architecture/protocol.md`](docs/architecture/protocol.md)、Save仕様は[`docs/specifications/save-data.md`](docs/specifications/save-data.md)を参照してください。採用理由は[`ADR-0001`](docs/decisions/ADR-0001-csharp-headless-simulation-server.md)と[`ADR-0007`](docs/decisions/ADR-0007-read-only-view-observation-management-boundary.md)に記録しています。

## Design Principles

- **Simulation authoritative** — Worldの状態・rule・意味・予定・状態遷移はSimulationで完結する
- **Read-only Gateway** — Gatewayは意味を生成せず、Observation Request / cache / delivery / resyncだけを担当する
- **Read-only View** — ViewはSimulationを変更せず、意味を推測・補完・再計算しない
- **Observation / Command separation** — 観測要求とauthoritative mutation commandを別境界にする
- **Spatial observation** — Viewには必要な3D範囲・明示targetだけをGatewayから配信する
- **View-independent simulation** — View / Gatewayの観測状態、接続数、Camera、LOD、FPSでSimulation結果を変えない
- **Separated clocks** — Simulation tick / observation publish / render frameを分離する
- **Measure first** — 最適化はprofiler・benchmark・実測値に基づく
- **Stable data contracts** — Protocol / Save Dataへ表示言語やUI文字列を混ぜない
- **Explicit versioning** — Application / Protocol / Save formatを独立してversioningする
- **Small, completable tasks** — 単独で実装・検証・完了できるTask ID単位で進める

## Implemented simulation domains

現在の基盤には、3D Agent / Building / POI、Road Network / Routing / Road Traffic / Intersection Control、Pedestrian、Population daily activity、Railway Infrastructure / Operations、Multimodal Transit、Industry / Jobs / Economy、Logistics / Freight、Power、Water / Sewer、Gas、Optical Communication、Radio / Spectrum、およびServer Administration / Remote MCP境界が含まれます。

仕様入口は[`docs/specifications/README.md`](docs/specifications/README.md)、実装境界は[`docs/architecture/README.md`](docs/architecture/README.md)を参照してください。

## Repository

```text
MachiVerseWorks/
├─ src/
│  ├─ MachiVerseWorks.Simulation/
│  ├─ MachiVerseWorks.Persistence/
│  ├─ MachiVerseWorks.Server/
│  ├─ MachiVerseWorks.Protocol/
│  └─ web/
├─ tests/
├─ benchmarks/
├─ assets/
├─ docs/
│  ├─ product/
│  ├─ architecture/
│  ├─ specifications/
│  ├─ development/
│  ├─ decisions/
│  ├─ roadmap/          # Phase補足設計・検討資料
│  └─ archive/
├─ roadmap/
│  ├─ SIMULATION_ROADMAP.md
│  ├─ GATEWAY_ROADMAP.md
│  ├─ VIEW_ROADMAP.md
│  └─ MANAGEMENT_ROADMAP.md
├─ scripts/
├─ tools/
├─ .github/
├─ global.json
├─ VERSION
├─ AGENTS.md
└─ README.md
```

## Development

.NET SDKはルートの[`global.json`](global.json)を正本として固定し、現在は **.NET 10** 系を採用しています。Viewは **TypeScript + Three.js** です。

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
| [`docs/specifications/`](docs/specifications/) | Simulationの振る舞い — **What / Why** |
| [`docs/development/`](docs/development/) | 開発・テスト・Git・CI・version運用 |
| [`docs/decisions/`](docs/decisions/) | Architecture Decision Record |
| [`docs/roadmap/`](docs/roadmap/) | Roadmapを補足するPhase設計・検討資料。進捗の正本ではない |
| [`docs/archive/`](docs/archive/) | Legacy資料・廃止済み設計・実験記録 |

進捗の正本は[`roadmap/SIMULATION_ROADMAP.md`](roadmap/SIMULATION_ROADMAP.md)、[`roadmap/GATEWAY_ROADMAP.md`](roadmap/GATEWAY_ROADMAP.md)、[`roadmap/VIEW_ROADMAP.md`](roadmap/VIEW_ROADMAP.md)、[`roadmap/MANAGEMENT_ROADMAP.md`](roadmap/MANAGEMENT_ROADMAP.md)、ドキュメント全体の索引は[`docs/README.md`](docs/README.md)を参照してください。

## Legacy

旧ブラウザ単体版は[`Machi-Sim_Legacy`](https://github.com/SUIREN-KazutoHashimoto/Machi-Sim_Legacy)として保存しています。

旧実装をそのまま移植するのではなく、必要なドメイン仕様・設計知見を選別し、新しいSimulation-authoritative architectureに合わせて再設計します。移行方針は[`docs/archive/legacy-machi-sim/README.md`](docs/archive/legacy-machi-sim/README.md)に記録しています。

## Contributing / Security

- 貢献方法: [`CONTRIBUTING.md`](CONTRIBUTING.md)
- 行動規範: [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)
- 脆弱性報告: [`SECURITY.md`](SECURITY.md)

## License

MachiVerseWorks は **Apache License 2.0** の下で提供します。

- [`LICENSE`](LICENSE)
- [`NOTICE`](NOTICE)
- [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt)
