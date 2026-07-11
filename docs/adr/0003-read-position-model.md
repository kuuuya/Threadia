# 0003: ReadPosition による既読・未読管理

- ステータス: 採用
- 日付: 2026-07-11

## 背景

既読・未読の管理方式を決める。Slack のような UI では会話ごとの未読数と既読境界が分かれば十分。

## 選択肢

1. **ユーザー × Conversation ごとの既読位置(採用)**
   - `ReadPositions(UserId, ConversationId, LastReadSequence, UpdatedAt)`、主キー `(UserId, ConversationId)`。
   - 長所: レコード数が「ユーザー数 × 参加会話数」に抑えられる。未読数は
     `ConversationSequences.LastSequence - LastReadSequence` で計算でき、メッセージ本体を数えない。
   - 短所: 「誰がどのメッセージまで読んだか」のメッセージ単位表示はできない(要件外)。
2. Message 単位の既読レコード
   - 長所: 細かい既読表示が可能。短所: レコード数がメッセージ数 × 参加者数で爆発する。

## 決定

選択肢1を採用する。Message 単位の既読レコードは作成しない。

## 不変条件と実装

- LastReadSequence は後退しない。API では `ON CONFLICT ... DO UPDATE SET GREATEST(...)` により
  並行更新下でも DB レベルで保証する(ドメインの `ReadPosition.Advance` も同じ規則を持つ)。
- クライアントは既読位置を一定間隔でまとめて送信してよい(現実装は約800msのデバウンス)。
- 大規模化した場合は未読数のキャッシュ(Redis)を検討する。
