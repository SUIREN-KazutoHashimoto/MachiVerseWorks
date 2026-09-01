# Gas Infrastructure Architecture

## Layer boundary

```text
Simulation
  Gas authoritative state
      | IGasSupplySolver
      v
  CapacityGasSupplySolver
      |
      +--> checkpoint -> Persistence
      +--> Gas snapshot ----+
      |                     +--> Server read-model join -> Protocol 2.14 -> Web debug overlay
      +--> Logistics snapshot+
      |
      +--> Delivered Gas -> existing Logistics inventory / order / freight
      +--> Economy production availability
```

SimulationがGas状態の正本である。Delivered Gasのinventory / Order / ShipmentはLogisticsが正本であり、Persistence / Protocol / Server / Webは独自のGas配送state machineを持たない。

## Pipeline Gas

`SimulationWorld.Gas`は次を所有する。

- stable ID採番とID index
- 3D GasNode topology
- directed GasPipeline
- GasSource / GasImportTerminal / GasStorage
- Building / Establishment GasServicePoint
- demand計算
- solver request生成とresult適用
- storage stock更新
- Economy availability係数
- checkpoint作成・restore・validation

標準solverは`IGasSupplySolver`で差し替え可能であり、Simulation本体を高精度なgas-flow libraryへ依存させない。

## Regulator boundary

Phase 25の標準Regulatorは`GasNodeKind.Regulator`としてtopology上に置く。Regulator nodeから下流へ接続する`GasPipeline`のcapacityがregulator capacity、`IsInService`がoperating stateとして機能する。

この表現により標準solverはRegulator専用の圧力式を持たずにflow bottleneck / outageを扱える。将来solverは同一nodeをpressure-control pointとして拡張できる。

## Delivered Gas / Logistics reuse

Delivered Gasは第二の配送simulationを作らない。

```text
Gas demand
  -> consumer Inventory (CommodityKind.Gas)
  -> Logistics reorder rule
  -> Order
  -> Shipment / Freight vehicle
  -> Road Network
  -> delivery
  -> consumer Inventory replenishment
  -> Gas service state
```

在庫・order・shipment・freight vehicleはPhase 22 Logisticsの正本を利用する。Gas側はconsumer inventory quantityをservice availabilityへ変換するだけで、道路輸送状態を複製しない。

## Tick dependency

概念上の依存は次のとおり。

```text
Power
  -> Water / Sewer
  -> Gas
  -> Economy operational constraints
  -> Logistics / Freight
```

Pipeline Gasは同一tickのnetwork stateから供給状態を計算する。Delivered Gasの補充は既存Logistics tickによって進み、到着したinventoryは後続tickのGas availabilityへ反映される。

## Capacity graph

`CapacityGasSupplySolver`はGasNodeをvertex、in-service GasPipelineをdirected edgeとしてresidual capacity graphを構築する。Source / ImportTerminal / Storageをsuper-sourceへ、Piped Gas loadをsuper-sinkへ接続してdeterministicなID順で最大流を解く。

Storage available capacityはrelease rateと残stockの小さい方で制限する。

## Persistence

Gas checkpointは`EconomyCheckpoint`のoptional extensionとしてSave Format 11へ保存する。stable-ID counter、topology、facility output / operating state、storage stock、service stateを復元する。

PersistenceはGas collectionをdeserialize前にもscanし、WorldSaveLimitsを超えるnode / pipeline / facility / service pointを拒否する。

## Protocol / Server read model

Protocol 2.14の`GasSnapshot`は以下を配信する。

- aggregate statistics
- GasNode kind / 3D position
- Pipeline capacity / in-service state
- Source / ImportTerminal / Storage capacity・output・stock・operating state
- Piped / Delivered ServicePointのdemand・served・unserved・service state
- Delivered ServicePointのconsumer inventory quantity / capacity
- Delivered ServicePoint宛てactive Shipmentのaggregate quantity / count

`GasPublishService`は同一`SimulationRuntime.Read`内でGas snapshotとLogistics snapshotを取得し、`GasMessageMapper`でread modelを結合する。これにより配信中にGasとLogisticsのtickがずれることを避ける。

Shipment ID、詳細state、vehicle、planned / delivered tick等は引き続き`LogisticsSnapshot`が正本である。Gas snapshotへはdebugとservice-state因果確認に必要な集約値だけを複製する。

Serverはdebug payloadをbounded entryとして配信し、Protocol 2.14以上のconnectionだけへ送信する。

## Web

Web ClientはGas snapshotをread-only debug modelとして扱う。Gas pipe、node、facility、service health、aggregate demand / capacity / pipeline storageに加え、Delivered Gasのinventory / active shipmentをoverlay表示し、Web側からSimulation正本を直接変更しない。

## Deterministic E2E

Phase 25 fixtureはPipeline GasとDelivered Gasを同時にseedする。Pipeline側はRegulator nodeを通るpipelineを周期的に停止・復旧させる。Delivered側はconsumer inventory、reorder、Freight shipment、delivery、inventory replenishmentを既存Logistics経由で進める。

E2EはProtocol 2.14のGas snapshotだけから、pipeline healthy→cut→recoveryと、Delivered Gas healthy→active Shipment→stockout→inventory replenishment→service recoveryを観測する。
