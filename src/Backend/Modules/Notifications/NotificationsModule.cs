using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Threadia.BuildingBlocks.Events;
using Threadia.Modules.Notifications.Application;
using Threadia.Modules.Notifications.Infrastructure;

namespace Threadia.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres が設定されていません。");

        services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(
            connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.Schema)));

        services.Configure<WebPushOptions>(configuration.GetSection(WebPushOptions.SectionName));

        var webPushConfigured = !string.IsNullOrWhiteSpace(configuration[$"{WebPushOptions.SectionName}:VapidPublicKey"]);
        if (webPushConfigured)
        {
            services.AddSingleton<IWebPushSender, WebPushSender>();
        }
        else
        {
            services.AddSingleton<IWebPushSender, NullWebPushSender>();
        }

        services.AddScoped<NotificationService>();
        services.AddScoped<PushSubscriptionService>();
        services.AddIntegrationEventConsumer<MessageSentNotificationConsumer>();

        return services;
    }
}
