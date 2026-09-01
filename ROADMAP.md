# MachiVerseWorks Roadmap

MachiVerseWorks のロードマップは、Simulation と View を独立したトラックとして管理します。

## Roadmap Tracks

| Track | 正本 | 状態 | 説明 |
| --- | --- | --- | --- |
| Simulation | [`docs/roadmap/simulation.md`](docs/roadmap/simulation.md) | ▶️ 進行中 | authoritative world state、各種 Simulation domain、world generation、最適化等 |
| View | [`docs/roadmap/view.md`](docs/roadmap/view.md) | 📝 策定中 | 描画、camera、visualization、interaction、rendering LOD、管理UI等 |

Simulation と View は同時並行で進めてよい。ただし、View が Simulation 内部実装へ直接依存せず、公開された state / protocol / snapshot / query 境界を介して接続することを基本方針とする。

## 現在地

- **Simulation:** Phase 29 — World & Physical Environment Generation
- **次の Simulation Task:** `P29-001`
- **View:** 専用ロードマップを新設。具体的な Phase / Task は未策定。

## 運用方針

- 各 Track は独立して Task / Phase を進められる。
- Track 間依存がある場合でも、片方の内部実装へ直接依存せず、安定した公開境界を定義する。
- 統合確認が必要な成果は、各 Track の完了条件または将来の Integration Milestone で明示する。
- 既存の `Pxx-yyy` Task ID は履歴・Issue・PRとの対応を壊さないため維持する。
- View の新規 Task ID は `Vxx-yyy` を使用する。
- 完了済み Phase の詳細や旧構成は `docs/archive/` に保存する。

## Track 分離前の Roadmap

分離直前の完全なロードマップは、履歴として [`docs/archive/roadmap-before-track-split.md`](docs/archive/roadmap-before-track-split.md) に保存します。

既存 Phase 0〜24 の closeout 履歴は、引き続き以下を参照してください。

- [`docs/archive/roadmap-through-phase24-closeout.md`](docs/archive/roadmap-through-phase24-closeout.md)
- [`docs/archive/roadmap-phase13-through-phase16-closeout.md`](docs/archive/roadmap-phase13-through-phase16-closeout.md)
