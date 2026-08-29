# Persistence Architecture

## 責務

Phase 8 では保存・復元を2つの境界へ分ける。

1. `MachiVerseWorks.Simulation` が in-memory の `SimulationCheckpoint` を作成・復元する。
2. `MachiVerseWorks.Persistence` が checkpoint と versioned Save Data JSON を相互変換し、外部データをvalidationする。

Simulation内部の `AgentStore`、`SpatialIndex`、random generator等をJSON serializerへ直接公開しない。

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
- 全created AgentのID / position / velocity / active state

削除済みAgentもcheckpointへ残す。`AgentStore` の内部listやdictionaryを外部へ渡すのではなく、immutable valueの配列としてcopyする。

復元時は新しい `SimulationWorld`、`SpatialIndex`、`AgentStore` を構築し、active Agentだけをspatial indexへ再登録する。

## Save Data validation

`WorldSaveSerializer` はJSONを内部DTOへ読み込み、次の順で扱う。

1. JSON構造をstrictにdecodeする。
2. Save format versionを確認する。
3. 必須fieldを確認する。
4. DTOを`SimulationCheckpoint`へ変換する。
5. `SimulationWorld.RestoreCheckpoint` がSimulation invariantを検証する。
6. 全validation成功時だけ復元Worldを返す。

未知fieldは拒否し、malformed dataやSimulation invariant違反は `InvalidDataException` として呼び出し側へ返す。

## Format evolution

`SaveFormatVersion.Current` をSave Data互換性の正本とする。

Phase 8ではversion 1だけを読み書きし、別versionは拒否する。将来migrationを追加する場合は、旧DTO → current DTO/checkpoint の明示的な変換として追加し、application versionからmigration可否を推測しない。

## Locale boundary

Save Data v1 のvalueには文字列を持たない。property名はschema識別子であり、ユーザー向け表示文言ではない。

locale、翻訳済みlabel、localized error textを保存しないことを自動テストで固定する。
