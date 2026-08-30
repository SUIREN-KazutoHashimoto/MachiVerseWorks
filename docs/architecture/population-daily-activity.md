# Population & Daily Activity Architecture

Phase 15のPopulation / Daily ActivityをSimulation CoreからSave / Protocol / Server / Webへ通す実装境界を記録する。

## State ownership

Populationのauthoritative stateは`SimulationWorld`に属する。

- `PopulationStore`がHousehold / Personのmutable stateとstable ID allocationを保持する。
- `SimulationWorld.Population`がvalidation、planner、Trip dispatch、arrival state machine、statistics / debug snapshotを提供する。
- Pedestrian / Vehicleは移動実行entityであり、Personの活動理由やscheduleを所有しない。

Population stateをServerやWebへ複製して正本化しない。

## Tick ordering

Population tickはfixed Simulation tickで次を行う。

1. Need satisfactionをelapsed timeに応じて更新する。
2. `AtActivity` Personについてminute-of-dayからdesired activityを選択する。
3. destination変更が必要ならstable `TripRequestId`を割り当てる。
4. private Vehicleが利用可能ならMotor routeを試し、成立時はVehicleを生成する。
5. MotorへdispatchしなかったTripはFootとしてPedestrianへdispatchする。
6. `Walking` / `Driving` Personは対応entityの到着を観測する。
7. 到着時にPersonのlocation / activityを更新してactive trip参照を解放する。

plannerはtraffic implementationから独立した`TripRequest`を境界に使うため、後続のmultimodal choiceはdispatch policyを差し替えて拡張できる。

## Pedestrian integration

Population由来徒歩Tripでも、Phase 16で確立した`SimulationWorld.CreatePedestrian(TripRequest, ...)`を使用する。

これによりwalking graph、Building / POI access、crossing、occupancy、route progress、Save、Protocol配信をPopulation側で重複実装しない。Personは`PedestrianId`だけをactive execution referenceとして保持し、Pedestrianが`Arrived`になった時点でactivity stateへ戻す。

## Road Traffic integration

`HasPrivateVehicle` Personでは既存Road RoutingへMotor Tripを問い合わせる。route成立時のみVehicleを生成し、`VehicleId`をPersonへ保持する。route不成立時にPersonを永久待機へせずFoot fallbackへ渡す。

Vehicleのcar-following、intersection、lane occupancy、route progressはRoad Traffic側の責務であり、Population plannerはそれらを操作しない。

## Checkpoint / persistence

`SimulationCheckpoint`へHousehold / Person / next ID stateを追加し、`SimulationWorld.RestoreCheckpoint`でPopulationStoreを再構築する。

Save format 7ではcheckpointと同じauthoritative fieldsをJSON DTOへ写像する。Format 3〜6からのmigrationではPopulation collectionを空として扱い、既存Worldを失敗させない。

active walking/driving Tripを保存した場合は、復元済みPedestrian / Vehicle stable IDとの対応を維持し、次tickから同じarrival state machineを継続する。

## Protocol 2.5

Population messageは可変domain codecを共通`ProtocolCodec`へ詰め込まず、`PopulationProtocolCodec`へ分離する。

Message IDs:

- 4: `InspectPerson` Client -> Server
- 600: `PopulationStatistics` Server -> Client
- 601: `PersonDebug` Server -> Client

`ProtocolFrameHeader.TryRead`でmessage typeを判別し、Population messageだけ専用codecへdispatchする。Protocol 2.4以前のnegotiated connectionにはPopulation messageを送らない。

## Server publish boundary

`PopulationPublishService`は既存Simulation tickとは別のread/publish境界として動作する。

- Protocol 2.5 connectionへ固定長Population statisticsを配信する。
- Person詳細はconnectionが`InspectPerson`でIDを指定した場合だけ取得・配信する。
- `SimulationRuntime`経由でlockされたSimulation read APIを使用する。
- Client切断や個別send failureがSimulation stateを変更しない。

100,000 Personの全詳細配列を毎snapshotで構築しないことを重要な境界とする。

## Web Client boundary

Web Clientは`population-protocol.ts`で2.5 messageをdecodeする。

- status panelへPopulation aggregateを表示する。
- debug inspectorでPerson stable IDを入力すると`InspectPerson`を送信する。
- `PersonDebug`受信時だけresidence / destination / current activity / travel stateを表示する。
- 再接続後はdesired inspected Person IDを再送する。

表示文字列はProtocolへ含めず、activity / travel stateはnumeric enumをWeb側locale resourceで表示する。

## Performance boundary

Population benchmarkは1,000 / 10,000 / 100,000 Personを同じGitHub Actions runner familyで計測し、tick latency、allocated bytes / tick、managed bytesを記録する。

benchmark scenarioは全PersonをHome activityへ置き、交通entity生成コストを混ぜずにPopulation planner / Need update / tick traversalの基礎コストを観測する。交通を伴う統合動作はdeterministic integration testで別途検証する。
