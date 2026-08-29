# MachiVerseWorks Roadmap

MachiVerseWorks の作業を、**実際に完了判定できる小さな Task** に分けて管理します。

> **現在:** Phase 10 — Building / POI Data Model Foundation（進行中）
> **次の実装タスク:** P10-002〜P10-006 — 実装済み変更のCI検証とcloseout

## 全体の現在地

| Phase | 内容 | 状態 |
| --- | --- | --- |
| 0 | リポジトリ初期セットアップ | ✅ 完了 |
| 1 | 開発プロジェクト骨格 | ✅ 完了 |
| 2 | Simulation Core 最小 PoC | ✅ 完了 |
| 3 | Protocol 最小実装 | ✅ 完了 |
| 4 | Headless Server 最小実装 | ✅ 完了 |
| 5 | Web Client 最小実装 | ✅ 完了 |
| 6 | End-to-End PoC | ✅ 完了 |
| 7 | 性能基盤の拡張 | ✅ 完了 |
| 8 | 保存・復元基盤 | ✅ 完了 |
| 9 | 3D Simulation Foundation | ✅ 完了 |
| 10 | Building / POI Data Model Foundation | ⬜ 進行中 |

Phase 0〜8の詳細TaskとPhase 9着手時点の計画状態は、履歴として [`docs/archive/roadmap-through-phase9-plan.md`](docs/archive/roadmap-through-phase9-plan.md) に保存しています。

## ROADMAP 運用ルール

- 状態記号を付けるのは、単独で完了判定できる作業だけとする。
- 1タスクは原則として「1つの観測可能な成果」を持つ。
- 1タスク内に独立した成果が複数ある場合は分割する。
- 大テーマは見出しまたは将来 Backlog として管理し、着手時に小Taskへ分解する。
- コード変更では、必要な build / test / benchmark / 実機確認まで含めて完了とする。
- 仕様や設計を変更した場合は、対応する docs / ADR の更新まで含めて完了とする。
- 「ほぼ完了」「一部完了」は ✅ にしない。残作業を別Taskへ明示的に切り出した場合のみ元Taskを完了にできる。
- 完了済みPhaseの詳細は必要に応じて `docs/archive/` へ移し、現行ROADMAPを次の判断に使いやすく保つ。

---

## Phase 10 — Building / POI Data Model Foundation（進行中）

> **状態: ⬜ 進行中**  
> Agent needs / schedule、交通、経済など後続Simulationが参照する都市オブジェクトとして、Building / POIのstable ID、native 3D state、参照整合性、checkpoint / Save Data基盤を確立する。

### 仕様・Simulation Core

- ✅ **P10-001** — Building / POIの責務、stable ID、3D state、参照整合性、非対象範囲を仕様として固定する
- ⬜ **P10-002** — `BuildingId` / `PoiId`、kind、snapshot、内部storeを実装し、単調増加IDとdetached snapshotを提供する
- ⬜ **P10-003** — POI→Building参照の存在・包含・削除整合性をcommand / checkpoint restoreで検証し、次IDを含むcheckpoint round-tripを実装する

### Save・検証

- ⬜ **P10-004** — Save format 3へ更新し、Building / POI state、次ID、参照、件数上限を保存・復元する
- ⬜ **P10-005** — Building / POI生成・削除・参照不整合・checkpoint / Save round-trip・resource limitの回帰testを追加する
- ⬜ **P10-006** — architecture / specification / ROADMAPを同期し、CI build / test成功を確認してPhase 10をcloseoutする

### Phase 10 の非対象

Phase 10ではBuilding / POIをSimulationの正本として安全に保持・保存できる基盤までを完成させ、以下は後続Taskへ分離する。

- Protocol / ServerによるBuilding / POI配信
- Web ClientでのBuilding / POI描画
- 建物mesh / floor / room / entrance / capacity
- Agent needs / schedule / householdと目的地選択
- Road / pedestrian / railway graphとの接続
- zoning / parcel / land use
- Build / edit commandとUI
- 名称・住所・追加locale
- 旧Save formatからSave format 3へのmigration

---

## Phase 9 — 3D Simulation Foundation（完了）

> **状態: ✅ 完了**  
> Simulation Worldの正本座標系をフルネイティブ3Dへ移行し、Simulation内部状態からProtocol・Server・Web Client・Audio・Save Dataまで高さ情報を欠落させない基盤を確立した。

### 座標契約・Simulation Core

