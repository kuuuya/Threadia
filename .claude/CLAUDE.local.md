# CLAUDE.local.md - Threadia

このリポジトリ固有の情報。汎用の開発方針・規約は `CLAUDE.md` を参照する。

## 概要

Slackを参考にしたリアルタイムチャットアプリケーション。
リアルタイム通信、メッセージング、分散システム、整合性、スケーラビリティの設計を実践する学習プロジェクト。

## 対象機能

* 1対1チャット、グループチャット、既読・未読管理、オンライン状態
* Webプッシュ通知、メッセージ検索、ファイル添付、メンション
* メッセージの編集・削除
* 音声通話、ビデオ通話、画面共有は対象外とする

## モジュール

```text
Modules/
  Identity/
  Workspaces/
  Conversations/
  Messaging/
  Presence/
  Notifications/
  Search/
  Attachments/
```

* 各モジュールは自身のデータを管理し、他モジュールのテーブルを直接更新しない

## ドメイン用語

* Workspace: ユーザーが所属する組織
* Conversation: 1対1またはグループの会話
* Member: Conversationの参加者
* Message: Conversationへ投稿されたメッセージ
* Sequence: Conversation内のメッセージ順序
* ReadPosition: ユーザーが最後に読んだ位置
* 主要エンティティ: `Workspace`、`WorkspaceMember`、`Conversation`、`ConversationMember`、`Message`、`MessageMention`、`ReadPosition`、`Attachment`、`Notification`、`OutboxMessage`、`ProcessedMessage`

## Conversation

* 種類は `Direct` と `Group` とする
* Direct Conversationは同じ2ユーザー間で重複作成しない
* Group Conversationは名前と複数の参加者を持つ
* Conversation参加者だけがメッセージを参照できる
* 参加・退出時刻を記録し、退出前の履歴を参照できるかは明示的なポリシーで決める

## メッセージ順序

* 順序はConversation単位で保証し、システム全体では保証しない
* Messageは `Id`、`ConversationId`、`Sequence`、`SenderId`、`Content`、`CreatedAt`、`EditedAt`、`DeletedAt` を持つ
* `(ConversationId, Sequence)`にUNIQUE制約を設定する
* SequenceはConversation内で単調増加させ、採番とMessage保存を同一トランザクションで行う
* クライアント時刻で順序を決めず、SignalRで先に受信してもSequence順に表示する
* 欠番を検知したクライアントはAPIから不足分を取得する
* 編集してもSequenceは変更しない
* 初期実装はConversation行の排他制御またはDBシーケンス管理とし、性能問題が確認されるまで分散採番器を導入しない

## メッセージ送信フロー

```text
Client
  -> POST /conversations/{id}/messages
  -> DB Transaction
       - Message保存
       - OutboxMessage保存
  -> Commit
  -> Outbox Worker
       - SignalR配信
       - 通知作成
       - 検索インデックス更新
```

* APIレスポンスはDB保存完了後に返す
* クライアントは `ClientMessageId` を送信する
* `(SenderId, ClientMessageId)`にUNIQUE制約を設定し、再送時の重複登録を防ぐ

## リアルタイム接続

* SignalRを使用し、APIサーバーはステートレスに保つ
* Redis Backplaneで複数サーバー間の配信を中継する
* 接続時に認証し、ConversationごとのSignalR Groupを使用する
* Group参加時にConversationへの所属をサーバー側で確認する
* 再接続時は最後に受信したSequence以降をAPIから取得する
* WebSocket配信成功を永続化成功の条件にしない
* SignalRは通知手段であり、データの正本ではない

## 既読・未読

* `ReadPosition(UserId, ConversationId, LastReadSequence, UpdatedAt)` を保存する
* `(UserId, ConversationId)`を主キーまたはUNIQUE制約にする
* LastReadSequenceは後退させず、未読数は最新Sequenceとの差から計算する
* Message単位の既読レコードは原則作成しない
* 既読位置は一定時間まとめて送信してよく、大規模化した場合は未読数のキャッシュを検討する

## オンライン状態

