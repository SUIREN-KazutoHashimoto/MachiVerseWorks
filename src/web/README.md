# Web Client

MachiVerseWorks の表示・入力を担当する Web Client です。

## Phase 1 の技術構成

- Node.js 24 LTS を `.node-version` で完全固定する。
- Vite + TypeScript の vanilla 構成とし、UI framework は Phase 1 では導入しない。
- 3D 描画の最小依存として Three.js を使用する。
- ESLint + typescript-eslint で TypeScript を静的検査する。
- `package.json` の直接依存は exact version とし、`package-lock.json` をコミットする。
- 依存更新は Dependabot の npm 設定から行う。
- アプリケーションversionは `package.json` へ重複管理せず、Vite build 時にリポジトリルート `VERSION` を読む。

## コマンド

```bash
npm ci
npm run dev
npm run lint
npm run typecheck
npm run build
```

Web Client は Simulation の正本ではありません。Server から受け取った状態の表示と、ユーザー入力の送信に責務を限定します。
