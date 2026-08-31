# Railway Operations Architecture

## Boundary

Railway OperationsはRailway Infrastructure上に構築するmutable Simulation subsystem。TrackNode / TrackSegment / TrackConnection / BlockSection / Station / Platform / Depotを参照するが、それらのtopology ownershipを持たない。

`SimulationWorld.RailwayOperations`がpublic façade、`RailwayOperationsStore`がFormation / Route / Timetable / Service / Trainとruntime Block / Platform owner indexを所有する。

Railway Operationsを一度初期化した後はInfrastructure mutationを拒否する。Operations storeは初期化時のInfrastructure snapshotからroute/block/platform/depot参照indexを構築するため、後からInfrastructureだけを書き換えてstale operations viewを作ることを許可しない。

## Authoritative / derived state

Definitionはstable IDで1回保存し、Route construction時にTrack geometryをordered route step / cumulative distanceへ解決する。各route stepはそのTrackSegmentが所属する`BlockSectionId?`も解決する。Service construction時にTimetable stopをroute distanceへ解決する。

Train mutable stateはroute distance、pose、speed、movement state、Block / Platform / Depot reference、dwell departure tick。Service stateはlifecycle、delay、next-stop index、Train reference。

`_blockOwners` / `_platformOwners`はTrain mutable stateから導出するruntime indexで、restore時に再構築してduplicate ownershipを拒否する。

## Tick pipeline

`SimulationWorld.Step()`内でstable Train ID順に処理する。lifecycle activation、dwell release、Platform look-ahead、braking、acceleration/deceleration、Block transition、route integration、Station arrival、delay update、Depot completionをsingle-thread deterministic orderで行う。

wall clock / async callback / Web stateはSimulation decisionに参加しない。

## Block contention / representative-point model

`_blockOwners`は`BlockSectionId -> TrainId`のexclusive resource index。ただしowner判定が表すのはTrain body envelopeではなく、**Trainの`RouteDistanceMeters`でsampleされる代表点が現在属するBlock**である。

Route stepにはnullable `BlockId`を持たせ、movement integrationでstep boundaryを越える前に次を行う。

1. next stepのBlockがcurrent Blockと異なり、nonnullならnext Blockをreserveする。
2. reserveできなければboundaryまででmovementを打ち切り`WaitingForBlock`にする。
3. reserveできた、またはnext stepがBlock未所属ならcurrent Blockをreleaseする。
4. `CurrentBlockId`をnext stepのBlockまたは`null`へ更新する。

この処理をmovement integration中のboundaryごとに行うため、高速Trainが1 tickで複数stepを跨ぐ場合も同じ順序を適用する。

`TrainFormation.LengthMeters`はこのowner indexやboundary transitionに入力されない。したがってTrain代表点が次Blockへ入った瞬間にprevious Blockは解放され、Formation後端がgeometry上previous Blockへ残るかは判定しない。これはPhase 18の意図した簡略modelであり、rear-clearance signalingではない。

Block未所属stepでは`BlockId == null`、Trainも`CurrentBlockId == null`になる。`_blockOwners`へ登録されないため、同じBlock未所属TrackSegment上に複数Trainが存在してもBlock contentionは発生しない。Block-based separationを必要とする運行区間はInfrastructure側でBlockSectionへ所属させる必要がある。

そのためOperations layerが提供する安全invariantは「同じ**nonnull BlockSection owner**を複数Trainが同時に持たない」まで。Train全長の占有、rear clearance、braking-distance interlocking、Block未所属Trackの衝突回避は別modelである。

## Platform contention

`_platformOwners`もexclusive resource index。reserveはcurrent ownerに対してidempotentで、別Train所有ならstop targetを越えない。

Platform assignmentはTrain route point / stop distanceを基準にし、Formation body lengthはplatform occupancy geometryへ使用しない。

## Delay pipeline

Station arrivalでだけ次を行う。

`DelayTicks = max(DelayTicks, max(0, actualArrival - plannedArrival))`

Dwell departureは`max(plannedDeparture + DelayTicks, actualArrival + minimumDwell)`。departure自体ではDelayTicksを更新しない。そのためadditional dwell / downstream waitは後続arrivalで初めてdelayへ反映され、Phase 18のDelayTicksはmonotonicで回復しない。

この意味はWeb Railway Debugの`plannedArrival + DelayTicks` projectionにも直接使われる。

## Persistence

Save Format 9でRailway Operationsを導入し、current Format 10でも同sectionを保持する。restoreはInfrastructure → Operationsの順。

Route / Timetable stop distanceを復元Infrastructureへ再validationし、Train materialization後にowner indexをrebuildする。`CurrentBlockId`は代表点ベースのownerだけを保存し、Formation bodyの占有区間は保存・再構築しない。Format 8以前はempty operations + next ID 1へmigrationする。

## Publish model

`SimulationRuntime.CapturePublishSnapshot()`がSimulation gate内でTrain snapshotとRailway Operations snapshotをcaptureし、`SimulationPublishSnapshot`がTrain positionのspatial indexを持つ。

per-client filteringはcapture後。visibility keyは**Train position point**で、Formation lengthやrender proxy boundsではない。mapperはvisible Trainと、そのTrainが参照するService / Timetableだけをmessageへ含める。

## Protocol boundary

Protocol 2.7 message 710。header + fixed Train / Service + variable Timetable Stop layout。

single-frame contractで、`RailwayOperationsProtocolCodec.GetPayloadLength()`がencoded payloadをallocation前に算出する。`RailwayOperationsSnapshotMessagePlanner`が1 MiB上限をpreflightし、oversize時はsubscription-local `InvalidRequest` / `railwayOperationsSnapshotTooLarge`を返す。

static Railway Infrastructure message 700はrevision-driven + chunk可能、dynamic Operations 710はtick-driven + single-frameという異なるdelivery contractを持つ。

## Web boundary

`RailwayOperationsLayer`はstable Train ID → Three.js Mesh mapを持ち、snapshotのposition / forwardを直接applyする。snapshot内に存在しないTrain meshを除去する。

Protocol 2.7はFormation physical dimensionsを含まないため、現在のgeometryは共通`BoxGeometry(18, 3, 3)`。debug visibility用proxyで、authoritative train lengthではない。

Railway Debugのnext arrivalはTimetableの`plannedArrivalTick + service.delayTicks`。kinematic ETAではなくschedule projection。Phase 19 Multimodal Transitの`estimatedArrivalTick`とはUI上も意味を分離する。

## Verification

- Simulation: Route / ownership / lifecycle / delay / checkpoint continuation
- Block contract regression: long Formationでもroute pointのboundaryでprevious Blockをreleaseすること
- Block contract regression: Block未所属Trackで複数Trainが`CurrentBlockId == null`のまま同時走行できること
- Persistence: Format 9+ / Format 8 migration
- Protocol: 2.7 roundtrip / malformed payload / 1 MiB boundary
- Server: point spatial filter / related definition mapping / oversize error planning
- Web: fixed proxy pose / mesh lifecycle / schedule projection
- E2E: complete two-Train operating cycle
- Benchmark: 100 / 1,000 Train-Service fixed-tick + snapshot scaling
