# Water & Sewer Infrastructure Specification

## 目的

Phase 24では、Building / Establishmentが上水道と下水道へ接続され、需要・排水量、network接続、pipe / facility capacity、施設稼働状態に応じてservice stateが変化する最小都市インフラモデルを定義する。

標準Simulationは詳細な水理モデルではない。水圧、Darcy-Weisbach式、Hazen-Williams式、管径、摩擦損失、ポンプ揚程、貯水池の時系列水位、管内滞留などはPhase 24の必須範囲外とし、`IWaterSupplySolver` / `ISewerSolver` の外部実装で置換できる境界を提供する。

## 単位と流向

- 水量・排水量・capacity: `m3/day`（立方メートル/日）
- 位置: world coordinateの`WorldPoint(X, Y, Z)`
- WaterPipe: `FromNodeId -> ToNodeId` の有向辺
- SewerPipe: `FromNodeId -> ToNodeId` の有向辺。Service / Collection側からTreatment側へ向ける
- Power連携: Pump / SewageTreatmentPlantは任意の`PowerLoadId`を参照し、参照loadが給電されないtickはavailable capacityを0とする

## Stable ID / topology

以下はSimulation内で単調増加するstable IDを持つ。

- `WaterNodeId`, `WaterPipeId`
- `SewerNodeId`, `SewerPipeId`
- `WaterSourceId`, `ReservoirId`, `PumpId`, `SewageTreatmentPlantId`
- `WaterSewerServicePointId`

Water / Sewer nodeは3D座標を持ち、spatial cell indexから`WorldVolume` queryできる。Pipe、Facility、ServicePointのcheckpoint restore時は参照先IDの存在と重複・capacity・enum値を検証する。

## Facility

- WaterSource: 上水networkへの供給capacity
- Reservoir: 上水networkへ放流できるrelease capacity
- Pump: WaterまたはSewer nodeのthroughput capacity。任意でPowerLoadへ接続可能
- SewageTreatmentPlant: Sewer networkの処理sink。任意でPowerLoadへ接続可能
- operating state: `Online` / `Offline`

Phase 24のReservoirは貯水量を時系列積分しない。release capacityを持つ供給源として扱う。

## Building / Establishment service point

`WaterSewerServicePoint`はWaterNodeとSewerNodeを1つずつ参照し、Building / Establishmentの少なくとも一方を参照する。EstablishmentにBuildingがある場合は矛盾するBuilding指定を拒否する。

各service pointはbase water demandとwastewater return ratioを持つ。標準値は0.9で、供給された水量にreturn ratioを乗じた量をwastewater generationとする。

## Water demand最小rule

標準Simulationのdemandは次の係数をbase demandへ乗じる。

1. 時刻帯: 深夜、朝、日中、夕方、夜で係数を変える。
2. Building用途: Residential / Commercial / Industrial / Civic / MixedUseで係数を変える。
3. Population: Building居住者数がある場合に需要を増加させる。
4. Industry: EstablishmentのIndustrySectorとfilled / required worker比を係数へ反映する。

これはゲームplay上のdeterministicな需要ruleであり、実都市の水需要予測モデルではない。

## Water Supply solver

`IWaterSupplySolver`はWater node / pipe / source / reservoir / pump / loadを入力し、source output、reservoir output、pump throughput、service point served waterを返す。

標準`CapacityWaterSupplySolver`は有向capacity graphの最大流として解く。Pump nodeはnode splittingでthroughput capacityを表現する。Pipeがout-of-serviceの場合、そのedge capacityは0として扱う。

## Sewer solver

`ISewerSolver`はSewer node / pipe / pump / treatment / wastewater loadを入力し、pump throughput、treatment processed、service point processed wastewaterを返す。

標準`CapacitySewerSolver`も有向capacity graphの最大流として解く。未処理量はservice pointのoverflowとして残る。

## Service state

Water:

- `Supplied`: demandが全量served
- `Constrained`: 一部served / 一部unserved
- `Unavailable`: servedが実質0でdemandが残る

Sewer:

- `Available`: generated wastewaterが処理可能、またはgenerationが実質0
- `Constrained`: 予約値。将来の詳細な排水制約表現用
- `Unavailable`: processedが実質0でwastewaterが残る
- `Overflow`: 一部処理されるが未処理量が残る

## Economy / Power連携

Simulation tick順はPower -> Water/Sewer -> Economyを基本とする。Power outageはPump / Treatment available capacityへ反映される。Economyの日次production増分はEstablishmentごとのPower availabilityとWater/Sewer availabilityの小さい方で制約する。

## Save / Protocol

`EconomyCheckpoint.WaterSewer`はoptionalで、既存Save Format 11へ後方互換追加する。Water/Sewerを持たないFormat 11は空のutility stateとしてrestoreする。

Protocol 2.13で`WaterSewerSnapshot(760)`を追加する。配信契約はSimulation内部型から独立したNode / Pipe / Facility / ServicePoint DTOとし、statistics、capacity、throughput、service stateを含む。Protocol 2.12以下へは送信しない。
