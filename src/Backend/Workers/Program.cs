using Threadia.BuildingBlocks.Events;
using Threadia.BuildingBlocks.Telemetry;
using Threadia.Modules.Attachments;
using Threadia.Modules.Attachments.Infrastructure;
using Threadia.Modules.Conversations;
using Threadia.Modules.Identity;
using Threadia.Modules.Notifications;
using Threadia.Modules.Presence;
using Threadia.Modules.Search;
using Threadia.Modules.Workspaces;

// Outbox イベントの Consumer(通知・検索インデックス・添付掃除)を RabbitMQ から処理するホスト。
// API プロセスと分離してデプロイ・スケールできる。
var builder = WebApplication.CreateBuilder(args);

// コミットしない開発者ローカルの上書き設定(VAPID 鍵など)。.gitignore 対象。
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddThreadiaTelemetry(builder.Configuration, "threadia-workers");

// Consumer が依存するモジュール群。Messaging は登録しない(Outbox Worker は API 側で動く)。
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddWorkspacesModule(builder.Configuration);
builder.Services.AddConversationsModule(builder.Configuration);
builder.Services.AddPresenceModule(builder.Configuration);
builder.Services.AddAttachmentsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddSearchModule(builder.Configuration);

// RabbitMQ Consumer ホスト。
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddRabbitMqConsumerHost();

// 孤立添付ファイルの定期掃除。
builder.Services.AddHostedService<OrphanAttachmentCleanupService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
