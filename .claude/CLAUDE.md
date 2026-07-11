# CLAUDE.md

このリポジトリでは、保守性・信頼性・拡張性を重視した Web アプリケーションを開発する。
Claude は単にコードを生成せず、要件・制約・トレードオフを考慮して実装する。

## 基本方針

* 最初はモジュラーモノリスとして実装する
* 将来のサービス分割を妨げない境界を設ける
* 過度な抽象化や先回りした分散化を避ける
* CQRS、MediatR、独自フレームワークは原則使用しない
* フレームワークよりドメインとユースケースを中心に設計する
* 読みやすく変更しやすいコードを優先する
* 新しい依存関係を追加する前に、その必要性を説明する

## 技術スタック

### Backend

* .NET 10、ASP.NET Core、SignalR、Entity Framework Core
* PostgreSQL、Redis、RabbitMQ、OpenTelemetry
* xUnit、FluentAssertions

### Frontend

* TypeScript、React、Vite
* TanStack Query、React Router
* Vitest、React Testing Library

### Infrastructure

* Docker Compose、S3 互換オブジェクトストレージ、GitHub Actions

## ディレクトリ構成

```text
src/
  Backend/
    Api/
    Modules/
    BuildingBlocks/
    Workers/
  Frontend/
    src/
      app/
      features/
      pages/
      shared/
tests/
  Backend/
    UnitTests/
    IntegrationTests/
  Frontend/
docs/
  adr/
  architecture/
```

## 設計判断の記録(ADR)

* 重大な設計判断は、実装前に選択肢とトレードオフを整理して ADR に記録する
* ADR は `docs/adr/NNNN-<kebab-case-title>.md` に1判断1ファイルで保存する

## バックエンド設計

各モジュールは、可能な限り次の構造を持つ。

```text
Modules/{ModuleName}/
  Domain/
  Application/
  Infrastructure/
  Endpoints/
```

依存方向は次のとおりとする。

```text
Endpoints -> Application -> Domain
Infrastructure -> Application / Domain
```

* Domain は EF Core、HTTP、SignalR に依存させない
* Application はユースケースを実装する
* Infrastructure は DB、キャッシュ、メッセージングなどを実装する
* Endpoints は入力変換とレスポンス生成に限定する
* モジュール間で DbContext や内部テーブルを直接参照しない
* モジュール間連携には公開インターフェースまたはイベントを使用する

## API 設計

* REST API を基本とし、リアルタイム配信には SignalR を使用する
* URL はリソースを表し、HTTP メソッドとステータスコードを適切に使用する
* エラーは Problem Details 形式で返す
* API の入出力に Domain Entity を直接使用しない
* 一覧 API にはページングを必須とする
* 更新系 API では必要に応じて冪等性を考慮する
* 公開 API の破壊的変更を避ける

## データ設計

* PostgreSQL を正本とし、Redis を正本として使用しない
* 外部キー、UNIQUE 制約、CHECK 制約を活用する
* 日時は UTC で保存する
* ID は UUID または ULID を使用する
* 金額や順序などの重要な値に浮動小数点数を使用しない
* 削除方式は要件に応じて物理削除と論理削除を選択する
* インデックスは実際の検索条件に基づいて追加する
* N+1 クエリと無制限なデータ取得を避ける

## 非同期処理

* DB 更新とイベント発行の整合性には Outbox Pattern を使用する
* Consumer は冪等にし、メッセージが複数回配信される前提で実装する
* リトライ回数に上限を設け、処理不能なメッセージは Dead Letter Queue へ送る
* イベントの順序保証が必要な範囲を明示する
* 非同期処理の結果を追跡可能にする

## セキュリティ

* すべての更新操作で認証・認可を確認する
* クライアントから渡されたユーザー ID を信用せず、権限判定はサーバー側で行う
* SQL、HTML、ファイル名などの入力を検証する
* 秘密情報をコードやログへ出力しない
* ログにアクセストークンや個人情報を残さない
* ファイルアップロードではサイズと形式を制限する

## 可観測性

* 構造化ログを使用し、TraceId、UserId、RequestId を記録する
* 個人情報やメッセージ本文を不用意にログへ出さない
* OpenTelemetry でトレースとメトリクスを収集する
* レイテンシ、エラー率、トラフィック、リソース飽和を監視する
* 外部サービス呼び出しの失敗を観測可能にする

## テスト

* Domain と Application の重要なルールを単体テストする
* DB、Redis、RabbitMQ を含む処理は統合テストする
* 主要ユースケースは API レベルで検証する
* モックの過剰使用を避け、実装詳細ではなく振る舞いをテストする
* 不具合修正時は再現テストを追加する
* テスト名から前提、操作、期待結果が分かるようにする

## 実装手順

機能追加時は次の順序で進める。

1. 要件と受け入れ条件を確認する
2. 影響するモジュールとデータを特定する
3. 必要に応じて ADR を作成する
4. 最小構成で実装する
5. テストを追加する
6. エラー処理と認可を確認する
7. ログとメトリクスを確認する
8. ドキュメントを更新する

## Claude への指示

* 作業前に関連コードを確認する
* 既存の命名、構造、実装パターンに合わせる
* 大規模変更は小さなステップに分割する
* 不明点を推測だけで埋めない
* 重大な設計判断は選択肢とトレードオフを示す
* 要求されていない全面的なリファクタリングをしない
* コンパイルエラーやテスト失敗を残さない
* 完了時に変更内容、設計判断、テスト結果、残課題を報告する
