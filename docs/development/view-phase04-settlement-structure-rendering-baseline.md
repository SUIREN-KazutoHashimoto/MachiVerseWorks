# View Phase 4 Settlement & Structure Rendering baseline

View Phase 4 は、Simulation Phase 30 の Protocol 2.18 `RegionalGenerationSnapshot` を静的geometry / stable relationのsource、Simulation Phase 31 の Protocol 2.19 `PersistentRegionalEvolutionSnapshot` を時間変化するsemantic stateのsourceとして扱う。

## Read model boundary

Phase 30 baselineから Settlement / corridor / District / Parcel / Building / POI / Human Toponym / Road Sign を受け取り、`ulong` stable IDをJavaScript `number`へ丸めず`bigint`で保持する。`RegionalGenerationStore` は District→Settlement、Parcel→District / Settlement、Building→Parcel / District / Settlement、POI→Building / Settlementを配送済みstable IDだけで解決する。

Phase 31 evolutionから次を受け取る。

- Settlement scale / trend / activity / population / jobs / service / density / accessibility / influence radius
- Parcel development demand / land value / development state / building relation
- Building use / condition / occupancy / capacity / lifecycle status
- Service catchment / infrastructure demand
- Regional relation / evolution event
- Commuting flow / freight flow

`PersistentRegionalEvolutionStore` は同じstable IDでPhase 30 geometryへ状態を重ねる。Viewは別の都市modelを生成せず、Simulation observationをread-onlyで保持する。

## Protocol 2.19 chunk contract

Phase 31 serverは1つのlogical full snapshotを最大128 item単位へ分割して順番に送信する。先頭chunkだけ`isFullSnapshot=true`、後続chunkは`false`で、同じ`currentYear` / `tickCount`を共有する。

Web decoderは各chunkを独立してdecodeする。後続chunk内のParcel→SettlementやBuilding→Parcel等の参照先が先頭chunkにだけ存在することがあるため、decoderはchunk単体でcross-chunk referenceを要求しない。Storeが先頭full chunkで旧batchを破棄し、同一年・同tickのcontinuation chunkを順次合成する。batchの一致しないcontinuationは拒否する。

## Presentation mapping

`SettlementStructureRenderer` はPhase 30 geometryにPhase 31 stateをstable IDでoverlayする。

- corridor / District / POI / Toponym / Road Sign: Phase 30 authoritative geometry
- Settlement marker: Phase 31 `SettlementScale` / `SettlementTrend` / `IsActive` / current position / influence radius
- Parcel: Phase 30 bounds / zone + Phase 31 current development state
- Building: Phase 30 bounds + Phase 31 current use / condition / lifecycle status
- Regional relation: Phase 31 delivered relationをSettlement位置間のpresentationとして表示

Demolished Buildingはstable relationを失わず、解体済みであることを示す低いpresentationへ変化させる。これはSimulation stateを予測・変更するものではない。

## Classification rule

Viewは人口、jobs、密度、accessibility、influence radius等からCity / Town / Village / Hamletを推測しない。

Phase 31が`SettlementScale`としてHamlet / Village / Town / City / Metropolisをauthoritativeに公開するため、Viewはその値をそのままpresentationへ利用する。Phase 30だけしか存在しない場合は従来のrole-based baselineを表示するが、semantic scaleを推定して補完しない。

## Current integration dependency

Phase 31 `PersistentRegionalEvolutionPublishService` はProtocol 2.19 clientへlive evolution snapshotを配信する。一方、Phase 30 `RegionalGenerationSnapshot` のSimulation→Gateway→Web live配信経路はIssue #330で引き続き追跡している。

したがって次を分離する。

- View-local Browser E2E: Phase 30とPhase 31のauthoritative read-model shapeを入力し、実Chrome / Chromium + Three.jsでstatic geometryと時間変化overlayを検証する。
- live closeout: Phase 30 baseline geometryがGatewayからlive deliveryされた後、実Server上でPhase 30 stable IDとPhase 31 stable IDが同じrendererへjoinされることを確認する。

View側で不足するbaseline geometryを生成したり、Protocol 2.18へfallbackしてPhase 31を擬似対応したりしない。

## Current Phase 4 task status

- `V4-001`: client-side 3D rendering baselineとBrowser-level確認を実装。Phase 30 live Gateway integrationはIssue #330待ち。
- `V4-002`: Phase 31 authoritative `SettlementScale`を利用して実装。View側の人口等による再分類なし。
- `V4-003`: stable ID relation index、renderer relation metadata、unit / Browser-level検証を実装。live delivery経由の最終確認はIssue #330待ち。
- `V4-004`: Phase 31のParcel development / Building lifecycle / Settlement trend・activityをrendererへ反映して実装。
- `V4-005`: dense core / suburb / rural / Village / Hamlet等を同一read-model/renderer contractで扱い、semantic settlement scaleはPhase 31提供値のみを利用。
- `V4-006`: full+continuation chunkを組み立て、離れた複数Settlementが時間変化しても単一都市へ集約されないBrowser E2Eを実装。Simulation→Gateway→Web全体のlive closeoutはIssue #330待ち。
- `V4-007`: 本文書とunit / Browser regression testでrendering baselineを記録。

## Regression coverage

`src/web/tests/persistent-regional-evolution-protocol.test.mjs` はProtocol 2.19 gate、64-bit stable ID exact decode、`isFullSnapshot`、cross-chunk referenceを含むcontinuation decodeを固定する。

`src/web/tests/persistent-regional-evolution-store.test.mjs` はfull chunkでのreset、同一年・同tick continuationの合成、次のfull snapshotへの置換、不正continuationの拒否を固定する。

`src/web/tests/persistent-regional-evolution-renderer.test.mjs` はauthoritative Settlement scale/trend、Parcel redevelopment、Building demolition、Regional relation、時間更新後の再描画を固定する。

既存`src/web/tests/browser/view-phase04-e2e.mjs`はPhase 30 static baselineを実ブラウザで検証する。追加した`src/web/tests/browser/view-phase04-evolution-e2e.mjs`はfull chunk→continuation chunkを実際に組み立てて、City / Hamlet、Dormant、Parcel redevelopment、Building demolition、Regional relation、複数Settlement分離を実Three.js draw callまで検証する。`scripts/run-view-phase04-e2e.sh`は両方を実行する。

## Known upstream contract issue

Issue #298 に記録済みのとおり、Simulation の `RoadSignKind.RockSlope = 9` に対して既存Regional Generation codec側との不整合がある場合、Viewで意味を変換して回避しない。authoritative enum contractの修正はProtocol / Simulation側で扱う。

## Version handling

Simulation Phase 31統合後の正式wire contractに合わせ、Web Clientの通常handshakeはProtocol 2.19を要求する。2.19→2.18の自動fallbackは実装しない。

ルート`VERSION`はView Phase 4専用のversion bumpとしては変更しない。developから取り込まれたversion値はそのまま利用する。
