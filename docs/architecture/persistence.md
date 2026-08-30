# Persistence Architecture

## 責務

保存・復元を2つの境界へ分ける。

1. `MachiVerseWorks.Simulation` が in-memory の `SimulationCheckpoint` を作成・復元する。
2. `MachiVerseWorks.Persistence` が checkpoint と versioned Save Data JSON を相互変換し、保存・読込のresource limitと外部データvalidationを担当する。

Simulation内部のStore、spatial index、routing / traffic派生index、random generator実装をJSON serializerへ直接公開しない。

## 依存方向

```text
MachiVerseWorks.Persistence
          ↓
MachiVerseWorks.Simulation
```

Simulation は Persistence を参照しない。

将来Serverへsave/load commandやファイルI/Oを追加する場合、実行ホストがPersistence APIを呼び出す。Persistence自体は保存先path、Web UI、HTTP、WebSocketを所有しない。

## SimulationCheckpoint

checkpoint は保存時点の継続実行に必要なauthoritative stateを保持する。

- Simulation config
- tick count / elapsed time
- deterministic random state
- Agent stateとnext ID
- Building / POI stateとnext ID
- RoadNode / RoadSegment / Lane / LaneConnection / RoadAccessPoint stateとnext ID
- Pedestrian state / crossing permissionとnext ID
- Vehicle dimensions / performance / Route / progress / movement stateとnext ID

削除済みAgentはcheckpointへ残す。Building / POI / Road / Pedestrian / Vehicleは各Storeのlifecycle契約に従い、next IDを保持してstable ID再利用を防ぐ。各Storeの内部collectionを外部へ渡すのではなく、immutable valueの配列としてcopyする。

復元時は新しい `SimulationWorld` と各Storeを構築する。authoritative stateをvalidationした後、次の派生stateを再構築する。

- Agent / Pedestrian spatial index
- Road Traffic topology
- Lane occupancy index
- Routing cache
- Pedestrian walking graph
- Intersection movement / conflict graph
- fixed-cycle Signal phase

fixed-cycle Signal phaseは`TickCount` / `TickRate` / Road topologyの純粋な派生stateである。独立phase offsetをcheckpointへ二重保存しない。

## Save Data resource limits

`WorldSaveLimits`はSave Dataのwrite/read両方へ同じ意味で適用する。

- `MaximumBytes`: serialized UTF-8 Save Data全体の最大byte数
- entityごとの最大件数: Agent / Building / POI / RoadNode / RoadSegment / Lane / LaneConnection / RoadAccessPoint / Pedestrian / PedestrianCrossing / Vehicle

Default `Serialize` / `Save` / `Deserialize` / `Load` はすべて `WorldSaveLimits.Default` を使用する。この対称性により、default APIで保存に成功したSaveがdefault loadのresource limitだけを理由に拒否される状態を作らない。

write側はcollection数をserialization前に検証する。JSONは`MaximumBytes`を総allocation上限とする分割bufferへ直接serializeし、次のwriteで上限を超える時点で`InvalidDataException`として停止する。oversizedなJSON全体を一度確保してから判定しない。`Serialize`はlimit内で完成したbufferだけを最終byte配列へ変換し、`Save(Stream, ...)`は全serialization成功後にdestinationへ転送するため、limit超過時に部分データを書かない。

read側はbyte上限をJSON materialization前に検証し、各大規模collection件数を`Utf8JsonReader`でDTO生成前にscanしたうえでDTO→checkpoint変換前にも再確認する。

custom limitで保存した場合、読込側にも同等以上のlimitを明示する責務はcallerにある。

## Save Data validation

`WorldSaveSerializer` はJSONを内部DTOへ読み込み、次の順で扱う。

1. byte数とcollection件数のresource limitを事前検証する。
2. JSON構造をstrictにdecodeする。
3. Save format versionを確認する。
4. 必須fieldを確認する。
5. DTOを`SimulationCheckpoint`へ変換する。
6. `SimulationWorld.RestoreCheckpoint` がSimulation invariant、stable ID、Road/Lane reference、POI/Building reference、Pedestrian route、Vehicle route / occupancyを検証する。
7. authoritative stateからrouting / walking / traffic / intersection controlを再構築する。
8. 全validation成功時だけ復元Worldを返す。

未知fieldは拒否し、malformed dataやSimulation invariant違反は `InvalidDataException` として呼び出し側へ返す。

## Format evolution

`SaveFormatVersion.Current` をSave Data互換性の正本とし、currentはFormat 10とする。

- Format 3: Building / POI state。
- Format 4: Road Network state。
- Format 5: Pedestrian state / crossing permission。
- Format 6: Vehicle state。
- Format 7: Population state。
- Format 8: Railway Infrastructure state。
- Format 9: Railway Operations state。
- Format 10: Multimodal Transit / Journey / Passenger / Taxi state。

Format 3〜9は不足する後続stateを空として明示的にmigrationし、current checkpointへ変換する。Format 2以前とcurrentより新しい未知versionは拒否する。Format 10ではRoad/Rail/Population復元後にMultimodal Transitを復元し、Lane / Station / Platform / Railway Service / Road Vehicle / active Trip参照を検証する。

Phase 14はformatを増やさない。Intersection movement / conflict / fixed-cycle phaseはFormat 6がすでに保存するRoad topology、`tickCount`、`tickRate`から再構築できるためである。`IntersectionControlSaveTests`がSave round trip後のcontroller phase / indication一致を固定する。

将来adaptive Signalがdetector履歴、manual offset、preemption stateなど独立mutable stateを持つ場合は、新しいSave fieldとformat versionを追加する。

migrationを追加する場合は、旧DTO → current DTO/checkpoint の明示的な変換として追加し、application versionからmigration可否を推測しない。

## Locale boundary

Save Dataのvalueには翻訳済みUI文字列を持たない。property名はschema識別子であり、ユーザー向け表示文言ではない。

kind / movement state / Road kind等はnumeric raw value、参照はstable IDとして保存する。locale、翻訳済みlabel、localized error textを保存しないことを自動テストで固定する。
