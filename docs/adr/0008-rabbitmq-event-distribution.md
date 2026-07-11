# 0008: RabbitMQ によるイベント配信と Workers ホストの分離

- ステータス: 採用
- 日付: 2026-07-11

## 背景

Outbox イベントの Consumer が SignalR 配信に加えて通知作成・検索インデックス更新・添付掃除へ増えた。
Consumer の処理時間が API のレイテンシやリソースへ影響しないよう、実行場所と配信方式を決める。

## 選択肢

1. **RabbitMQ(topic exchange)+ 専用 Workers ホスト(採用)**
   - Outbox Worker はイベントを exchange へ発行し、Consumer は Workers プロセスの quorum キューで処理する。
   - 長所: Consumer を API と独立にデプロイ・スケール・再起動できる。キュー滞留で負荷を吸収できる。
     x-delivery-limit + Dead Letter Exchange でリトライ上限と DLQ が宣言的に実現できる。
   - 短所: 運用コンポーネントが増える。ローカル開発の前提が増える。
2. すべて API プロセス内で処理(従来)
   - 長所: 単純。短所: 通知や検索の重い処理が API と資源を奪い合う。片方の障害が全体へ波及する。

## 決定

選択肢1を採用する。ただし境界は `IIntegrationEventBus` に抽象化し、
`EventBus:Provider=InProcess` でプロセス内直接呼び出しに切り替えられる(テスト・最小構成の開発用)。
SignalR 配信のみ API 側 Outbox Worker が直接行う(Hub コンテキストと Redis Backplane が API に属するため)。

## 信頼性の設計

- 配信保証は at-least-once。Consumer は次のいずれかで冪等にする:
  - `ProcessedEvent(ConsumerName, EventId)` の記録と同一トランザクション処理(通知)
  - キー付き UPSERT / DELETE による自然な冪等性(検索インデックス、添付掃除)
- 処理失敗は requeue し、配信回数上限(5回)超過で `threadia.dead` キューへ移す
- RabbitMQ 停止時もメッセージ送信は成功し続け、Outbox のリトライが発行を回復する(障害試験手順参照)
