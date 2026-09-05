# View Legacy Visual Parity Roadmap

## Status

- **Priority:** P0 / View stream最優先
- **Tracking:** #465
- **Goal:** 新版Viewのユーザー向け視覚品質をLegacy版と同等以上にする
- **Parity gate:** VQ-7完了までViewのvisual foundationを完成扱いにしない
- **Execution:** Simulation / Gatewayの並行開発は止めない。View側ではVQ-0〜VQ-3を先行する

この文書は[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)のPhase番号を変更せず、Phase 5 / 6 / 11を横断して先行させるvisual quality trackの詳細計画を定義する。Viewのread-only責務、Simulation semantic source、Gateway delivery contractへの依存は既存Roadmapを維持する。

## 背景

現行ViewはSimulation → Gateway / Server → Viewのdelivery、Terrain / Structure / Road / Person / Vehicle / Railway等の描画pipeline、Visual RegressionのTechnical Goldenを持つ。一方、現在のRuntime Viewはユーザーが都市として認識・観測するpresentationとしてLegacy版を下回っている。

Technical Goldenが成功することは、必要なlayerやruntime integrationが壊れていないことの証跡であり、ユーザー向けvisual qualityの完成を意味しない。Legacy参照映像で確認できる都市密度、道路階層、立体交差、鉄道、交通活動、奥行き、広域から近景までのscale continuityを最低品質ラインとして別Gateを設ける。

## Legacy最低品質基準

Legacy parityでは少なくとも次を評価する。

1. **Urban silhouette / density** — 高密度中心市街地、低密度郊外、農村・小規模Settlementを視覚的に区別できる。
2. **Road hierarchy** — Local / Collector / Arterial / Highway等、authoritative classificationに基づく道路階層が読める。
3. **Civil structures** — Intersection、Bridge、高架、立体交差、Interchangeの上下関係が誤読されない。
4. **Railway presence** — Track / Station / Trainを都市構造と活動の一部として認識できる。
5. **City activity** — Vehicle / Pedestrian / Signal / Traffic FlowをDebug UIなしで認識できる。
6. **Environment readability** — Terrain / Water / Road / Buildingをlighting、shadow、fog / haze、materialによって識別できる。
7. **Scale continuity** — World overviewからSettlement / District / Street / Buildingへ寄っても主要構造が不自然に欠落しない。
8. **Presentation hierarchy** — Label / UI / diagnosticsが都市観測を妨げない。

Legacyの内部実装やassetをそのまま復元することは目的にしない。新版Architectureのread-only境界とauthoritative Observationを維持したまま、ユーザーが得る視覚情報量と可読性を同等以上にする。

## Golden policy

### Technical Golden

対象:

- renderer / layerの存在
- Simulation → Gateway / Server → View delivery
- runtime integration
- stable rendering contract
- 意図しないpixel regression

Technical Goldenは維持するが、これ単独ではLegacy parityを証明しない。

### User-facing Golden

対象:

- Debug UIなしのproduction View
- 固定World seed / observation
- 固定Camera preset
- 固定Browser / viewport / font / renderer環境
- Legacy比較用の代表scene

代表sceneは少なくとも次を持つ。

- World / regional overview
- Dense urban / skyline
- Road hierarchy / interchange
- Railway corridor
- Street activity

Pixel diffだけで品質を決めず、runtime structural assertionとreview checklistを組み合わせる。

## 実装Track

| Track | Issue | 内容 | 優先度 |
| --- | --- | --- | --- |
| VQ-0 | #466 | Legacy baseline / User-facing Golden | Immediate |
| VQ-1 | #467 | Environment / lighting / fog / terrain-water material | Immediate |
| VQ-2a | #468 | Road surface / hierarchy | Immediate |
| VQ-2b | #469 | Intersection / bridge / elevated / grade separation | Immediate |
| VQ-3a | #470 | Building appearance / height / use / skyline | Immediate |
| VQ-3b | #471 | Urban block / density / alignment / LOD / instancing | Immediate |
| VQ-4 | #472 | Railway / track / station / train | Parity |
| VQ-5 | #473 | Vehicle / pedestrian / signal / traffic activity | Parity |
| VQ-6a | #474 | Camera preset / wide-to-close scale continuity | Parity |
| VQ-6b | #475 | Label decluttering / UI hierarchy / Debug suppression | Parity |
| VQ-7 | #476 | Legacy parity User-facing Visual E2E Gate | Required gate |
| VQ-8 | #477 | Legacy+ presentation of new Simulation capabilities | After parity |

## VQ-0 — Legacy Baseline

Legacy参照を再現可能な比較条件へ落とし、Technical GoldenとUser-facing Goldenを分離する。

完了条件:

- Legacy比較用の代表Camera / sceneが定義される。
- User-facing screenshotを固定環境で再取得できる。
- Technical Golden成功をvisual parity成功として扱わないtest / docs境界がある。

## VQ-1 — Environment Foundation

