# Legacy Machi-Sim からの移行メモ

このディレクトリは、旧ブラウザ単体実装 [`Machi-Sim_Legacy`](https://github.com/SUIREN-KazutoHashimoto/Machi-Sim_Legacy) から引き継ぐ価値がある知見を整理するための履歴資料です。

ここにある内容は **MachiVerseWorks の現行仕様そのものではありません**。現行仕様は `docs/specifications/`、現行設計は `docs/architecture/` を正本とします。

## 引き継ぐもの

### 都市シミュレーションのドメイン知識

旧実装で作り込まれた次の分野は、仕様策定時の参考資料として利用します。

- Agent の生活行動と目的地選択
- 道路交通、車線、信号、交差点
- バス、タクシー、鉄道、徒歩の移動
- 物流、産業、職場、在庫
- 電力、発電、燃料、ライフライン
- 都市生成、用途、道路、建物、POI
- Inspector / Dashboard / 診断 UI の情報設計

ただし、旧コードの実装都合を新仕様へそのまま持ち込みません。

### 性能面の知見

旧実装から特に引き継ぐ教訓:

- 大規模 world を毎 frame 全件走査しない
- simulation tick と rendering frame を分離する
- 頻繁な object allocation を hot path へ入れない
- active set / spatial partition / event-driven update を優先する
- pathfinding、交通、歩行者、描画を計測可能な単位へ分ける
- optimization は profiler / benchmark の結果から行う
- logical state と rendered state を混同しない

### 開発運用

旧repoで有効だった次の原則も引き継ぎます。

- 症状と原因を分ける
- 根本原因を持つ責務で直す
- CI success と実機確認を区別する
- 未確認事項を確認済みと報告しない
- 仕様変更とドキュメント更新を同じ作業で扱う
- 実験コードは本流と分離する

## 引き継がないもの

次は旧アーキテクチャ固有のため、原則として新実装へコピーしません。

- Browser が authoritative simulation world を所有する構成
- SharedArrayBuffer / Worker pool を Simulation Core の基本構造にする設計
- runtime monkey patch / tuning patch の積み重ね
- `src/version.ts` の import 順に runtime 挙動を依存させる構成
- Web Client 内で Simulation 状態を直接変更する仕組み
- Browser rendering 都合で Simulation データモデルを決める設計

## 旧資料の参照方法

旧repoの資料は必要なときだけ参照し、以下の手順で新repoへ取り込みます。

1. 旧資料が説明している「ユーザーから見た仕様」と「実装都合」を分ける
2. 現在も必要な仕様だけ `docs/specifications/` へ書き直す
3. 新アーキテクチャでの実現方法を `docs/architecture/` へ書く
4. 重要な設計判断なら `docs/decisions/` に ADR を作る
5. コピーしたコード・文書・素材がある場合は `LICENSE` / `NOTICE` / `THIRD_PARTY_NOTICES.txt` の義務を確認する

旧repoの巨大な設計書を、そのまま新repoの正本としてコピーすることは避けます。

## 参照元

- Legacy repository: https://github.com/SUIREN-KazutoHashimoto/Machi-Sim_Legacy
- Legacy README: https://github.com/SUIREN-KazutoHashimoto/Machi-Sim_Legacy/blob/main/README.md
- Legacy development guide: https://github.com/SUIREN-KazutoHashimoto/Machi-Sim_Legacy/blob/main/doc/%E9%96%8B%E7%99%BA%E3%83%BB%E4%B8%8D%E5%85%B7%E5%90%88%E4%BF%AE%E6%AD%A3%E6%89%8B%E9%A0%86.md
- Legacy documentation index: https://github.com/SUIREN-KazutoHashimoto/Machi-Sim_Legacy/blob/main/doc/README.md
