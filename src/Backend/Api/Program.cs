using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Threadia.Api;
using Threadia.BuildingBlocks.Auth;
using Threadia.BuildingBlocks.Events;
using Threadia.BuildingBlocks.Telemetry;
using Threadia.Modules.Attachments;
using Threadia.Modules.Attachments.Endpoints;
using Threadia.Modules.Attachments.Infrastructure;
using Threadia.Modules.Conversations;
using Threadia.Modules.Conversations.Endpoints;
using Threadia.Modules.Conversations.Infrastructure;
using Threadia.Modules.Identity;
using Threadia.Modules.Identity.Endpoints;
using Threadia.Modules.Identity.Infrastructure;
using Threadia.Modules.Messaging;
using Threadia.Modules.Messaging.Endpoints;
using Threadia.Modules.Messaging.Infrastructure;
using Threadia.Modules.Notifications;
using Threadia.Modules.Notifications.Endpoints;
using Threadia.Modules.Notifications.Infrastructure;
using Threadia.Modules.Presence;
using Threadia.Modules.Presence.Endpoints;
using Threadia.Modules.Search;
using Threadia.Modules.Search.Endpoints;
using Threadia.Modules.Search.Infrastructure;
using Threadia.Modules.Workspaces;
using Threadia.Modules.Workspaces.Endpoints;
using Threadia.Modules.Workspaces.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// コミットしない開発者ローカルの上書き設定(VAPID 鍵など)。.gitignore 対象。
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

// 構造化ログ。TraceId は ActivityTrackingOptions で付与される。
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId;
});

// --- 認証(JWT) ---
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey に32文字以上の署名鍵を設定してください。");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // WebSocket はヘッダーを送れないため、Hub への接続時のみクエリ文字列のトークンを許可する。
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// --- 共通サービス ---
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddThreadiaTelemetry(builder.Configuration, "threadia-api");
builder.Services.AddIntegrationEventBus(builder.Configuration);

// --- SignalR(Redis Backplane は接続文字列があるときのみ有効化) ---
var signalR = builder.Services.AddSignalR();
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    signalR.AddStackExchangeRedis(redisConnectionString, o => o.Configuration.ChannelPrefix =
        StackExchange.Redis.RedisChannel.Literal("threadia"));
}

// --- CORS(開発用フロントエンド) ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// --- モジュール登録 ---
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddWorkspacesModule(builder.Configuration);
builder.Services.AddConversationsModule(builder.Configuration);
builder.Services.AddPresenceModule(builder.Configuration);
builder.Services.AddAttachmentsModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddSearchModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();
app.MapWorkspaceEndpoints();
app.MapConversationEndpoints();
app.MapMessagingEndpoints();
app.MapPresenceEndpoints();
app.MapAttachmentEndpoints();
app.MapNotificationEndpoints();
app.MapSearchEndpoints();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();
app.MapHealthChecks("/health");

// 開発時はスキーマを自動適用する。本番はデプロイパイプラインでマイグレーションを実行する。
if (app.Configuration.GetValue("ApplyMigrationsOnStartup", app.Environment.IsDevelopment()))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ConversationsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<AttachmentsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<SearchDbContext>().Database.MigrateAsync();
}

app.Run();

// 統合テスト(WebApplicationFactory)から参照するためのマーカー。
public partial class Program;
