# MachiVerseWorks 開発ルール

このファイルは、ChatGPT / Codex を含む開発エージェントと人間の開発者が、MachiVerseWorks で作業するときの共通ルールです。

## 1. 基本方針

- ドキュメント、Issue、PR の説明は原則として日本語で記述する。
- クラス名、メソッド名、API 名などのコード識別子は英語を基本とする。
- 仕様を推測で変更しない。仕様変更が必要な場合は、関連ドキュメントも同時に更新する。
- 既存の責務分離を崩して短期的に動かすだけの実装を避ける。
- 大規模シミュレーションを前提とし、ホットパスでの不要な allocation、LINQ、全件走査、Agent ごとの Task 作成を安易に導入しない。
- 最適化は必ず計測可能な根拠を持って行う。

## 2. アーキテクチャ境界

### MachiVerseWorks.Simulation

都市シミュレーションの唯一の意味的正本です。

- HTTP、WebSocket、ASP.NET Core、ブラウザ固有 API に依存しない。
- World、Agent、Traffic、Transit、Logistics、Powerなどの状態とruleを保持する。
- Activity、Status、分類、ETA、schedule、state transition、semantic event等の意味的処理はSimulation側で完結させる。
- 外部からは明確な command / step / semantic observation source / checkpoint 境界を通して操作・観測する。

### MachiVerseWorks.Persistence

versioned Save Data と Simulation checkpoint の変換・検証を担当します。

- Save format version を application version / Protocol version とは独立して管理する。
- Simulation 内部の mutable Store を直接 serializer へ公開しない。
- Save Data へ locale 依存表示文字列を持ち込まず、stable ID / raw value / state を保存する。
- World のルールや tick を所有せず、Simulation の正本性を奪わない。
- ファイル配置、ユーザー操作、HTTP 等の外部I/O方針は実行ホスト側の責務とする。

### Gateway

GatewayはSimulationとread-only consumerの間にある**完全read-onlyな観測境界**です。現時点では主に`MachiVerseWorks.Server`内へ実装しますが、責務・進捗は独立したGateway Roadmapで管理します。

- Observation Request、subscription、spatial filtering、snapshot / delta / chunk deliveryを担当する。
- Entity / Spatial / Static cache、request deduplication、encoded payload cache、reconnect / resyncを担当できる。
- Simulation内部の可変データをネットワーク処理から直接参照し続けず、detached authoritative sourceを利用する。
- Activity、ETA、分類、予定、semantic event等の意味的stateを生成・推測・補完・再計算しない。
- GatewayからAdministration / Management mutation APIへ到達するrouteを持たせない。
- Camera / Selection / View接続数 / Gateway cache状態をSimulation workload / fidelity / ruleの判定条件に使用しない。

GatewayのTask状態は`roadmap/GATEWAY_ROADMAP.md`、architectureは`docs/architecture/observation-gateway.md`を正本とする。

### MachiVerseWorks.Server

実行ホストと通信境界です。

- Simulation Core のライフサイクルと tick を管理する。
- **Gateway** と **Administration / Management command boundary** を分離してhostする。
- Gateway側はread-only observation、Administration / Management側はauthoritative mutationを扱う。
- transport / network処理がSimulationの意味的正本にならない。

### MachiVerseWorks.Protocol

クライアント・サーバー間契約です。

- message type、version、binary layout、control message を管理する。
- read-only Observation Requestとauthoritative mutation commandを区別する。
- domainの意味・field / unitはSimulation側authoritative contract、Observation control / delivery envelope / negotiationはGateway責務としてRoadmap ownershipを分ける。
- Simulation の内部データ構造をそのまま公開 API にしない。
- 後方互換性が必要になった場合に protocol version を独立して管理できる構成にする。

### View

Viewは**完全read-only**な観測・描画層です。

- Gatewayから受け取った状態を忠実に描画する。
- Camera、Selection、Inspector、Historical viewing、Rendering LOD、interpolation等を担当する。
- Simulationの正本にならない。
- View側で意味的state、分類、予定、ETA、分析結果等を推測・補完・再計算しない。
- `SubscribeVolume`やInspect系requestは観測対象を指定するだけとし、World mutationを行わない。
- Viewの存在・非存在、接続数、Camera、Selection、FPS、LOD、cacheでSimulation結果を変えない。
- View moduleからAdministration / Management mutation APIへ到達させない。

