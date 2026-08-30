# Pedestrian Simulation 仕様

Phase 16で導入する歩行者シミュレーションのauthoritative contractを定義する。

## 目的

- Building / POI間を歩行だけで移動できること。
- Road Networkを正本とし、歩行可能なNetworkを決定的に派生できること。
- fixed tick、3D座標、stable ID、Save / Protocolの既存原則を維持すること。
- 歩行者同士を全組合せ比較せず、1,000〜10,000規模へ拡張可能なhot pathを持つこと。

## Walkable Network

歩行者NetworkはRoad Networkから派生する。独立編集される第二の道路正本にはしない。

- `RoadKind.Highway`は歩行不可。
- それ以外のRoadSegmentは双方向の歩行edgeとして扱う。
- `RoadAccessMode.Foot`を含むRoadAccessPointは歩行者access nodeとなる。
- Building / POIはFoot accessを持つ場合だけ歩行route endpointとして解決できる。
- Building / POIが複数のFoot accessを持つ場合は、その全accessをrouting候補に含める。
- RoadNodeが`Intersection`で、異なるRoadSegment間を移る箇所にはcrossingを派生する。
- XYZをすべて保持し、勾配・高低差をroute lengthと移動へ反映する。

Node / Edge / Crossing IDは元Road IDから決定的に導出し、同一Road Networkからは同じwalking topologyを得る。

## Route

`TripRequest`はPopulation / Trip generationとの境界であり、Phase 16では次だけを要求する。

- stable `TripRequestId`
- origin `TripEndpoint`
- destination `TripEndpoint`
- `TravelMode`

`TripEndpoint`はBuildingまたはPOIのどちらか一方を参照する。歩行者生成時は`Foot`または`Any`のみ許可する。

Walking routeはedge lengthをcostとする決定的な最短経路とする。origin / destinationそれぞれの全Foot accessをmulti-source / multi-target候補とし、その組合せ全体で最短となるrouteを選ぶ。同costの場合もstable ID順のtie-breakにより同じ経路を返す。

## Pedestrian state

各Pedestrianは少なくとも次をauthoritative stateとして持つ。

- stable `PedestrianId`
- `TripRequestId`
- origin / destination
- walking speed
- route leg index
- current leg progress
- movement state
- XYZ position / velocity

Movement stateは次の4種類。

- `Walking`
- `WaitingForCrossing`
- `WaitingForOccupancy`
- `Arrived`

## Fixed-tick movement

Simulation tickごとに`walkingSpeedMetersPerSecond × tickSeconds`だけroute上を進める。1 tickでedge終端を越える場合は残距離を次edgeへ持ち越す。

Crossingが閉じている場合は交差点直前で停止し、開いた次tickから再開する。到着後はvelocityを0とし、Pedestrian自体は明示削除されるまでstable stateとして残る。

Pedestrianのtick処理順はstable ID順とする。通常tickでは全Pedestrianの配列化・再sortを行わず、作成時から保持しているstable orderを再利用する。

## Occupancy constraint

歩行者同士の最低限の重なり抑制はedge上を0.75m単位のoccupancy binへ量子化して行う。

- tick開始時に`(edgeId, bin)`へ占有を登録する。
- `bin`はedgeのstable node IDが小さい側を共通原点とし、逆方向routeのprogressは`edgeLength - progress`へ正規化する。
- 同一binへ複数が競合した場合は小さいPedestrian IDを優先する。
- 移動先binが占有済みなら`WaitingForOccupancy`になる。
- 全歩行者の全組合せ距離比較は行わない。

これにより同じ双方向edgeを逆向きに歩くPedestrianでも、同じ物理位置は同じbinへ写像される。

これはPhase 16の簡易密度制約であり、群集力学や連続的なpersonal-space modelは将来拡張とする。

## Spatial subscription

Pedestrian positionは専用3D spatial indexへ同期する。

- 生成時に登録する。
- tick移動時にcellを更新する。
- 削除時にindexから除去する。
- checkpoint restore時に再登録する。
- `WorldVolume` snapshotはindexから候補を取得し、全Pedestrian走査を行わない。

## Road mutation

walking topologyはRoad Networkから派生するため、Pedestrianが保持するrouteを無効化するRoadNode / RoadSegment / RoadAccessPoint変更は、Pedestrianが存在する間は拒否する。Pedestrianが存在しない場合はderived networkをdirty化し、次回利用時に再構築する。

## Crossing permission

Crossingのopen / closedはmutable authoritative stateである。`SetPedestrianCrossingOpen`で変更したpermissionはcheckpoint / Saveに含め、復元後も同じcrossing permissionを適用してからPedestrianを継続する。

## Save / Protocol / Web

- Save format 5はPedestrian ID、Trip endpoint、mode、speed、route progress、movement state、crossing permissionを保持する。
- Protocol 2.2は`PedestrianSpawn` / `PedestrianUpdate` / `PedestrianRemove`を追加する。
- Serverは既存の3D volume subscriptionをPedestrianにも適用し、Pedestrian spatial indexからvolume内候補だけを取得する。
- Web ClientはPedestrian stateを補間し、InstancedMeshで描画する。

## 非目標

Phase 16では次を扱わない。

- 詳細な群集シミュレーション
- 歩道幅や横断歩道polygonの高精度geometry
- 階段 / elevator / indoor navigation
- Population生成そのもの
- mode choiceや自動車との完全な優先制御

Population / Trip generationが後続実装された際は`TripRequest`境界へ接続する。
