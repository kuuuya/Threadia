# Threadia

Slack を参考にしたリアルタイムチャットアプリケーション。
リアルタイム通信、メッセージング、分散システム、整合性、スケーラビリティの設計を実践する学習プロジェクト。

モジュラーモノリス構成。設計方針は [`.claude/CLAUDE.md`](.claude/CLAUDE.md) と
[`.claude/CLAUDE.local.md`](.claude/CLAUDE.local.md)、主要な設計判断は [`docs/adr/`](docs/adr/) を参照。

## 技術スタック

- Backend: .NET 10 / ASP.NET Core / SignalR / EF Core / PostgreSQL / Redis(Backplane)
- Frontend: TypeScript / React / Vite / TanStack Query / React Router
- Infra: Docker Compose

## 起動方法

前提: .NET 10 SDK、Node.js 20+、Docker

```bash
# 1. インフラ(PostgreSQL / Redis / RabbitMQ / MinIO)
docker compose up -d

# 2. API(http://localhost:5100、起動時にマイグレーション自動適用)
dotnet run --project src/Backend/Api

# 3. Workers(通知・検索インデックス・添付掃除の Consumer)
dotnet run --project src/Backend/Workers

# 4. フロントエンド(http://localhost:5173、/api と /hubs は 5100 へプロキシ)
cd src/Frontend
npm install
npm run dev
```

管理 UI: RabbitMQ http://localhost:15672 、MinIO http://localhost:9001(いずれも threadia / threadia-secret ※RabbitMQ は threadia / threadia)

### Web プッシュ通知(任意)

VAPID 鍵はリポジトリに含めない。ローカルで生成し、gitignore 対象の
`appsettings.Development.local.json` へ配置すると有効になる(未設定でもプッシュ以外は動作する):

```bash
npx web-push generate-vapid-keys --json
```

生成した鍵を `src/Backend/Api` と `src/Backend/Workers` の両方に置く:

```json
{
  "WebPush": {
    "VapidSubject": "mailto:dev@threadia.local",
    "VapidPublicKey": "<publicKey>",
    "VapidPrivateKey": "<privateKey>"
  }
}
```

ブラウザで http://localhost:5173 を開き、ユーザー登録 → ワークスペース作成 →
別ブラウザ(またはシークレットウィンドウ)で2人目を登録 → メールアドレスで招待 → チャット開始。

## テスト

```bash
# バックエンド単体テスト
dotnet test tests/Backend/UnitTests

# バックエンド統合テスト(Docker 必須。Testcontainers が PostgreSQL を自動起動)
dotnet test tests/Backend/IntegrationTests

# フロントエンド
cd src/Frontend && npm test
```

## ディレクトリ構成

```text
src/
  Backend/
    Api/                 合成ルート(認証、SignalR、CORS、モジュール登録、Outbox Worker)
    Workers/             イベント Consumer ホスト(通知、検索インデックス、添付掃除)
    BuildingBlocks/      共通基盤(ID生成、例外、ICurrentUser、イベントバス、OTel)
    Contracts/           モジュール間で共有するイベント契約(依存なし)
    Modules/
      Identity/          ユーザー登録・ログイン(JWT)
      Workspaces/        ワークスペースとメンバー
      Conversations/     Direct / Group 会話と参加者
      Messaging/         メッセージ、Sequence採番、既読位置、Outbox、ChatHub
      Presence/          オンライン状態(Redis TTL)
      Notifications/     通知作成と Web Push(Consumer)
      Search/            メッセージ検索(pg_trgm、Consumer でインデックス更新)
      Attachments/       ファイル添付(S3互換、署名付きURL)
  Frontend/              React SPA
tests/
  Backend/UnitTests/     ドメインルールの単体テスト
  Backend/IntegrationTests/  API レベルの統合テスト(Testcontainers)
scripts/
  load/                  負荷試験スクリプト
docs/
  adr/                   設計判断の記録
  architecture/          アーキテクチャ概要
  testing/               負荷試験・障害試験の手順
```

各モジュールは `Domain / Application / Infrastructure / Endpoints / PublicApi` を持ち、
他モジュールへは `PublicApi` のインターフェース経由でのみアクセスする(DbContext を共有しない)。

## 実装状況

CLAUDE.local.md の優先実装順に対して:

| # | 機能 | 状態 |
|---|------|------|
| 1 | 認証と Workspace | 実装済み |
| 2 | Conversation と参加者 | 実装済み |
| 3 | Message 送信・履歴取得 | 実装済み(Sequence採番、ClientMessageId冪等性、カーソルページング) |
| 4 | SignalR リアルタイム配信 | 実装済み(Outbox 経由、Redis Backplane は設定で有効化) |
| 5 | 既読位置 | 実装済み(非後退保証、未読数API) |
| 6 | オンライン状態(Presence) | 実装済み(Redis TTL + heartbeat、ADR 0011) |
| 7 | Outbox と通知 | 実装済み(RabbitMQ + Workers、Webプッシュ、冪等 Consumer、ADR 0008/0010) |
| 8 | メンション | 実装済み(入力UI、ハイライト、mention通知) |
| 9 | 編集・削除 | 実装済み |
| 10 | ファイル添付 | 実装済み(MinIO 署名付きURL、孤立掃除、ADR 0009) |
| 11 | メッセージ検索 | 実装済み(pg_trgm 部分一致、権限フィルタ、ADR 0006) |
| 12 | 負荷試験・障害試験 | スクリプト・手順整備済み(docs/testing/) |

将来拡張(未実装): 会話ごとの通知設定、既読イベントのリアルタイム配信、OpenSearch への置き換え、
通知のバッチ送信、監査用の変更履歴テーブル。
