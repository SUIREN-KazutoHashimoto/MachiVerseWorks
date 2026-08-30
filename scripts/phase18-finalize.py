from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Expected exactly one {label} marker, found {count}.")
    return text.replace(old, new, 1)


roadmap_path = ROOT / "ROADMAP.md"
roadmap = roadmap_path.read_text(encoding="utf-8")
roadmap = replace_once(
    roadmap,
    "> **現在:** Phase 18 — Railway Operations（次）  \n> **次の実装タスク:** P18-001 — Train / Formation / Service / Timetableの責務とstable ID契約を仕様化する",
    "> **現在:** Phase 19 — Multimodal Transit（次）\n> **次の実装タスク:** P19-001 — Transit Stop / Line / Service pattern / Trip legの共通契約を仕様化する",
    "roadmap current phase",
)
roadmap = replace_once(roadmap, "| 18 | Railway Operations | ⏭️ 次 |", "| 18 | Railway Operations | ✅ 完了 |", "Phase 18 summary status")
roadmap = replace_once(roadmap, "| 19 | Multimodal Transit | ⏳ 待機 |", "| 19 | Multimodal Transit | ⏭️ 次 |", "Phase 19 summary status")
roadmap = replace_once(
    roadmap,
    "## Phase 9〜17 — 完了済みFoundation / Simulation Domains",
    "## Phase 9〜18 — 完了済みFoundation / Simulation Domains",
    "completed phase heading",
)
roadmap = replace_once(
    roadmap,
    "Phase 9〜17は正式closeout済み。現行ROADMAPでは完了履歴の詳細Taskを繰り返さず、実装・仕様・benchmarkの正本へ参照を集約する。",
    "Phase 9〜18は正式closeout済み。現行ROADMAPでは完了履歴の詳細Taskを繰り返さず、実装・仕様・benchmarkの正本へ参照を集約する。",
    "completed phase summary",
)
phase17_row = "| Phase 17 — Railway Infrastructure | [`docs/specifications/railway-infrastructure.md`](docs/specifications/railway-infrastructure.md)、[`docs/architecture/railway-infrastructure.md`](docs/architecture/railway-infrastructure.md)、Phase 17 E2E / Railway benchmark workflow、PR #78 |"
phase18_row = "| Phase 18 — Railway Operations | [`docs/specifications/railway-operations.md`](docs/specifications/railway-operations.md)、[`docs/architecture/railway-operations.md`](docs/architecture/railway-operations.md)、[`docs/development/railway-operations-benchmark.md`](docs/development/railway-operations-benchmark.md)、Phase 18 E2E / Railway Operations benchmark workflow、PR #131 |"
roadmap = replace_once(roadmap, phase17_row, phase17_row + "\n" + phase18_row, "Phase 17 evidence row")
roadmap = replace_once(roadmap, "> **状態: ⬜ 未着手（次）**  \n> **依存:** Phase 17", "> **状態: ✅ 完了**\n> **依存:** Phase 17", "Phase 18 section status")
for task in range(1, 17):
    old = f"- ⬜ **P18-{task:03d}**"
    new = f"- ✅ **P18-{task:03d}**"
    roadmap = replace_once(roadmap, old, new, f"P18-{task:03d} status")
closeout_marker = "- 同一seed / timetableで再現可能な運行結果を得られる。\n\n---\n\n## Phase 19 — Multimodal Transit"
closeout_block = """- 同一seed / timetableで再現可能な運行結果を得られる。

### Phase 18 closeout

- `Phase 18 Railway Operations E2E` run `33318887371`: 実Server→WebSocket→headless browserでProtocol 2.7をnegotiationし、2 Train / 2 Service / 2 Station / 2 Platformについて移動、Platform割当、dwell、delayを観測した。両Serviceは完了し、delayは276 / 717 tickだった。
- `Phase 18 Railway Operations Benchmark` run `33318887363`: 100 / 1,000 Train・Serviceのfixed tickとsnapshotをShortRunで計測した。基準値は [`docs/development/railway-operations-benchmark.md`](docs/development/railway-operations-benchmark.md) を正本とする。
- Phase 18のProtocol / Save / Web / E2E / benchmarkを含む最終検証はPR #131を統合単位とする。

---

## Phase 19 — Multimodal Transit"""
roadmap = replace_once(roadmap, closeout_marker, closeout_block, "Phase 18 closeout marker")
roadmap_path.write_text(roadmap, encoding="utf-8")

readme_path = ROOT / "docs/development/README.md"
readme = readme_path.read_text(encoding="utf-8")
pedestrian_line = "- [`pedestrian-benchmark.md`](pedestrian-benchmark.md): Phase 16 1,000 / 10,000 Pedestrian fixed-tick / routing benchmark"
railway_line = "- [`railway-operations-benchmark.md`](railway-operations-benchmark.md): Phase 18 100 / 1,000 Train・Service fixed-tick / snapshot benchmark baseline"
readme = replace_once(readme, pedestrian_line, pedestrian_line + "\n" + railway_line, "development benchmark index")
readme_path.write_text(readme, encoding="utf-8")

