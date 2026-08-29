# Original Assets

このディレクトリは、MachiVerseWorks の画像・ロゴ等の**加工前原本**を保管するための場所です。

## 保存ルール

- 原本の画素、圧縮、カラープロファイル、透過情報、メタデータ等は意図なく変更しない。
- ファイル名は用途が分かる英小文字 kebab-case を基本とする。例: `machiverseworks-icon.png`。
- 同一デザインの単なるリサイズ・WebP変換・favicon化などは原本として増やさず、派生アセットとして扱う。
- デザイン自体を更新した新しいマスターは、旧原本を必要に応じて履歴に残したうえで差し替える。
- Web Clientからこのディレクトリを直接runtime参照しない。必要な形式・サイズへ変換したファイルをWeb側のasset領域へ配置する。
- チャット添付やプレビュー表示から得たハッシュ値ではなく、Repositoryへ保存された原本そのものを正として扱う。

## 原本台帳

| ファイル | 用途 | 原本情報 |
| --- | --- | --- |
| `machiverseworks-icon.png` | MachiVerseWorks アイコン原本 | 1254×1254 px / 1,055,908 bytes / Git blob: `6cfb090d167b3b444d748ff1b767c074e1ae2d3a` |
| `machiverseworks-social-preview.png` | GitHub Social Preview / プロジェクト紹介用横長ビジュアル原本 | 1774×887 px / 1,457,352 bytes / Git blob: `cf132be818346d9169321fd9f69f5c8a92ef4367` |

Git blob ID はRepository内の原本同一性確認用です。外部配布用にSHA-256等が必要になった場合は、Repositoryから取得した原本ファイルに対して生成します。

## 大容量ファイル

通常サイズのPNG/SVG等はGitで管理します。PSD、動画、非常に大きな画像などでRepository容量への影響が無視できなくなった場合は、対象を確認してからGit LFS等へ切り替えます。