### Management

ManagementはWorld / City / Serverを明示的に変更するUI / command clientです。

- build / edit / remove
- naming / override
- simulation pause / resume / step
- Server configuration
- Save / Load
- destructive operation confirmation

read-only View componentをManagement画面で再利用してよいが、mutation責務をView / Gateway moduleへ持ち込まない。commandはServerのauthoritative command境界から実行し、結果のWorld表示はGatewayから再観測したauthoritative stateを正とする。

### Analytics

人口統計、経済分析、交通分析、trend、heatmap等の分析処理はViewへ実装しない。必要になった場合は専用Listener / data pipeline / analysis clientとして別責務で設計する。

## 3. ディレクトリルール

- `src/`: 実行コード
- `tests/`: 自動テスト
- `benchmarks/`: 性能評価コード
- `docs/product/`: 目的・概念・用語
- `docs/architecture/`: 実装アーキテクチャ（How）
- `docs/specifications/`: シミュレーション仕様（What / Why）
- `docs/development/`: 開発・テスト・Git 運用
- `docs/decisions/`: ADR
- `docs/roadmap/`: Phaseを補足する詳細設計・検討資料。Task状態の正本にはしない
- `docs/archive/`: 廃止済み資料・Legacy 資料・実験記録
- `roadmap/`: 領域別の実装ロードマップ。Simulationは`roadmap/SIMULATION_ROADMAP.md`、Gatewayは`roadmap/GATEWAY_ROADMAP.md`、Viewは`roadmap/VIEW_ROADMAP.md`、Managementは`roadmap/MANAGEMENT_ROADMAP.md`を正本とする
- `scripts/`: 開発・CI 補助スクリプト
- `tools/`: 独立した開発支援ツール

ドキュメントをルートへ無秩序に追加しない。ルートへ置くのは、`README.md`、ライセンス・貢献・開発ルールなど、リポジトリ全体の入口として必要なファイルに限定する。

## 4. ドキュメントルール

- 仕様（What / Why）と設計（How）を同じ文書へ混在させすぎない。
- 現行仕様は `docs/specifications/` を正とする。
- 技術構成や責務分離は `docs/architecture/` を正とする。
- 採用理由を将来説明する必要がある設計判断は `docs/decisions/` に ADR を作成する。
- Phaseの詳細な検討メモを残す場合は `docs/roadmap/` を利用できるが、進捗・Task状態は必ずルート `roadmap/` の正本へ反映する。
- 廃止した資料は削除ではなく、参照価値がある場合のみ `docs/archive/` へ移す。
- `archive` を未整理ファイルの一時置き場として使わない。
- 将来の予定や作業状態は仕様書へ混ぜず、`roadmap/` 配下の領域別ロードマップで管理する。
- Simulation / Gateway / View / Managementの責務を変更した場合は4 Roadmapと主要READMEを同時に同期する。
- 文書を移動・改名した場合は、README・開発ルール・CI・他Markdownからの相対リンクを更新し、`python scripts/check-markdown-links.py` またはCIのMarkdown link validationでリンク切れがないことを確認する。

## 5. Git 運用

通常開発では次の流れを基本とする。

```text
main
  └─ develop
       └─ feature/* / fix/* / perf/* / docs/* / refactor/* / experiment/*
```

標準の短命ブランチ名は次の通りとする。

- `feature/<topic>`: 新機能
- `fix/<topic>`: 不具合修正
- `perf/<topic>`: 性能改善
- `refactor/<topic>`: 振る舞いを変えない構造改善
- `docs/<topic>`: 文書・公開設定
- `experiment/<topic>`: 採用未確定の実験

- 通常の実装は短命な作業ブランチで行う。
- `develop` への統合は PR を使用する。
- リリースは `develop` から `main` への PR を使用する。
- PR の標準マージ方式は merge commit とし、通常は squash / rebase merge を使用しない。
- GitHub が PR マージ時に生成する merge commit は管理上の統合コミットとして扱う。
- Application `VERSION` はPRやmerge commitの回数に連動させず、Release時だけ更新する。
- マージ済みの短命ブランチは原則として削除する。
- 実験コードは実験ブランチに閉じ込め、採用しない場合は本流へ混ぜない。
- PR をマージする前に、必要な build / test / benchmark / static analysis を確認する。

