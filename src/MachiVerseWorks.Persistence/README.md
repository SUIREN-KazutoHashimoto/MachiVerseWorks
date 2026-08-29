# MachiVerseWorks.Persistence

Simulation の checkpoint と versioned Save Data の変換・検証を担当します。

- Save format version は application version / protocol version と独立して管理します。
- Simulation 内部の可変Storeを直接JSONへ露出しません。
- 翻訳済みUI文字列やlocale依存ラベルをSave Dataへ保存しません。
- ファイル配置やユーザー操作はこのprojectの責務に含めません。
