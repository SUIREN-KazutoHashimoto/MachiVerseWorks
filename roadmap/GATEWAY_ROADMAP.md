# Gateway Roadmap

このファイルは、MachiVerseWorks の **Gateway側の実装ロードマップ**です。GatewayはSimulationとread-only consumerの間にあるServer側の観測境界として、Observation Request、subscription、detached read model delivery、cache、deduplication、Protocol adaptation、reconnect / resyncを担当します。

GatewayはSimulationの意味的正本ではありません。Activity、Status、分類、ETA、予定、semantic event、history等の意味はSimulation側で完結し、Gatewayはそれを生成・推測・補完・予測しません。

- authoritative World / rule / semantic state / source read model / command contractは[`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md)を正本とする。
- read-only rendering / Camera / Selection / Inspector / Historical presentationは[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)を正本とする。
- mutation / administration UIは[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)を正本とする。
- Gatewayのarchitecture詳細は[`../docs/architecture/observation-gateway.md`](../docs/architecture/observation-gateway.md)を正本とする。

Gateway Roadmapの分離は責務と進捗管理の分離であり、直ちに別process / repository / deploy unitへ分離することを意味しません。現行では`MachiVerseWorks.Server`内のObservation側責務を明確化し、将来必要なら独立deploy可能な境界へ育てます。

> **現在:** Gateway Phase 1 — Observation Boundary Foundation  
> **次の実装タスク:** `G1-001` — Observation Requestとauthoritative mutation commandの境界をProtocol / Server責務として固定する  
> **並行可能:** Simulation Phase 29 / View Phase 1

## 最上位原則

- **Gatewayはread-onlyである。** Gatewayからauthoritative Simulation stateを変更するAPIへ到達しない。
- **Simulationが唯一の意味的正本である。** Gatewayはsemantic stateを新規生成・推測・補完・予測しない。
- **Gatewayはdetached dataだけを配送する。** network処理がSimulation内部mutable Storeを直接所有・長時間参照しない。
- **cacheは意味を変えない。** cache hit / miss / rebuildで同一authoritative revisionなら同一Observation結果を返す。
- **freshnessはSimulation由来markerを優先する。** wall-clock TTLだけに依存せずtick / revision / generation等でstale判定する。
- **Viewの観測状態をSimulation workload policyへ逆流させない。** Camera / Selection / LOD / View接続数はSimulation fidelityやruleを変更しない。
- **Management command境界と分離する。** 同じServer processに実装されてもObservation routeへmutation commandを混在させない。
- **Protocol ownershipを分ける。** domainの意味・field定義はSimulation側source contractを正とし、GatewayはObservation control message、subscription、delivery envelope、serialization / negotiationを担当する。

## 全体の現在地

| Gateway Phase | 内容 | 主な依存 | 状態 |
| --- | --- | --- | --- |
| 1 | Observation Boundary Foundation | 現行SimulationRuntime / Server publish / Protocol 2.x | ▶️ 次 |
| 2 | Shared Observation Cache & Request Deduplication | Gateway Phase 1 | ⏳ 待機 |
| 3 | Subscription, Delivery & Resynchronization | Gateway Phase 1 / 2 | ⏳ 待機 |
| 4 | Generic Entity & Temporal Observation | Gateway Phase 1 / Simulation semantic observation | ⏳ Simulation依存待ち |
| 5 | Historical Observation & Replay Delivery | Gateway Phase 3 / Simulation Phase 35 | ⏳ Simulation依存待ち |
| 6 | Gateway Fidelity, Scalability & Closeout | Gateway Phase 1〜5 | ⏳ 待機 |

## 依存関係の読み方

Gateway Roadmapでは依存を次の3種類に分ける。

- **必須依存** — 対象Taskを正しく実装・完了するために必要なauthoritative contractまたはGateway内前提Task。
- **並行可能依存** — interface / transport / cache等を並行実装できるがintegration / closeoutまでに必要な依存。
- **統合依存** — View / Management等の利用側との最終統合に必要だが、Gateway基盤の着手を止めない依存。

GatewayはSimulation Phase番号へ同期しない。現在のServer / Protocolで成立するObservation境界やcache基盤はGateway Phase 1から独立して進め、World / Settlement / Historical / Planned等のdomain dataが必要なTaskだけ対応Simulation Phaseを待つ。

## Gateway Roadmap 運用ルール

- 状態記号を付けるのは単独で完了判定できるTaskだけとする。
- 未完了Taskは`⬜`、必要な検証まで済んだTaskは`✅`で表す。
- Server / Protocol / serialization / cache / deliveryの変更は、同一authoritative dataに対するequivalence testを持つ。
- Simulation側の意味的state追加が必要なら[`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md)へTaskを切り出し、Gatewayで代替計算しない。
- View側の描画・Selection・UI処理は[`VIEW_ROADMAP.md`](VIEW_ROADMAP.md)へ切り出す。
- mutation / administration command追加はSimulation側command contractと[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)へ切り出す。
- Protocol versionは互換性が変わる場合だけ更新し、application `VERSION`とは独立して管理する。
- cache / deduplication / encoded payload reuseは最適化であり、無効化しても正しいObservationを返せることを維持する。
- World replacement / reconnect / negotiated version変更時はClient-local delivery stateをauthoritative revisionへ再同期する。

