# 0002: SignalR + Redis Backplane によるリアルタイム配信

- ステータス: 採用
- 日付: 2026-07-11

## 背景

メッセージのリアルタイム配信が必要。API サーバーはステートレスに保ち、水平スケールできる構成にしたい。

## 選択肢

1. **SignalR + Redis Backplane(採用)**
   - 長所: ASP.NET Core 標準。WebSocket / SSE / Long Polling のフォールバックが組み込み。
     Redis Backplane で複数サーバー間のグループ配信を中継できる。
   - 短所: Backplane は全メッセージを全サーバーへブロードキャストするため、大規模化で帯域が問題になり得る。
2. 生 WebSocket 自前実装
   - 長所: 依存が少ない。短所: 再接続・グループ管理・スケールアウトをすべて自作することになる。
3. 外部 Pub/Sub サービス(Pusher 等)
   - 長所: 運用レス。短所: 学習プロジェクトの目的に反し、ベンダーロックインが生じる。

## 決定

選択肢1を採用する。単一サーバーの開発環境では Backplane なしで動作し、
`ConnectionStrings:Redis` を設定したときのみ有効化する。

## 制約(データの正本は PostgreSQL)

- WebSocket 配信成功を永続化成功の条件にしない。API レスポンスは DB コミット後に返す。
- SignalR は通知手段であり正本ではない。再接続時は最後に受信した Sequence 以降を REST API から取得する。
- Group 参加時は Conversation への所属をサーバー側で必ず確認する(`ChatHub.JoinConversation`)。
