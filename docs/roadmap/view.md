# View Roadmap

View Track は、Simulation が公開する state / protocol / snapshot / query を利用し、MachiVerseWorks の描画・可視化・操作体験を独立して発展させるためのロードマップです。

> **状態:** 📝 フォーマット作成済み / 具体的なPhaseは未策定

## 基本方針

- View は Simulation の内部Storeや内部実装へ直接依存しない。
- Simulation Tick と Render Frame は独立して扱える設計を前提とする。
- Camera・描画範囲・Rendering LOD が authoritative Simulation state に影響してはならない。
- 表示性能の最適化は View 側で積極的に行ってよい。
- View向けTask IDは `Vxx-yyy` を使用し、既存Simulation Task ID `Pxx-yyy` と衝突させない。
- Simulation側の未完成機能を待たず、安定した公開境界またはfixture / recorded snapshotを利用して先行実装できるようにする。

## 現在地

| View Phase | 内容 | 状態 |
| --- | --- | --- |
| 未策定 | View Roadmapの具体化 | 📝 未策定 |

## View Phase テンプレート

新しいView Phaseを追加するときは、以下の形式を使用します。

```markdown
## View Phase VXX — Title

> **状態: ⏳ 待機**  
> **依存:** Simulation Phase XX / View Phase VXX / 公開境界 ...  
> このPhaseで利用者から観測可能になるView上の成果を記述する。

### Scope

- このPhaseで扱う描画・操作・可視化範囲
- Simulation側に必要な公開契約
- このPhaseでは扱わない事項

### Tasks

- ⬜ **VXX-001** — Task
- ⬜ **VXX-002** — Task
- ⬜ **VXX-003** — Task

### 完了条件

- ⬜ 観測可能なView上の成果
- ⬜ Simulation stateへ副作用を与えないことの確認
- ⬜ 必要なperformance / rendering benchmark
- ⬜ E2E / visual regression / interaction test
- ⬜ docs / architecture / ADR同期

### closeout evidence

- PR / merge commit
- CI / E2E / benchmark
- screenshot / recording等の必要な確認証跡
- docs / ADR同期
```

## Integration 記述テンプレート

Simulation Trackとの接続が必要な場合は、Phase内に次を明示します。

```markdown
### Simulation Integration

- **入力:** WorldSnapshot / Protocol / Query / Event 等
- **更新頻度:** Simulation Tickとは独立したView側の取得・補間方針
- **Fallback:** 未提供データの扱い
- **禁止依存:** Simulation内部Store / mutable internal object 等
```

## Backlog

具体的なView Phaseはまだ定義しません。World / terrain / city のSimulation契約が進むのと並行して、Camera、Layer、Entity描画、Interaction、Rendering LOD、管理UI等を今後このファイルへ追加します。
