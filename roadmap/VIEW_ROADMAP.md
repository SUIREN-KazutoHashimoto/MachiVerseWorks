# View Roadmap

このファイルは、MachiVerseWorks の **View 側の実装ロードマップ**です。Web Client の描画、カメラ、表示表現、UI / UX、可視化、描画最適化など、Simulation の正本状態を利用してユーザーへ提示する機能を対象とします。

Simulation Core、authoritative World、Simulation rule、determinism、Simulation workload 最適化などは [`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md) で管理します。

MachiVerseWorks の View 開発を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** 未計画  
> **次の実装タスク:** 未定

## 全体の現在地

| Phase | 内容 | 状態 |
| --- | --- | --- |
| - | 未計画 | 未着手 |

## View Roadmap 運用ルール

- 状態を付けるのは、単独で完了判定できる作業だけとする。
- 1タスクは原則として「1つの観測可能な成果」を持つ。
- 1タスク内に独立した成果が複数ある場合は分割する。
- 描画、UI、UX、操作性、performance、accessibility 等を必要に応じて独立Taskへ分割する。
- Simulation の正本状態や rule を View 都合で変更しない。
- Simulation 側の仕様変更が必要になった場合は、[`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md) と関連仕様・設計へ切り分ける。
- 実装だけでなく、必要な Browser 確認、test、performance 計測、docs 同期まで含めて完了判定する。
- 未実装の大テーマは Task へ詰め込まず、Phase または Backlog として整理してから分解する。

---

## Phase テンプレート

## Phase X — Phase Name

> **状態:** 未着手  
> **依存:** 未定  
> このPhaseの目的を記述する。

- [ ] **VX-001** — Taskを記述する
- [ ] **VX-002** — Taskを記述する

### Phase X 完了条件

- 完了条件を記述する。
- 完了条件を記述する。

---

## 継続Backlog

現時点では未定。

## 新規Backlogの扱い

View 開発中に新しい大テーマが見つかった場合は、既存Phaseへ無理に詰め込まない。

1. 既存Phaseの完了に必須なら、そのPhaseへ独立Taskとして追加する。
2. 完了に必須でない大テーマなら、このROADMAP末尾へBacklogとして記録する。
3. 着手時に必要な仕様・設計・UX方針を関連文書へ切り分ける。
4. 実装・描画・操作・検証・performanceのどこまでをPhase完了条件とするか明示する。
5. Phase完了時に、残件が暗黙に持ち越されていないことを確認する。
