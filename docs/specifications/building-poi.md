# Building / POI 基盤仕様

MachiVerseWorks の都市オブジェクトとして、Building と POI（Point of Interest）の最小authoritative data modelを定義する。

## 目的

- Agentの生活・勤務・買物・交通など後続システムが参照できるstableな都市オブジェクトIDを用意する。
- 建物の3D占有範囲と、意味を持つ地点をSimulation Worldの正本状態として保持する。
- Save / Load後もIDと参照関係を維持し、表示文字列やWeb Client状態に依存しない契約にする。
- Road graph、schedule、economy等を導入する前に、参照先となる最小データモデルだけを確立する。

## Building

Buildingは次の値を持つ。

- `BuildingId`: `ulong`を包むstable ID
- `BuildingKind`: 建物用途の粗い分類
- `Bounds`: `WorldVolume`によるnative 3D AABB

`BuildingKind`は現時点で次を定義する。

| 値 | Kind | 意味 |
| ---: | --- | --- |
| 0 | `Generic` | 未分類 |
| 1 | `Residential` | 住宅 |
| 2 | `Commercial` | 商業 |
| 3 | `Industrial` | 産業 |
| 4 | `Civic` | 公共・行政・公共施設 |
| 5 | `MixedUse` | 複合用途 |

Buildingの形状はPhase 10では`WorldVolume`だけを正本とする。polygon footprint、rotation、mesh、floor、entrance、capacity、所有者、住所、名称は持たない。`Bounds`のMinZ / MaxZが3D占有高度を表すため、別のbase altitude / height fieldを重複して正本化しない。

## POI

POIは、後続Simulationが目的地・機能地点として参照する最小の意味付き地点である。

- `PoiId`: `ulong`を包むstable ID
- `PoiKind`: 地点用途の粗い分類
- `Position`: native 3D `WorldPoint`
- `BuildingId?`: 任意の所属Building参照

`PoiKind`は現時点で次を定義する。

| 値 | Kind | 意味 |
| ---: | --- | --- |
| 0 | `Generic` | 未分類 |
| 1 | `Residence` | 居住地点 |
| 2 | `Workplace` | 就業地点 |
| 3 | `Retail` | 小売・買物地点 |
| 4 | `Education` | 教育地点 |
| 5 | `Healthcare` | 医療地点 |
| 6 | `Recreation` | 娯楽・余暇地点 |
| 7 | `Transit` | 交通接続地点 |
| 8 | `Service` | その他サービス地点 |

`BuildingId`を持つPOIは、参照先Buildingが存在し、かつ`Position`がその`Bounds`内に含まれなければならない。Buildingに属さない屋外・ネットワーク上の地点を表現するため、`BuildingId = null`も正式に許可する。

## Stable ID

BuildingとPOIはAgentとは別のID namespaceを持つ。

- 0は無効IDとする。
- 生成IDは1から単調増加する。
- 削除したIDを再利用・再採番しない。
- checkpoint / Save Dataでは`nextBuildingId`と`nextPoiId`を保存する。
- ID空間をこれ以上進められない場合、生成はstate mutationなしで失敗する。

IDを表示名の代わりに使うことは想定しない。名称・住所などユーザー向け文字列は、それらの仕様を追加するときにstable IDとは別のfieldとして定義する。

## 参照整合性と削除

- 存在しないBuildingを参照するPOIは作成・復元できない。
- Buildingに所属するPOIはBuilding範囲外へ置けない。
- POIから参照されているBuildingは削除できない。
- RoadAccessPointやPedestrian Trip等、後続domainから参照されているBuilding / POIも参照を解消するまで削除できない。
- checkpoint / Save Data復元時も同じ参照整合性を全件検証する。

## Snapshot / checkpoint / Save

Simulationのmutable storeは外部公開しない。

- `BuildingSnapshot` / `PoiSnapshot`は値コピーとして返す。
- 全件snapshotはID昇順で返し、同一stateから決定的な順序を得る。
- `SimulationCheckpoint`はBuilding / POIの全stateと次IDを保持する。
- restore完了後に新規生成したIDは、保存・復元しなかったWorldと同じID系列を継続する。
- Building / POIはSave Dataのauthoritative stateとして保存し、round-trip後もstable ID、3D値、親Building参照を維持する。

## Phase 10 の責務境界

Phase 10は**Building / POIの最小authoritative foundation**までを対象とする。Parcel、zoning、generic urban rendering等までを同じPhaseの完了条件へ含めない。この境界をROADMAPのPhase 10と正本として一致させる。

Phase 10で扱わない計画済み項目は削除せず、次へ明示的に委譲する。

| 項目 | 正本となるPhase / 境界 |
| --- | --- |
| Road上のBuilding / POI access | Phase 11 `RoadAccessPoint` |
| 徒歩networkへのBuilding / POI access | Phase 16 pedestrian network |
| Parcel / zoning / land use / development | Phase 24 Urban Growth & City Generation |
| Building / Parcel / POIのInspector・編集UI | Phase 25 City Management UI |
| 建物mesh / floor / room / entrance | 必要になるdomain Phaseで契約追加。Phase 10では`WorldVolume`を正本とする |
| Agent needs / schedule / householdとPOI選択 | Phase 15 Population & Daily Activity |
| 名称・住所・locale対応 | UI / localization境界の後続Phase |

Protocol / Server / WebへBuilding / POIそのものを汎用配信する必要が生じた場合も、利用目的を持つ後続Phaseでversioned contractとして追加する。Phase 10のmodelをWeb表示都合へ拡張して完了条件を後付けしない。