- ✅ **P9-001** — 3D座標系の軸・単位・境界・rendererへの写像を仕様とADRで固定する
- ✅ **P9-002** — `WorldPoint` / `WorldVector` を3軸化し、全成分のfinite validationを実装する
- ✅ **P9-003** — `SpatialCell` / `SpatialGrid` を3次元cellへ拡張する
- ✅ **P9-004** — `WorldVolume`を導入し、`SpatialIndex`の登録・移動・volume queryを3D化する
- ✅ **P9-005** — `AgentStore` / `SimulationWorld` の生成・移動・tick更新を3軸状態へ移行する
- ✅ **P9-006** — snapshot / checkpointを3軸化し、determinism・境界条件・failure atomicityの回帰testを追加する

### Protocol・Server

- ✅ **P9-007** — Agent position / velocityとsubscription volumeを3軸wire contractへ更新し、Protocol 2.0へ上げる
- ✅ **P9-008** — Serverのsubscription state・snapshot取得・spawn/update配信で3D座標を欠落なく扱う

### Web Client・Audio

- ✅ **P9-009** — Web Client protocol decoder / EntityStore / interpolationを3軸状態へ移行する
- ✅ **P9-010** — Simulation座標をThree.js座標へ明示的に写像し、Agent高度とcamera由来3D subscription volumeを描画・配信へ反映する
- ✅ **P9-011** — positional audio / listener / Ambient Zoneを3D位置へ移行し、高度差を距離・位置判定へ反映する

### Save・性能・E2E

- ✅ **P9-012** — Save Dataを3軸stateへ更新し、Save format 2とsave/load round-trip testを更新する
- ✅ **P9-013** — 3D Spatial Index / tick / snapshot / Protocol benchmarkを更新し、3D化直前commitとの同一runner比較結果を[`docs/development/performance-benchmark.md`](docs/development/performance-benchmark.md)へ記録する
- ✅ **P9-014** — 同一水平位置・異高度Agentを実Server→Browser→`THREE.InstancedMesh`までE2E検証し、Save→Load→Protocol 2.0統合testでも高度保持を確認する
- ✅ **P9-015** — architecture / specification / ROADMAPと検証結果を同期し、Phase 9の完了条件を記録する

### Phase 9 closeout evidence

- Protocolは2D fallbackを持たない2.0 contract、Save Dataは3D必須のformat 2。
- Web Client subscriptionは固定高度bandを廃止し、OrthographicCameraのnear/farを含む8 frustum cornerから3D AABBを算出する。
- Server外部subscriptionはXYZ cell budgetで制限し、Simulation内部の巨大疎volume queryはoccupied-cell走査へadaptiveに切り替える。
- Browser E2Eはhelper値ではなく実`InstancedMesh` instance matrixの高度差を観測する。
- 3D化直前 `2ada7e8736c7d93038f3291fd7db154f58db09e0` とPhase 9 closeout候補を同一GitHub runnerで比較し、通常Spatial Query / Snapshot / Protocolはほぼ横ばい、100,000 Agent tick p99は1.3878msで30Hz budgetの約4.2%であることを記録した。
- PR #44のcloseout検証で CI、Dependency Review、Phase 6 E2E、Phase 7 benchmark、Phase 9 regression benchmarkが成功する構成を確認した。

### Phase 9 の非対象

Phase 9では3D座標を正本として扱える基盤までを完成させ、以下の具体的な物理・交通ルールは後続Taskへ分離する。

- 重力・ジャンプ・落下・飛行などの物理挙動
- terrain collision / ground snapping
- 道路・線路・建物ごとの高度制約
- 地下・高架を考慮したpathfindingルール
- 旧Save formatから新formatへのmigration

---

## 将来 Backlog

以下は**テーマ**であり、完了状態記号の対象ではありません。着手するときに、その時点の設計に合わせて上記と同程度の粒度へ分解します。

- Agent needs / schedule / household
- Road graph / lane model
- Pathfinding / route cache
- Road traffic simulation
- Intersection / signal control
- Pedestrian simulation
- Railway infrastructure
- Railway operation / timetable
- Bus / taxi / multimodal transit
- Logistics / freight
- Industry / jobs / economy
- Power generation / grid / demand
- City generation
- Zoning / land use
- Inspector / dashboard / statistics UI
- Build / edit commands
- Server configuration UI
- Save migration
- Release packaging
- Server binary distribution
- Web Client deployment
- Container image
- Mod / extension architecture
- Additional locales

### Backlog を着手可能にする条件

1. 現在の仕様・依存関係を確認する。
2. What / Why が必要なら `docs/specifications/` を作成・更新する。
3. How の重要判断が必要なら `docs/architecture/` / ADR を作成・更新する。
4. 1つずつ完了判定できるTaskへ分割する。
5. 最初の数項目だけを優先順に並べ、巨大な一括実装を始めない。
