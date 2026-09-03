# MachiVerseWorks.Persistence

Simulation の checkpoint と versioned Save Data の変換・検証を担当します。

- Save format version は application version / protocol version と独立して管理します。
- Simulation 内部の可変Storeを直接JSONへ露出しません。
- 翻訳済みUI文字列やlocale依存ラベルをSave Dataへ保存しません。
- ファイル配置やユーザー操作はこのprojectの責務に含めません。

Save format versionの運用では、authoritativeな永続化schemaを追加・変更した場合は新しい`SaveFormatVersion`を割り当てます。format 11は旧Economy系Saveの入力互換用として維持し、現在のSaveは拡張schema用のversionで書き出します。