* Presenceは一時情報としてRedisへ保存し、接続時とheartbeat時にTTLを更新する
* 切断イベントだけに依存せず、複数端末の接続を個別に管理する
* 1つ以上の有効な接続があればOnlineとする
* 最終アクセス時刻は必要に応じてPostgreSQLへ保存する
* 強い整合性を求めず、Presence障害がメッセージ送受信へ影響しないようにする

## 大規模グループ配信

* 初期実装はSignalR Groupとし、小規模グループでは接続中ユーザーへ即時配信する
* オフラインユーザーごとのメッセージ複製は作らず、再接続後に履歴を取得する
* 大規模グループではPresenceや既読イベントの配信を抑制する
* 大量通知はキューへ送りバッチ処理し、負荷試験で問題が確認されてから配信方式を変更する

## 通知

* 対象はDirect Conversationの新規メッセージ、メンション、通知を有効にしたConversationとする
* オフラインまたは一定時間非アクティブなユーザーへ、Webプッシュ通知を非同期で送る
* `NotificationId` を冪等性キーとし、Consumerは処理済みIDを記録する
* 複数回配信を許容し、クライアントも `NotificationId` で重複表示を防ぐ
* Push送信失敗でMessage保存をロールバックしない
* 無効なPush Subscriptionは削除または無効化する

## メンション

* 表示文字列だけで対象者を決めず、APIで対象 `UserId` を明示する
* Conversation参加者だけをメンション可能にする
* Message保存時にMessageMentionを保存し、通知はOutbox経由で作成する

## メッセージ編集・削除

* 送信者または許可された管理者だけが操作でき、編集日時を記録する
* 削除後は本文を通常APIで返さず、削除イベントをリアルタイム配信する
* 添付ファイル削除と検索インデックス更新は非同期で実行してよい
* 監査要件がある場合は変更履歴を別テーブルへ保存する

## 履歴取得と検索

```text
GET /conversations/{id}/messages?beforeSequence=1000&limit=50
GET /conversations/{id}/messages?afterSequence=1000&limit=50
```

* OFFSETページングを避け、Sequenceをカーソルとして使用する
* limitに上限を設定し、デフォルトは新しい順に取得する
* Conversation参加権限を必ず確認し、`(ConversationId, Sequence)`を基本インデックスとする
* 初期検索はPostgreSQL全文検索とし、権限外または削除済みMessageを返さない
* 検索インデックスはOutbox経由で更新し、検索サービスを正本にしない
* 将来OpenSearchへ置き換えられる境界を設ける

## ファイル添付

* ファイル本体はS3互換ストレージ、メタデータはDBへ保存する
* クライアントは署名付きURLでストレージへ直接アップロードする
* Conversation参加者だけがダウンロードできるようにする
* ファイルサイズ、MIME Type、拡張子を検証する
* 完了前のAttachmentをMessageへ関連付けず、孤立ファイルは定期的に削除する

## パーティショニング

* 初期段階は単一PostgreSQLとし、実測なしにシャーディングしない
* 将来の分割キーは、取得・順序保証の境界と一致する `ConversationId` を基本とする
* 大規模Conversationがホットパーティションになる可能性を考慮する
* `WorkspaceId` とのトレードオフをADRへ記録し、クロスパーティショントランザクションを避ける

## 優先実装順

1. 認証とWorkspace
2. Conversationと参加者
3. Message送信・履歴取得
4. SignalRリアルタイム配信
5. 既読位置
6. オンライン状態
7. Outboxと通知
8. メンション
9. 編集・削除
10. ファイル添付
11. メッセージ検索
12. 負荷試験と障害試験

## 必須テスト

* 同じConversationへの並行投稿でSequenceが重複しない
* `ClientMessageId` の再送でMessageが重複しない
* LastReadSequenceが後退しない
* 非参加者がMessageを参照・投稿できない
* Outbox再実行で通知が重複処理されない
* SignalR切断後に不足Messageを再取得できる
* 編集・削除イベントが正しく配信される
* 検索結果に権限外Messageが含まれない

## 設計ドキュメント

重要な判断は `docs/adr/` へ記録する。最低限、Conversation単位の順序保証、SignalRとRedis Backplane、ReadPosition、Outbox Pattern、ConversationIdを分割キー候補とする理由、PostgreSQL全文検索から始める理由、大規模グループの配信戦略をADRに残す。
