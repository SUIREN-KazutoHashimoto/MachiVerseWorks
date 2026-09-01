# Documentation

MachiVerseWorks のドキュメント入口です。

## まず読む

- プロジェクト全体の概要: [`../README.md`](../README.md)
- Simulation側の現在地と次の作業: [`../roadmap/SIMULATION_ROADMAP.md`](../roadmap/SIMULATION_ROADMAP.md)
- View側の現在地と次の作業: [`../roadmap/VIEW_ROADMAP.md`](../roadmap/VIEW_ROADMAP.md)
- 共通の開発・文書ルール: [`../AGENTS.md`](../AGENTS.md)

分野別の現行文書は、次の各 README を索引として辿ります。

| ディレクトリ | 役割 | 主な入口 |
| --- | --- | --- |
| [`product/`](product/) | プロジェクトの目的、概念、用語、ユーザー視点の整理 | [`product/README.md`](product/README.md) |
| [`architecture/`](architecture/) | システム構成、責務分離、通信、並列化などの技術設計 | [`architecture/README.md`](architecture/README.md) |
| [`specifications/`](specifications/) | Simulation、交通、公共交通、物流、都市Infrastructureなどの現行仕様 | [`specifications/README.md`](specifications/README.md) |
| [`development/`](development/) | 開発環境、Git、version、CI、テスト、性能計測などの運用 | [`development/README.md`](development/README.md) |
| [`decisions/`](decisions/) | ADR（Architecture Decision Record） | [`decisions/README.md`](decisions/README.md) |
| [`archive/`](archive/) | Legacy資料、完了済みROADMAP、廃止設計、過去の実験記録 | [`archive/README.md`](archive/README.md) |

## 管理原則

- 仕様は What / Why、アーキテクチャは How を中心に記述します。
- 同じ内容を複数文書へコピーせず、正本へのリンクを使います。
- 大きな設計判断は ADR に理由を残します。
- 現行ドキュメントと過去資料を混在させません。
- Legacy資料をそのまま現行仕様へ昇格させず、必要な内容を新アーキテクチャに合わせて書き直します。
- 実装と文書が食い違う場合は、実効コード・test・意図した仕様を確認し、どちらかを黙って正と決めつけず同期します。
- Phase番号、Protocol version、Save Formatなど別の正本を持つ値は、必要な場合だけ記載し、索引READMEではできるだけ正本へリンクします。

## 文書を追加・更新するときのチェック

1. What / Why は `specifications/`、How は `architecture/`、開発手順や計測結果は `development/` に置きます。
2. 現行文書を追加したら、そのディレクトリの `README.md` に索引を追加します。
3. Phaseの挿入・並べ替えを行った場合は、他文書に固定されたPhase番号が残っていないか確認します。
4. リポジトリルートで `python scripts/check-markdown-links.py` を実行し、ローカルリンクとMarkdown heading anchorを検証します。
5. 現行契約ではなくなった資料は、参照関係を確認してから `archive/` へ移します。