空・背景、lighting、shadow、fog / haze、Terrain / Water materialをproduction表示として成立させ、黒い平面とdebug geometryに見える状態を解消する。

完了条件:

- Terrain / Water / Road / Buildingを即座に識別できる。
- 遠景にdepth cueがあり黒潰れしない。
- Legacy overviewと同等以上のenvironment readabilityを持つ。

## VQ-2 — Road & Civil Infrastructure

Road / Laneを幅のあるsurfaceとして描画し、道路階層、交差点、Bridge、高架、立体交差、Interchangeをproduction表現にする。

完了条件:

- Roadがdebug lineではなく路面として認識できる。
- overviewでも道路階層を読める。
- 上下関係や立体交差を誤読しない。
- Legacyのroad network / interchange readabilityと同等以上。

## VQ-3 — Urban Morphology

Buildingの高さ・用途・外観差、Urban block、道路との整列、密度差、Skyline、LOD / instancingを整え、都市を箱の集合ではなく都市景観として成立させる。

完了条件:

- 高密度中心部と低密度地域をLabelなしでも判別できる。
- 都市overviewでSkylineが成立する。
- Building LOD変更で都市構造が不自然に消失しない。
- Legacyの都市密度感と同等以上。

## VQ-4 — Railway & Transit

Railway geometry、Station、Trainを都市構造・活動の一部としてproduction表示する。

完了条件:

- overview / middle distanceの両方でRailway corridorとTrainを認識できる。
- Track / Station / Trainの関係と上下構造を誤読しない。
- Legacyのrailway presenceと同等以上。

## VQ-5 — Dynamic City Life

Vehicle / Pedestrian / Signal / Traffic Flowをauthoritative observationに基づいて視認可能にする。Interpolationはvisual smoothingだけに限定し、Client predictionを意味的stateとして扱わない。

完了条件:

- Debug overlayなしで交通・人流の存在を認識できる。
- observed stateと表示が一致する。
- Legacyのcity activityと同等以上の視認性を持つ。

## VQ-6 — Camera / LOD / UI

Legacy比較用Camera preset、Near / Mid / Farのtransition、Label hierarchy、collision回避、production UI、Debug suppressionを仕上げる。

完了条件:

- World overviewから道路付近まで連続して寄っても主要構造が破綻しない。
- Camera focusとGateway subscriptionがずれて空表示にならない。
- 通常起動ではDebug overlayが都市を覆わない。
- Labelが都市構造の可読性を壊さない。

## VQ-7 — Legacy Parity Gate

VQ-0〜VQ-6の代表sceneをUser-facing Visual E2Eへ固定し、Legacy未満の明確な視覚劣化をView closeout成功にしない。

完了条件:

- User-facing Visual E2EがTechnical Goldenと独立して実行される。
- Intentional visual changeのGolden更新にvisual reviewが必要である。
- 都市silhouette / density、road hierarchy、grade separation、railway、activity、environment depth、scale continuity、UI visibilityを確認できる。
- VQ-7通過前はView visual foundationを完成扱いにしない。

## VQ-8 — Legacy+

Parity達成後、新版Simulationが持つlogistics、utility、regional generation、outage / degradation等のauthoritative stateをLegacy以上のscale / detailで観測できるようにする。

Base city viewを常時overlayで埋めず、必要な情報はread-only optional layerとして分離する。分析値やsemantic eventをView側で生成しない。

## 優先実行順

### Wave 1 — Immediate

#466 → #467 → #468 / #470 → #469 / #471

VQ-0で評価基準を固定し、Environment / Road / Urban Morphologyを先に改善する。ここまでを現行Viewの第一印象をLegacy水準へ戻す最短経路とする。

### Wave 2 — Parity completion

#472 → #473 → #474 / #475 → #476

Railway / activity / Camera / UIを統合し、User-facing Gateを完成させる。

### Wave 3 — Legacy+

#477

Legacy parityを維持したまま新版Simulation固有の情報表現を追加する。

## 既存View Roadmapとの対応

このTrackは既存Phaseを置換しない。

- View Phase 5のRoad / Railway / Dynamic Entity / visual resolverをVQ-2 / VQ-4 / VQ-5でproduction qualityへ引き上げる。
- View Phase 6のCamera / LOD / label hierarchyをVQ-3 / VQ-6で先行統合する。
- View Phase 11のproduction model / material / lighting / presentationのうち、Legacy parityに必須な部分をVQ-1〜VQ-6へ前倒しする。
- View Phase 11以降でしか成立しない非必須presentationは既存Phaseに残す。

つまり、既存Phaseの責務や依存関係を壊さず、**Legacy parityに必要なproduction presentationだけをP0として先行する**。

## Completion policy

- VQ-0〜VQ-7はLegacy parity達成に必須。
- VQ-8はParity後の拡張でありVQ-7のblockerではない。
- View visual qualityに関する新規Taskは、Legacy parityを悪化させる場合このTrackを優先する。
- Simulation / Gatewayの機能開発を停止させるglobal blockerにはしない。
- `VERSION`はこのRoadmap追加では変更しない。