benchmark_path = ROOT / "docs/development/railway-operations-benchmark.md"
benchmark_path.write_text("""# Phase 18 Railway Operations Benchmark Baseline

Phase 18のTrain / Service運行処理について、fixed tick・route traversal / block arbitration・publish snapshotの継続的な回帰検知に使う基準値を記録する。

## 対象

Benchmark class: `RailwayOperationsBenchmarks`

fixtureは4 TrackSegment / 4 Block、2 Station / 2 Platform、origin / destination Depotを1本の連続RailwayRouteで接続する。1 Formation / 1 Timetableを共有し、`TrainCount`と同数のService / Trainを生成する。各Serviceはstable ID順にplanned start tickをずらし、測定前に60 tick進める。

測定method:

- `FixedTickOperations`: `SimulationWorld.Step()`を1 tick進める。Service activation、route上の3D移動、加減速、Block予約・所有権遷移、Platform look-ahead / 競合、dwell、delay、Depot lifecycleを含む。
- `CreateOperationsSnapshot`: Formation / Route / Timetable / Service / Trainを含むRailway Operations全体snapshotを生成する。
- `CreateTrainSnapshot`: publish用途のTrain mutable-state snapshotを生成する。

このfixtureは全Trainが同じRoute / Block列を共有するため、Block所有権競合と待機時のper-Train処理を継続的に含む。一方で、独立した多数路線やnetwork-wide journey planningを再現するものではない。

## Baseline

GitHub Actions `Phase 18 Railway Operations Benchmark` run `33318887363`、head commit `26e4321e39dec5f4a40fe1def79c5d04f1cf4809`で取得したShortRun baseline。

環境:

- Ubuntu 24.04.4 LTS
- AMD EPYC 9V74 2.60 GHz
- 2 physical / 4 logical cores
- .NET SDK 10.0.400
- .NET runtime 10.0.11
- BenchmarkDotNet 0.15.8
- ShortRun: 1 launch / 3 warmup / 3 measurement iterations

| Method | TrainCount | Mean | Allocated |
| --- | ---: | ---: | ---: |
| FixedTickOperations | 100 | 1.968 us | 64 B |
| CreateOperationsSnapshot | 100 | 9.852 us | 37,928 B |
| CreateTrainSnapshot | 100 | 3.134 us | 20,896 B |
| FixedTickOperations | 1,000 | 23.324 us | 64 B |
| CreateOperationsSnapshot | 1,000 | 99.089 us | 361,928 B |
| CreateTrainSnapshot | 1,000 | 30.663 us | 208,096 B |

## Interpretation

100→1,000 Trainで、full operations snapshotとTrain snapshotは概ねTrain数に比例して増加している。1,000 Train caseでもfixed tick平均は23.324 usで、30 Hzの1 tick budget 33.333 msに対して十分小さい値だった。

`FixedTickOperations`のmanaged allocationは両caseとも64 B / operationで、Train数に比例するtick allocationはこのbaselineでは観測されなかった。一方、snapshotは配列をmaterializeするため1,000 Trainでoperations snapshot約362 KB、Train snapshot約208 KBを割り当てる。publish頻度やvisible filteringを拡張するときはこのallocationを回帰監視対象とする。

この値は性能保証ではなくregression baselineである。GitHub-hosted runnerのhardware条件は変化し得るため、微小な単一run差よりも、allocationの構造的増加、桁違いのlatency悪化、Train数に対するスケーリング曲線の変化を重視する。

## Related E2E evidence

`Phase 18 Railway Operations E2E` run `33318887371`では、実Server→WebSocket→headless browserでProtocol 2.7をnegotiationし、2 Train / 2 Serviceについて3D移動、Platform assignment、dwell、delayを観測した。最終delayは276 / 717 tickで、両Service / TrainがCompletedとなりDepotへ戻ることを確認した。

## Automation

`.github/workflows/phase18-benchmark.yml`がSimulationまたはbenchmark変更時に`*RailwayOperationsBenchmarks*`をShortRunで実行し、BenchmarkDotNet artifactを14日保持する。`.github/workflows/phase18-e2e.yml`はServer→Browserの1運行周期を検証し、E2E artifactを7日保持する。
""", encoding="utf-8")

(ROOT / "VERSION").write_text("0.20.0\n", encoding="utf-8")

for relative in [
    ".github/workflows/phase18-patch.yml",
    "scripts/phase18-patch.py",
    "scripts/phase18-fix.py",
    "scripts/phase18-web-patch.py",
    "scripts/phase18-finalize.py",
]:
    (ROOT / relative).unlink(missing_ok=True)
