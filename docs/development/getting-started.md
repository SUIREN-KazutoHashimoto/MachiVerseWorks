# ローカル開発の開始手順

MachiVerseWorks をローカルで build / test / 実行するための最小手順です。

## 前提

- Git
- `global.json` が要求する .NET SDK
- `src/web/.node-version` が要求する Node.js
- npm（Node.js 同梱版）

SDK / runtime のversionは個別に手入力して管理せず、Repository内の固定ファイルを正とします。

## Repository の取得

通常開発は `develop` から短命branchを作成します。

```bash
git clone https://github.com/SUIREN-KazutoHashimoto/MachiVerseWorks.git
cd MachiVerseWorks
git switch develop
git pull --ff-only
git switch -c feature/<topic>
```

## .NET

ルートで次を実行します。

```bash
dotnet --version
dotnet restore MachiVerseWorks.slnx
dotnet build MachiVerseWorks.slnx --configuration Release --no-restore
dotnet test MachiVerseWorks.slnx --configuration Release --no-build
```

## Server + Web Client のローカル起動

Phase 6 の通常確認では2つのterminalを使います。

Terminal 1 でHeadless Serverを起動します。

```bash
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

既定では `http://127.0.0.1:5080` をlistenし、WebSocket endpointは `ws://127.0.0.1:5080/ws` です。healthとPhase 6 metricsは別terminalから確認できます。

```bash
curl http://127.0.0.1:5080/health
curl http://127.0.0.1:5080/metrics/e2e
```

Terminal 2 でWeb Clientを起動します。

```bash
cd src/web
node --version
npm ci
npm run dev
```

Viteが表示するlocal URLをブラウザで開きます。Web Clientの既定Server URLは `ws://127.0.0.1:5080/ws` なので、追加設定なしで接続します。

ブラウザでは次を確認できます。

- 接続状態とProtocol version
- subscription内のAgent数
- drag / wheelによるcamera移動とsubscription更新
- Protocol decode時間
- animation frame間隔

Server URLを変更する場合はWeb Client起動時に `VITE_SERVER_URL` を指定します。

## Phase 6 E2E の一括確認

ChromeまたはChromiumが `PATH` にある環境では、Repository rootから次の1コマンドでPhase 6 scenarioを再現できます。

```bash
bash scripts/run-phase6-e2e.sh
```

このscriptは1,000 / 10,000 / 100,000 AgentのServerを順に起動し、実Browserで接続、表示state、camera由来subscription、remove、再接続、近傍配信、Server/Client metricsを検証します。詳細は [`e2e-poc.md`](e2e-poc.md) を参照してください。

## Web Client の静的検証

```bash
cd src/web
npm ci
npm run lint
npm run typecheck
npm test
npm run build
```

## Version

通常開発開始後はルート `VERSION` がapplication versionの唯一の正本です。C# buildとWeb buildはここからversionを取得します。個別 `.csproj` や `package.json` に同じapp versionを手入力しません。

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

Phase 6以降のEnd-to-End変更では、加えてRepository rootから次を実行します。

```bash
bash scripts/run-phase6-e2e.sh
```

Repository内Markdown linkはCIの `repository` jobでも検証されます。
