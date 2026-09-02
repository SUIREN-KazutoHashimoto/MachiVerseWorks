# View Phase 4 Settlement & Structure Rendering baseline

View Phase 4 は、Simulation が公開する read-only regional observation をそのまま presentation state へ写像する。

- Simulation Phase 30 / Protocol 2.18 `RegionalGenerationSnapshot` — Settlement / District / Parcel / Building / POI / naming / Road Sign の baseline geometry と stable relation
- Simulation Phase 31 / Protocol 2.19 `PersistentRegionalEvolutionSnapshot` — Settlement classification / trend、Parcel development、Building lifecycle、regional relation / event / flow の persistent evolution state

View はこれらの semantic state を人口・jobs・密度・位置等から再計算しない。

## Read model boundary

### Protocol 2.18 baseline

Web Client は `RegionalGenerationSnapshot` から次を lossless に受け取る。

- Settlement / historical growth event
- Regional corridor
- District / Parcel / Zone / development state
- Generated Building / POI
- Human Toponym
- Road Sign
- Regional quality metadata

`ulong` stable ID は JavaScript `number` へ丸めず `bigint` として保持する。Settlement / District / Parcel / Building / POI / Toponym / Corridor / RoadSign は stable ID index を持ち、relation は配送済み ID をそのまま辿る。

`RegionalGenerationStore` は District→Settlement、Parcel→District / Settlement、Building→Parcel / District / Settlement、POI→Building / Settlement を authoritative stable ID だけで解決する。Renderer も Settlement / District / Parcel / Building / POI の relation metadata を描画 primitive と同じ revision で保持し、後続の Selection / Inspector が別の推測 index を作らなくてよい境界にする。

### Protocol 2.19 persistent evolution

`PersistentRegionalEvolutionStore` は Simulation Phase 31 の snapshot を別revisionとして保持する。

- Settlement: position / population / jobs / service / density / accessibility / influence radius / `SettlementScale` / `SettlementTrend` / active state / established year / dormant year
- Parcel: development demand / land value / development state / current Building relation
- Building: use / built year / last changed year / condition / occupancy / capacity / `BuildingLifecycleStatus`
- Service catchment / infrastructure demand
- Regional relation
- Regional evolution event
- Commuting / freight flow

Phase 30 geometry と Phase 31 evolution state は同じ stable ID で結合する。View は Phase 31 snapshot を受信しても別の都市モデルを生成せず、既存の Settlement / Parcel / Building presentation revisionへ authoritative evolution state を重ねる。

## Presentation mapping

`SettlementStructureRenderer` は Phase 30 baseline から次を描画する。

- corridor: authoritative `RegionalCorridorKind`
- settlement marker: authoritative `RegionalRole` と `influenceRadiusMeters`
- district outline: authoritative District bounds
- parcel: authoritative `ZoneKind` / `ParcelDevelopmentState`
- building: authoritative `GeneratedBuildingUse` / bounds
- POI: authoritative position
- human toponym: authoritative name / stable provenance ID
- road sign: authoritative position / kind / text / destination relation

Protocol 2.19 evolution が存在する場合は、同じ描画primitiveへ次を反映する。

- Settlement marker: authoritative `SettlementScale` / `SettlementTrend` / active state / current influence radius
- Parcel: authoritative current development state / demand / land value metadata
- Building: authoritative current use / condition / occupancy / lifecycle status
- Regional relation: authoritative relation kind / strength / active state / since year
- root revision metadata: authoritative `currentYear`

Building `Demolished` は既存stable ID relationを失わず、geometryをpresentation上で極小化してdemolition stateを識別できるようにする。`Vacant` / `Renovating` / `Repurposing` / `Abandoned` も lifecycle status をvisual mappingへ反映する。

## Classification rule

View は人口、jobs、位置、密度、影響半径等から City / Town / Village / Hamlet を推測しない。

Protocol 2.18 `ProtocolSettlement` 自体には City / Town / Village / Hamlet に相当する明示分類は存在しないが、Simulation Phase 31 / Protocol 2.19 は authoritative `SettlementScale` を公開する。

- `Hamlet`
- `Village`
- `Town`
- `City`
- `Metropolis`

View はこの値だけを都市規模分類として使用する。人口閾値等のView-side fallback ruleは実装しない。Protocol 2.19が未配送の2.18接続では、既存`RegionalRole`表現を維持し、City / Town / Village / Hamletという意味ラベルを捏造しない。

## Protocol negotiation

Web Client のcurrent protocolは2.19とする。

並行開発中の現行`develop` Serverは2.18であり、高いminorを自動downgradeせず `UnsupportedProtocolVersion` を返してconnectionをcloseする。このためWeb Clientは最初に2.19を要求し、同一majorのserver-provided `supportedVersion` が2.19未満なら一度だけそのminorへfallbackして再接続する。

これにより次を両立する。

- Simulation Phase 31 Serverでは2.19 Persistent Regional Evolutionを受信する
- 現行develop Serverでは2.18 Regional Generationまでの機能を維持する
- major mismatchやclientより新しいversionへはfallbackしない

