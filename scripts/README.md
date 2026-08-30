# Scripts

build、test、benchmark、開発環境セットアップなどの補助スクリプトを配置します。

アプリケーション本体のロジックは置きません。

## Windows 開発環境

- `setup-dev.bat`: `global.json` と `src/web/.node-version` を読み、Repository-local の `.tools/` に必要な .NET SDK / Node.js を配置して、`.NET restore + build` と `npm ci + build` まで実行する。
- `run-dev.bat`: `setup-dev.bat` が準備した `.tools/` を使用し、Server と Web Client を別ウィンドウで起動する。health / HTTP 応答を確認後、既定ブラウザで Web Client を開く。

## E2E

- `run-phase13-e2e.sh`: Phase 13 Road Traffic の複数 Vehicle spawn / update / Route completion / Browser instancing を実 Server → Browser で確認する。
- `run-phase14-e2e.sh`: Phase 14 Signal Traffic の queue / signal phase / Vehicle restart を実 Server → Browser で確認する。
- `run-phase16-e2e.sh`: Phase 16 Pedestrian の Building 間徒歩を実 Server → Browser で確認する。

詳細は [`docs/development/getting-started.md`](../docs/development/getting-started.md) を参照してください。
