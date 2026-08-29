# Scripts

build、test、benchmark、開発環境セットアップなどの補助スクリプトを配置します。

アプリケーション本体のロジックは置きません。

## Windows 開発環境

- `setup-dev.bat`: `global.json` と `src/web/.node-version` を読み、Repository-local の `.tools/` に必要な .NET SDK / Node.js を配置して、`.NET restore + build` と `npm ci + build` まで実行する。
- `run-dev.bat`: `setup-dev.bat` が準備した `.tools/` を使用し、Server と Web Client を別ウィンドウで起動する。health / HTTP 応答を確認後、既定ブラウザで Web Client を開く。

詳細は [`docs/development/getting-started.md`](../docs/development/getting-started.md) を参照してください。
