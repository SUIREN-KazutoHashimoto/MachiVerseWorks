# ADR-0003: Versioned Save Data Boundary

## Status

Accepted

## Context

Simulation内部のmutable data structureをそのままJSON化すると、`AgentStore` やspatial indexの実装変更がSave Data互換性へ直結する。またapplication versionとSave互換性を同一視すると、互換性のない変更と無関係なreleaseを区別できない。

保存データは外部入力でもあるため、不正値をSimulation内部へ直接流し込まず、明示的なvalidation境界も必要になる。

## Decision

保存・復元を次の2層へ分離する。

- `MachiVerseWorks.Simulation` は `SimulationCheckpoint` のcreate / restoreを提供する。
- `MachiVerseWorks.Persistence` はversioned Save Data contract、JSON serialization、validationを担当する。

Save format versionはapplication version / Protocol versionから独立させる。初期Save formatはversion 1とし、未対応versionは拒否する。

PersistenceはSimulationのpublic checkpoint境界だけを使用し、内部Storeをserializerへ公開しない。

Save Dataには翻訳済み表示文字列を保存せず、stable ID / raw numeric state / boolean stateを保存する。

## Consequences

### Positive

- Simulation内部data layoutを変更してもSave formatを独立して維持しやすい。
- Save migrationをversion単位の明示的処理として追加できる。
- 不正な外部データをSimulation invariant検証前に隔離できる。
- locale変更でSave formatを変更する必要がない。
- file pathやUIとserialization責務を分離できる。

### Negative

- checkpoint DTOとSave DTOのmapping codeが必要になる。
- 新しい永続化対象を追加するとSimulation checkpointとSave schemaの両方を更新する必要がある。
- current version以外を読むには将来migration codeが必要になる。

## Notes

Phase 8では最小Worldの保存・復元までを対象とし、autosave、save slot、cloud storage、migration UI、Server commandは将来タスクとする。

詳細は `docs/specifications/save-data.md` と `docs/architecture/persistence.md` を参照する。
