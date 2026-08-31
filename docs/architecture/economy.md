# Economy Architecture

Phase 21 の Industry / Jobs / Economy は、Population / Urban World / Multimodal Transit の既存正本を再利用しながら、Company・Establishment・Job・Employment・Household economy の状態を Simulation Core に保持する。

## Ownership

- `SimulationWorld` が economy state の authoritative owner であり、固定 tick の一部として economic cycle を進める。
- Company / Establishment / Job は stable ID を持ち、Establishment は既存 Building / POI を参照する。
- Employment は Person と Job の関連として保持し、Person / Household の正本そのものは Population domain に残す。
- Household cash / income / spending と Company cash / revenue / expense / production は raw numeric state として保持し、表示文言を Simulation / Save / Protocol に持ち込まない。

## Tick boundary

`SimulationWorld.Step()` では次の順で economy と既存 domain を接続する。

1. Simulation time を次 tick へ進める。
2. Agent state を更新する。
3. `StepEconomy(nextTime)` で production・wage payment・household consumption を deterministic に更新する。
4. `PlanPopulationAndEconomyTrips(nextTime)` で residence / workplace と economic activity から Trip 需要を計画する。
5. Road Traffic / Railway / Pedestrian / Multimodal Transit を既存順序で更新する。
6. 完了した Population Trip を反映し、Simulation time を確定する。

Economy は独自の移動実装を持たず、通勤・消費に必要な移動は Population / Trip planner と既存 transport domain へ委譲する。

## Persistence

Economy state は Simulation checkpoint に含め、Save Format 11 で永続化する。stable ID と raw value を保存し、locale 依存の文字列は保存しない。旧 Save format は economy state を持たない前提で既存互換性境界に従って復元する。

## Protocol / Server

Protocol 2.10 の `EconomySnapshot` を negotiated client に配信する。snapshot は aggregate statistics に加え、debug 用に bounded な Company / Household detail を含む。Server は Simulation の正本を複製せず、publish 時点の read model を生成する。

## Web Client

Web Client は binary `EconomySnapshot` を decode し、aggregate statistics と Company / Household detail を Economy Debug UI に表示する。表示用文言は Client 側に閉じ、Protocol payload は stable ID と raw value のまま扱う。

## Performance boundary

Economy benchmark は同一 fixture で次を個別に計測する。

- `SimulationWorld.Step()` に含まれる economy tick hot path
- `CreateEconomyStatistics()` の集計
- `CreateEconomySnapshot()` の publish snapshot 構築

`MemoryDiagnoser` と 100 / 1,000 Household の parameter を用い、今後の economy tick・planner・snapshot の回帰 baseline とする。

## Related documents

- [`../specifications/economy.md`](../specifications/economy.md)
- [`population-daily-activity.md`](population-daily-activity.md)
- [`multimodal-transit.md`](multimodal-transit.md)
- [`persistence.md`](persistence.md)
- [`protocol.md`](protocol.md)
