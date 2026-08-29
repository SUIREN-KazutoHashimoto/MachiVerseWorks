# Documentation

MachiVerseWorks のドキュメント入口です。

| ディレクトリ | 役割 |
| --- | --- |
| [`product/`](product/) | プロジェクトの目的、概念、用語、ユーザー視点の整理 |
| [`architecture/`](architecture/) | システム構成、責務分離、通信、並列化などの技術設計 |
| [`specifications/`](specifications/) | Agent、交通、公共交通、物流、電力などの現行仕様 |
| [`development/`](development/) | 開発環境、Git、version、テスト、性能計測などの運用 |
| [`decisions/`](decisions/) | ADR（Architecture Decision Record） |
| [`archive/`](archive/) | Legacy 資料、廃止設計、過去の実験記録 |

## 管理原則

- 仕様は What / Why、アーキテクチャは How を中心に記述します。
- 同じ内容を複数文書へコピーせず、正本へのリンクを使います。
- 大きな設計判断は ADR に理由を残します。
- 現行ドキュメントと過去資料を混在させません。
