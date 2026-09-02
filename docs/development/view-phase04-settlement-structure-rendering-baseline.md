# View Phase 4 Settlement & Structure Rendering baseline

View Phase 4 の client-side baseline は、Simulation Phase 30 が Protocol 2.18 で公開する `RegionalGenerationSnapshot` を唯一の semantic source として扱う。

## Read model boundary

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

## Presentation mapping

`SettlementStructureRenderer` は同一 read model から次を描画する。

- corridor: authoritative `RegionalCorridorKind`
- settlement marker: authoritative `RegionalRole` と `influenceRadiusMeters`
- district outline: authoritative District bounds
- parcel: authoritative `ZoneKind` / `ParcelDevelopmentState`
- building: authoritative `GeneratedBuildingUse` / bounds
- POI: authoritative position
- human toponym: authoritative name / stable provenance ID
- road sign: authoritative position / kind / text / destination relation

Settlement marker の大きさ等は presentation-only な visual mapping であり、都市分類を生成しない。高密度な中心Settlement、低密度なSettlement、農業系Settlement等も別のView modelへ分岐せず、同じ snapshot / renderer contract内で個別のstable IDを保ったまま並存させる。

## Classification rule

View は人口、jobs、位置、密度、影響半径等から City / Town / Village / Hamlet を推測しない。

現行 Protocol 2.18 `ProtocolSettlement` には Settlement environment / origin / role / economy は存在するが、City / Town / Village / Hamlet に相当する明示的 semantic classification は存在しない。その値が Simulation observation として追加されるまでは View 側へ代替 rule を実装しない。

このため `V4-002` は upstream semantic classification待ちである。また `V4-005` に含まれる Village / Hamlet を意味的に区別した最終visual representationも同じauthoritative classification待ちとする。提供済みrole / zone / building bounds等だけで、性質の異なる複数Settlementを単一read modelから同時表示するView-local baselineまでは先行実装する。

## Current integration dependency

Phase 30 baseline には `RegionalGenerationMessageMapper` と Protocol 2.18 codec が存在する。しかし現行 Gateway の `IObservationSource` には Regional Generation snapshot のcapture契約がなく、`AddObservationGateway` にも Regional Generation publish service が登録されていない。`ObservationProtocolAdapter` のserialize対応だけではWeb Clientまでsnapshotは到達しない。

したがって次を明確に分離する。

- View-local Browser baseline: authoritative Protocol 2.18 と同じread model shapeを入力し、実ブラウザ/Three.js上でrenderer contractを検証する。
- live Gateway integration: Simulationからcaptureした実snapshotをGateway経由でWeb Clientへ配送し、同じrendererへ適用する。Gateway delivery contract実装後に追加する。

前者のfixtureを後者の代替やPhase完了証跡として扱わない。

Simulation Phase 31 の建設、用途変更、vacancy、demolition、Settlement の成長・停滞・衰退も同様に、authoritative observation が公開されるまで View 側で推測しない。Store は snapshot replacement ごとに同じ renderer contract を更新できるため、Phase 31 source は別の都市 model を作らずこの境界へ統合する。

## Current Phase 4 task status

- `V4-001`: client-side 3D rendering baselineとBrowser-level rendering確認を実装。live Gateway integrationはdelivery contract待ち。
- `V4-002`: City / Town / Village / Hamlet authoritative classification待ち。
- `V4-003`: stable ID relation index、renderer relation metadata、unit / Browser-level検証を実装。live delivery経由の最終確認待ち。
- `V4-004`: Simulation Phase 31 state transition observation待ち。
- `V4-005`: heterogeneous multi-Settlementの同一read model baselineとBrowser-level検証を実装。Village / Hamlet等の意味分類を使う最終表現は`V4-002`依存。
- `V4-006`: Simulation Phase 31 persistent regional observationを使うlive E2E待ち。
- `V4-007`: 本文書と回帰testでclient baselineを記録。Phase全体の最終closeoutは上流依存解消後に行う。

## Regression coverage

`src/web/tests/regional-generation-protocol.test.mjs` は次を固定する。

- Protocol 2.18 gate
- 64-bit stable ID の exact decode
- broken stable-ID relation の拒否

`src/web/tests/regional-generation-store.test.mjs` は Settlement / District / Parcel / Building / POI relation を stable ID で必要な範囲まで辿れること、および connection reset 相当の clear で state が破棄されることを固定する。

`src/web/tests/settlement-structure-renderer.test.mjs` は次を固定する。

- 異なるrole / influence radius / land-use / building densityを持つ複数Settlementを単一read modelで同時描画し、単一都市へ集約しない
- Settlement / District / Parcel / Building / POI のstable ID relationをrenderer revisionと同時に保持する
- clear後にrelation metadataを破棄する

`src/web/tests/browser/view-phase04-e2e.mjs` と `scripts/run-view-phase04-e2e.sh` は実Chrome / Chromium上で次を確認する。

- Settlement / Corridor / District / Parcel / Building / POI / Toponym / Road SignがThree.js sceneへ生成され、実draw callが発生する
- 離れた複数Settlementが1つへ集約されず、Simulation提供role等の差をpresentationへ反映する
- Parcel→District / Settlement、Building→Parcel / District / Settlement、POI→Building等のstable ID relationを`bigint`のまま保持する
- browser canvas上でhuman Toponym spriteを生成できる

このBrowser E2EはView-local rendering boundaryの証跡であり、Simulation→Gateway→Webのlive Regional Generation deliveryを証明するものではない。

## Known upstream contract issue

Issue #298 に記録済みのとおり、Simulation の `RoadSignKind.RockSlope = 9` に対して現行 C# `RegionalGenerationProtocolCodec` が `Kind > 8` を拒否する不整合がある。Web decoder は authoritative Simulation enum に合わせ 0〜9 を受理する。Server 側不整合の修正は View で意味を変換して回避しない。

## Version handling

View Phase 4 は wire contract を受けるため Web Client の negotiated Protocol version を 2.18 へ更新する。アプリケーション release version を示すルート `VERSION` はこの実装では変更しない。
