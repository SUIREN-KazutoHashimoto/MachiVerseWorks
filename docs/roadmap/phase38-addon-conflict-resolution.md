# Phase 38 — Extension Platform & Localization Addon Platform Update

Phase 38は、単なるsolver差し替え機構ではなく、MachiVerseWorksを今後Addon-firstで拡張していくための公開Extension Platformとして完成させる。
小規模で独立した機能は原則Addonとして実装し、Core変更は汎用概念・安定したExtension Point・Save / Protocol / Security / Performance等の基盤変更に限定する。

## 開発方針

新しい機能を実装する際は、まずAddonとして表現できるかを検討する。

```text
新機能
  ↓
Addonとして実装可能か？
  ├─ Yes → Addonとして実装
  └─ No  → 汎用Extension API / Extension PointをCoreへ追加
                  ↓
              Addonとして実装
```

Coreへ直接追加するのは、特定機能そのものではなく、複数Addonから再利用できる汎用概念・公開API・Extension Point・互換性基盤を優先する。

例:

- Core: `Establishment`, `Building`, `Job`, `Commodity`, `Recipe`, `ConsumerDemand`
- Addon: Pizza Shop, Ramen Shop, Cafe, Bakery

- Core: `TransportEntity`, `MovementProvider`, `RouteProvider`, `Capacity`, `EnergySource`, `Cargo`, `Passenger`
- Addon: Drone Transport, Flying Car, Advanced Traffic

## Addon分類

少なくとも以下の3種類を正式な利用モデルとして扱う。

### Content Addon

既存の汎用概念を組み合わせて、新しいコンテンツを追加する。

例:

- Pizza Shop
- Convenience Store
- Fire Station
- Industry / Commodity / Recipe追加
- Building / POI / Job追加

Content Addonはstable ID / namespaceが衝突しない限り、複数Addonの共存を既定とする。

### Replacement Addon

既存のprovider / solver / rule / systemを差し替える。

例:

- 自動車移動をDrone Transportへ置換
- 標準Radio propagation solverを高精度solverへ置換
- 標準Utility solverを詳細物理solverへ置換

Replacement AddonはCore内部を書き換えず、versionedなExtension Point経由でproviderを選択する。

### View Addon

Simulation semanticsを変えず、見た目・情報表示・操作UIを拡張または置換する。

例:

- 車両3D model差し替え
- Building model / material差し替え
- Rendering theme
- Map layer / overlay
- Inspector / Panel / Tool追加

View overrideとSimulation replacementは別契約として扱う。

## Public Extension API

公式AddonとCommunity Addonは同じPublic Extension APIだけを使用する。
公式Addon専用の内部APIアクセスを設けない。

Addonは `MachiVerseWorks.Simulation.Internal.*` 等の内部namespaceへ依存せず、公開された `MachiVerseWorks.Extensions.*` / `MachiVerseWorks.PublicApi.*` 相当の境界だけを参照する。

将来的にCIで内部namespaceへのAddon依存を検出・拒否できるようにする。

## Extension Lifecycle / Hooks

Addonのライフサイクルとイベント境界をversioned public APIとして提供する。

少なくとも以下を扱えるようにする。

- discovery
- validation
- dependency resolution
- load
- enable
- disable
- unload / shutdown
- failure state
- ordered hook / event registration

Hook / Eventはdeterministicな順序を持ち、同じAddon構成から同じSimulation結果を得られることを保証する。

## View Extension Points

Web / Rendering層で少なくとも以下を拡張可能にする。

- model override / resolver
- material / texture override
- rendering layer
- map overlay
- inspector section
- panel
- tool
- command / menu contribution

例:

```text
vehicle.car.default
      ↓
Model Resolver
      ↓
Core default-car.glb
      ↑ override
View Addon realistic-japanese-cars
```

View resource IDはstable IDとして扱い、Addonがfilesystem pathへ直接依存しないようにする。

## Extension Settings / Configuration

Addon固有設定をCore設定と衝突しないnamespace付きconfigurationとして保存・取得できるPublic APIを実装する。

- Addon IDごとのsettings namespace
- default value / schema / validation
- restart-required設定とruntime変更可能設定の区別
- UIからの設定表示境界

## Enable / Disable / Failure Isolation

Addonの存在と有効状態を分離する。

```text
Installed
Enabled
Disabled
Failed
Required by Save
Missing
Incompatible
```

