# Water & Sewer Infrastructure Architecture

## Layer boundary

```text
Simulation
  Water/Sewer authoritative state
      | IWaterSupplySolver / ISewerSolver
      v
  CapacityWaterSupplySolver / CapacitySewerSolver
      |
      +--> checkpoint -> Persistence
      +--> snapshot -> Server mapper -> Protocol 2.13 -> Web debug overlay
```

Simulationが唯一の正本であり、Persistence / Protocol / Server / WebはSimulation stateを変更する独自のWater/Sewer modelを持たない。

## Simulation

`SimulationWorld.WaterSewer`は次を所有する。

- stable ID採番とentity index
- Water / Sewer 3D node spatial index
- directed pipe topology
- Facility / ServicePoint state
- demand計算
- solver request生成とresult適用
- Power facility availability連携
- Economy availability係数
- checkpoint作成・restore・validation

solverはconstructor injectionで差し替え可能であり、Simulation本体は特定の高精度水理libraryへ依存しない。

## Tick dependency

```text
Agent step
  -> capture economy production baseline
  -> Power
  -> Water / Sewer
  -> Economy
  -> apply Power + Water/Sewer operational constraints
  -> Logistics / traffic / transit
```

この順序により、同一tickのPower outageをPump / Treatmentへ反映してからEconomy productionを評価する。

## Capacity graph

標準solverはdirected residual graphを構築し、deterministicなID順で最大流を計算する。Pumpはnode splittingによってnode throughput capacityとして扱う。詳細水圧や管摩擦はgraphへ埋め込まず、将来solverの責務とする。

## Persistence

Water/Sewer checkpointは`EconomyCheckpoint`のoptional extensionとして保存する。既存Format 11を維持しつつ、deserialize前のnested collection scanにもWater/Sewer配列の上限を設定する。

## Protocol / Server

Protocol 2.13の`WaterSewerSnapshot`は以下を送る。

- aggregate statistics
- network discriminator付きNode / Pipe
- Facility kind、capacity、throughput、operating state、optional PowerLoad ID
- ServicePoint demand / served / unserved、wastewater generated / processed / overflow、service state

Serverはdebug payloadを各category最大512件へ制限し、2.13以上へ定期配信する。E2E fixtureは通常状態、需要追加、Treatment停止、WaterPipe切断、復旧を決定論的に再現する。

## Web

WebはWater/Sewer snapshotを表示専用に扱う。debug overlayはWater / Sewer pipe、facility、service point healthを描画し、Simulationへ逆向きのstate mutationを行わない。