詳細は `docs/development/git-workflow.md` と `docs/development/repository-settings.md` を参照する。

## 6. バージョン運用

ルート `VERSION` はGit運用の番号ではなく、**公開成果物のRelease versionの唯一の正本**とする。

- 通常のfeature / fix / perf / refactor / docs作業では原則として`VERSION`を変更しない。
- worker branchや`develop`向けPRをmergeしても、Releaseを決めるまでは既存`VERSION`を維持してよい。
- `develop`向けPRだから`B+1`、`main`向けPRだから`A+1`、通常コミットだから`C+1`といったbranch / commit依存のincrement規則は設けない。
- Releaseとして公開するversionを決めたときだけ、`VERSION`を意図した`A.B.C`へ明示的に変更する。
- `develop -> main`のRelease PRには、公開したいversionが設定済みの状態で含める。
- A / B / Cのどこを変更するかはReleaseの互換性・規模・公開方針で決定し、Git branch名から機械的に決めない。
- 公開済みversionを別内容のReleaseへ再利用しない。

通常CIは`VERSION`の存在と`A.B.C`形式だけを検証し、PR baseとのversion transitionは強制しない。

C# Server / Web Client など各成果物は `VERSION` から値を取得し、個別に同じバージョン文字列を手管理しない。Protocol version と Save format version は互換性の意味が異なるため、アプリケーションバージョンとは独立して管理する。

詳細は `docs/development/versioning.md` を参照する。

## 7. 完了条件

作業を Done とするには、対象に応じて次を満たすこと。

- 実装またはドキュメント変更が完了している。
- 必要な build / test / benchmark が成功している、またはnon-blocking benchmarkの未解決事項が明記されている。
- 仕様を変更した場合は関連ドキュメントが更新されている。
- 新しい設計判断が重要な場合は ADR が追加または更新されている。
- 一時的なデバッグコード、不要なログ、実験用フラグが本流に残っていない。
- 対応する`roadmap/SIMULATION_ROADMAP.md`、`roadmap/GATEWAY_ROADMAP.md`、`roadmap/VIEW_ROADMAP.md`、`roadmap/MANAGEMENT_ROADMAP.md`の対象Taskがある場合は、実際の完了状態と状態記号が一致している。
- Markdownを追加・移動・改名した場合は、ローカルリンクとheading anchorの検証が成功している。
- 実装変更は対象コンポーネント1つに限定し、必要な他コンポーネント作業はIssueへ切り出している。

## 8. エージェント向け注意

- **1 AGENT = 1コンポーネント**を原則とする。1つの作業でSimulation / Gateway / Server / Protocol / Persistence / View / Managementの複数コンポーネントを跨いで実装しない。
- 作業開始時に対象コンポーネントを1つ決める。Gatewayは物理的に`src/server/`内へ実装されていても、責務上は独立した対象コンポーネントとして扱う。
- 対象コンポーネントの変更によって別コンポーネントの修正が必要になった場合、同じAGENTが続けて修正しない。必要な変更・理由・依存関係をIssueへ追加し、別AGENTへ引き継ぐ。
- Protocolなどの共有contract変更も、Protocol側の作業として分割する。利用側コンポーネントと同じAGENTが便宜上まとめて変更しない。
- Repository全体のCI、運用ルール、共通ドキュメントだけを変更する作業は`Repository-wide tooling/docs`として扱える。この例外を機能実装の跨ぎ変更に使用しない。
- PR本文には対象コンポーネントと、必要な他コンポーネントfollow-up Issueを明記する。
- 依頼されていない破壊的変更、ブランチ削除、Release 削除、Repository 設定変更を勝手に行わない。
- PR のマージは、ユーザーが明示的に依頼した場合、または現在の作業指示から明確にマージまで求められている場合のみ行う。
- 既に会話やリポジトリから判明している情報を再質問しない。
- 大きな変更では、実装前に既存構造と関連コードを調査する。
- 変更は可能な限り論理的にまとまった単位で行う。

