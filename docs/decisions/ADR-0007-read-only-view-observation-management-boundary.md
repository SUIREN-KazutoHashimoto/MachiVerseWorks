# ADR-0007: Read-only View / Observation Gateway / Management Boundary

## Status

Accepted

## Context

MachiVerseWorksではSimulation Coreがauthoritative Worldと全ての意味的処理を所有する。World規模の拡大、複数Viewer、Historical viewing、将来のAnalyticsやManagement UIを考えると、Browser ClientがSimulation内部stateへ直接依存したり、View自身がActivity・ETA・分類・予定・分析結果等を推測したりすると、複数の意味的正本が生まれる。

また、描画ClientへWorld編集・Simulation運転・Save / Load・Server設定等のmutation機能を同居させると、「見ること」と「変更すること」のtrust boundaryが曖昧になる。

View向けread modelのfiltering・subscription・serialization・cacheをClientごとにSimulationへ直接問い合わせ続ける構成は、複数Viewer時の重複処理も増やす。そのため、Simulationとread-only consumerの間に明示的なGateway境界が必要である。

さらにGatewayはSimulation / View双方と独立した実装順・performance課題・reconnect / cache / Protocol adaptation責務を持つため、進捗管理をSimulation Roadmapのcross-cutting Taskへ埋め込まず独立Roadmapとして扱う。

## Decision

MachiVerseWorksの主要実装責務を次のように分離する。

```text
                         read-only
Simulation ─ semantic source ─→ Gateway ──→ View
     ▲                          │
     │ authoritative command    └──→ Management read side
     │
Management ─────────────────────┘

Analytics: 将来、専用Listener / data pipeline / analysis clientとして別責務にする
```

Task状態は **Simulation / Gateway / View / Management の4 Roadmap**で独立管理する。Gateway Roadmapの分離は責務・進捗の分離であり、直ちに別process / repository / deploy unitへ分けることを意味しない。現行では主に`MachiVerseWorks.Server`内へ実装し、module boundaryを先に固定する。

### Simulation

Simulationを唯一の意味的正本とする。

- authoritative World state / rule / state transitionを所有する。
- Activity、Status、分類、ETA、schedule、planned state、semantic event等を必要なdomainで生成する。
- Current / Recent Past / Planned Futureを表示するための意味的情報はSimulation側のsemantic observation source / event / historical projectionとして公開する。
- Gateway / View接続数、Camera、Selection、FPS、Rendering LOD、cacheをSimulation state / fidelity / workload policyの判定条件に使用しない。
- server-authoritative mutation commandの意味・validationを所有する。

### Gateway

Simulationとread-only consumerの間にread-only Gateway責務を設ける。

Gatewayは次を行ってよい。

- Observation Request受付
- subscription / interest management
- detached authoritative source取得
- spatial filtering
- snapshot / delta / chunk planning
- serialization / Protocol adaptation / negotiation
- reconnect / resync
- Entity / Spatial / Static read-model cache
- request deduplication
- encoded payload cache
- slow client isolation

Gatewayは意味的stateを生成・推測・補完・予測してはならない。

cache freshnessは単純なwall-clock TTLだけに依存せず、Simulation由来のtick / revision / generation等を使用する。cacheはSimulationが公開した意味を保存・再利用するだけとする。

Gatewayからauthoritative mutation APIへ到達するrouteを設けない。Observation RequestとManagement commandは同じServer process / Protocol projectを利用しても責務上は別境界とする。

### View

Viewを完全read-onlyな観測・描画層とする。

- World rendering
- Camera / navigation
- Selection / focus / follow
- Inspector
- Current / Recent / Planned表示
- interpolation / animation
- Rendering LOD / culling / presentation cache
- Historical World viewing
- localization

`SubscribeVolume`やInspect系requestはGatewayへ送るObservation Requestであり、World mutation commandではない。

ViewはSimulation stateを変更するAPIを持たず、位置・時刻・destination等からActivity / ETA / classification等を意味的に再計算しない。描画補間やLODはpresentation上の表現でありauthoritative stateではない。

### Management

World / City / Serverを変更するGUI / command clientはManagementとしてView / Gatewayから分離する。

- build / edit / remove
- naming / override
- Simulation pause / resume / step
- Server configuration
- Save / Load
- Addon install / enable / disable / settings
- destructive operation confirmation

Managementはread-only View componentを再利用してよいが、command clientをView / Gateway moduleへ注入してmutation可能にしない。mutationはSimulation側のserver-authoritative command / validation / authorization境界を必ず通る。

command成功後のWorld表示はClient側の推測状態ではなく、Gatewayから再観測したauthoritative stateを正とする。

### Analytics

人口統計、経済分析、交通分析、trend、heatmap等の意味的集計・分析はViewに実装しない。必要になった場合はSimulationから必要なstate / eventを購読する専用Listener / data pipeline / analysis clientとして別責務にする。

## Consequences

### Positive

- Simulationだけが意味的正本になり、Gateway / View実装によるSimulation semanticsの分岐を防げる。
- Gatewayのcache / delivery / reconnect / Protocol adaptationをSimulation domain Phaseと独立して進められる。
- Viewerの有無・Camera・LOD・Gateway cacheによってSimulation結果が変化しないことをarchitecture invariantとして検証できる。
- Gatewayのcache / deduplicationにより、複数Viewerの重複read-model生成やserializationを削減できる。
- View / Gatewayへmutation APIを持たせないため、観測側trust boundaryが明確になる。
- Management / Analyticsを独立進化させてもView責務を膨張させずに済む。
- Simulation / Gateway / Viewはそれぞれ独自Phaseで並行開発でき、利用側は必要なcontract完成時点で追従できる。

### Negative / Trade-offs

- Simulation semantic source、Gateway Observation contract、Management command contractを別々に設計・保守する必要がある。
- 同じProtocol project内でもdomain semanticsとObservation delivery ownershipを区別する必要がある。
- Management UIでView componentを再利用する場合、module boundaryを守るための依存設計が必要になる。
- Current / Recent / Planned等を詳しく表示したい場合、Simulation側に十分なsemantic source / history / schedule contractを用意する必要がある。
- cache invalidation / reconnect / revision整合性はGateway側の独立した設計課題として扱う必要がある。

## Follow-up

- 4 Roadmapの責務・依存関係の索引は[`../../roadmap/README.md`](../../roadmap/README.md)を参照する。
- Simulation側Taskは[`../../roadmap/SIMULATION_ROADMAP.md`](../../roadmap/SIMULATION_ROADMAP.md)を正本とする。
- Gateway側Taskは[`../../roadmap/GATEWAY_ROADMAP.md`](../../roadmap/GATEWAY_ROADMAP.md)を正本とする。
- Observation Gatewayのarchitecture詳細は[`../architecture/observation-gateway.md`](../architecture/observation-gateway.md)を正本とする。
- read-only View側Taskは[`../../roadmap/VIEW_ROADMAP.md`](../../roadmap/VIEW_ROADMAP.md)を正本とする。
- mutation / administration UIは[`../../roadmap/MANAGEMENT_ROADMAP.md`](../../roadmap/MANAGEMENT_ROADMAP.md)を正本とする。
