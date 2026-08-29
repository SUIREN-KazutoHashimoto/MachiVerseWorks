# ローカル開発の開始手順

MachiVerseWorks の初回実装をローカルで build / test / 実行するための最小手順です。

## 前提

- Git
- `global.json` が要求する .NET SDK
- `src/web/.node-version` が要求する Node.js
- npm（Node.js 同梱版）

SDK / runtime の version は個別に手入力して管理せず、Repository 内の固定ファイルを正とします。

## Repository の取得

通常開発は `develop` から短命 branch を作成します。

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

Headless Server を起動する場合は次を実行します。

```bash
dotnet run --project src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj
```

既定では `http://127.0.0.1:5080` を listen します。別 terminal から health endpoint を確認できます。

```bash
curl http://127.0.0.1:5080/health
```

WebSocket endpoint は `ws://127.0.0.1:5080/ws` です。接続後は MachiVerseWorks Protocol の binary `Hello` frame が最初の message として必要です。

## Web Client

Node.js の version が `src/web/.node-version` と一致することを確認してから実行します。

```bash
cd src/web
node --version
npm ci
npm run lint
npm run typecheck
npm run build
npm run dev
```

Vite が表示するローカル URL をブラウザで開き、MachiVerseWorks の空の 3D viewport と version 表示を確認します。

## Version

通常開発開始後はルート `VERSION` がアプリケーションversionの唯一の正本です。C# build と Web build はここから version を取得します。個別 `.csproj` や `package.json` に同じ app version を手入力しません。

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
npm run build
```

Repository 内 Markdown link は CI の `repository` job でも検証されます。
