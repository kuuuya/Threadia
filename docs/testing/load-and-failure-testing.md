# 負荷試験・障害試験

## 負荷試験

### メッセージ送信(Sequence 競合)

同一 Conversation への並行送信は行ロックで直列化されるため、最も競合しやすいパス。

```bash
# 並行数 10、総数 200(API 起動済みであること)
node scripts/load/send-messages.mjs 10 200
```

確認事項:

- p95 レイテンシが許容範囲か(行ロック待ちが支配的になる)
- Sequence が範囲内で重複・欠番なし
- 送信失敗 0 件

単一 Conversation の直列化がボトルネックになった場合の対応方針は ADR 0001(分散採番器は実測後)を参照。

### 実施記録

| 日付 | 構成 | 結果 |
|------|------|------|
| 2026-07-11 | ローカル(単一API・Docker PostgreSQL、並行10・総数200) | 228 msg/s、p50=21ms、p95=144ms、p99=266ms。Sequence 1..200 重複・欠番なし、失敗0件 |

## 障害試験(実施手順)

前提: `docker compose up -d` で全サービス起動、API + Workers 起動済み。

### 1. RabbitMQ 停止(イベントバス障害)— 2026-07-11 実施・合格

```bash
docker compose stop rabbitmq
```

期待される挙動(実施時にすべて確認済み):

- メッセージ送信 API は成功し続ける(Outbox は DB トランザクション内で完結)
- Outbox Worker の発行が失敗し、指数バックオフでリトライされる(`Attempts` 増加)
- SignalR 配信は Outbox 再実行のたびに再送される(クライアントは Message Id で重複除去)
- `docker compose start rabbitmq` 後、未処理 Outbox が排出され、通知・検索インデックスが追いつく
- 5回失敗した行は `DeadLetteredAt` が付き、以後は自動処理されない(手動リカバリ対象)

### 2. Redis 停止(Presence / Backplane 障害)— 2026-07-11 実施・合格

```bash
docker compose stop redis
```

期待される挙動(実施時に確認済み: 停止中も並行送信 20/20 成功):

- メッセージ送受信・履歴取得は影響を受けない(Presence は例外を握りつぶしオフライン扱い)
- Presence API は全員オフラインを返す
- 単一 API インスタンス構成では SignalR 配信も継続する(Backplane はインスタンス間中継のみ)

### 3. Worker 停止(Consumer 障害)

```bash
# Workers プロセスを停止
```

期待される挙動:

- メッセージ送受信・SignalR 配信は影響を受けない
- 通知・検索インデックスの反映が遅延し、キューに滞留する(RabbitMQ 管理 UI: http://localhost:15672)
- Worker 再起動で滞留分が処理される(冪等性により重複処理されない)

### 4. Consumer の処理失敗(Dead Letter)

不正なペイロードのメッセージを `threadia.events` へ発行すると、5回の再配信後に
`threadia.dead` キューへ移動する(quorum キューの x-delivery-limit)。
`threadia.dead` の滞留は監視対象とする。