1つのAddon validation / load failureが、可能な範囲で他の独立AddonやCore全体の破損へ波及しないfailure isolationを設計する。

ただしcode extensionは原則sandboxではないことを明示し、完全なprocess隔離を保証するものとはしない。

## Addon Package Format

Repositoryをinstall unitにせず、Addon packageを配布・installation unitとする。

推奨package形式:

```text
example.pizza-1.4.0.mvaddon
├─ manifest.json
├─ bin/
├─ assets/
│  ├─ models/
│  └─ textures/
├─ locales/
└─ LICENSE
```

`.mvaddon` はzip系archiveとして実装可能だが、format version / manifest / checksum / signature metadataを持つ独立した公開契約とする。

Repository / Addon / Package / Versionは別概念として扱う。

```text
Git Repository ≠ Addon ≠ Package ≠ Release Version
```

## Manifest

少なくとも以下を宣言可能にする。

- `id`
- `name`
- `version`
- `apiVersion`
- `dependencies`
- `optionalDependencies`
- `conflicts`
- `provides`
- `overrides`
- `replaces`
- `permissions` / `capabilities`
- package metadata

例:

```json
{
  "id": "example.pizza",
  "version": "1.4.0",
  "apiVersion": "1",
  "dependencies": [],
  "permissions": [
    "content.register",
    "simulation.extend",
    "view.model.register"
  ]
}
```

## Capabilities / Permissions

Addonが要求する能力をmanifestで明示する。

例:

- `content.register`
- `simulation.extend`
- `simulation.replace`
- `view.model.override`
- `view.layer.register`
- `view.panel.register`

Install / Enable時にPublisher・requested capability・code/data-only区分を表示できるようにする。

Data-only AddonとCode Addonを分け、Code Addonは任意コード実行能力を持ち得る非sandbox extensionであることを明示する。

## Extension Point Contract

Extension Pointは少なくとも以下のmodeを持つ。

- `Single` — 同時に1 providerのみ有効
- `Multiple` — 複数登録可能
- `Ordered Multiple` — 複数登録可能だがdeterministicな順序を要求

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

## Conflict Resolution

Addon競合はruntime開始後ではなく、Addon構成確定時に可能な限り検出する。

- missing dependency
- dependency cycle
- incompatible version
- explicit `conflicts`
- duplicate stable ID
- exclusive Extension Point conflict
- override target conflict
- invalid / ambiguous priority
- deterministic ordering不能なOrdered Multiple

解決不能な競合がある場合はSimulationを開始せず、stable error codeと構造化された競合情報を返す。

Simulation replacementでは「最後にloadされたAddonが勝つ」を禁止する。

```text
Core Transport Provider
      ↑ replace
Drone Transport
      ↑ replace
Flying Car Transport

=> conflict
```

View overrideはSimulation semanticsを変更しない範囲で明示priority解決を許可する。

```text
vehicle.car.default

1. Winter Vehicle Pack
2. Japanese Vehicle Pack
3. Realistic Vehicle Pack
4. Core Default
```

filesystem / repository列挙順へ依存しない。

## Integration Addon

2つのAddonを直接相互依存させず、必要に応じてIntegration Addonで橋渡しできるようにする。

```text
Weather Addon
     │
     ├─ Weather-Drone Integration
     │
Drone Transport Addon
```

Integration Addonは両Addonをdependency / optional dependencyとして宣言し、連携ロジックだけを担当する。

## Save Data

Addon固有Save DataはAddon stable ID namespaceへ隔離する。

```text
save
├─ core
└─ addons
   ├─ example.weather
   ├─ example.drone-transport
   └─ example.pizza
```

Saveには必要なAddon ID / version compatibility情報を保持する。

missing / incompatible Addonがある場合は、暗黙にAddon stateを破棄せず、`Required by Save` / `Missing` / `Incompatible` として明示する。

## Protocol Boundary

Extension固有wire typeをCore Protocol namespaceへ無制限に追加しない。
Extension message / payloadはversioned envelope・namespace等の公開境界を通し、Addon同士のwire type衝突を避ける。

翻訳済み文字列はProtocol / Save / Simulation正本へ保存せず、stable code / structured parameterをView側でlocalizeする。

## Official Addon Repository Strategy

公式Addonは1 Addon = 1 Repositoryを強制しない。

推奨構成:

