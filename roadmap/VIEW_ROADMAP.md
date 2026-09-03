# View Roadmap

このファイルは、MachiVerseWorks の **View側の実装ロードマップ**です。ViewはSimulationが生成したWorldをGateway経由で忠実に観測・描画するための完全read-only clientとし、Simulation stateを変更する責務を持ちません。

- Simulation Core、authoritative World、Simulation rule、意味的state、予定、履歴、authoritative observation sourceは[`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md)で管理します。
- Observation Request、subscription、cache、deduplication、delivery、Protocol adaptation、reconnect / resyncは[`GATEWAY_ROADMAP.md`](GATEWAY_ROADMAP.md)で管理します。
- World / City / Serverを変更するeditor・運転control・Save / Load・configuration等は[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)で管理します。
- 人口統計、経済分析、交通分析、heatmap、trend等の分析処理はViewへ含めず、将来のAnalytics Listener / analysis clientとして別途設計します。
- Observation Gatewayのarchitectureは[`../docs/architecture/observation-gateway.md`](../docs/architecture/observation-gateway.md)を正本とします。

> **現在:** View Phase 5 — Infrastructure & Dynamic Entity Fidelity  
> **進め方:** View固有の基盤はPhase 1から進め、Simulationから移管された描画Taskは依存するSimulation semantic sourceとGateway delivery contractが実装できた時点で順次着手する

## 最上位原則

- **Viewは完全read-onlyである。** Viewからauthoritative Simulation stateを変更しない。
- **意味的処理はSimulationで完結する。** ViewはActivity、ETA、分類、予定、状態遷移、semantic event等を推測・補完・再計算しない。
- **Viewは受け取った意味を視覚・聴覚表現へ変換するだけである。**
- Viewが所有してよいのはCamera、Selection、renderer / audio resource、描画cache、LOD、interpolation、presentation設定等のClient-local stateだけとする。
- Viewの存在・非存在、接続数、Camera位置、Selection、描画FPS、Rendering LOD、View cache、quality profileによってSimulation結果が変化してはならない。
- ViewはSimulation内部Storeへ直接アクセスせず、Gateway / Protocolから提供されるread-only contractだけを使用する。
- `SubscribeVolume`、Inspect系request等は「何を見るか」を指定するObservation Requestであり、World mutation commandとは分離する。
- View moduleにManagement command clientを注入してmutation可能にしない。Management ClientがView componentを再利用する場合もcommand責務はManagement shell側に置く。
- **Simulation FidelityとRendering Fidelityを分離する。** 遠距離・非表示・低品質設定で描画を大胆に簡略化してよいが、背後のSimulation stateやruleを簡略化する理由にしない。
- **都市中心だけを特別扱いしない。** City / Town / Village / Hamlet、郊外、農村、遠隔集落を同じWorld上の観測対象として扱い、Camera原点や主要都市からの距離でView機能そのものを欠落させない。

## 全体の現在地

| View Phase | 内容 | 主な必須依存 | 状態 |
| --- | --- | --- | --- |
| 1 | Read-Only View Foundation | 現行read-only Protocol / Gateway Phase 1境界 | ✅ 完了 |
| 2 | Camera & Observation Navigation | Gateway subscription contract | ✅ 完了 |
| 3 | Physical World Rendering | Simulation Phase 29 source + Gateway delivery | ✅ 完了 |
| 4 | Settlement & Structure Rendering | Simulation Phase 30 baseline / Phase 31 evolution + Gateway delivery | ✅ 完了 |
| 5 | Infrastructure & Dynamic Entity Fidelity | 各Simulation domain source / Gateway delivery contract | ⏳ 実装待ち |
| 6 | Large World Rendering & Rendering LOD | Simulation Phase 29〜31 source / Gateway Phase 2〜3 | ⏳ Simulation / Gateway依存待ち |
| 7 | Object Selection & Inspector | Gateway Phase 4 Current / Relations | ⏳ Gateway依存待ち |
| 8 | Temporal Observation | Simulation semantic history / schedule + Gateway Phase 4 | ⏳ Simulation / Gateway依存待ち |
| 9 | Historical World View | Simulation Phase 35 + Gateway Phase 5 | ⏳ Simulation / Gateway依存待ち |
| 10 | Localization | stable observation / error contract | ⏳ 待機 |
| 11 | Production Visual & Audio Presentation | available authoritative presentation source | ⏳ View基盤待ち |
| 12 | View Addon & Customization | Simulation Phase 38 Extension Platform | ⏳ Simulation依存待ち |
| 13 | Fidelity, Accessibility & Performance Closeout | View Phase 1〜12 / Gateway Phase 6 integration | ⏳ 待機 |

## 依存関係の読み方

View Roadmapでは、依存を次の3種類に分ける。

- **必須依存** — 対象を正しく観測するauthoritative state / Gateway Observation contract、またはView内の前提Task。満たさない限りそのTaskを完了できない。
- **並行可能依存** — renderer / state boundary / Gateway境界等を並行実装できるが、integration / closeoutまでに合流が必要な依存。
- **統合依存** — 後から同じcomponentを組み合わせるための依存。対象Phaseの基礎実装開始を止めない。

Gateway Phase 1全体をView Phase 1の一括hard gateにはしない。現行read-only Protocolで成立するView-local基盤は先行でき、Gateway `G1-001` / `G1-003`の境界整理と並行して進める。generic InspectorはGateway Phase 4 `G4-001` / `G4-002`のCurrent / Relations契約、Temporal Observationは`G4-003` / `G4-004`のRecent / Planned契約と各Simulation domainのsemantic observation sourceが揃った時点でcloseoutできる。

Simulation Phase 32のSchedulerやPhase 33のparallelismはSimulation内部のworkload / execution戦略であり、authoritative sourceとGateway Observation contractが変わらない限りView Renderingの必須依存にしない。ViewはSimulation内部の計算方式やGateway内部のcache実装ではなく、公開されたread-only observationだけへ依存する。

## Simulation / Gateway Roadmap追従ルール

View RoadmapはSimulation / Gateway RoadmapとPhase番号を一致させない。View自身の技術的依存順でPhase 1から積み上げる。

一方、Simulationから移管されたTaskや特定domainの描画Taskには**必須となるSimulation Phase / semantic source**と、必要な場合は**Gateway Phase / delivery contract**を明示する。

実装可能条件は原則として次を満たすこととする。

1. 対象TaskのView側必須依存が完了している。
2. 対象を意味的に表現するSimulation側authoritative state / semantic observation sourceが実装済みである。
3. 対象をViewへ届けるGateway contractが必要な場合、そのdelivery contractが利用可能である。

Simulation側の依存が未完成ならViewが仮の意味を生成して先行実装しない。Gateway側の最適化が未完成でも正しいbaseline Observation contractがあるなら、geometry / renderer / View state等は先行してよい。

Simulation / Gateway Phaseがcloseoutした際は、対応する未着手View Taskが実装可能になったかを確認し、View Roadmapの状態を更新する。

## View Roadmap 運用ルール

- 状態記号を付けるのは、単独で完了判定できる作業だけとする。
- 未完了Taskは`⬜`、必要な検証まで済んだ完了Taskは`✅`で表す。
- 1 Taskは原則として1つの観測可能な成果を持つ。
- Rendering / Camera / Selection / Inspector / asset / audio / performance / accessibility / localization等を必要に応じて分割する。
- Simulation側の意味・source contract変更が必要なら[`SIMULATION_ROADMAP.md`](SIMULATION_ROADMAP.md)へ切り分ける。
- Observation Request / subscription / cache / delivery / reconnect等の変更が必要なら[`GATEWAY_ROADMAP.md`](GATEWAY_ROADMAP.md)へ切り分ける。
- mutation UIが必要なら[`MANAGEMENT_ROADMAP.md`](MANAGEMENT_ROADMAP.md)へ切り分ける。
- 分析・集計が必要ならView内へ実装せず、Analytics系の別境界として設計する。
- Browser確認、test、performance計測、docs同期まで含めて完了判定する。
- View Addonはread-only extensionに限定し、Simulation replacement / Management commandとは別契約にする。

## Simulation Roadmapからの移管対応

| 移管元 | View側の扱い |
| --- | --- |
| 旧`P29-026` | View Phase 3 `V3-001` |
| 旧`P30-028`のWeb Client 3D可視化部分 | View Phase 4 `V4-001` |
| 旧Phase 34 `P34-001`〜`P34-015` | 主にView Phase 6へ再整理 |
| 旧`P35-010` / `P35-015`のtimeline rendering部分 | View Phase 9 |
| 旧`P36-003` / `P36-004`のSelection / Inspector | View Phase 7 |
| 旧Phase 36のeditor / command / operation UI | Management Roadmapへ移管 |
| 旧`P36-016` Dashboard / statistics分析系 | Viewへ移管せず将来Analytics系へ分離 |
| 旧Phase 38 Localization `P38-010`〜`P38-015`等 | View Phase 10 |
| Phase 38 View extension / model / material / rendering layer関連 | View Phase 12 |

完了済みの`P25-014`、`P26-013`、`P28-016`等のdebug可視化は各Simulation PhaseのE2E / closeout証跡として履歴に残す。View Roadmapでは、それらをproduction Viewへ昇格する作業だけを必要に応じて新Task化する。

---

## View Phase 1 — Read-Only View Foundation

> **状態: ✅ 完了**  
> **必須依存:** 現行Server / read-only Protocol message flow  
> **並行可能依存:** Gateway Phase 1 `G1-001` / `G1-003` のObservation / mutation境界整理

Viewを完全read-onlyなPresentation clientとして固定し、Gatewayから受け取ったread modelだけで描画できる基盤を作る。

- ✅ **V1-001** — View / Gateway / Managementの責務境界と禁止事項をWeb Client architecture / module dependencyへ反映する
- ✅ **V1-002** — Protocol messageをView-local rendering stateへ一方向適用する共通state boundaryを整理する
- ✅ **V1-003** — View-local stateをCamera / Selection / rendering resource / audio resource / cache / interpolationへ限定する契約を実装・testする
- ✅ **V1-004** — authoritative observationとprevious/current visual interpolation stateを型・module境界で分離する
- ✅ **V1-005** — reconnect / resync時にconnection-local View stateを安全に破棄し、新authoritative observationから再構築する
- ✅ **V1-006** — Viewからmutation Protocol / Administration APIへ到達しないことをdependency / E2Eで検証する
- ✅ **V1-007** — View未接続 / 単一View / 複数View接続でSimulation state digestが一致する基礎E2Eを整備する
- ✅ **V1-008** — Read-Only View Foundationのarchitecture / test / Roadmapを同期する

### View Phase 1 完了条件

- ViewがGateway Observation contractだけから成立する。
- View codeからauthoritative mutation APIへ到達する経路がない。
- View接続数や描画状態がSimulation結果へ影響しない。
- Gatewayのcache最適化が未実装でも、正しいread-only observationからViewを構築できる。

---

## View Phase 2 — Camera & Observation Navigation

> **状態: ✅ 完了**  
> **必須依存:** View Phase 1 / Gateway Phase 1のObservation Request / subscription boundary  
> **並行可能依存:** Gateway Phase 2 / 3のcache / dedup / delivery / resync最適化

- ✅ **V2-001** — pan / zoom / rotate / altitudeを含むWorld navigationを整理する
- ✅ **V2-002** — View frustum / focus targetからread-only `SubscribeVolume`等のObservation Requestを生成する
- ✅ **V2-003** — ほぼ同一subscriptionの再送抑制とCamera移動時の安定した更新を実装する
- ✅ **V2-004** — Entity / Settlement / GeographicFeatureへのfocus / follow / jumpをView-local navigationとして実装する
- ✅ **V2-005** — World overviewから遠隔Settlementまで、原点からの距離に依存せず直接jump / focusできるnavigationを実装する
- ✅ **V2-006** — reconnect後に最新desired observationだけを再要求する
- ✅ **V2-007** — Camera操作・subscription変更でSimulation state digestが変化しないE2Eを追加する

### View Phase 2 完了条件

- 大規模WorldをCameraで自由に観測できる。
- 都市中心・郊外・農村・遠隔集落のどこへでも同じnavigation契約で移動できる。
- Observation RequestはGatewayの配送対象だけを変え、Simulation workload / fidelity / resultを変えない。

---

## View Phase 3 — Physical World Rendering

> **状態: ✅ 完了**  
> **必須依存:** View Phase 1 / 2、Simulation Phase 29 `P29-025`のWorld / Terrain / GeographicFeature / Toponym semantic source、対応Gateway delivery contract

- ✅ **V3-001** — flat `GridHelper`依存を置換し、Terrain / Water / GeographicFeature / 自然地名を3D描画する（旧`P29-026`）
- ✅ **V3-002** — Simulationから提供されたsurface / material / feature typeをView側で意味付けし直さずvisual resolverへmappingする
- ✅ **V3-003** — Cave / overhang / multi-surface等の3D terrain observationを表現できるgeometry境界を整える
- ✅ **V3-004** — coastline / river / lake / mountain / valley等をstable GeographicFeature observationから識別可能な表現へmappingする
- ✅ **V3-005** — Physical World RenderingのBrowser E2Eを追加する
- ✅ **V3-006** — Terrain renderingのframe time / draw call / memory baselineを記録する

### View Phase 3 完了条件

- Simulation Phase 29が公開しGatewayが配送した物理Worldをflat gridへ簡略化せず観測できる。
- GeographicFeatureや地名の意味をView独自ruleで生成しない。
- 都市外・未開発地域もWorldの一部として地形・水系・地理Featureを観測できる。

---

## View Phase 4 — Settlement & Structure Rendering

> **状態: ✅ 完了**  
> **必須依存:** View Phase 3、Simulation Phase 30 `P30-028`のSettlement / Parcel / Zone / naming baseline source、対応Gateway delivery contract  
> **統合依存:** Simulation Phase 31 Persistent Regional source（`V4-004` / `V4-006`の動的変化表示）

- ✅ **V4-001** — Settlement network / Parcel / Zone / development / urban naming / Road Signを3D可視化する（旧`P30-028`のView部分）
- ✅ **V4-002** — City / Town / Village / Hamlet等の分類はSimulation提供値だけを使用して表示する
- ✅ **V4-003** — Building / POI / Parcel / District / Settlement relationをstable ID参照に基づき表示する
- ✅ **V4-004** — Simulation Phase 31が公開する建設・用途変更・vacancy・demolition等のstate transitionを描画へ反映する
- ✅ **V4-005** — 高密度中心市街地、低密度郊外、農村、Village / Hamlet、遠隔集落のvisual representationを同じread model契約から成立させる
- ✅ **V4-006** — Simulation Phase 31の複数Settlementが連続市街地化・分離・成長・衰退してもView側で単一都市へ固定集約しないことをE2E確認する
- ✅ **V4-007** — Settlement / Structure rendering baselineを記録する
- ✅ **V4-008** — FHD Golden Imageを固定Browser / SwiftShader環境で比較するVisual Regression E2Eを整備し、構造・数値assertionと組み合わせて描画回帰を検出する

Phase 4 closeoutでは、checked-in rendering baselineに加えて、実ServerからPhase 30 `RegionalGenerationSnapshot`とPhase 31 `PersistentRegionalEvolutionSnapshot`を受信するBrowser E2Eでstable ID joinとThree.js描画を検証する。

### View Phase 4 完了条件

- Simulation Phase 30のbaseline Settlement / StructureをGateway経由で忠実に観測できる。
- Phase 31のPersistent Regional observationが利用可能な場合は、複数Settlementの成長・停滞・衰退・再成長を同じ表示契約へ反映できる。
- 人口や位置からViewが都市分類・土地利用・成長状態を推測しない。
- 一極集中を前提にせず、複数都市・町・村・集落が同じWorld内に並存する状態を視覚的に確認できる。

---

## View Phase 5 — Infrastructure & Dynamic Entity Fidelity

> **状態: ⏳ 実装待ち**  
> **必須依存:** View Phase 1、対象domainのSimulation semantic source / Gateway delivery contract  
> **統合依存:** View Phase 3 / 4（Terrain / Settlement上へ統合表示する場合）

- ⬜ **V5-001** — Road / Lane / Intersectionをproduction View representationへ整理する
- ⬜ **V5-002** — Railway Infrastructure / Train / Serviceをauthoritative geometry / stateに忠実な表示へ整理する
- ⬜ **V5-003** — Person / Pedestrian / Vehicle / Bus / Taxi / Freightのdynamic renderingを統一する
- ⬜ **V5-004** — Power / Water / Sewer / Gas / Optical / Radio等を観測可能なView layerとして整理する
- ⬜ **V5-005** — snapshot間interpolation / animationをvisual smoothingだけに限定し、Client predictionを意味的stateとして扱わない
- ⬜ **V5-006** — Entity kind / visual ID / stateからmodel・material・icon等へ解決するView-local visual resolver境界を実装する
- ⬜ **V5-007** — assetが不足・未対応でもstable fallback representationでEntityを観測可能にする
- ⬜ **V5-008** — spawn / update / removeとstatic revision更新のdomain横断整合性E2Eを追加する

### View Phase 5 完了条件

- 主要Simulation Entityを一貫したstable ID / Gateway observation contractから表示できる。
- debug proxyがproduction Viewで誤った意味を表す場合は、Simulation semantic sourceまたはvisual resolverを明示して解消する。
- asset不足がEntity消失やSimulation意味の捏造につながらない。

---

## View Phase 6 — Large World Rendering & Rendering LOD

> **状態: ⏳ Simulation / Gateway依存待ち**  
> **必須依存:** View Phase 3〜5、Simulation Phase 29〜31のWorld / Settlement sourceとstable coordinate contract、Gateway Phase 2〜3の共有delivery / resync基盤  
> **統合依存:** Simulation Phase 32 / 33とのinvariance / performance回帰確認。Scheduler / worker / partition実装そのものには依存しない

旧Phase 34のWorld Rendering / Rendering LOD計画を、read-only原則と複数Settlementを持つ巨大World前提に合わせて再構成する。

- ⬜ **V6-001** — Simulation observation / Rendering state / Camera state / LODの一方向契約を固定する（旧`P34-001`）
- ⬜ **V6-002** — World / Region / Settlement / District / Street / Buildingを連続ズームできるCamera / coordinate strategyを実装する（旧`P34-002`）
- ⬜ **V6-003** — Terrain / Water / GeographicFeatureのdistance-based mesh LOD / cullingを実装する（旧`P34-003`）
- ⬜ **V6-004** — 遠距離SettlementをSimulation提供の観測値から意味を変えない簡略representationで描画する（旧`P34-004`）
- ⬜ **V6-005** — Road / Railway / Utility corridorのscale別geometry representationを実装する（旧`P34-005`）
- ⬜ **V6-006** — Buildingのindividual / block mass / footprint rendering LODを実装する（旧`P34-006`）
- ⬜ **V6-007** — Person / Vehicle等のfrustum / distance culling・instancing・visual virtualizationを実装する（旧`P34-007`）
- ⬜ **V6-008** — View data streaming / cache / evictionを実装し、Simulation Entity lifecycleと独立させる（旧`P34-008`）
- ⬜ **V6-009** — Settlement / District / GeographicFeature / Road等のlabel hierarchy / collision回避を実装する（旧`P34-009`）
- ⬜ **V6-010** — World scaleから単一Buildingまでselection精度を維持するfloating-origin等のprecision対策を実装する（旧`P34-012`）
- ⬜ **V6-011** — Camera中心から遠いRegionでも同じ座標精度・selection・streaming契約を維持する
- ⬜ **V6-012** — 都市中心→郊外→農村→遠隔集落を移動した際、LOD境界・chunk境界で地形やInfrastructureが不自然に欠落しないtransitionを実装する
- ⬜ **V6-013** — Camera / LOD / View cache変更時もSimulation state digestが一致するE2Eを追加する（旧`P34-013`）
- ⬜ **V6-014** — Simulation Phase 32 / 33導入前後でも同一Observation contractを同じView pipelineで表示できるregression testを追加する
- ⬜ **V6-015** — 都市中心・郊外・農村・遠隔集落・World overviewのframe time / draw call / memory benchmarkを記録する（旧`P34-014`）
- ⬜ **V6-016** — World Rendering / LOD architecture / guideline / Roadmapを同期する（旧`P34-015`）

旧`P34-010` / `P34-011`のPopulation / economy / influence / catchment等の分析overlayはViewから除外する。Simulationが直接持つ状態をそのまま表示するvisual layerが必要になった場合だけ、意味的処理なしのTaskとして再追加する。

### View Phase 6 完了条件

- 巨大Worldを広域から個別Entityまで連続して観測できる。
- 複数都市、郊外、農村、Village / Hamlet、遠隔集落を同じWorld View上で移動・比較できる。
- View側LOD / culling / cacheとGateway側delivery / cacheがSimulation結果やEntity lifecycleへ影響しない。
- 遠距離地域の描画負荷を下げても、その地域のSimulation精度が下がったことを意味しない。
- Simulation Scheduler / parallel worker / partition構成が変化しても、公開Observation contractが同じならView実装を分岐させない。

---

## View Phase 7 — Object Selection & Inspector

> **状態: ⏳ Gateway依存待ち**  
> **必須依存:** View Phase 1 / 2、Gateway Phase 4 `G4-001` / `G4-002` のgeneric inspection Current / Relations契約  
> **統合依存:** View Phase 3〜6の対象renderer（3D pickingへ利用）

- ⬜ **V7-001** — Map / 3D Entityを選択するpicking / selection基盤を実装する（旧`P36-003`）
- ⬜ **V7-002** — Entity kindに依存しすぎない共通Inspector shellを実装する（旧`P36-004`）
- ⬜ **V7-003** — Region / Settlement / Building / Parcel / POI / Person / Vehicle / Infrastructure / GeographicFeature等のCurrent stateを表示する
- ⬜ **V7-004** — related Entityをstable ID relationから辿るnavigationを実装する
- ⬜ **V7-005** — focus / follow targetとSelectionをView-local stateとして管理する
- ⬜ **V7-006** — World overview / dense urban / rural / remote areaの各scaleでselection精度を検証する
- ⬜ **V7-007** — Inspector request / clear / reconnect / missing EntityのBrowser E2Eを追加する

### View Phase 7 完了条件

- 主要Objectを選択してauthoritative Current stateを詳細表示できる。
- InspectorはWorldを変更する操作を提供しない。
- Entityの存在場所やSettlement規模によらず同じSelection / Inspector契約を使える。

---

## View Phase 8 — Temporal Observation

> **状態: ⏳ Simulation / Gateway依存待ち**  
> **必須依存:** View Phase 7、Gateway Phase 4 `G4-003` / `G4-004`のRecent Past / Planned Future契約、対象Simulation domainが公開するsemantic event / schedule / planned state

- ⬜ **V8-001** — InspectorにCurrent / Recent Past / Planned Futureの3軸を持つ共通表示contractを実装する
- ⬜ **V8-002** — Recent PastはSimulationが公開しGatewayが配送したstate / eventだけを表示する
- ⬜ **V8-003** — Planned FutureはSimulationが公開しGatewayが配送したschedule / planned action / estimated valueだけを表示する
- ⬜ **V8-004** — View側でposition差分等からsemantic eventを生成しないことをtestする
- ⬜ **V8-005** — trajectory等の純粋なvisual historyをsemantic historyと区別して表示する
- ⬜ **V8-006** — Person / Vehicle / Train / Building / Settlement等の代表Entityでtemporal Inspector E2Eを追加する

### View Phase 8 完了条件

- 選択Objectについて現在、少し過去、予定を観測できる。
- 過去・予定の意味をView / Gatewayが生成しない。

---

## View Phase 9 — Historical World View

> **状態: ⏳ Simulation / Gateway依存待ち**  
> **必須依存:** View Phase 6〜8、Simulation Phase 35のHistorical read-only projection、Gateway Phase 5 Historical delivery

- ⬜ **V9-001** — World timeline / time sliderからHistorical read-only projectionを選択できるようにする（旧`P35-010`）
- ⬜ **V9-002** — Historical projectionをlive Viewと同じrendering pipelineへ適用する
- ⬜ **V9-003** — 過去時点のEntity Selection / Inspectorを実装する
- ⬜ **V9-004** — Settlement成立・成長・衰退・再成長やNetwork変化を時点切替で観測できるようにする
- ⬜ **V9-005** — timeline操作中もlive Simulationを停止・巻き戻し・変更しないE2Eを追加する
- ⬜ **V9-006** — 100年以上のtimeline rendering / cache / memory benchmarkを記録する（旧`P35-015`のView部分）

### View Phase 9 完了条件

- Gatewayを通じて指定時点のWorldを観測し、その時点のObjectを選択・詳細表示できる。
- 複数SettlementやInfrastructureの長期変化をWorld全体と局所の両scaleで追跡できる。
- Historical viewingがlive Simulationへ一切干渉しない。

---

## View Phase 10 — Localization

> **状態: ⏳ 待機**  
> **必須依存:** stable Gateway observation / error code / structured parameter contract、主要Inspector UI

- ⬜ **V10-001** — `ja-JP` defaultのlocale discovery / fallback policyを固定する（旧`P38-010`）
- ⬜ **V10-002** — 追加locale resource pack loading境界を実装する（旧`P38-011`）
- ⬜ **V10-003** — 数値・日時・単位・plural等のlocale formattingを共通化する（旧`P38-012`）
- ⬜ **V10-004** — stable code / structured parameterから表示文を生成するcoverageを拡張する（旧`P38-013`）
- ⬜ **V10-005** — translation key欠落・未使用key・parameter不一致をCIで検出する（旧`P38-014`）
- ⬜ **V10-006** — 追加localeで主要View / InspectorをE2E確認する（旧`P38-015`）
- ⬜ **V10-007** — localization resource loading / formattingのstartup / memory costをbenchmarkする

### View Phase 10 完了条件

- Protocol / Save / Simulationへ翻訳済み文言を持ち込まず、read-only Viewを複数localeで表示できる。
- Managementが同じClient resource / formatting基盤を再利用しても、Management固有key / TaskはManagement Roadmapで管理する。

---

## View Phase 11 — Production Visual & Audio Presentation

> **状態: ⏳ View基盤待ち**  
> **必須依存:** View Phase 5のvisual resolver / dynamic rendering基盤  
> **統合依存:** View Phase 3 / 4 / 6〜10、Simulationが公開しGatewayが配送するWorld time / environment / semantic event等のavailable presentation source

Debug proxy中心の表示から、街・地域・Infrastructure・移動Entityを長時間観測できるproduction presentationへ仕上げる。ここでも見た目のためにSimulation semanticsをView側で作らない。

- ⬜ **V11-001** — stable visual asset ID / Entity kind / explicit presentation metadataからmodel・material・icon・audio cueを解決するasset catalog / resolverを実装する
- ⬜ **V11-002** — Building / Infrastructure / Vehicle / Train / Pedestrian等をproduction model / materialへ置換し、未対応時のfallbackを定義する
- ⬜ **V11-003** — authoritative stateとsnapshot interpolationだけを入力に、vehicle wheel / pedestrian locomotion / door / signal等のvisual animationを構成する
- ⬜ **V11-004** — Simulation time / geographic environmentが公開する値からday / night・sun direction等を描画し、View独自のWorld時刻を持たない
- ⬜ **V11-005** — weather / atmosphere / cloud / fog等の意味がSimulationから提供される場合はその値を表示し、exposure / tone mapping等の純Presentation設定と区別する
- ⬜ **V11-006** — semantic event / stateを受信した場合だけparticle / highlight / notification等のvisual effectへ変換し、Viewがeventを推測生成しない
- ⬜ **V11-007** — 既存Web Audio foundationをproduction化し、Master / Music / UI / Ambient / World / Voice bus、positional cue、ambient zone、voice budgetをView-local presentationとして整理する
- ⬜ **V11-008** — audio asset pathをProtocolへ露出せず、stable cue IDからClient-local manifest / resolverで解決する
- ⬜ **V11-009** — World overview / urban / suburban / rural / remoteでlabel・icon・visual densityがscaleに応じて破綻しないpresentation hierarchyを仕上げる
- ⬜ **V11-010** — asset missing / Web Audio非対応 / reduced qualityでもWorld観測自体を継続できるgraceful fallback E2Eを追加する
- ⬜ **V11-011** — production asset / animation / lighting / audioを有効化したframe time / GPU memory / audio voice baselineを記録する

### View Phase 11 完了条件

- debug proxyだけではなく、都市・町・村・集落・Infrastructure・移動Entityを意味の分かるproduction representationで観測できる。
- 昼夜・天候・animation・effect・audioを追加してもSimulation stateや意味をView側で作らない。
- assetや音声機能の一部が利用不能でもread-only観測機能は維持される。

---

## View Phase 12 — View Addon & Customization

> **状態: ⏳ Simulation依存待ち**  
> **必須依存:** Simulation Phase 38 Extension Platformのpackage / lifecycle / conflict / public extension contract、View Phase 5 / 7 / 10 / 11  
> **統合依存:** Management Phase 5（install / enable / trust / conflict操作UI）

model差し替え、material変更、描画layer追加等をCore Viewの改造なしで行えるread-only View Extension境界を作る。Addonのinstall / enable / disable / trust / conflict操作UIはManagement Roadmap Phase 5の責務とする。

- ⬜ **V12-001** — Core View内部実装を直接参照しないversioned View Extension API / capability contractを定義する
- ⬜ **V12-002** — model / material / icon / audio cue resolverをView Addonから追加・overrideできるextension pointを実装する
- ⬜ **V12-003** — read-only rendering layer / map layer / label layerを追加できるextension pointを実装する
- ⬜ **V12-004** — Inspector section / read-only panelを追加できるextension pointを実装する
- ⬜ **V12-005** — theme / presentation resource / locale resource contributionをCore resource namespaceと衝突しない形で追加できるようにする
- ⬜ **V12-006** — View AddonへGateway Observation contractだけを公開し、Simulation mutation API / Management command client / mutable internal storeへ到達させない
- ⬜ **V12-007** — Extension Platformが解決したload order / provider selection / conflict resultをViewが受け取り、View側で独自の競合ruleを再実装しない
- ⬜ **V12-008** — Addon disable / update / reload時にaddon-owned renderer / audio / cache resourceを安全に破棄・再構築する
- ⬜ **V12-009** — model差し替えとread-only layer追加を行う公式sample View Addonを追加する
- ⬜ **V12-010** — View Addon有無・組合せ・load order差でSimulation state digestが一致するE2Eを追加する
- ⬜ **V12-011** — View Addon author guide / compatibility policy / performance guidelineを整備する

### View Phase 12 完了条件

- 車両・Building等のmodel / materialやread-only layerをCore変更なしで差し替え・追加できる。
- View AddonはSimulation semanticsやauthoritative stateを変更できない。
- Addon管理操作とread-only extension実行責務がManagement / Viewで分離されている。
- Management Clientが未実装でもView Extension API自体をcontract / unit testできる。

---

## View Phase 13 — Fidelity, Accessibility & Performance Closeout

> **状態: ⏳ 待機**  
> **必須依存:** View Phase 1〜12  
> **統合依存:** Gateway Phase 6のinvariance / scalability closeout

- ⬜ **V13-001** — Simulation observationとView表示のmissing / stale / wrong-revision検出testを整備する
- ⬜ **V13-002** — long-running Viewでspawn / remove / reconnect / cache eviction / historical switching / addon reload後の整合性を検証する
- ⬜ **V13-003** — large World / large Entity countでframe time / GPU・CPU memory / draw call / decode cost / audio voice costを継続計測する
- ⬜ **V13-004** — low / standard / high rendering quality profileを意味的stateを変えずに定義する
- ⬜ **V13-005** — device pixel ratio / resolution / GPU capability差に応じたView-local quality fallbackを実装する
- ⬜ **V13-006** — UI scale / keyboard navigation / focus visibility / contrast / color-only information回避等のaccessibility基盤を実装する
- ⬜ **V13-007** — reduced motion / animation intensity等のaccessibility設定をSimulation stateと独立したPresentation設定として実装する
- ⬜ **V13-008** — WebGL context loss / renderer resource再生成 / audio interruption等からread-only Viewを復旧できるrobustness testを追加する
- ⬜ **V13-009** — View無効 / quality差 / FPS差 / Camera差 / Client数差 / Addon差でSimulation state digestが一致する最終E2Eを追加する
- ⬜ **V13-010** — 都市中心・郊外・農村・遠隔集落・World overview・Historical Viewを含むproduction performance baselineを記録する
- ⬜ **V13-011** — View production architecture / accessibility / performance / addon guideline / Roadmapをcloseout同期する

### View Phase 13 完了条件

- 表示品質・端末性能・アクセシビリティ設定を変えても同じauthoritative Worldを意味的に同一として観測できる。
- 長時間・大規模WorldでもView cacheやGateway delivery stateがSimulationと混同されない。
- World overviewから遠隔集落、個別Buildingまで一貫して観測・選択・詳細確認できる。
- Core ViewとView Addonの双方がread-only境界を維持する。

---

## 継続Backlog

- VR / AR等の追加read-only presentation client
- cinematic camera / screenshot / video capture等の観測専用tool
- 高度なrenderer backend差し替えや将来WebGPU移行

Management操作やAnalytics処理はこのBacklogへ入れない。Simulation semanticsを必要とする新しい表示案が生じた場合はSimulation Roadmapへ、Observation Request / subscription / delivery変更が必要ならGateway Roadmapへ切り分け、View側だけで推測実装しない。