## Current integration dependency

Simulation Phase 31 branchには次が実装されている。

- Protocol 2.19 `PersistentRegionalEvolutionSnapshot`
- `PersistentRegionalEvolutionMessageMapper`
- `IObservationSource.CapturePersistentRegionalEvolutionSnapshot`
- `PersistentRegionalEvolutionPublishService`

したがってPhase 31 evolution contractそのものはView側で実装・検証可能になった。

一方、Phase 30 baseline `RegionalGenerationSnapshot` については、現行Gatewayおよび確認したPhase 31 Server構成に専用のlive publish serviceが存在しない。Protocol serializer / mapperが存在しても、baseline District / Parcel / Building bounds等がWebへ自動配送される経路はまだ成立していない。

したがって次を分離する。

- View-local Browser baseline: Protocol 2.18 / 2.19と同じread model shapeを入力し、実ブラウザ/Three.js上でrenderer contractを検証する
- live Gateway integration: Simulationから実RegionalGeneration baseline + PersistentRegionalEvolutionをGateway経由でWeb Clientへ配送し、同じrendererへ適用する

前者を後者の代替やPhase全体の完了証跡として扱わない。

## Current Phase 4 task status

- `V4-001`: client-side 3D rendering baselineとBrowser-level rendering確認を実装。live RegionalGeneration delivery待ち。
- `V4-002`: Protocol 2.19 `SettlementScale` のHamlet / Village / Town / City / Metropolisをauthoritative値のまま表示するclient実装・testを追加。live integration待ち。
- `V4-003`: stable ID relation index、renderer relation metadata、unit / Browser-level検証を実装。live delivery経由の最終確認待ち。
- `V4-004`: Protocol 2.19 Parcel development / Building lifecycle / Settlement trendを既存geometryへ反映するclient実装・testを追加。live integration待ち。
- `V4-005`: heterogeneous multi-Settlement baselineに加え、CityとHamlet等のauthoritative `SettlementScale` を同一read model contractで同時表示するBrowser-level検証を追加。live integration待ち。
- `V4-006`: Phase 31 relation / trend / active stateを複数Settlementへ個別適用し、単一都市へ固定集約しないBrowser-level検証を追加。実Simulation→Gateway→Web E2E待ち。
- `V4-007`: 本文書とunit / Browser regressionでclient baselineを記録。Phase全体の最終closeoutはlive integration後に行う。

## Regression coverage

`src/web/tests/regional-generation-protocol.test.mjs`

- Protocol 2.18 gate
- 64-bit stable ID exact decode
- broken stable-ID relation rejection

`src/web/tests/regional-generation-store.test.mjs`

- Settlement / District / Parcel / Building / POI stable-ID traversal
- connection reset相当のclear

`src/web/tests/persistent-regional-evolution-protocol.test.mjs`

- Protocol 2.19 gate
- 64-bit stable ID exact decode
- authoritative SettlementScale / SettlementTrend / BuildingLifecycleStatus / RegionalRelationKind
- broken stable-ID relation rejection

`src/web/tests/persistent-regional-evolution-store.test.mjs`

- Parcel / Building / Settlement stable-ID traversal
- Relation / Event grouping
- snapshot replacementとclear

`src/web/tests/persistent-regional-evolution-renderer.test.mjs`

- Phase 30 geometryを維持したままPhase 31 revisionだけでSettlement scale / trendを更新
- Parcel redevelopment stateの反映
- Building demolished / active lifecycleの描画差分
- Regional relation stable-ID metadata

`src/web/tests/browser/view-phase04-e2e.mjs` と `scripts/run-view-phase04-e2e.sh`

- Settlement / Corridor / District / Parcel / Building / POI / Toponym / Road SignがThree.js sceneへ生成され、実draw callが発生する
- 離れた複数Settlementが1つへ集約されない
- City / Hamlet等のauthoritative `SettlementScale` とtrend / active stateを保持する
- Parcel→District / Settlement、Building→Parcel / District / Settlement、POI→Building等のstable ID relationを`bigint`のまま保持する
- Building demolitionとregional relationを実ブラウザ上で反映する
- browser canvas上でhuman Toponym spriteを生成できる

このBrowser E2EはView-local rendering boundaryの証跡であり、Simulation→Gateway→Webのlive Regional Generation deliveryを証明するものではない。

## Known upstream contract issue

Issue #298 に記録済みのとおり、Simulation の `RoadSignKind.RockSlope = 9` に対して現行 C# `RegionalGenerationProtocolCodec` が `Kind > 8` を拒否する不整合がある。Web decoder は authoritative Simulation enum に合わせ 0〜9 を受理する。Server 側不整合の修正は View で意味を変換して回避しない。

## Version handling

View Phase 4 はWeb Clientのcurrent negotiated Protocolを2.19へ進める。Protocol 2.18 Serverへのminor fallbackはconnection-local compatibilityであり、semantic stateをView側で補完するものではない。

アプリケーションrelease versionを示すルート `VERSION` はこの実装では変更しない。
