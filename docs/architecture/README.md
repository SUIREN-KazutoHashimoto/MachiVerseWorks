# Architecture

MachiVerseWorks の技術アーキテクチャを管理します。

主な対象:

- Simulation Core と Server の責務分離
- Server / Browser 間プロトコル
- Simulation tick と snapshot 配信
- spatial interest management
- threading / job system
- data model / memory layout
- save / load
- localization / internationalization boundary

主要文書:

- [`overview.md`](overview.md): 全体アーキテクチャ
- [`simulation-core.md`](simulation-core.md): Simulation Core 最小 PoC の state ownership と hot path
- [`protocol.md`](protocol.md): Server / Web Client 間 binary protocol の versioning と wire layout
- [`headless-server.md`](headless-server.md): Headless Server の lifecycle、WebSocket、command queue、snapshot publish
- [`localization.md`](localization.md): 多言語対応を見越した言語境界

仕様上の振る舞いそのものは `docs/specifications/` に記述します。
