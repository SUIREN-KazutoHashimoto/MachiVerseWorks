# Logistics / Freight Architecture

## Boundary

Phase 22 の Logistics は `SimulationWorld.Logistics` を authoritative boundary とする。Economy、Urban World、Routing、Road Traffic を再利用し、物流専用の topology、vehicle solver、server-side duplicate state は作らない。

```text
Economy production / consumption
        |
        v
Inventory -> LogisticsOrder -> Shipment
                              | pickup/delivery RoadAccessPoint
                              v
                           Routing
                              |
                              v
                    Road Traffic VehicleStore
                              |
                     Arrived / release
                              v
                      Shipment delivery
                              |
                              v
                    destination Inventory
```

## Simulation

`Logistics.cs` は公開 ID、enum、snapshot、checkpoint 契約を定義する。`SimulationWorld.Logistics.cs` は mutable state とルールを保持する。

`SimulationWorld.Step()` は Economy 更新後、Population trip planning と Road Traffic step より前に Logistics を進める。これにより当該 tick の Economy 生産を Inventory へ取り込み、必要な Freight Vehicle を Road Traffic の通常 step へ参加させられる。

処理順序は以下とする。

1. 未処理 economic cycle を Logistics cycle へ反映する。
2. Company ごとの production delta を 1 回だけ計算し、Supplier inventory を stable order で capacity まで充填する。
3. Consumer inventory を消費する。
4. `(EstablishmentId, CommodityId)` active-order index を使う reorder rule から Order を生成する。
5. 既存 Shipment の state を stable ID 順に進める。
6. Open Order を stable ID 順に allocation する。
7. InTransit Shipment の Freight Vehicle は同じ tick の Road Traffic step で進む。

Company production は Company の累積 `ProducedUnits` を正本とする。複数 Supplier inventory が同一 Company を参照しても全 inventory が同じ delta を個別加算してはならない。各 inventory に保存済みの観測値の最小値から未処理 delta を求め、`EstablishmentId` / `CommodityId` 安定順へ一度だけ配分した後、Company 内の観測値を現在値へ揃える。

未完了 Order の存在確認は履歴全走査ではなく `_activeLogisticsOrderKeys` で行う。この index は checkpoint の正本データではなく Order state から復元できる派生 state とし、Order completion 時に削除する。

## Urban World integration

Warehouse / loading point / delivery point は新しい重複 Entity を作らず、Establishment が配置される Building / POI と既存 Motor `RoadAccessPoint` の組で表す。Logistics configuration 時に Building / POI 参照が一致することを検証する。

この境界により将来 warehouse 固有モデルを追加しても、Road access の正本は Road Network に残る。

## Road Traffic integration

Shipment は `VehicleId?` を保持するだけで、走行中の車両状態自体は `VehicleStore` が正本となる。Freight dimensions / performance を使って既存 `CreateVehicle(RouteResult, ...)` API へ生成し、lane occupancy、speed limit、前走車との制約を通常 Vehicle と共有する。

Logistics は Vehicle の `Arrived` を観測した時点で Shipment を `Unloading` へ進め、`RemoveVehicleCore` で Freight Vehicle を `VehicleStore` から解放する。以後 Shipment の `VehicleId` は historical reference であり、Road Traffic snapshot、traffic metrics、checkpoint vehicle collectionには残さない。これにより配送履歴の増加が Road Traffic の resident state / memory を増やし続けることを防ぐ。

## Routing integration

Pickup / delivery の `RoadAccessPoint` を segment 上の world position に変換し、既存 `FindRoadRoute` を `EstimatedTravelTime` metric で呼び出す。route が作れない Shipment は Loading のまま残り、後続 tick で再試行する。

Phase 22 では shipment sequencing や VRP solver を持たない。allocation の stable ID 順が配送順序の deterministic baseline となる。

## Delay observation

dispatch 時の route `EstimatedTravelTimeSeconds` から `PlannedDeliveryTick` を計算する。Road Traffic の実走行が予定を超えた場合、未完了 Shipment の `DelayTicks` と aggregate delayed count に反映する。

Inventory への補充は Delivered 遷移まで行わないため、渋滞による遅延は需要側 inventory の低在庫期間をそのまま延長する。

## Persistence

`EconomyCheckpoint.Logistics` を optional sub-state として持たせ、existing Save Format 11 document の Economy JSON 配下へ additive に保存する。Simulation checkpoint restore 時は Economy を復元した後に Logistics を復元する。`InTransit` Shipment の `VehicleId` は checkpoint vehicle collection に存在することを要求する一方、`Unloading` / `Delivered` Shipment の historical `VehicleId` は Vehicle の存在を要求しない。

Deserialize 前の `Utf8JsonReader` scan は `simulation.economy.logistics` を明示的に辿り、Commodity / Inventory / Order / Shipment collection の上限を DTO materialization より前に適用する。materialization 後の既存 checkpoint limit validation と二重化し、巨大 JSON による先行 allocation を避ける。

古い v11 document で `logistics` が欠落している場合は空状態へ復元する。format number は JSON field addition 自体では変更しない。

## Protocol / Server

Protocol 2.11 `LogisticsSnapshot` は statistics と bounded debug entries を持つ。`LogisticsPublishService` は negotiated version が 2.11 以上の connection のみに送信し、encode は `LogisticsProtocolCodec` が担当する。

Shipment debug entries は active (`Pickup` / `Loading` / `InTransit` / `Unloading`) を先に、各群では新しい `ShipmentId` を先に選択し、最大 256 件へ制限する。これにより累積 Delivered history が上限を超えても現在の配送が debug stream から押し出されない。

Server の E2E fixture は `LogisticsFixtureHostedService` に閉じ込め、本番 Simulation rule へ fixture 分岐を持ち込まない。

## Web

`logistics-protocol.ts` が binary contract を decode し、`connection.ts` が Logistics frame を専門 codec へ分岐する。走行中 Freight Vehicle は existing traffic stream で描画され、`logistics-debug.ts` は Shipment に保持された historical Vehicle ID と state の対応確認用 overlay を持つ。

## Performance

Snapshot debug entries は Server 側で 256 件へ制限する。Simulation の authoritative Shipment history は全件保持するが、完了した Freight Vehicle は resident `VehicleStore` から除去する。benchmark では 100 / 1,000 Inventory を使って tick、routing batch、snapshot allocation を BenchmarkDotNet + MemoryDiagnoser で測定する。