## Simulation Roadmapからの移管対応

旧Simulation Roadmapの`Observation Gateway Foundation — Cross-cutting`を本Roadmapへ移管する。旧Task IDは履歴参照用として対応を残す。

| 旧Task | Gateway側の扱い |
| --- | --- |
| `OBS-001` | Gateway Phase 1 `G1-001` |
| `OBS-002` | Gateway Phase 1 `G1-002` + Gateway Phase 4 |
| `OBS-003` | Gateway Phase 1 `G1-003` |
| `OBS-004` | Gateway Phase 2 `G2-001`〜`G2-004` |
| `OBS-005` | Gateway Phase 2 `G2-005` |
| `OBS-006` | Gateway Phase 2 `G2-006` |
| `OBS-007` | Gateway Phase 2 / 3 `G2-004`, `G3-004`, `G3-005` |
| `OBS-008` | Gateway Phase 4 `G4-001`〜`G4-004` |
| `OBS-009` | Gateway Phase 6 `G6-001` / `G6-002` |
| `OBS-010` | Gateway Phase 2 `G2-008` + Gateway Phase 6 `G6-004` |
| `OBS-011` | 各Phaseのdocs同期 + Gateway Phase 6 `G6-006` |

---

## Gateway Phase 1 — Observation Boundary Foundation

> **状態: ▶️ 次**  
> **必須依存:** 現行SimulationRuntime / detached publish snapshot / Server WebSocket / Protocol 2.x  
> **並行可能依存:** Simulation Phase 29、View Phase 1

現行Serverに存在するpublish / subscription / inspection経路を、意味的処理を持たないread-only Observation Gatewayとして明示的に整理する。

- ⬜ **G1-001** — Observation Requestとauthoritative mutation commandをProtocol / Server責務として明示的に分離する（旧`OBS-001`）
- ⬜ **G1-002** — SimulationRuntimeからdetached observation sourceを取得する共通境界を定義し、Gatewayがmutable Storeへ直接依存しないようにする（旧`OBS-002`の基盤部分）
- ⬜ **G1-003** — 現行publish / `SubscribeVolume` / Inspect処理をServer内のObservation Gateway責務としてmodule境界へ整理する（旧`OBS-003`）
- ⬜ **G1-004** — Observation request / connection state / delivery stateとSimulation Entity stateのownershipを型・moduleで分離する
- ⬜ **G1-005** — negotiated Protocol versionごとのObservation message adaptationをGateway境界へ整理し、domain semanticsをcodec側で生成しない
- ⬜ **G1-006** — Gateway routeからAdmin / Management mutation APIへ到達できないdependency test / negative E2Eを追加する
- ⬜ **G1-007** — 現行Protocol互換のViewがGateway境界整理前後で同一Observationを受け取るregression testを追加する
- ⬜ **G1-008** — Gateway Phase 1のarchitecture / Protocol / Server README / Roadmapを同期する

### Gateway Phase 1 完了条件

- read-only Observation routeとauthoritative mutation routeがarchitecture / code dependency / Protocol上で識別できる。
- GatewayがSimulation mutable Storeを直接所有しない。
- 現行ViewのObservation機能を破壊せず境界を整理できる。
- Gatewayの存在・非存在を意味的計算条件としてSimulationへ持ち込まない。

