# Logistics / Freight Specification

## 目的

Phase 22 は Phase 21 の Economy が生成する物資を、都市内の在庫・注文・配送として authoritative Simulation state に接続する。物流状態は Web 表示や Server fixture の都合ではなく `MachiVerseWorks.Simulation` を正本とする。

## 正本 Entity

- `Commodity`: 物流単位を stable `CommodityId` と種別で識別する。
- `Inventory`: `(EstablishmentId, CommodityId)` ごとに数量、capacity、reorder point、target quantity、日次消費量、Road Access Point を持つ。
- `LogisticsOrder`: 需要側 Establishment が必要とする Commodity と数量を stable `LogisticsOrderId` で保持する。
- `Shipment`: Order を供給側から需要側へ搬送する実配送単位で、stable `ShipmentId` と Freight `VehicleId` を持つ。

## Inventory と Economy

Supplier inventory は対応 Establishment の Company が持つ累積 `ProducedUnits` の増分を、capacity の範囲で在庫へ受け入れる。Consumer inventory は economic day ごとに `DailyConsumptionUnits` を消費する。

Consumer inventory が reorder point 以下になり、同一 Establishment / Commodity の未完了 Order が存在しない場合、`TargetQuantity - Quantity` を補充数量とする Order を生成する。1 unit 未満の Order は生成しない。

## Allocation

Open Order は `LogisticsOrderId` 昇順で処理する。供給候補は同一 Commodity を持つ Supplier inventory のうち必要数量を引き当て可能なものを `EstablishmentId` 昇順で選ぶ。引当時に供給在庫から数量を予約減算し、1 Order に対して 1 Shipment を生成する。

この Phase の最小モデルでは multi-stop、split shipment、partial fill、backorder priority は扱わない。

## Urban World / Routing 境界

Inventory は Motor 対応 `RoadAccessPointId` を必須とし、その Access Point は Inventory の Establishment が配置される Building または POI と一致しなければならない。Pickup / delivery position は Road Access Point の segment offset から導出する。

Freight route は既存 Road Routing の `EstimatedTravelTime` cost を使う。Shipment 専用の道路 topology や交通 solver は導入しない。

## Shipment state machine

Shipment は配送準備、積込、道路輸送、荷下ろし、到着を追跡する。道路輸送開始時に既存 `VehicleStore` へ Freight Vehicle を生成し、`VehicleMovementState.Arrived` を観測して荷下ろしへ遷移する。

- `Pickup`: Order allocation 後の集荷開始状態。
- `Loading`: fixed loading duration 中。
- `InTransit`: Freight Vehicle が Road Traffic 上を走行中。
- `Unloading`: Freight Vehicle 到着後の fixed unloading duration 中。
- `Delivered`: destination inventory へ数量を反映し Order を完了した状態。

`PlannedDeliveryTick` は route の推定所要時間と unloading duration から算出する。現在 tick が予定 tick を超えた未完了 Shipment を delayed とし、`DelayTicks` を公開する。これにより Freight は既存 Road Traffic の混雑・速度制約を共有し、その結果を物流遅延として観測できる。

## Determinism

Order generation、allocation、Shipment processing は stable ID 順で処理する。checkpoint には Logistics の next ID、economic cycle 位置、Inventory、Order、Shipment、Freight Vehicle 参照を含める。復元後も同じ tick 入力から同じ結果へ継続できなければならない。

## Persistence

Logistics checkpoint は Economy checkpoint の optional sub-state として保存する。既存 Save Format 11 の JSON 契約へ additive optional field として追加するため、`logistics` を持たない既存 v11 Save は空 Logistics state として読み込める。Save format version は変更しない。

## Protocol / Server

Protocol 2.11 に `LogisticsSnapshot` (`MessageType = 740`) を追加する。集計は全件を表し、debug Inventory / Shipment entry は Server mapper で上限 256 件に制限する。Protocol 2.10 以下の client へ Logistics message は配信しない。

## Web

Web Client は Protocol 2.11 を negotiation し、Logistics snapshot を decode する。Freight Vehicle の位置描画は既存 Vehicle stream を再利用し、Logistics debug overlay では Shipment ID、Vehicle ID、state、quantity、delay、Inventory quantity/capacity を表示する。

## 対象外

- 複数 Commodity の BOM / recipe
- 港湾・航空貨物・鉄道貨物
- multi-stop routing / vehicle capacity packing
- 市場価格、契約、仕入先最適化
- warehouse 内部の詳細搬送

これらは後続 Phase の要件として別途仕様化する。
