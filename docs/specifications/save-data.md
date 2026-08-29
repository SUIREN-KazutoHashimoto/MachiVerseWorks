# Save Data 基盤仕様

MachiVerseWorks のauthoritativeな`SimulationWorld`を停止点から同じ状態へ復元するSave Data契約を定義する。

## 目的

- authoritative な Simulation 状態を保存・復元できること。
- save → load 後も Agent ID、tick、乱数系列を継続できること。
- application version や表示localeに依存しない保存契約を持つこと。
- 将来の migration 判断に使える独立した Save format version を持つこと。
- defaultの保存APIで生成できたデータはdefaultの読込APIで復元できること。

## Save format version

current formatは `formatVersion = 2` とする。

Save format version はルート `VERSION` のapplication version、およびProtocol versionとは独立する。current以外のversionは暗黙migrationせず安全に拒否する。

Format 2はネイティブ3D Simulation契約に合わせ、Agentのposition / velocityをXYZ全成分で必須保存する。Format 1の2D payloadはFormat 2として解釈しない。

## Version 2 が保持する情報

トップレベル:

- `formatVersion`
- `simulation`

`simulation`:

- `tickRate`
- `seed`
- `spatialCellSize`
- `tickCount`
- `elapsedTicks`
- `randomState`
- `nextAgentId`
- `agents`

各 Agent:

- `id`
- `x`
- `y`
- `z`
- `velocityX`
- `velocityY`
- `velocityZ`
- `isActive`

`isActive = false` の Agent も保持する。これは削除済みIDを再利用せず、`TotalCreatedAgentCount` と次回生成IDを保存前と同じ意味で継続するためである。

`randomState` はseedだけではなく保存時点のdeterministic random generator状態を表す。これによりload後に新規Agentを生成した場合も、保存しなかった場合と同じ乱数系列を継続できる。

時間は固定tick durationで進む。したがって `elapsedTicks` は `tickCount × SimulationConfig.TickDuration.Ticks` と一致しなければならず、両者を独立した任意値として扱わない。

## JSON例

```json
{
  "formatVersion": 2,
  "simulation": {
    "tickRate": 30,
    "seed": 1,
    "spatialCellSize": 64,
    "tickCount": 120,
    "elapsedTicks": 39999960,
    "randomState": 1663341875487337578,
    "nextAgentId": 3,
    "agents": [
      {
        "id": 1,
        "x": 10.5,
        "y": 20.25,
        "z": 4.0,
        "velocityX": 0.5,
        "velocityY": -0.25,
        "velocityZ": 0.0,
        "isActive": true
      },
      {
        "id": 2,
        "x": 30,
        "y": 40,
        "z": -12,
        "velocityX": 0,
        "velocityY": 0,
        "velocityZ": 0,
        "isActive": false
      }
    ]
  }
}
```

## 保存・読込の資源上限

Save Dataは構造が正しくても無制限には扱わない。`WorldSaveLimits` の既定値は次とする。

- 最大UTF-8 Save Dataサイズ: 128 MiB
- 最大Agent数: 1,000,000

同じ`WorldSaveLimits`契約をwrite/readの両方へ適用する。

### Serialize / Save

- Agent数上限はJSON生成前にcheckpointへ適用する。
- serialized UTF-8 byte数が上限を超えた場合は`InvalidDataException`で失敗する。
- `Save(Stream, ...)`は全体がlimit内であることを確認してからdestinationへ書き込むため、limit超過で部分Saveを残さない。
- 引数を省略したdefault `Serialize` / `Save`は`WorldSaveLimits.Default`を使用する。

### Deserialize / Load

- byte上限はJSON parse前に適用する。
- Stream入力は上限を超えて無制限にbufferしない。
- Agent数は`Utf8JsonReader`によるallocation-freeなtoken scanでDTO deserialization前に検証し、上限を超えた時点で拒否する。
- DTOからcheckpoint配列へ変換する前にも同じ上限を再確認する。
- 引数を省略したdefault `Deserialize` / `Load`は`WorldSaveLimits.Default`を使用する。

したがって、default `Serialize` / `Save`が成功した出力は、同じformat versionのdefault `Deserialize` / `Load`のresource limitによって拒否されない。custom limitを使用して保存した場合は、読込にも同等以上のlimitを明示する。

## 保存しない情報

Save Format 2には次を保存しない。

- application version
- Protocol version
- locale tag
- 翻訳済みUIラベル / エラーメッセージ
- Web Clientのcamera / connection / subscription状態
- Audio Client状態
- Serverの接続一覧やWebSocket状態
- benchmark / diagnostics値

表示文字列ではなく raw value / stable ID を保存する。ユーザー入力名など将来追加される非翻訳データは、その機能の仕様策定時に別途契約を定義する。

## 読込時の拒否条件

少なくとも次は不正Save Dataとして拒否する。

- JSONとして不正
- configured byte上限超過
- configured Agent数上限超過
- current以外の`formatVersion`
- 必須field欠落
- 未知field
- 無効なSimulation設定値
- 負のelapsed time
- `tickCount` と `elapsedTicks` が固定tick durationから導かれる値として一致しない状態
- 次の1 tickで`tickCount`またはelapsed timeがoverflowする状態
- 0のAgent ID
- 重複Agent ID
- `nextAgentId` が保存済みAgent ID以下
- 非finiteなXYZ座標 / XYZ速度

不正Save Dataから部分的にWorldを構築して使用しない。全体のvalidation成功後に復元Worldとして返す。
