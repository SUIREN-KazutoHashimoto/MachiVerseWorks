# ローカル開発の開始手順

MachiVerseWorks をローカルで build / test / 実行するための手順です。

SDK / runtime の version は個別に手入力して管理せず、Repository 内の固定ファイルを正とします。

- .NET SDK: ルート [`global.json`](../../global.json)
- Node.js: [`src/web/.node-version`](../../src/web/.node-version)
- npm dependency: [`src/web/package-lock.json`](../../src/web/package-lock.json)

実装計画はSimulation側[`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)、View側[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)を正本とします。

## Windows 実機での推奨セットアップ

Windows では `scripts/setup-dev.bat` と `scripts/run-dev.bat` を推奨入口とします。

### 前提

- Windows 10 / 11
- x64 または ARM64
- Windows PowerShell 5.1 以降
- 初回セットアップ時に `dot.net` と `nodejs.org` へ HTTPS 接続できること
- Repository を取得する場合は Git

`.NET SDK` と `Node.js` は PC 全体へインストールせず、Repository root の `.tools/` へ project-local tool として配置します。`.tools/` は Git 管理対象外です。

この方式では、PC に別 version の .NET / Node.js が入っていても MachiVerseWorks の起動時には Repository が指定する version を使用できます。

### 1. Repository の取得

```bat
git clone https://github.com/SUIREN-KazutoHashimoto/MachiVerseWorks.git
cd MachiVerseWorks
git switch develop
git pull --ff-only
```

通常開発を行う場合は `develop` から短命 branch を作成します。

```bat
git switch -c feature/<topic>
```

### 2. 開発環境の構築

Repository root で次を実行します。

```bat
scripts\setup-dev.bat
```

`setup-dev.bat` は次を順番に行います。

1. `global.json` から必要な .NET SDK version を取得
2. `src/web/.node-version` から必要な Node.js version を取得
3. Microsoft 公式 `dotnet-install.ps1` を使用して `.tools/dotnet/` へ .NET SDK を配置
4. Node.js 公式配布 ZIP を `nodejs.org` から取得し `.tools/node/` へ配置
5. `dotnet restore`
6. Release configuration の `dotnet build`
7. Web Client の `npm ci`
8. Web Client の `npm run build`

2回目以降は既に存在する同一 version の SDK / Node.js を再利用します。

セットアップに失敗した場合は途中で終了し、失敗した step を console に表示します。

### 3. Server + Web Client の起動

```bat
scripts\run-dev.bat
```

`run-dev.bat` は次を行います。

1. MachiVerseWorks.Server を別 console window で起動
2. `http://127.0.0.1:5080/health` の応答を確認
3. Web Client を別 console window で `127.0.0.1:5173` に固定して起動
4. Web Client の HTTP 応答を確認
5. 既定 browser で `http://127.0.0.1:5173` を開く

既定 endpoint は次のとおりです。

| 用途 | URL |
| --- | --- |
| Server | `http://127.0.0.1:5080` |
| Health | `http://127.0.0.1:5080/health` |
| E2E metrics | `http://127.0.0.1:5080/metrics/e2e` |
| WebSocket | `ws://127.0.0.1:5080/ws` |
| Web Client | `http://127.0.0.1:5173` |

停止するときは Server / Web の各 console window で `Ctrl+C` を押します。

Web Client の port は Server の既定 WebSocket Origin allowlist と一致させるため `5173` に固定し、使用中の場合は別 port へ自動 fallback せず起動失敗とします。

### 4. 再セットアップ

`global.json` または `.node-version` が更新された場合は、再度次を実行してください。

```bat
scripts\setup-dev.bat
```

新しい version は `.tools/` 配下の別 directory に配置されます。

完全に作り直す場合は MachiVerseWorks の Server / Web Client を停止したうえで `.tools/` と `src/web/node_modules/` を削除し、`setup-dev.bat` を再実行します。

## 手動セットアップ / Windows 以外

project-local bootstrap を使用しない場合は、各 OS へ `global.json` が要求する .NET SDK と `.node-version` が要求する Node.js を用意します。

### .NET

Repository root で次を実行します。

```bash
dotnet --version
dotnet restore MachiVerseWorks.slnx
dotnet build MachiVerseWorks.slnx --configuration Release --no-restore
dotnet test MachiVerseWorks.slnx --configuration Release --no-build
```

### Web Client

```bash
cd src/web
node --version
npm ci
npm run lint
npm run typecheck
npm test
npm run build
```

## Server + Web Client の手動起動

2つの terminal を使います。

Terminal 1:

```bash
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

Terminal 2:

```bash
cd src/web
npm run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

Web Client の既定 Server URL は `ws://127.0.0.1:5080/ws` なので、既定構成では追加設定なしで接続します。

別 Server へ接続する場合は Web Client 起動時に `VITE_SERVER_URL` を指定します。

## End-to-End のローカル確認

実Server / Protocol / Web Clientを横断するE2Eの正規一覧は[`../../.github/workflows/e2e.yml`](../../.github/workflows/e2e.yml)です。現在はCore PoCからRadio / Spectrumまでの実装済み主要domainをmatrixで管理しています。

各scenarioは`script`欄に対応する`bash scripts/run-phaseXX-e2e.sh`をローカルでも実行できます。変更したdomainに対応するscenarioを選び、無関係な古いPhase scriptを一律に実行する運用にはしません。

Core / Browser基盤だけを確認したい場合は、Chrome または Chromium が `PATH` にある環境で次を実行できます。

```bash
bash scripts/run-phase6-e2e.sh
```

このscenarioの詳細は[`e2e-poc.md`](e2e-poc.md)を参照してください。

## PR 前の確認

最低限、次を成功させます。

```bash
dotnet restore MachiVerseWorks.slnx
dotnet build MachiVerseWorks.slnx --configuration Release --no-restore
dotnet test MachiVerseWorks.slnx --configuration Release --no-build
cd src/web
npm ci
npm run lint
npm run typecheck
npm test
npm run build
```

Server / Protocol / Browserを変更した場合は、加えて[`../../.github/workflows/e2e.yml`](../../.github/workflows/e2e.yml)の対象domainに対応するE2E scriptを実行します。性能へ影響する変更では[`../../.github/workflows/benchmarks.yml`](../../.github/workflows/benchmarks.yml)の対象benchmarkも確認します。

Repository 内 Markdown link は CI の `repository` job でも検証されます。Markdownを移動・改名した場合は特に`python scripts/check-markdown-links.py`で事前確認することを推奨します。

## Version

通常開発開始後はルート `VERSION` が application version の唯一の正本です。C# build と Web build はここから version を取得します。個別 `.csproj` や `package.json` に同じ app version を手入力しません。
