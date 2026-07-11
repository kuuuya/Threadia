# 0001: Conversation 単位のメッセージ順序保証

- ステータス: 採用
- 日付: 2026-07-11

## 背景

チャットではメッセージの表示順が全クライアントで一致する必要がある。クライアント時刻は信頼できず、
SignalR の配信順もネットワーク状況により入れ替わる。順序の正本をどこで決めるかを定める必要がある。

## 選択肢

1. **Conversation ごとの単調増加 Sequence を DB で採番する(採用)**
   - `ConversationSequences(ConversationId, LastSequence)` を `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` で
     UPSERT し、行ロックで採番を直列化する。Message 保存と同一トランザクションで行う。
   - 長所: 実装が単純。`(ConversationId, Sequence)` の UNIQUE 制約で重複を DB レベルで防げる。
     欠番検知・再取得・既読位置がすべて Sequence を基準に統一できる。
   - 短所: 同一 Conversation への書き込みが直列化される。超高頻度の単一会話がボトルネックになり得る。
2. サーバー時刻 + タイブレーク
   - 長所: ロック不要。短所: 複数サーバー間の時計ずれで順序が壊れる。欠番検知ができない。
3. 分散採番器(Snowflake など)
   - 長所: 高スループット。短所: 追加コンポーネントの運用が必要。現段階では過剰。

## 決定

選択肢1を採用する。順序保証は Conversation 単位に限定し、システム全体の順序は保証しない。
性能問題が実測されるまで分散採番器は導入しない(CLAUDE.local.md の方針どおり)。

## 影響

- 並行送信の一意性は統合テスト「同一Conversationへの並行投稿でSequenceが重複しない」で担保する。
- クライアントは Sequence 順に表示し、欠番検知時は `afterSequence` API で補完する。
- 編集しても Sequence は変更しない。