---

## Gateway Phase 2 — Shared Observation Cache & Request Deduplication

> **状態: ⏳ 待機**  
> **必須依存:** Gateway Phase 1

同じauthoritative observationを複数Client / requestで再生成・再encodeし続けない共有cache基盤を作る。

- ⬜ **G2-001** — Entity Observation CacheをEntity ID + authoritative observation revisionで共有する（旧`OBS-004`）
- ⬜ **G2-002** — Spatial Observation Cacheをchunk / region + revisionで共有する（旧`OBS-004`）
- ⬜ **G2-003** — Terrain / Road / Railway / Building topology等のStatic Revision Cacheを共通化する（旧`OBS-004`）
- ⬜ **G2-004** — tick / revision / generationを使うcache invalidation / eviction contractを実装する（旧`OBS-004` / `OBS-007`）
- ⬜ **G2-005** — 同一revisionの同一Observation Requestを重複生成しないin-flight request deduplicationを実装する（旧`OBS-005`）
- ⬜ **G2-006** — negotiated Protocol version + observation revision単位の再利用可能encoded payload cacheを実装する（旧`OBS-006`）
- ⬜ **G2-007** — cache disabled / miss / hit / rebuild / dedup経路でpayload semanticsが一致するequivalence testを追加する
- ⬜ **G2-008** — cache hit率、CPU、allocation、encoding回数、memory budgetをbenchmarkし基準値を記録する（旧`OBS-010`）

### Gateway Phase 2 完了条件

- cache最適化の有無でObservation結果が変わらない。
- stale判定がwall-clock TTLだけに依存しない。
- 複数Viewerが同一World範囲を観測してもread-model生成とencodingを必要以上に重複しない。
- cacheがSimulationの意味的stateを新規生成しない。

---

## Gateway Phase 3 — Subscription, Delivery & Resynchronization

> **状態: ⏳ 待機**  
> **必須依存:** Gateway Phase 1  
> **並行可能依存:** Gateway Phase 2

Camera移動、slow client、reconnect、World replacementを含む長時間接続でもauthoritative observationへ収束する配送境界を完成させる。

- ⬜ **G3-001** — connectionごとのdesired subscription / committed delivery revisionを分離し、古いdelivery完了で新subscriptionを汚染しないstate modelを整理する
- ⬜ **G3-002** — static / dynamic / explicit inspectionごとのsnapshot / delta / chunk planning境界を共通化する
- ⬜ **G3-003** — slow clientをconnection単位のin-flight budget / timeout / cancellationで隔離する
- ⬜ **G3-004** — reconnect時にconnection-local delivery/cache stateを破棄し最新authoritative observationへresyncする（旧`OBS-007`）
- ⬜ **G3-005** — World load / replacement / topology revision変更時のcache / known-ID / delivery marker invalidationを統一する（旧`OBS-007`）
- ⬜ **G3-006** — negotiated minor version変更・旧Client接続で未対応messageを送らないcompatibility testを追加する
- ⬜ **G3-007** — subscriptionを高速に切り替えてもremove / static revision / inspect stateがeventually consistentになるE2Eを追加する
- ⬜ **G3-008** — 多数Viewer / 広範囲subscription / reconnect stormのServer負荷とfairnessを計測する

### Gateway Phase 3 完了条件

- Camera / subscription更新がSimulation stateへ影響しない。
- slow / reconnecting Clientが他ClientやSimulation tickへ無制限backpressureを波及させない。
- World replacement後に旧Worldのdelivery stateを正本として残さない。
- negotiated Protocol互換性を保ったまま再同期できる。

---

## Gateway Phase 4 — Generic Entity & Temporal Observation

> **状態: ⏳ Simulation依存待ち**  
> **必須依存:** Gateway Phase 1、対象domainのauthoritative semantic observation source  
> **統合依存:** View Phase 7 / 8

Selection / Inspectorから共通利用できるEntity observationを、Gateway側で意味を再計算せず配送する。