```text
MachiVerseWorks

MachiVerseWorks-Addons
└─ addons/
   ├─ pizza/
   ├─ drone-transport/
   ├─ realistic-vehicles/
   ├─ weather/
   └─ japanese-buildings/
```

`MachiVerseWorks-Addons` は公式Addon monorepoとして運用し、各Addonは独立manifest / version / package / releaseを持つ。

1 repository内に複数Addonが存在しても、build / test / package / release単位はAddonごとに分離できるようにする。

## Core CloneとOfficial Addon

開発環境ではCore repositoryから公式Addon monorepoを取得できるようにする。

推奨構成:

```text
MachiVerseWorks/
├─ src/
├─ tests/
└─ extensions/
   └─ official/   # Git submodule -> MachiVerseWorks-Addons
```

標準developer cloneは以下を推奨する。

```bash
git clone --recurse-submodules <repo>
```

setup scriptも提供し、plain clone後でもofficial addon submoduleを初期化できるようにする。

Runtime起動時にnetwork経由でGit cloneする設計にはしない。

```text
Git / Setup → Official Addon取得
Runtime     → 既に存在するAddonをvalidation / load
```

## Release Distribution

Git submoduleはsource management用とし、end-user releaseではCore + Official Addonを1つの配布物へbundleする。

```text
dist/
├─ server/
├─ web/
└─ extensions/
   └─ official/
      ├─ pizza/
      ├─ weather/
      └─ ...
```

Official AddonもCommunity Addonも同じPublic Extension API・manifest・package format・conflict ruleを使用する。

## Addon Install Location

Community / User AddonはCore installation directoryへ直接書き込まない。

```text
MachiVerseWorks installation
├─ Core
├─ Server
└─ Web

User Data
└─ addons/
   ├─ example.pizza/
   │  └─ 1.4.0/
   └─ example.drone/
      └─ 2.0.1/
```

Core upgrade後もUser Addonを保持できる構成にする。

## Addon Manager

User-facing Addon Managerを提供する。

主要View:

```text
Addon Manager

Installed
Official
Community
Updates
```

少なくとも以下を扱う。

- install
- uninstall
- enable
- disable
- update
- compatibility status
- dependency status
- conflict status
- publisher / trust information
- requested capabilities

## Install Sources

少なくとも3つの導入経路を想定する。

### Install from File

`.mvaddon`を直接指定する。offline / manual distribution / development release向け。

### Install from URL

GitHub Release等のURLからpackageを取得する。
Repository sourceを直接cloneして実行せず、公開された`.mvaddon` artifactをdownloadしてvalidation後にinstallする。

### Addon Registry / Source

将来的にOfficial / Community source indexを登録できるようにする。

```text
Addon Sources

Official
Community A
Community B
```

Source indexはAddon ID / version / package URL / checksum / publisher metadata等を返す。

GitHub repositoryはsource管理・distribution元として利用できるが、runtime installation unitは`.mvaddon` packageとする。

## Dependency Resolver

Addon installation / enable時にdependency graphを解決する。

例:

```text
Japanese Pizza Shops
       ↓ requires
Pizza Industry
       ↓ requires
Food Industry Framework
```

必要なdependencyを自動提示し、version range / dependency cycle / incompatible dependencyをvalidationする。

## Developer Mode

Addon developer向けにpackage生成なしでlocal directoryをlinkできるDeveloper Modeを提供する。

```text
Addon Manager
→ Developer Mode
→ Link Development Addon
→ /path/to/MyAddon/
```

通常利用では`.mvaddon`をinstallし、local directory loadは明示的Developer Modeに限定する。

## Localization

既存Phase 38 Localization方針を維持する。

- default localeは`ja-JP`
- additional locale resource pack
- locale fallback
- number / date / unit / plural formatting
- stable error code + structured parameterからlocalized UIを生成
- translation key validation
- additional locale E2E

Addonも自身のlocale resourceをpackageへ含められ、Core translation key namespaceと衝突しないようAddon ID namespaceを使用する。

## Phase 38 Task更新案

既存P38-001〜P38-019を基礎に、以下をPhase 38へ追加・再整理する。

