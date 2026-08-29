# Architecture

MachiVerseWorks の技術アーキテクチャを管理します。

## 現在の入口

- [`overview.md`](overview.md): 全体構成、責務境界、tick / snapshot、interest management、性能原則

## 主な対象

- Simulation Core と Server の責務分離
- Server / Browser 間プロトコル
- Simulation tick と snapshot 配信
- spatial interest management
- threading / job system
- data model / memory layout
- save / load

仕様上の振る舞いそのものは `docs/specifications/` に記述します。

長期間影響する採用理由は `docs/decisions/` の ADR に記録します。