- ⬜ **G4-001** — Entity ID / Entity Typeを共通targetとするgeneric inspection request / response contractを設計する（旧`OBS-008`）
- ⬜ **G4-002** — Current state / RelationsをSimulation提供値のままgeneric inspectionへ載せる（旧`OBS-008`）
- ⬜ **G4-003** — bounded Recent Past / semantic eventをSimulationのhistory projectionから配信する（旧`OBS-008`）
- ⬜ **G4-004** — committed / scheduled Planned FutureをSimulation提供値のまま配信し、predictionと区別する（旧`OBS-008`）
- ⬜ **G4-005** — selected Entityだけ高詳細Observationへ昇格できるconnection-local subscriptionを実装する
- ⬜ **G4-006** — recent/planned payloadの件数・期間・payload size上限をProtocol互換で定義する
- ⬜ **G4-007** — Person / Building / Vehicle / Train等の複数domainでGatewayがActivity / ETA等を再計算していないことをcontract testする
- ⬜ **G4-008** — View Phase 7 / 8とのInspector / temporal observation E2Eを追加する

### Gateway Phase 4 完了条件

- Viewはgeneric Observation APIだけでCurrent / Recent / Planned / Relationsを表示できる。
- GatewayはRecent / Plannedの意味を生成せず、Simulation sourceを配送するだけである。
- predictionとauthoritative planned stateを混同しない。
- 選択中Entityの高詳細配信がWorld全体の高詳細転送を要求しない。

---

## Gateway Phase 5 — Historical Observation & Replay Delivery

> **状態: ⏳ Simulation依存待ち**  
> **必須依存:** Gateway Phase 3、Simulation Phase 35 Historical World & Replay  
> **統合依存:** View Phase 9

Simulationが所有するhistorical projection / replayをread-only consumerへ効率良く配送する。

- ⬜ **G5-001** — historical tick / timestamp / revisionを指定するread-only observation requestを設計する
- ⬜ **G5-002** — historical World範囲をCamera subscriptionと同様にspatially filterして配送する
- ⬜ **G5-003** — replay sequence / timeline scrub向けのbounded prefetch / cancellationを実装する
- ⬜ **G5-004** — historical selected EntityのInspectorをSimulation historical projectionから配信する
- ⬜ **G5-005** — live observationとhistorical observationのcache / revision namespaceを分離する
- ⬜ **G5-006** — timelineを高速scrubしても古いhistorical responseが最新selection / timeへ上書きされないordering testを追加する
- ⬜ **G5-007** — View Phase 9とのhistorical World / Inspector / replay E2Eを追加する

### Gateway Phase 5 完了条件

- ViewがSimulation内部history storeへ直接依存せず過去Worldを観測できる。
- Gatewayで歴史を再構成・推測せずSimulation historical projectionを配送する。
- live / historical requestが互いのcache / revision stateを汚染しない。

---

## Gateway Phase 6 — Gateway Fidelity, Scalability & Closeout

> **状態: ⏳ 待機**  
> **必須依存:** Gateway Phase 1〜5

Gateway境界がSimulation determinismとread-only invariantを壊さず、多数Clientでも運用可能であることを最終検証する。

- ⬜ **G6-001** — View未接続 / 単一View / 複数View / Camera・Selection差でSimulation state digestが一致するinvariance E2Eを追加する（旧`OBS-009`）
- ⬜ **G6-002** — cache enabled / disabled、subscription pattern、reconnect頻度を変えてもSimulation state digestが一致することを検証する（旧`OBS-009`）
- ⬜ **G6-003** — Gatewayからauthoritative mutation APIへ到達するrouteがないことをsecurity / dependency testで固定する
- ⬜ **G6-004** — cache / dedup / encoding / subscription / inspector / historical deliveryのCPU・allocation・memory benchmarkを統合する（旧`OBS-010`）
- ⬜ **G6-005** — 多数Viewer長時間接続でcache growth / delivery backlog / reconnect leakがboundedであることをsoak testする
- ⬜ **G6-006** — Gateway architecture / Protocol / Server README / 4 Roadmap依存 / ADRを最終同期する（旧`OBS-011`）

### Gateway Phase 6 完了条件

- Gatewayの観測負荷・cache状態・Viewer数でSimulation結果が変化しない。
- read-only routeとmutation routeの分離がE2E / dependency testで継続検証される。
- Gateway最適化を無効化しても正しいObservationへfallbackできる。
- CPU / allocation / memory / delivery backlogに回帰監視可能なbaselineがある。
- Simulation / Gateway / View / Managementの4 Roadmapで責務と依存が一意に追跡できる。
