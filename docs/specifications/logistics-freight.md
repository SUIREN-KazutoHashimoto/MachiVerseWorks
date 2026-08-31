# Logistics / Freight Specification

## 目的

Phase 22 は Phase 21 の Economy が生成する物資を、都市内の在庫・注文・配送として authoritative Simulation state に接続する。物流状態は Web 表示や Server fixture の都合ではなく `MachiVerseWorks.Simulation` を正本とする。

## 正本 Entity

- `Commodity`: 物流単位を stable `CommodityId` と種別で識別する。
- `Inventory`: `(EstablishmentId, CommodityId)` ごとに数量、capacity、reorder point、target quantity、日次消費量、Road Access Point を持つ。
- `LogisticsOrder`: 需要側 Establishment が必要とする Commodity と数量を stable `LogisticsOrderId` で保持する。
- `Shipment`: Order を供給側から需要側へ搬送する実配送単位で、stable `ShipmentId` と Freight `VehicleId` を持つ。

## Inventory と Economy

Supplier inventory は対応 Establishment の Company が持つ累積 `ProducedUnits` の増分を受け入れる。`ProducedUnits` は Company 単位の累積値なので、同一 Company に Supplier inventory が複数存在しても増分は 1 回だけ消費する。対象 Supplier を `EstablishmentId`、`CommodityId` の安定順に並べ、各 inventory の空き capacity へ順に配分する。全 Supplier の capacity を超えた余剰生産は Phase 22 の最小モデルでは保持しない。

各 Supplier inventory が checkpoint に保持する `ObservedCompanyProducedUnits` は Company の生産増分を再適用しないための観測値である。Supplier が economic cycle の途中で追加された場合も、Company 内で最も古い未処理観測値から差分を 1 回だけ計算し、処理後は同一 Company の Supplier 群を現在の累積 `ProducedUnits` へ揃える。

Consumer inventory は economic day ごとに `DailyConsumptionUnits` を消費する。

Consumer inventory が reorder point 以下になり、同一 Establishment / Commodity の未完了 Order が存在しない場合、`TargetQuantity - Quantity` を補充数量とする Order を生成する。未完了 Order の存在判定は `(EstablishmentId, CommodityId)` の active index を正本 collection から派生させて行う。1 unit 未満の Order は生成しない。

## Allocation

Open Order は `LogisticsOrderId` 昇順で処理する。供給候補は同一 Commodity を持つ Supplier inventory のうち必要数量を引き当て可能なものを `EstablishmentId` 昇順で選ぶ。引当時に供給在庫から数量を予約減算し、1 Order に対して 1 Shipment を生成する。

この Phase の最小モデルでは multi-stop、split shipment、partial fill、backorder priority は扱わない。

## Urban World / Routing 境界

Inventory は Motor 対応 `RoadAccessPointId` を必須とし、その Access Point は Inventory の Establishment が配置される Building または POI と一致しなければならない。Pickup / delivery position は Road Access Point の segment offset から導出する。

Freight route は既存 Road Routing の `EstimatedTravelTime` cost を使う。Shipment 専用の道路 topology や交通 solver は導入しない。

## Shipment state machine

Shipment は配送準備、積込、道路輸送、荷下ろし、到着を追跡する。道路輸送開始時に既存 `VehicleStore` へ Freight Vehicle を生成し、`VehicleMovementState.Arrived` を観測して荷下ろしへ遷移する。到着確認後は Freight Vehicle を Road Traffic の active state / checkpoint から削除し、Shipment には監査・debug 用の historical `VehicleId` だけを残す。

- `Pickup`: Order allocation 後の集荷開始状態。
- `Loading`: fixed loading duration 中。
- `InTransit`: Freight Vehicle が Road Traffic 上を走行中。
- `Unloading`: Freight Vehicle 到着後の fixed unloading duration 中。Vehicle は Road Traffic から解放済み。
- `Delivered`: destination inventory へ数量を反映し Order を完了した状態。Vehicle ID は履歴参照のみ。

`PlannedDeliveryTick` は route の推定所要時間と unloading duration から算出する。現在 tick が予定 tick を超えた未完了 Shipment を delayed とし、`DelayTicks` を公開する。これにより Freight は既存 Road Traffic の混雑・速度制約を共有し、その結果を物流遅延として観測できる。

## Determinism

Order generation、allocation、Shipment processing は stable ID 順で処理する。checkpoint には Logistics の next ID、economic cycle 位置、Inventory、Order、Shipment、走行中 Freight Vehicle 参照を含める。Unloading / Delivered Shipment の `VehicleId` は historical reference であり、対応 Vehicle が checkpoint に存在する必要はない。復元後も同じ tick 入力から同じ結果へ継続できなければならない。

## Persistence

Logistics checkpoint は Economy checkpoint の optional sub-state として保存する。既存 Save Format 11 の JSON 契約へ additive optional field として追加するため、`logistics` を持たない既存 v11 Save は空 Logistics state として読み込める。Save format version は変更しない。

Save Data の collection limit は DTO materialization 前の streaming scan と materialization 後の checkpoint validation の両方で適用する。`commodities` / `inventories` は Building 系上限、`orders` は Person 系上限、`shipments` は Vehicle 系上限を再利用する。

## Protocol / Server

Protocol 2.11 に `LogisticsSnapshot` (`MessageType = 740`) を追加する。集計は全件を表し、debug Inventory / Shipment entry は Server mapper で上限 256 件に制限する。Shipment は active state を最優先し、残り枠へ新しい Shipment ID から順に履歴を入れるため、配送履歴が 256 件を超えても現在進行中の配送を観測できる。Protocol 2.10 以下の client へ Logistics message は配信しない。

## Web

Web Client は Protocol 2.11 を negotiation し、Logistics snapshot を decode する。走行中 Freight Vehicle の位置描画は既存 Vehicle stream を再利用し、Logistics debug overlay では Shipment ID、historical Vehicle ID、state、quantity、delay、Inventory quantity/capacity を表示する。

## 対象外

- 複数 Commodity の BOM / recipe
- 港湾・航空貨物・鉄道貨物
- multi-stop routing / vehicle capacity packing
- 市場価格、契約、仕入先最適化
- warehouse 内部の詳細搬送

これらは後続 Phase の要件として別途仕様化する。
