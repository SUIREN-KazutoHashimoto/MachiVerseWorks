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
2. Supplier inventory へ production delta を受け入れる。
3. Consumer inventory を消費する。
4. reorder rule から Order を生成する。
5. 既存 Shipment の state を stable ID 順に進める。
6. Open Order を stable ID 順に allocation する。
7. InTransit Shipment の Freight Vehicle は同じ tick の Road Traffic step で進む。

## Urban World integration

Warehouse / loading point / delivery point は新しい重複 Entity を作らず、Establishment が配置される Building / POI と既存 Motor `RoadAccessPoint` の組で表す。Logistics configuration 時に Building / POI 参照が一致することを検証する。

この境界により将来 warehouse 固有モデルを追加しても、Road access の正本は Road Network に残る。

## Road Traffic integration

Shipment は `VehicleId?` を保持するだけで、車両状態自体は `VehicleStore` が正本となる。Freight dimensions / performance を使って既存 `CreateVehicle(RouteResult, ...)` API へ生成し、lane occupancy、speed limit、前走車との制約を通常 Vehicle と共有する。

Logistics は Vehicle の `Arrived` のみを配送完了判定に使う。位置、速度、lane は複製しない。

## Routing integration

Pickup / delivery の `RoadAccessPoint` を segment 上の world position に変換し、既存 `FindRoadRoute` を `EstimatedTravelTime` metric で呼び出す。route が作れない Shipment は Loading のまま残り、後続 tick で再試行する。

Phase 22 では shipment sequencing や VRP solver を持たない。allocation の stable ID 順が配送順序の deterministic baseline となる。

## Delay observation

dispatch 時の route `EstimatedTravelTimeSeconds` から `PlannedDeliveryTick` を計算する。Road Traffic の実走行が予定を超えた場合、未完了 Shipment の `DelayTicks` と aggregate delayed count に反映する。

Inventory への補充は Delivered 遷移まで行わないため、渋滞による遅延は需要側 inventory の低在庫期間をそのまま延長する。

## Persistence

`EconomyCheckpoint.Logistics` を optional sub-state として持たせ、existing Save Format 11 document の Economy JSON 配下へ additive に保存する。Simulation checkpoint restore 時は Economy を復元した後に Logistics を復元し、Shipment が参照する `VehicleId` の存在を validation する。

古い v11 document で `logistics` が欠落している場合は空状態へ復元する。format number は JSON field addition 自体では変更しない。

## Protocol / Server

Protocol 2.11 `LogisticsSnapshot` は statistics と bounded debug entries を持つ。`LogisticsPublishService` は negotiated version が 2.11 以上の connection のみに送信し、encode は `LogisticsProtocolCodec` が担当する。

Server の E2E fixture は `LogisticsFixtureHostedService` に閉じ込め、本番 Simulation rule へ fixture 分岐を持ち込まない。

## Web

`logistics-protocol.ts` が binary contract を decode し、`connection.ts` が Logistics frame を専門 codec へ分岐する。Freight Vehicle は existing traffic stream で描画され、`logistics-debug.ts` は ID と state の対応確認用 overlay のみを持つ。

## Performance

Snapshot debug entries は Server 側で 256 件へ制限する。Simulation の authoritative collections は全件保持し、benchmark では 100 / 1,000 Inventory を使って tick、routing batch、snapshot allocation を BenchmarkDotNet + MemoryDiagnoser で測定する。
