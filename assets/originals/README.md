# Original Assets

このディレクトリは、MachiVerseWorks の画像・ロゴ等の**加工前原本**を保管するための場所です。

## 保存ルール

- 原本の画素、圧縮、カラープロファイル、透過情報、メタデータ等は意図なく変更しない。
- ファイル名は用途が分かる英小文字 kebab-case を基本とする。例: `machiverseworks-icon.png`。
- 同一デザインの単なるリサイズ・WebP変換・favicon化などは原本として増やさず、派生アセットとして扱う。
- デザイン自体を更新した新しいマスターは、旧原本を必要に応じて履歴に残したうえで差し替える。
- Web Clientからこのディレクトリを直接runtime参照しない。必要な形式・サイズへ変換したファイルをWeb側のasset領域へ配置する。
- 原本を追加するときは、必要に応じてSHA-256を併記し、意図しない再圧縮や差し替えを検出できるようにする。

## 原本台帳

| ファイル | 用途 | 原本情報 |
| --- | --- | --- |
| `machiverseworks-social-preview.png` | GitHub Social Preview / プロジェクト紹介用横長ビジュアル | 1774×887 px / 2,335,044 bytes / SHA-256: `33da21333949b7d05cd5d6ee9b1ebaa0ef1d8717f54e91f3fc835163d37bfab2` |

`machiverseworks-social-preview.png` のチェックサムは `machiverseworks-social-preview.png.sha256` にも保存します。

## 大容量ファイル

通常サイズのPNG/SVG等はGitで管理します。PSD、動画、非常に大きな画像などでRepository容量への影響が無視できなくなった場合は、対象を確認してからGit LFS等へ切り替えます。
