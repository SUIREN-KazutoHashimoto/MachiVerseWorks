# Railway Operations Specification

## Purpose

Phase 18で導入したdeterministic Train operationを定義する。Railway OperationsはFormation、Route、Timetable、Service、Train movement、Block ownership、Platform assignment、delay、Depot lifecycleを所有する。

Track / Station / Platform / Depot topologyはRailway Infrastructureが正本で、Operationsはstable ID参照だけを持つ。

## Stable IDs / definitions

Formation / RailwayRoute / Timetable / RailwayService / Trainはmonotonic `ulong` stable ID。0 invalid。Checkpoint / Saveはnext-ID counterも保存する。

- Formation: length、max speed / acceleration / service deceleration、capacity
- RailwayRoute: explicit TrackConnection / TrackDirectionに従うordered TrackSegment sequence
- Timetable: ordered Station stop、planned arrival/departure、minimum dwell、optional preferred Platform
- RailwayService: Formation / Route / Timetable / origin Depot / destination Depot / planned start
- Train: Serviceを実行するmutable physical state。1 Serviceにつき高々1 Train

Preferred Platformのfallback semantics等、実装変更を伴う未確定事項はこの文書整理では変更せず、既存runtime contractを別Issueで扱う。

## Fixed-tick movement

Trainは`SimulationWorld.Step()`だけで進行する。wall clockはauthoritativeでない。stable Train ID順に処理するため同じinput/stateではcontention解決もdeterministic。

Trainはroute distance、3D position/forward、speed、movement state、Block / Platform / Depot reference、dwell departure tickを持つ。target speedはFormation上限とTrackSegment speed limitで制約し、加減速後のRoute distanceから3D poseをsampleする。

## Block separation

各TrackSegmentは任意のBlockSectionへ所属する。OperationsはBlockごとに単一Train ownerを持つ。

次Blockへ入る前にreserveし、別Train所有ならboundary前で停止して`WaitingForBlock`。next Block取得後にprevious Blockをreleaseする。2 Trainが同一Blockを同時所有しない。

## Station / Platform

次Timetable stopへ接近するとRoute上のeligible Platformを選び、reserveできなければstop手前で待つ。assignment後はPlatform center distanceへbrakeし、arrivalで`Dwelling`へ遷移する。

Platform ownershipはexclusive。departure時にPlatformをreleaseして次stopへ進む。

## Delay semantics

`RailwayService.DelayTicks`は**arrival時だけ更新する単調非減少値**である。

stopへtick `actualArrivalTick`で到着したとき:

```text
arrivalDelay = max(0, actualArrivalTick - plannedArrivalTick)
DelayTicks   = max(previous DelayTicks, arrivalDelay)
```

つまりPhase 18では遅延回復を表現せず、一度記録した最大arrival delayを後続stopへ持ち越す。

Dwell departure tickは到着時に次で決める。

```text
delayedPlannedDeparture = plannedDepartureTick + DelayTicks
minimumDwellDeparture    = actualArrivalTick + minimumDwellTicks
DwellDepartureTick       = max(delayedPlannedDeparture, minimumDwellDeparture)
```

**departure時には`DelayTicks`を再計算しない。** minimum dwellやPlatform/Block待ちによってplanned departureよりさらに遅れても、その追加遅延は次のStation arrivalで初めて`DelayTicks`へ反映される。

## Depot lifecycle

Serviceは`Planned`、Trainはorigin Depotから開始する。planned start以降、first Blockを取得できたらdepartureしてService `Active`。

全Timetable stop完了後Route endpointまで進み、destination Depotでremaining ownershipをreleaseしてTrain / Serviceを`Completed`へする。

## Checkpoint / Save

Railway OperationsはSave Format 9で導入され、current Format 10でも同じoperations sectionを保持する。Formation / Route / Timetable / Service / Train、next IDs、mutable Train stateを保存する。

restoreはRailway Infrastructureの後。Route topology、Station/Platform/Depot references、Train ownershipを再検証し、Block / Platform owner indexをTrain stateから再構築する。Format 8以前は空operationsへmigrationする。

## Protocol / Server

Protocol 2.7 message 710 `RailwayOperationsSnapshot`はtick、visible Train、関連Service、関連Timetableを持つ。Protocol 2.6以下へ送らない。

ServerのTrain visibilityは`TrainSnapshot.Position`という**1点**をClient `WorldVolume`へ照合する。Formation length/body envelopeとの交差判定ではない。長いTrainの一部だけがvolumeに入るケースを現2.7 subscription contractは表現しない。

message 710はsingle-frame。payloadは1 MiBまでで、Serverが`GetPayloadLength()`でpreflightする。超過時はpartial snapshotを送らず`InvalidRequest` / `railwayOperationsSnapshotTooLarge`を対象subscriptionへ返す。

## Web rendering

WebはTrain position / forwardをsnapshotから直接Three.js meshへ適用する。Protocol 2.7はFormation definition / actual train lengthを配信しないため、現在のTrain meshは**固定18 × 3 × 3のdebug proxy**である。physical Formation lengthを表すvisual contractではない。

## Railway Debug next-arrival semantics

Railway Debugの次到着表示は、Serviceの次Timetable stopに対して次を表示する。

`projectedArrivalTick = plannedArrivalTick + DelayTicks`

これはschedule-based projectionで、Train position / speed / braking distance / current Block・Platform waitから連続再計算するrealtime ETAではない。arrival間で新しい待ちが発生しても`DelayTicks`が更新される次arrivalまでprojectionへ現れない場合がある。

Phase 19の[`multimodal-transit.md`](multimodal-transit.md)が提供する`estimatedArrivalTick`はBus等のTransit Debug用arrival estimateであり、このRailway timetable projectionとは別contractである。

## Determinism / verification

検証対象:

- explicit Route / direction / connection validation
- exclusive Block / Platform ownership
- deterministic Train ID processing
- arrival-only monotonic delay semantics
- checkpoint / Save continuation
- Protocol 2.7 roundtrip / oversize preflight
- Serverのpoint-based Train spatial filtering
- Web fixed debug mesh / schedule projection
- E2Eのmovement / dwell / delay / completion