- **P38-020** — Addon-first開発方針とCore / Addon責務境界をarchitecture / contributor policyとして仕様化する
- **P38-021** — Content / Replacement / View Addonの公開分類と必要な登録契約を定義する
- **P38-022** — Extension lifecycle / enable / disable / failure state / ordered hook APIを実装する
- **P38-023** — Web Viewのmodel / material / layer / overlay extension pointを実装する
- **P38-024** — Panel / Inspector / Tool / command contribution extension pointを実装する
- **P38-025** — Extension settings / configuration APIとAddon ID namespaceを実装する
- **P38-026** — Addon enable / disable / failure isolation / status modelを実装する
- **P38-027** — `.mvaddon` package format・manifest packaging・integrity metadata契約を定義する
- **P38-028** — Official / Community / File / URLを扱えるAddon source abstractionを実装する
- **P38-029** — Addon ManagerのInstalled / Official / Community / Updates UIとinstall / uninstall / enable / disable / update操作を実装する
- **P38-030** — User Data側Addon installation rootとCore installationからの分離を実装する
- **P38-031** — dependency resolverとdependency auto-install proposalを実装する
- **P38-032** — capability / permission表示、data-only / code extension trust UIを実装する
- **P38-033** — local directoryを明示的Developer Modeでlinkするdevelopment addon loaderを実装する
- **P38-034** — Official Addon monorepo / Core submodule / setup scriptのdevelopment integration contractを整備する
- **P38-035** — Release artifactへOfficial Addon packageをbundleし、Core versionと対応Addon revision / package versionを追跡する
- **P38-036** — Extension Pointに `Single` / `Multiple` / `Ordered Multiple` cardinality contractを定義する
- **P38-037** — manifestへ `optionalDependencies` / `conflicts` / `provides` / `overrides` / `replaces` 契約を追加する
- **P38-038** — version / dependency / explicit conflict / Extension Point conflictを起動前に検出するConflict Resolverを実装する
- **P38-039** — Simulationの排他的replacementで暗黙load-order勝者を禁止し、明示provider selectionを実装する
- **P38-040** — View resource overrideへdeterministic priority resolutionを実装する
- **P38-041** — Integration Addonが複数Addonをdependency / optional dependencyとして橋渡しできる契約を実装する
- **P38-042** — Addon Managerへ競合理由・対象Extension Point・解決候補を表示するConflict UIを実装する
- **P38-043** — Official AddonとCommunity Addonが同じPublic API / package / resolver / conflict ruleで動作するintegration testを追加する
- **P38-044** — Content追加・Simulation replacement・View override・Save Data・UI・localizationを含む実用sample addon E2Eを追加する
- **P38-045** — Addon install / update / dependency / conflict / startup / memory / package validation benchmarkを追加する
- **P38-046** — Addon author guide / package guide / View extension guide / compatibility policy / trust policyを整備する
- **P38-047** — Phase 38全体のarchitecture / ADR / ROADMAPを同期し、Addon-first開発へ移行可能であることをcloseoutする

## Phase 38 完了条件への更新

- MachiVerseWorks Coreを変更せず、Public Extension APIだけで実用Addonを作成できる。
- Content Addonで新規Building / Establishment / Industry等を追加できる。
- Replacement AddonでSimulation provider / solver / ruleを安全に差し替えられる。
- View Addonでmodel / material / overlay / panel / inspector / toolを追加・置換できる。
- Addon固有Save Data / Protocol payload / settings / localeがstable ID namespaceで衝突しない。
- Official AddonとCommunity Addonが同じPublic Extension API・package format・trust / conflict ruleに従う。
- `.mvaddon`をFile / URL / Sourceからinstallでき、Addon Managerからinstall / uninstall / enable / disable / updateできる。
- AddonはCore installationと分離したUser Dataへ保存でき、Core upgrade後も保持できる。
- dependency / incompatible version / conflict / missing required AddonをSimulation開始前に検出できる。
- 複数Extensionが同じ排他的Extension Pointを要求した場合、暗黙load orderで勝者を決めない。
- View overrideは明示priorityでdeterministicに解決できる。
- Ordered Hook / Eventは同じAddon構成からdeterministicな順序を得る。
- Integration Addonにより独立Addon同士を本体変更なしで連携できる。
- Official Addon sourceをCore development checkoutから取得でき、releaseではCoreと対応するOfficial Addonがbundleされる。
- 少なくとも1つの実用sample addonがSimulation追加/差し替え、View拡張、Save Data、設定、localizationを含めてCore変更なしにbuild / package / install / enableできる。

Phase 38完了後の通常機能開発では、「まずAddonで実装し、不足する汎用Extension PointだけをCoreへ追加する」ことを標準開発フローとする。