## 9. 多言語対応の前提

現時点では日本語を主言語として開発するが、将来の localization を壊さないため次を守る。

- default locale は `ja-JP` とし、locale tag は BCP 47 形式で扱う。
- Simulation Core の状態へ翻訳済み UI 文言を持ち込まない。
- Gateway / Protocol の正式契約へ日本語や英語などの翻訳済みエラーメッセージを埋め込まず、stable code と structured parameter を使用する。
- Save Data には翻訳済みラベルではなく stable ID / enum / code / raw value を保存する。
- ユーザー向け表示文言の localization と数値・日時・単位 formatting は Client presentation 層の責務とする。
- locale resource は `src/view/locales/` を正規入口とする。
- 本実装開始後は、固定 UI 文言を可能な限り locale resource key 経由で参照する。
- 翻訳文を単語単位でコード上で連結せず、named parameter を持つ message として扱う。
- ユーザー入力文字列、固有名詞、外部コンテンツはシステム UI 文言と区別する。
- i18n library は Web presentation 実装開始時に選定し、初期セットアップ段階では固定しない。

詳細は `docs/architecture/localization.md`、`docs/development/localization-guidelines.md`、`docs/decisions/ADR-0002-localization-boundary.md` を参照する。read-only Viewの実装計画と進捗は `roadmap/VIEW_ROADMAP.md` のLocalization Phaseを正本とし、Management固有UIは`roadmap/MANAGEMENT_ROADMAP.md`で管理する。

## 10. Roadmap 運用

`roadmap/SIMULATION_ROADMAP.md`、`roadmap/GATEWAY_ROADMAP.md`、`roadmap/VIEW_ROADMAP.md`、`roadmap/MANAGEMENT_ROADMAP.md`を小さな完了可能Taskとして追跡する正本とする。

責務の基本分類:

- **Simulation Roadmap** — authoritative state / rule / semantic processing / schedule / history / semantic observation source / Save Data / server-authoritative command contract
- **Gateway Roadmap** — Observation Request / subscription / filtering / cache / deduplication / Protocol adaptation / delivery / reconnect / resync
- **View Roadmap** — read-only rendering / Camera / Selection / Inspector / Temporal & Historical viewing / Rendering LOD / View performance / localization
- **Management Roadmap** — editor / build / edit / remove / runtime control / Server configuration / Save / Load / destructive operation UX
- **Analytics** — 分析・統計・trend・heatmap等はView/Managementへ混在させず、必要になった時点で別Listener / clientとして設計する

各RoadmapはPhase番号を一致させない。Simulation / Gateway / ViewはそれぞれPhase 1以降の独立順序または既存Simulation Phase順で進め、依存するauthoritative contract / delivery contractが実装された時点で利用側Taskを順次着手する。

- 未完了Taskは `⬜`、必要な検証まで済んだ完了Taskは `✅` で表す。
- 作業開始前に、依頼内容に対応する既存 Task ID があるか4 Roadmapで確認する。
- 対応Taskが存在しない計画済み作業は、責務に応じたRoadmapへ小さなTaskとして追加する。
- Simulationの意味・authoritative sourceが必要ならSimulation Roadmap、Observation delivery能力が必要ならGateway Roadmapへ切り出す。
- `docs/roadmap/` の補足資料へTask案を書いた場合も、実際に着手対象とするTask ID・状態はルート `roadmap/` の正本へ同期する。
- 1つのTaskへ複数の独立した成果を詰め込まない。
- 「交通を完成」「UIを完成」など長期間閉じられない粒度のTaskを作らない。
- 作業中に想定より大きいことが分かった場合は、元Taskを無理に完了させず残作業を新しいTask IDへ分割する。
- 実装だけ終わって検証が残っている項目は `✅` にしない。
- 完了報告をする前に、対象Task IDの状態記号を`⬜`から`✅`へ同期する。
- 未実装の大テーマはTaskではなくBacklogとして置き、着手時に分解する。
- Roadmapは仕様書ではない。仕様の正本は `docs/specifications/`、設計の正本は `docs/architecture/` とする。
