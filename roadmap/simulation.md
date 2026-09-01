# Simulation Roadmap

Simulation Track は、MachiVerseWorks の authoritative world state と Simulation domain を管理します。

> **現在:** Phase 29 — World & Physical Environment Generation  
> **次の実装タスク:** `P29-001` — `WorldEnvironmentConfig` / world seed / geographic north / latitude・hemisphere・sea level等の正本契約を仕様化する

## 現在地

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0〜27 | Foundation / Simulation / Infrastructure / Remote Administration | ✅ 完了 |
| 28 | Radio & Spectrum Foundation | ✅ 完了 |
| 29 | World & Physical Environment Generation | ▶️ 次 |
| 30 | Regional & Urban Generation | ⏳ 待機 |
| 31 | Persistent Regional & Settlement Evolution | ⏳ 待機 |
| 32 | Simulation Scheduling & Workload Optimization | ⏳ 待機 |
| 33 | Deterministic Parallel Simulation | ⏳ 待機 |
| 35 | Historical World & Replay | ⏳ 待機 |

Phase 34 `World Rendering & Rendering LOD` と Phase 36 `World & City Management UI` は View Track へ分離します。具体的な View Phase 番号・Task は [`view.md`](view.md) で改めて策定します。

Phase 37 `Distribution & Compatibility` と Phase 38 `Extension Platform & Localization` は Simulation / View のどちらか一方へ閉じない横断領域のため、今後 Master Roadmap 側で Integration / Platform Track として再整理します。

## Simulation Track 運用ルール

- 状態記号を付けるのは、単独で完了判定できる作業だけとする。
- 1タスクは原則として「1つの観測可能な成果」を持つ。
- コード変更では必要な build / test / benchmark / 実機確認まで含めて完了とする。
- 仕様や設計を変更した場合は、対応する docs / ADR の更新まで含めて完了とする。
- Task実装状態・`develop`統合状態・Phase正式closeoutは別の状態として扱う。
- 既存 Task ID `Pxx-yyy` はIssue / PR / closeout証跡との対応維持のため変更しない。
- View向けの描画・操作実装は原則として View Track に置き、Simulation Phase では View が利用できる公開データ境界までを責務とする。

## World-scale Simulation の不変条件

- **Simulation FidelityはCamera距離・表示状態・都市/郊外/農村の区分で変更しない。**
- **CameraやRendering LODはSimulation結果へ影響しない。** 同一seed・初期状態・外部入力・経過時間なら、観測状態にかかわらず同一のauthoritative stateを得る。
- **負荷軽減はSimulationの省略ではなく不要な計算の排除で行う。** Event scheduling、dirty update、dependency tracking、spatial index、時刻からの派生値、deterministic parallelism等を使用する。
- **Global coarse fieldは生成・検索・indexの補助表現であり、詳細Simulationの代替正本にしない。**
- Viewへは公開された state / protocol / snapshot / query 境界を提供し、View固有の都合を authoritative Simulation state に持ち込まない。

## 依存順

```text
3D Simulation Foundation
  -> Urban World
  -> Road Network
  -> Routing
  -> Road Traffic
  -> Intersection / Signal
  -> Population / Daily Activity
  -> Pedestrian
  -> Railway Infrastructure
  -> Railway Operations
  -> Multimodal Transit
  -> Server Administration Console
  -> Industry / Jobs / Economy
  -> Logistics / Freight
  -> Power Infrastructure
  -> Water / Sewer Infrastructure
  -> Gas Infrastructure
  -> Optical Communication Infrastructure
  -> Remote Administration / MCP
  -> Radio / Spectrum Foundation
  -> World / Physical Environment Generation
  -> Regional / Urban Generation
  -> Persistent Regional / Settlement Evolution
  -> Simulation Scheduling / Workload Optimization
  -> Deterministic Parallel Simulation
  -> Historical World / Replay
```

## 詳細Taskの移行

Track分離前の Phase 25以降の詳細Task・完了条件・closeout証跡は、情報を失わないよう [`../docs/archive/roadmap-before-track-split.md`](../docs/archive/roadmap-before-track-split.md) に保存しています。

今後、各Phaseへ着手する際に必要なTaskをこのSimulation Roadmapへ移し、View固有Taskが混在している場合は View Roadmapへ切り出します。

### Phase 着手時テンプレート

```markdown
## Phase XX — Title

> **状態: ⏳ 待機**  
> **依存:** Phase XX / ...  
> Phaseの目的と責務。

- ⬜ **PXX-001** — Task
- ⬜ **PXX-002** — Task

### Phase XX 完了条件

- ⬜ 観測可能な完了条件

### Phase XX closeout evidence

- PR / merge commit
- CI / E2E / benchmark
- docs / ADR同期
```
