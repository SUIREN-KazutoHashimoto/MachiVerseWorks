# Documentation

MachiVerseWorks のドキュメント入口です。

| ディレクトリ | 役割 | 主な入口 |
| --- | --- | --- |
| [`product/`](product/) | プロジェクトの目的、概念、用語、ユーザー視点の整理 | [`product/README.md`](product/README.md) |
| [`architecture/`](architecture/) | システム構成、責務分離、通信、並列化などの技術設計 | [`architecture/overview.md`](architecture/overview.md) |
| [`specifications/`](specifications/) | Agent、交通、公共交通、物流、電力などの現行仕様 | [`specifications/README.md`](specifications/README.md) |
| [`development/`](development/) | 開発環境、Git、version、テスト、性能計測などの運用 | [`development/git-workflow.md`](development/git-workflow.md) |
| [`decisions/`](decisions/) | ADR（Architecture Decision Record） | [`decisions/README.md`](decisions/README.md) |
| [`archive/`](archive/) | Legacy 資料、廃止設計、過去の実験記録 | [`archive/legacy-machi-sim/README.md`](archive/legacy-machi-sim/README.md) |

## 管理原則

- 仕様は What / Why、アーキテクチャは How を中心に記述します。
- 同じ内容を複数文書へコピーせず、正本へのリンクを使います。
- 大きな設計判断は ADR に理由を残します。
- 現行ドキュメントと過去資料を混在させません。
- Legacy 資料をそのまま現行仕様へ昇格させず、必要な内容を新アーキテクチャに合わせて書き直します。
- 実装と文書が食い違う場合は、実効コード・test・意図した仕様を確認し、どちらかを黙って正と決めつけず同期します。
