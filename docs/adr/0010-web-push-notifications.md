# 0010: Web Push 通知と冪等な通知作成

- ステータス: 採用
- 日付: 2026-07-11

## 背景

オフラインユーザーへ新着を届ける手段が必要。通知は Outbox イベントから非同期に作られるため、
再配信(at-least-once)でも重複しない仕組みが要る。

## 決定

### 通知対象(CLAUDE.local.md)

- Direct Conversation の新規メッセージ → 相手
- メンション → メンションされた参加者(direct_message より優先)
- 「通知を有効にした Conversation」は将来拡張(会話ごとの通知設定は未実装)

### 冪等性(二重の防御)

1. `ProcessedEvent(ConsumerName, EventId)` — Consumer が処理済みイベントをスキップ
2. `NotificationId = DeterministicGuid(EventId, UserId)` — 万一再処理されても主キー衝突で重複作成されない。
   クライアントも同じ ID(Push の tag)で重複表示を防ぐ

### Web Push

- VAPID + RFC 8291 暗号化は自前実装が現実的でないため `WebPush` ライブラリを使用する(依存追加の理由)
- Push はオンラインユーザーへ送らない(SignalR で受信済みのため)。オンライン判定は Presence(ADR 0011)
- Push 送信は通知作成のコミット後に行い、失敗しても通知・メッセージをロールバックしない
- 404/410 を返した Subscription は削除する
- 大量通知のバッチ化・スロットリングは負荷試験で問題が確認されてから導入する(ADR 0007)
