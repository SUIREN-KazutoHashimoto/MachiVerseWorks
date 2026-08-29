# Save Data 基盤仕様

Phase 8 では、都市全体の将来仕様を先取りせず、現在の `SimulationWorld` を停止点から同じ状態へ復元できる最小 Save Data を定義する。

## 目的

- authoritative な Simulation 状態を保存・復元できること。
- save → load 後も Agent ID、tick、乱数系列を継続できること。
- application version や表示localeに依存しない保存契約を持つこと。
- 将来の migration 判断に使える独立した Save format version を持つこと。

## Save format version

初期形式は `formatVersion = 1` とする。

Save format version はルート `VERSION` の application version、および Protocol version とは独立する。Phase 8 では migration は実装せず、current version 以外は安全に拒否する。

## Version 1 が保持する情報

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
- `velocityX`
- `velocityY`
- `isActive`

`isActive = false` の Agent も保持する。これは削除済みIDを再利用せず、`TotalCreatedAgentCount` と次回生成IDを保存前と同じ意味で継続するためである。

`randomState` は seed だけではなく保存時点の deterministic random generator 状態を表す。これによりロード後に新規Agentを生成した場合も、保存しなかった場合と同じ乱数系列を継続できる。

Version 1 の時間は固定tick durationで進む。したがって `elapsedTicks` は `tickCount × SimulationConfig.TickDuration.Ticks` と一致しなければならず、両者を独立した任意値として扱わない。

## JSON例

```json
{
  "formatVersion": 1,
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
        "velocityX": 0.5,
        "velocityY": -0.25,
        "isActive": true
      },
      {
        "id": 2,
        "x": 30,
        "y": 40,
        "velocityX": 0,
        "velocityY": 0,
        "isActive": false
      }
    ]
  }
}
```

## 読込時の資源上限

外部Save Dataは構造が正しくても無制限には受け入れない。`WorldSaveLimits` の既定値は次とする。

- 最大UTF-8入力サイズ: 128 MiB
- 最大Agent数: 1,000,000

byte上限はJSON parse前に適用する。Stream入力は上限を超えて無制限にbufferしない。Agent数は`Utf8JsonReader`によるallocation-freeなtoken scanでDTO deserialization前に検証し、上限を超えた時点で拒否する。DTOからcheckpoint配列へ変換する前にも同じ上限を再確認する。

テストや将来のhost要件では明示的な`WorldSaveLimits`を指定できるが、上限を引き上げる場合は利用可能メモリと想定都市規模を考慮する。

## 保存しない情報

Phase 8 version 1 には次を保存しない。

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
- 非finiteな座標 / 速度

不正Save Dataから部分的にWorldを構築して使用しない。全体のvalidation成功後に復元Worldとして返す。
