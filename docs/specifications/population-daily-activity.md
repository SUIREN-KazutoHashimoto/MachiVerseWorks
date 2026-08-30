# Population & Daily Activity 仕様

Phase 15で導入するPopulation / Daily Activityのauthoritative contractを定義する。

## 目的

Populationは「どの移動手段で動くか」ではなく「誰が、なぜ、いつ、どこへ移動する必要があるか」を正本化する。

- `Household`が居住地を所有する。
- `Person`がdemographic state、日次schedule、Need、現在activity、移動状態を所有する。
- plannerがSimulation時刻から次のactivity destinationを決め、mode-independentな`TripRequest`を生成する。
- 実際の移動は既存のRoad Traffic / Pedestrian境界へdispatchする。

## Stable IDと所有関係

- `HouseholdId`、`PersonId`、`TripRequestId`は0を使用しない単調増加stable IDとする。
- Householdは1つのresidence `TripEndpoint`を持つ。
- Personは必ず1つのHouseholdに所属し、初期residenceはそのHouseholdのresidenceと一致する。
- `TripEndpoint`はBuildingまたはPOIのどちらか一方を参照する。両方・どちらも指定しないendpointは無効とする。
- Building / POI削除時はPopulationから参照されている対象を黙ってdanglingにしない。

## Person demographic state

最小demographic stateは次を持つ。

- `AgeYears`
- `IsEmployed`
- `IsStudent`
- `HasPrivateVehicle`

このstateは将来の就業・教育・所得・世帯構成の拡張点であり、Phase 15では統計モデルそのものを実装しない。

## Activity / Need

Activity種別:

- `Home`
- `Work`
- `Education`
- `Shopping`
- `Healthcare`
- `Recreation`
- `Errand`

Need種別はActivityと対応する最小集合を持ち、各Needは`Satisfaction`と`DecayPerHour`を保持する。Activity候補の比較ではschedule windowとpriorityを基本とし、Needは将来のdestination policy拡張に利用できるauthoritative stateとして保持する。

Activity priority:

- `Low`
- `Normal`
- `High`
- `Critical`

## Daily schedule

`DailyActivityWindow`は次を持つ。

- activity kind
- start minute of day
- end minute of day
- optional destination
- priority

minute-of-dayは1日のSimulation時刻に対して評価する。`Home`でdestinationが省略された場合はPersonのresidenceを使用する。移動が必要なwindowへ遷移した場合だけTripを生成し、すでに目的地にいる場合はその場でactivityを開始する。

## Trip Requestとmode dispatch

Population plannerが生成する需要の正本は既存`TripRequest`とする。Trip Requestはorigin / destination / requested modeを持つが、Populationのactivity stateはPedestrianやVehicle entityを直接正本にしない。

Phase 15のdispatch policy:

1. `HasPrivateVehicle=true`でMotor routeが成立する場合はRoad TrafficへVehicle Tripとしてdispatchする。
2. Motorを利用できない、またはMotor routeが成立しない場合はFoot TripとしてPedestrianへdispatchする。
3. dispatchされたentity IDと`TripRequestId`をPersonのactive trip stateへ記録する。

この境界によりP16のPedestrian SimulationはPopulation由来Trip Requestを直接受け取れる。

## Person state machine

`PersonTravelState`:

- `AtActivity`
- `Walking`
- `Driving`

基本遷移:

`AtActivity -> planner -> TripRequest -> Walking/Driving -> arrival -> AtActivity`

到着時はdestinationを`CurrentLocation`へ反映し、`DestinationActivity`を`CurrentActivity`へ反映してactive trip / Pedestrian / Vehicle参照を解放する。その後のtickでscheduleを再評価し、必要なら次Tripを生成する。

## Checkpoint / Save Data

Population stateはSimulation checkpointとSave format 7へ含める。

保存対象:

- next Household / Person / TripRequest ID
- Household ID / residence
- Person ID / Household ID / demographics
- residence / current location
- current activity / travel state
- destination / destination activity
- active TripRequest / travel mode / Pedestrian / Vehicle ID
- daily schedule
- Need state

load後はactive Tripを含め、同一fixed-tick入力で継続できることを要求する。Format 6以前はPopulationを空としてmigrationする。

## Statistics / debug delivery

Protocol 2.5でPopulationの観測用contractを追加する。

- `PopulationStatistics`: Household / Person数、travel state別人数、activity別人数、tick count。
- `InspectPerson`: Clientがstable Person IDを指定するdebug request。
- `PersonDebug`: 指定Personのresidence、current location/activity、destination、active Trip / mode / Pedestrian / Vehicle参照を返す。

全Personの詳細stateを毎publishで配列送信しない。集計は固定長message、個別詳細は明示inspection時だけ配信する。

## Determinism / performance

- stable ID順・fixed tickで同じ入力から同じTrip生成とactivity遷移を得る。
- Person plannerはPerson同士の全件相互比較を行わない。
- 1,000 / 10,000 / 100,000 Personでplanner / tick / managed memoryを継続計測する。

## Phase 15の範囲外

- 人口生成・出生・死亡・転居
- 所得・雇用市場・企業によるjob assignment
- 鉄道・公共交通を含むmultimodal mode choice
- zoning / land development
- 市民向け本番UI

これらは後続PhaseでPopulationのstable ID / Trip Request / activity stateを再利用する。
