# Phase 38 Addon Conflict Resolution

Phase 38 — Extension Platform & Localization において、Addon同士の競合は例外ではなく通常発生し得るものとして扱う。
Simulation差し替え、View override、Hook、Content追加など性質の異なる拡張を同じload-order規則だけで処理せず、Extension Pointごとの契約と起動前validationで安全に解決する。

## 設計原則

- Addon競合はruntime開始後に発覚させず、Addon構成確定時に可能な限り検出する。
- 単純な「後からloadされたAddonが勝つ」をSimulation replacementの既定挙動にしない。
- Extension Pointは `Single` / `Multiple` / `Ordered Multiple` のcardinalityを持つ。
- Simulation replacementは厳格に扱い、排他的なproviderを複数Addonが要求した場合は明示解決を要求する。
- View overrideはSimulation stateを変更しない範囲で、明示的priorityによる解決を許可できる。
- Content追加はstable ID / namespaceが衝突しない限り複数Addonを共存可能にする。
- Hook / Event listenerはdeterministicな順序を定義し、同一Addon構成から同一結果を得る。
- 公式AddonとCommunity Addonは同じPublic Extension API / conflict ruleに従う。

## Extension Point Contract

Extension Pointは少なくとも以下のmodeを持つ。

- `Single` — 同時に1 providerのみ有効。Simulation solver / replacement等。
- `Multiple` — 複数登録可能。Content / View panel / overlay等。
- `Ordered Multiple` — 複数登録可能だがdeterministicな順序を必要とするHook / Event等。

例:

```text
simulation.transport.movement-provider  Single
simulation.radio.propagation-solver     Single
view.model.override                     Single/Priority
view.overlay                            Multiple
view.panel                              Multiple
simulation.event-listener               Ordered Multiple
content.establishment                   Multiple
```

## Manifest Conflict Contract

Extension manifestで以下を宣言可能にする。

- `dependencies`
- `optionalDependencies`
- `conflicts`
- `provides`
- `overrides`
- `replaces`

例:

```json
{
  "id": "example.drone-transport",
  "version": "2.0.0",
  "dependencies": [
    { "id": "official.transport", "version": ">=2.0" }
  ],
  "optionalDependencies": [
    { "id": "example.weather", "version": ">=1.0" }
  ],
  "conflicts": [
    "example.flying-cars"
  ],
  "replaces": [
    "simulation.transport.movement-provider"
  ]
}
```

## Conflict Resolver

Addon load前に少なくとも以下をvalidationする。

- missing dependency
- dependency cycle
- incompatible version
- explicit `conflicts`
- duplicate stable ID
- exclusive Extension Point conflict
- override target conflict
- invalid priority / ambiguous priority
- deterministic ordering不能なOrdered Multiple

解決不能な競合がある場合はSimulationを開始せず、stable error codeと構造化された競合情報を返す。

## Simulation Replacement

Simulationの単一providerを複数Addonが置換する場合は暗黙のload orderで勝者を決めない。

```text
Core Transport Provider
      ↑ replace
Drone Transport
      ↑ replace
Flying Car Transport

=> conflict
```

Addon Managerまたは設定でどちらを有効にするか明示的に決定する。

## View Override Priority

Simulation semanticsを変更しないView resource overrideは明示priorityで解決可能にする。

```text
vehicle.car.default

1. Winter Vehicle Pack
2. Japanese Vehicle Pack
3. Realistic Vehicle Pack
4. Core Default
```

priorityが同一で解決不能な場合は警告または競合として扱い、暗黙のfilesystem / repository順には依存しない。

## Integration Addon

2つの独立Addonを直接結合させず、必要に応じてIntegration Addonで連携できるようにする。

```text
Weather Addon
     │
     ├─ Weather-Drone Integration Addon
     │
Drone Transport Addon
```

Integration Addonは両方をdependencyまたはoptional dependencyとして宣言し、両Addon固有の連携だけを担当する。

## Save Data

Extension固有Save DataはAddon stable ID namespaceへ隔離する。

```text
save
├─ core
└─ addons
   ├─ example.weather
   ├─ example.drone-transport
   └─ example.pizza
```

Saveには必要なAddon ID / version compatibility情報を保持し、load時に `Required by Save` / `Missing` / `Incompatible` を判定できるようにする。

## Addon Manager Conflict UI

Addon Managerでは少なくとも以下を表示する。

- conflicting Addon
- conflict type
-対象Extension Point / resource ID
- required dependency / incompatible version
- user-selectable resolutionが存在する場合の候補

Simulation replacement等の安全に自動解決できない競合は、ユーザー選択なしで勝者を決めない。

## Phase 38 Task追加案

既存 `P38-008` のload order / dependency validationを基礎に、以下を独立Taskとして追加する。

- **P38-020** — Extension Pointに `Single` / `Multiple` / `Ordered Multiple` cardinality contractを定義する
- **P38-021** — manifestへ `optionalDependencies` / `conflicts` / `provides` / `overrides` / `replaces` 契約を追加する
- **P38-022** — version / dependency / explicit conflict / Extension Point conflictを起動前に検出するConflict Resolverを実装する
- **P38-023** — Simulationの排他的replacementで暗黙load-order勝者を禁止し、明示的provider selectionを実装する
- **P38-024** — View resource overrideへdeterministic priority resolutionを実装する
- **P38-025** — `Ordered Multiple` Hook / Event handlerのdeterministic ordering contractを実装する
- **P38-026** — Integration Addonが複数Addonをdependency / optional dependencyとして橋渡しできる契約を実装する
- **P38-027** — Addon Managerへ競合理由・対象Extension Point・解決候補を表示するConflict UIを実装する
- **P38-028** — conflicting replacement / View priority / multiple Hook / dependency cycle / optional integrationを検証するdeterministic E2Eを追加する

## Phase 38 完了条件への追加

- 複数Extensionが同一の排他的Extension Pointを要求した場合、暗黙のload orderで勝者を決めず、Simulation開始前に競合を検出できる。
- 共存可能なExtension Pointでは複数Addonをdeterministicな順序で実行できる。
- View overrideは明示priorityで解決でき、filesystem順・repository順等の非契約な順序へ依存しない。
- Addon間dependency / optional dependency / conflict / replacement関係をmanifestから検証できる。
- Integration Addonにより、独立Addon本体へ相互依存を埋め込まず機能連携を追加できる。
- Addon構成・load orderの違いから意図しないSimulation nondeterminismを発生させない。
