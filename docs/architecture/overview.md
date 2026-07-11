# アーキテクチャ概要

## モジュール構成と依存方向

```text
Api(合成ルート・Outbox Worker)          Workers(イベント Consumer ホスト)
  ├─ Identity        ユーザー・認証(JWT)
  ├─ Workspaces      ─(PublicApi)→ Identity.IUserDirectory
  ├─ Conversations   ─(PublicApi)→ Workspaces.IWorkspaceMembership
  ├─ Presence        ─(PublicApi)→ Workspaces(Redis TTL、DB なし)
  ├─ Attachments     ─(PublicApi)→ Conversations(S3互換ストレージ)
  ├─ Messaging       ─(PublicApi)→ Conversations / Attachments / Presence
  ├─ Notifications   (Consumer)─→ Conversations / Identity / Presence
  └─ Search          (Consumer)─→ Conversations

Contracts: モジュール間で共有するイベント契約のみ(依存なし)
```

- 各モジュールは独自の DbContext と PostgreSQL スキーマ
  (identity / workspaces / conversations / messaging / attachments / notifications / search)を持つ
- モジュール間は `PublicApi/` のインターフェースまたはイベント(Contracts)経由でのみ連携し、テーブルを直接参照しない
- モジュール内の依存方向: `Endpoints -> Application -> Domain`、`Infrastructure -> Application / Domain`

## メッセージ送信フロー

```text
Client
  -> POST /api/conversations/{id}/messages  (ClientMessageId 付き)
  -> 参加者チェック(IConversationMembership)
  -> DB Transaction
       - ConversationSequences を UPSERT + 行ロックで採番
       - Message / MessageMention 保存
       - OutboxMessage 保存(message.sent)
  -> Commit → API レスポンス(この時点で永続化済み)
  -> OutboxProcessor(API 内 BackgroundService)
       - FOR UPDATE SKIP LOCKED で未処理行を取得
       - SignalR Group "conversation:{id}" へ配信
       - RabbitMQ topic exchange "threadia.events" へ発行
       - 失敗は指数バックオフでリトライ、上限超過は DeadLettered
  -> Workers(RabbitMQ quorum キュー、x-delivery-limit 5 + DLQ)
       - notifications: 通知作成(ProcessedEvent + 決定的 NotificationId で冪等)→ オフラインユーザーへ Web Push
       - search-index: 検索インデックスの UPSERT / DELETE
       - attachments-cleanup: 削除メッセージの添付を非同期削除
```

## クライアント側の整合性維持

- 表示順はサーバー採番の Sequence に従う(クライアント時刻・受信順に依存しない)
- 受信 Sequence に欠番を検知したら `GET /messages?afterSequence=` で補完
- 再接続時も同様に最後の Sequence 以降を取得
- Message Id で重複除去(at-least-once 配信を許容)

## 認証・認可

- JWT(HMAC-SHA256)。SignalR は `access_token` クエリパラメータ(/hubs 配下のみ)
- 権限判定はすべてサーバー側: ワークスペース所属 → 会話参加 → 操作者本人、の順に確認
- 非参加者にはリソースの存在自体を秘匿する(403 ではなく 404)

## 履歴参照ポリシー

参加中(LeftAt が null)のメンバーは、自身の参加前を含む全履歴を参照できる。
退出後は一切参照できない。再参加すると再び全履歴を参照できる。
