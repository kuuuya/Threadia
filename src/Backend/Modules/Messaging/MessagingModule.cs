using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Threadia.Modules.Messaging.Application;
using Threadia.Modules.Messaging.Infrastructure;

namespace Threadia.Modules.Messaging;

public static class MessagingModule
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres が設定されていません。");

        services.AddDbContext<MessagingDbContext>(options => options.UseNpgsql(
            connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", MessagingDbContext.Schema)));

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        services.AddScoped<MessageService>();
        services.AddScoped<ReadPositionService>();
        services.AddScoped<IOutboxEventDispatcher, SignalROutboxEventDispatcher>();
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
