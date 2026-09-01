# Gas Infrastructure Specification

## 目的

Phase 25では、Building / Establishmentのgas demandをPipeline GasとDelivered Gasの2方式から供給し、需要・network capacity・facility state・物流在庫に応じて共通のservice stateが変化する最小都市インフラモデルを定義する。

標準Pipeline Gas Simulationは詳細な圧力・管径・圧縮性・温度・Weymouth式等を扱わず、接続性とcapacityを中心とする。高精度な圧力・流量計算は`IGasSupplySolver`を交換して拡張する。

## 単位と供給方式

- gas flow / demand / pipeline capacity: `m3/day`
- storage stock: `m3`
- position: `WorldPoint(X, Y, Z)`
- Pipeline Gas: directed `GasPipeline`を通じて供給する
- Delivered Gas: `CommodityKind.Gas`のLogistics consumer inventoryを需要側storageとして利用する

`GasDeliveryMode`は`Piped`または`Delivered`であり、どちらも`GasServiceState`へ収束する。

## Stable ID / topology

以下はSimulation内で単調増加するstable IDを持つ。

- `GasNodeId`
- `GasPipelineId`
- `GasSourceId`
- `GasImportTerminalId`
- `GasStorageId`
- `GasServicePointId`

Gas nodeは3D座標を持ち、`QueryGasNodes(WorldVolume)`で空間範囲を問い合わせられる。Pipeline / facility / service pointはcheckpoint restore時に参照先、重複ID、capacity、enum、Building / Establishment / Commodity参照を検証する。

## Gas node / Regulator

`GasNodeKind`はSource / ImportTerminal / Storage / Distribution / Service / Regulatorを持つ。

Phase 25のRegulatorは軽量モデルとし、独立した圧力物理entityを導入しない。Regulator nodeの下流`GasPipeline`がregulatorのflow capacityを、`GasPipeline.IsInService`がoperating stateを表す。したがってRegulatorのcapacity制約・停止・復旧は標準capacity solverの通常のedge制約として扱われ、Regulator node自身は`GasNodeId`をstable identityとして使用する。

将来の高精度solverは同じtopology境界を使いながら、pressure setpointやvalve特性等を追加できる。

## Facility

- GasSource: pipeline networkへ供給するcapacityを持つ
- GasImportTerminal: 外部供給を表すcapacity source
- GasStorage: finite stock、release capacity、operating stateを持つ
- operating state: `Online` / `Offline`

Storageは1 tickあたりのdispatch分を`EconomyDefaults.TicksPerEconomicDay`で日量から換算してstockから差し引く。

## Building / Establishment gas load

`GasServicePoint`はBuilding / Establishmentの少なくとも一方を参照する。

Piped modeは`GasNodeId`を参照し、Delivered modeはEstablishmentと`CommodityKind.Gas`のconsumer inventoryを参照する。Delivered modeでは独自の物流inventoryを作らずPhase 22のLogisticsを正本とする。

## Gas demand最小rule

標準Simulationのgas demandはbase demandへ次のdeterministic係数を適用する。

1. 時刻帯
2. Building用途
3. Building居住者数
4. EstablishmentのIndustrySector
5. filled / required worker比

これはゲームplay / Simulation向けの軽量ruleであり、実都市の需要予測や熱需要モデルではない。

## Pipeline Gas solver

`IGasSupplySolver`はnode / pipeline / source / import terminal / storage / piped loadを入力し、各供給元outputと各load served flowを返す。

標準`CapacityGasSupplySolver`はdirected capacity graphの最大流として解く。Pipelineがout-of-serviceの場合はedgeを使用しない。Source / ImportTerminalがOfflineの場合、またはStorageがOffline / depletedの場合はavailable capacityを0とする。

## Delivered Gas / Logistics

Delivered Gasは既存Logisticsを再利用する。

- `CommodityKind.Gas`をsupplier / consumer inventoryへ設定する
- consumer inventoryのreorder point以下で既存Logistics order ruleが補充Orderを生成する
- 既存Freight shipment / road vehicleがsupplierからconsumerへ輸送する
- Shipment delivery後のinventory quantityがGas service availabilityへ反映される

このため道路混雑・配送遅延・supplier stock不足は追加のGas専用物流ロジックなしでDelivered Gasへ影響する。

## Service state

- `Supplied`: demandを全量供給
- `Constrained`: 一部供給、一部unserved
- `Unavailable`: servedが実質0でdemandが残る

Pipeline / Deliveredのどちらも同じstate契約を使う。

## Economy連携

Establishmentのproduction availabilityは既存Power、Water/Sewer、Gas availabilityの最小値で制約する。Gas outageやDelivered Gas inventory不足は、他utilityと同じoperational constraint境界から生産へ反映される。

## Save / Protocol

`EconomyCheckpoint.Gas`はoptional extensionとしてSave Format 11へ追加し、既存Format 11との後方互換を維持する。Gasを持たないSaveは空Gas stateとしてrestoreする。nested collectionはdeserialize前後で既存WorldSaveLimitsに基づいて上限検証する。

Protocol 2.14で`GasSnapshot(770)`を追加する。Node / Pipeline / Facility / ServicePointとaggregate statisticsを送信し、Protocol 2.13以下にはGas snapshotを配信しない。

Delivered GasのServicePointには、既存Logistics snapshotをServer側で結合して次の観測値も含める。

- consumer inventory quantity / capacity
- active Shipmentの合計quantity
- active Shipment count

Order / Shipmentのstable ID、詳細state、vehicle参照等の完全な正本はPhase 22の`LogisticsSnapshot`に残す。Gas Protocolは配送供給状態をdebug・E2Eで追跡するための集約read modelだけを持ち、Logistics stateを二重管理しない。
