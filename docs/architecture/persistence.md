# Persistence Architecture

## 責務

保存・復元を2つの境界へ分ける。

1. `MachiVerseWorks.Simulation` が in-memory の `SimulationCheckpoint` を作成・復元する。
2. `MachiVerseWorks.Persistence` が checkpoint と versioned Save Data JSON を相互変換し、保存・読込のresource limitと外部データvalidationを担当する。

Simulation内部の `AgentStore`、`BuildingStore`、`PoiStore`、`SpatialIndex`、random generator等をJSON serializerへ直接公開しない。

## 依存方向

```text
MachiVerseWorks.Persistence
          ↓
MachiVerseWorks.Simulation
```

Simulation は Persistence を参照しない。

将来Serverへsave/load commandやファイルI/Oを追加する場合、実行ホストがPersistence APIを呼び出す。Persistence自体は保存先path、Web UI、HTTP、WebSocketを所有しない。

## SimulationCheckpoint

checkpoint は保存時点の継続実行に必要な状態を保持する。

- Simulation config
- tick count / elapsed time
- deterministic random state
- next Agent ID
- 全created AgentのID / XYZ position / XYZ velocity / active state
- next Building ID
- 全BuildingのID / kind / native 3D bounds
- next POI ID
- 全POIのID / kind / native 3D position / optional Building ID

削除済みAgentはcheckpointへ残す。Building / POIは削除済みentryを残さず、next IDを保持してID再利用を防ぐ。各Storeの内部collectionを外部へ渡すのではなく、immutable valueの配列としてcopyする。

復元時は新しい `SimulationWorld` と各Storeを構築する。Agentはactive entryだけを3D spatial indexへ再登録し、Building / POIは全ID・kind・bounds / position・参照整合性のvalidation成功後にstoreへ復元する。

## Save Data resource limits

`WorldSaveLimits`はSave Dataのwrite/read両方へ同じ意味で適用する。

- `MaximumBytes`: serialized UTF-8 Save Data全体の最大byte数
- `MaximumAgentCount`: checkpoint / Save Dataに含められる最大Agent数
- `MaximumBuildingCount`: checkpoint / Save Dataに含められる最大Building数
- `MaximumPoiCount`: checkpoint / Save Dataに含められる最大POI数

Default `Serialize` / `Save` / `Deserialize` / `Load` はすべて `WorldSaveLimits.Default` を使用する。この対称性により、default APIで保存に成功したSaveがdefault loadのresource limitだけを理由に拒否される状態を作らない。

write側はAgent / Building / POI数をserialization前に検証する。JSONは`MaximumBytes`を総allocation上限とする分割bufferへ直接serializeし、次のwriteで上限を超える時点で`InvalidDataException`として停止する。oversizedなJSON全体を一度確保してから判定しない。`Serialize`はlimit内で完成したbufferだけを最終byte配列へ変換し、`Save(Stream, ...)`は全serialization成功後にdestinationへ転送するため、limit超過時に部分データを書かない。

read側はbyte上限をJSON materialization前に検証し、`agents` / `buildings` / `pois`の各件数は`Utf8JsonReader`でDTO生成前にscanしたうえでDTO→checkpoint変換前にも再確認する。

custom limitで保存した場合、読込側にも同等以上のlimitを明示する責務はcallerにある。

## Save Data validation

`WorldSaveSerializer` はJSONを内部DTOへ読み込み、次の順で扱う。

1. byte数とcollection件数のresource limitを事前検証する。
2. JSON構造をstrictにdecodeする。
3. Save format versionを確認する。
4. 必須fieldを確認する。
5. DTOを`SimulationCheckpoint`へ変換する。
6. `SimulationWorld.RestoreCheckpoint` がSimulation invariantとPOI→Building参照整合性を検証する。
7. 全validation成功時だけ復元Worldを返す。

未知fieldは拒否し、malformed dataやSimulation invariant違反は `InvalidDataException` として呼び出し側へ返す。

## Format evolution

`SaveFormatVersion.Current` をSave Data互換性の正本とし、currentはFormat 3とする。

- Format 2はXYZ Agent position / velocityを必須とするネイティブ3D契約。
- Format 3はBuilding / POI stateと`nextBuildingId` / `nextPoiId`を追加する。
- Format 1 / 2を暗黙変換しない。

将来migrationを追加する場合は、旧DTO → current DTO/checkpoint の明示的な変換として追加し、application versionからmigration可否を推測しない。

## Locale boundary

Save Dataのvalueには翻訳済みUI文字列を持たない。property名はschema識別子であり、ユーザー向け表示文言ではない。

Building / POI kindはnumeric raw value、参照はstable IDとして保存する。locale、翻訳済みlabel、localized error textを保存しないことを自動テストで固定する。
